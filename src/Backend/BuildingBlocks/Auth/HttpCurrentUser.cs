using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Threadia.BuildingBlocks.Auth;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (value is null || !Guid.TryParse(value, out var id))
            {
                throw new InvalidOperationException("認証済みユーザーが存在しません。");
            }

            return id;
        }
    }
}
