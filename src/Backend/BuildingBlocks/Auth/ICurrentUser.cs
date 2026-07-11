namespace Threadia.BuildingBlocks.Auth;

/// <summary>
/// 認証済みユーザーの情報。権限判定は必ずサーバー側でこの値を使い、
/// クライアントから渡されたユーザー ID を信用しない。
/// </summary>
public interface ICurrentUser
{
    /// <summary>認証済みでない場合に参照すると例外を送出する。</summary>
    Guid UserId { get; }

    bool IsAuthenticated { get; }
}
