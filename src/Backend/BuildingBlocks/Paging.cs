namespace Threadia.BuildingBlocks;

/// <summary>一覧 API のページングパラメータ。limit は必ず上限で丸める。</summary>
public static class Paging
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;

    public static int ClampLimit(int? limit) =>
        limit is null or <= 0 ? DefaultLimit : Math.Min(limit.Value, MaxLimit);
}
