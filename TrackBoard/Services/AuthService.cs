using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TrackBoard.Auth;
using TrackBoard.Common;
using TrackBoard.Data;
using TrackBoard.Dtos;
using TrackBoard.Entities;

namespace TrackBoard.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);

    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct);

    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct);

    Task LogoutAsync(Guid userId, RefreshRequest request, CancellationToken ct);
}

public partial class AuthService(
    TrackBoardDbContext db,
    ITokenService tokens,
    IPasswordHasher<User> passwordHasher,
    ILoginAttemptTracker lockout,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    /// <summary>
    /// Every failed login returns this, whether the address is unknown or the password is
    /// wrong, so the endpoint cannot be used to discover which accounts exist.
    /// </summary>
    private const string InvalidCredentials = "Email or password is incorrect.";

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = Normalise(request.Email);

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            throw new ConflictException("An account with that email already exists.");
        }

        var user = new User
        {
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            Role = UserRole.Driver,
        };

        // Never store or log the plaintext. PasswordHasher<T> is PBKDF2-HMAC-SHA512
        // with a per-user salt, and encodes its own format version for future rehashing.
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = Normalise(request.Email);

        // First, so a locked address costs no hashing and a correct password cannot end the
        // lock early: that would leave the lock no obstacle to someone guessing.
        if (lockout.GetRetryAfter(email) is { } retryAfter)
        {
            throw new TooManyRequestsException(retryAfter);
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null)
        {
            // Hash anyway so an unknown address and a wrong password take similar time;
            // otherwise response latency alone reveals which accounts exist. Failures are
            // counted for the same reason: an unknown address must lock like a real one.
            passwordHasher.HashPassword(new User(), request.Password);
            lockout.RecordFailure(email);
            throw new UnauthorizedException(InvalidCredentials);
        }

        var verification = passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash, request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            lockout.RecordFailure(email);
            throw new UnauthorizedException(InvalidCredentials);
        }

        lockout.Reset(email);

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            // The stored hash used older parameters; upgrade it now that we have the plaintext.
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        }

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        var hash = tokens.HashRefreshToken(request.RefreshToken);
        var now = timeProvider.GetUtcNow();

        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
            ?? throw new UnauthorizedException("The refresh token is not valid.");

        if (!stored.IsActive(now))
        {
            // A revoked token being presented again means the token was captured after
            // rotation. Treat the whole session family as compromised and cut it off.
            if (stored.RevokedAt is not null)
            {
                LogRefreshTokenReuse(logger, stored.UserId);

                await RevokeAllForUserAsync(stored.UserId, now, ct);
            }

            throw new UnauthorizedException("The refresh token is not valid.");
        }

        var replacement = await IssueTokensAsync(stored.User, ct, markRevokedNow: stored);
        return replacement;
    }

    public async Task LogoutAsync(Guid userId, RefreshRequest request, CancellationToken ct)
    {
        var hash = tokens.HashRefreshToken(request.RefreshToken);

        var stored = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.UserId == userId, ct);

        // Logging out an already-invalid token is not an error worth reporting.
        if (stored is null || stored.RevokedAt is not null)
        {
            return;
        }

        stored.RevokedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    private async Task<AuthResponse> IssueTokensAsync(
        User user,
        CancellationToken ct,
        RefreshToken? markRevokedNow = null)
    {
        var access = tokens.CreateAccessToken(user);
        var refresh = tokens.CreateRefreshToken();

        var entity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refresh.Hash,
            ExpiresAt = refresh.ExpiresAt,
        };

        db.RefreshTokens.Add(entity);

        if (markRevokedNow is not null)
        {
            // Rotation: the presented token dies as its successor is created.
            markRevokedNow.RevokedAt = timeProvider.GetUtcNow();
            markRevokedNow.ReplacedByTokenId = entity.Id;
        }

        await db.SaveChangesAsync(ct);

        return new AuthResponse(
            access.Value,
            access.ExpiresAt,
            refresh.PlainText,
            new AuthenticatedUserResponse(user.Id, user.Email, user.DisplayName, user.Role));
    }

    private async Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    private static string Normalise(string email) => email.Trim().ToLowerInvariant();

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Reuse of a revoked refresh token for user {UserId}; revoking all sessions.")]
    private static partial void LogRefreshTokenReuse(ILogger logger, Guid userId);
}
