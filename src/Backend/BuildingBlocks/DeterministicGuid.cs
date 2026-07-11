using System.Security.Cryptography;
using System.Text;

namespace Threadia.BuildingBlocks;

/// <summary>
/// 入力文字列から常に同じ Guid を導出する。
/// イベント再処理時に同じ ID(例: NotificationId)を得るための冪等性キー生成に使う。
/// </summary>
public static class DeterministicGuid
{
    public static Guid Create(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(hash.AsSpan(0, 16));
    }
}
