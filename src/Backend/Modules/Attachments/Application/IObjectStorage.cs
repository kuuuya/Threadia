namespace Threadia.Modules.Attachments.Application;

/// <summary>S3 互換オブジェクトストレージの境界。テストではインメモリ実装に差し替える。</summary>
public interface IObjectStorage
{
    /// <summary>クライアントが直接 PUT するための署名付き URL。</summary>
    Task<string> GetUploadUrlAsync(string key, string contentType, TimeSpan expiry, CancellationToken ct);

    /// <summary>ダウンロード用の署名付き URL(Content-Disposition: attachment)。</summary>
    Task<string> GetDownloadUrlAsync(string key, string fileName, TimeSpan expiry, CancellationToken ct);

    /// <summary>オブジェクトの実サイズ(バイト)。存在しない場合は null。</summary>
    Task<long?> GetObjectSizeAsync(string key, CancellationToken ct);

    Task DeleteAsync(string key, CancellationToken ct);
}
