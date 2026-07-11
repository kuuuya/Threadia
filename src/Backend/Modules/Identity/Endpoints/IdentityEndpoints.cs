using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Threadia.BuildingBlocks;
using Threadia.BuildingBlocks.Auth;
using Threadia.Modules.Identity.Application;
using Threadia.Modules.Identity.PublicApi;

namespace Threadia.Modules.Identity.Endpoints;

public sealed record RegisterRequest(string Email, string DisplayName, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(Guid UserId, string Email, string DisplayName, string Token, DateTime ExpiresAt);

public sealed record UserResponse(Guid Id, string Email, string DisplayName);

public static class IdentityEndpoints
{
    public static void MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth");

        auth.MapPost("/register", async (RegisterRequest request, AuthService service, CancellationToken ct) =>
        {
            var result = await service.RegisterAsync(request.Email, request.DisplayName, request.Password, ct);
            return Results.Created("/api/users/me", ToResponse(result));
        }).AllowAnonymous();

        auth.MapPost("/login", async (LoginRequest request, AuthService service, CancellationToken ct) =>
        {
            var result = await service.LoginAsync(request.Email, request.Password, ct);
            return Results.Ok(ToResponse(result));
        }).AllowAnonymous();

        var users = app.MapGroup("/api/users").RequireAuthorization();

        users.MapGet("/me", async (ICurrentUser currentUser, AuthService service, CancellationToken ct) =>
        {
            var result = await service.GetProfileAsync(currentUser.UserId, ct);
            return Results.Ok(new UserResponse(result.UserId, result.Email, result.DisplayName));
        });

        // 表示名解決用。id 指定の一覧取得(最大件数は Paging.MaxLimit)。
        users.MapGet("/", async (Guid[] ids, IUserDirectory directory, CancellationToken ct) =>
        {
            var limited = ids.Distinct().Take(Paging.MaxLimit).ToArray();
            var found = await directory.GetByIdsAsync(limited, ct);
            return Results.Ok(found.Select(u => new UserResponse(u.Id, u.Email, u.DisplayName)));
        });
    }

    private static AuthResponse ToResponse(AuthResult result) =>
        new(result.UserId, result.Email, result.DisplayName, result.Token, result.ExpiresAt);
}
