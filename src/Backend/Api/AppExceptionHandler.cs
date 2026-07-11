using Microsoft.AspNetCore.Diagnostics;
using Threadia.BuildingBlocks.Exceptions;

namespace Threadia.Api;

/// <summary>AppException を Problem Details 形式のレスポンスへ変換する。</summary>
public sealed class AppExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not AppException appException)
        {
            return false;
        }

        await Results.Problem(statusCode: appException.StatusCode, detail: appException.Message)
            .ExecuteAsync(httpContext);
        return true;
    }
}
