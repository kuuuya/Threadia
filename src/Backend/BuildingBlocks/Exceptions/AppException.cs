using Microsoft.AspNetCore.Http;

namespace Threadia.BuildingBlocks.Exceptions;

/// <summary>
/// アプリケーション層で発生させる例外の基底。Api 層で Problem Details へ変換される。
/// </summary>
public abstract class AppException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }
}

/// <summary>入力値が不正な場合(400)。</summary>
public sealed class ValidationException(string message) : AppException(message)
{
    public override int StatusCode => StatusCodes.Status400BadRequest;
}

/// <summary>認可されていない操作(403)。存在の秘匿が必要な場合は NotFoundException を使う。</summary>
public sealed class ForbiddenException(string message) : AppException(message)
{
    public override int StatusCode => StatusCodes.Status403Forbidden;
}

/// <summary>対象が存在しない、または参照権限がない場合(404)。</summary>
public sealed class NotFoundException(string message) : AppException(message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;
}

/// <summary>一意制約違反など現在の状態と矛盾する操作(409)。</summary>
public sealed class ConflictException(string message) : AppException(message)
{
    public override int StatusCode => StatusCodes.Status409Conflict;
}
