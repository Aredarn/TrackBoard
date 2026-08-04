using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using TrackBoard.Common;
using TrackBoard.Entities;

namespace TrackBoard.Auth;

public static class ResourceOperations
{
    public static readonly OperationAuthorizationRequirement Read = new() { Name = nameof(Read) };

    public static readonly OperationAuthorizationRequirement Update = new() { Name = nameof(Update) };

    public static readonly OperationAuthorizationRequirement Delete = new() { Name = nameof(Delete) };
}

/// <summary>
/// Wraps <see cref="IAuthorizationService"/> so services can enforce ownership on a loaded
/// entity without taking a dependency on <c>HttpContext</c> shapes at every call site.
/// </summary>
public interface IResourceAuthorizer
{
    Task EnsureAsync<TResource>(TResource resource, OperationAuthorizationRequirement operation)
        where TResource : notnull;
}

public class ResourceAuthorizer(
    IAuthorizationService authorization,
    IHttpContextAccessor httpContextAccessor) : IResourceAuthorizer
{
    public async Task EnsureAsync<TResource>(
        TResource resource,
        OperationAuthorizationRequirement operation)
        where TResource : notnull
    {
        var user = httpContextAccessor.HttpContext?.User
            ?? throw new InvalidOperationException("No HTTP context to authorize against.");

        var result = await authorization.AuthorizeAsync(user, resource, operation);

        if (!result.Succeeded)
        {
            // 403, not 404: the caller is authenticated, they simply may not do this.
            throw new ForbiddenException(
                $"You are not allowed to {operation.Name?.ToLowerInvariant()} this resource.");
        }
    }
}

/// <summary>A driver may act on their own vehicles; an admin may act on any.</summary>
public class VehicleOwnerHandler
    : AuthorizationHandler<OperationAuthorizationRequirement, Vehicle>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        Vehicle resource)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        if (context.User.IsAdmin() || resource.OwnerId == context.User.GetUserId())
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>A driver may amend their own results; an admin may amend any.</summary>
public class ResultOwnerHandler
    : AuthorizationHandler<OperationAuthorizationRequirement, Result>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        Result resource)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        if (context.User.IsAdmin() || resource.UserId == context.User.GetUserId())
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
