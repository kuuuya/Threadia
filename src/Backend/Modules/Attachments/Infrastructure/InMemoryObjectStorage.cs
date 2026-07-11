using System.Collections.Concurrent;
using Threadia.Modules.Attachments.Application;

namespace Threadia.Modules.Attachments.Infrastructure;

/// <summary>
/// テスト・ストレージなし環境用のインメモリ実装。
/// 署名付き URL の代わりに擬似 URL を返し、アップロードは MarkUploaded で模擬する。
/// </summary>
public sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly ConcurrentDictionary<string, long> _objects = new();

    public Task<string> GetUploadUrlAsync(string key, string contentType, TimeSpan expiry, CancellationToken ct) =>
        Task.FromResult($"https://storage.invalid/upload/{Uri.EscapeDataString(key)}");

    public Task<string> GetDownloadUrlAsync(string key, string fileName, TimeSpan expiry, CancellationToken ct) =>
        Task.FromResult($"https://storage.invalid/download/{Uri.EscapeDataString(key)}");

    public Task<long?> GetObjectSizeAsync(string key, CancellationToken ct) =>
        Task.FromResult(_objects.TryGetValue(key, out var size) ? size : (long?)null);

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        _objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    /// <summary>テストからアップロード完了状態を模擬する。</summary>
    public void MarkUploaded(string key, long size) => _objects[key] = size;
}
