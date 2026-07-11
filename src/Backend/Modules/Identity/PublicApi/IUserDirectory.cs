namespace Threadia.Modules.Identity.PublicApi;

/// <summary>
/// 他モジュールへ公開するユーザー参照インターフェース。
/// モジュール間で DbContext やテーブルを直接参照しないための境界。
/// </summary>
public interface IUserDirectory
{
    Task<UserSummary?> FindByEmailAsync(string email, CancellationToken ct = default);

    Task<IReadOnlyList<UserSummary>> GetByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);

    Task<bool> ExistsAsync(Guid userId, CancellationToken ct = default);
}

public sealed record UserSummary(Guid Id, string Email, string DisplayName);
