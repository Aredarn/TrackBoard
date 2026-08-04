using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using TrackBoard.Auth;
using TrackBoard.Common;
using TrackBoard.Data;
using TrackBoard.Data.Interceptors;
using TrackBoard.Entities;
using TrackBoard.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ────────────────────────────────────────────────────────────
// Secrets come from configuration, which in production means environment variables
// (ConnectionStrings__Default, Jwt__Secret). Nothing sensitive is committed.
var connectionString = builder.Configuration.GetConnectionString("Default");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "No 'Default' connection string. Set ConnectionStrings__Default in the environment.");
}

builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    // Refuses to start on a missing or under-strength signing key rather than
    // serving traffic with one.
    .ValidateOnStart();

builder.Services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();

// ── Persistence ──────────────────────────────────────────────────────────────
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AuditingInterceptor>();

builder.Services.AddDbContext<TrackBoardDbContext>((sp, options) =>
{
    options
        .UseNpgsql(connectionString)
        // Postgres convention: users.display_name rather than "Users"."DisplayName".
        .UseSnakeCaseNamingConvention()
        .AddInterceptors(sp.GetRequiredService<AuditingInterceptor>());

    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
    }
});

// ── Authentication ───────────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("The Jwt configuration section is missing.");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ValidateLifetime = true,
            // Default is five minutes of leeway, which silently extends every token's life.
            ClockSkew = TimeSpan.Zero,
            // Pinned explicitly because MapInboundClaims is off below: without these the
            // handler would not know which claim carries the role, and [Authorize(Roles=…)]
            // would silently never match.
            NameClaimType = JwtRegisteredClaimNames.Email,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        };

        // Reject anything not signed with the algorithm we issue, so a token cannot be
        // downgraded to 'none' or to an algorithm confusion attack.
        options.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.HmacSha256];

        // Keep claim names exactly as issued rather than rewriting them to WS-Fed URIs.
        options.MapInboundClaims = false;
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthorizationHandler, VehicleOwnerHandler>();
builder.Services.AddScoped<IAuthorizationHandler, ResultOwnerHandler>();
builder.Services.AddScoped<IResourceAuthorizer, ResourceAuthorizer>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<ITokenService, TokenService>();

// ── Rate limiting ────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // An empty 429 tells a well-behaved client nothing about when to retry.
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests",
                Detail = "Rate limit exceeded. Retry after the interval in the Retry-After header.",
            },
            cancellationToken);
    };

    // Credential endpoints get a tight per-IP budget to blunt brute-force attempts.
    options.AddPolicy(RateLimitPolicies.Authentication, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ClientKey(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Everything else gets a looser ceiling, keyed by user once authenticated.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ClientKey(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    static string ClientKey(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true
            ? context.User.GetUserId().ToString()
            // NOTE: behind a reverse proxy this is the proxy's address unless forwarded
            // headers are configured for that specific deployment.
            : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
});

// ── CORS ─────────────────────────────────────────────────────────────────────
// Allow-list only. With no configured origins the policy permits nothing, which is
// the correct default for an API with no first-party browser client yet.
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
    }));

// ── Application services ─────────────────────────────────────────────────────
builder.Services.AddScoped<IPointsCalculator, PointsCalculator>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IVehicleService, VehicleService>();
builder.Services.AddScoped<ICircuitService, CircuitService>();
builder.Services.AddScoped<IPointsSchemeService, PointsSchemeService>();
builder.Services.AddScoped<ISeriesService, SeriesService>();
builder.Services.AddScoped<IRaceEventService, RaceEventService>();
builder.Services.AddScoped<IResultService, ResultService>();

// ── Web ──────────────────────────────────────────────────────────────────────
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        // Without this, enums cross the wire as bare integers: responses carry "role": 1
        // instead of "Admin", and a request sending "Completed" is rejected outright.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi(options =>
    options.AddDocumentTransformer<OpenApiSecuritySchemeTransformer>());

var app = builder.Build();

// First in the pipeline so nothing downstream can emit an unformatted error page.
app.UseExceptionHandler();
app.UseSecurityHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Microsoft.AspNetCore.OpenApi only serves the document; Scalar renders the UI.
    app.MapScalarApiReference();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

/// <summary>
/// Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can bootstrap the app in
/// integration tests — top-level statements otherwise compile to an internal class.
/// </summary>
public partial class Program;
