using Microsoft.EntityFrameworkCore;
using TrackBoard.Common;
using TrackBoard.Data;
using TrackBoard.Dtos;

namespace TrackBoard.Services;

public interface IUserService
{
    Task<AuthenticatedUserResponse> GetAuthenticatedAsync(Guid userId, CancellationToken ct);
}

public class UserService(TrackBoardDbContext db) : IUserService
{
    public async Task<AuthenticatedUserResponse> GetAuthenticatedAsync(
        Guid userId,
        CancellationToken ct)
    {
        // Projected, so PasswordHash never leaves the database.
        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new AuthenticatedUserResponse(u.Id, u.Email, u.DisplayName, u.Role))
            .FirstOrDefaultAsync(ct);

        return user ?? throw new NotFoundException("User", userId);
    }
}
