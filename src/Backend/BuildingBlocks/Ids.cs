namespace Threadia.BuildingBlocks;

/// <summary>
/// ID生成の共通実装。UUIDv7 を使用し、時系列順に近い並びでインデックス局所性を確保する。
/// </summary>
public static class Ids
{
    public static Guid New() => Guid.CreateVersion7();
}
