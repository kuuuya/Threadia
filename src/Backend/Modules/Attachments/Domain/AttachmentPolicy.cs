namespace Threadia.Modules.Attachments.Domain;

/// <summary>ファイルサイズ・形式・ファイル名の制限(CLAUDE.local.md「ファイルサイズ、MIME Type、拡張子を検証する」)。</summary>
public static class AttachmentPolicy
{
    public const long MaxSizeBytes = 25 * 1024 * 1024;
    public const int MaxFileNameLength = 200;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg",
        ".pdf", ".txt", ".md", ".csv", ".json", ".zip",
        ".docx", ".xlsx", ".pptx", ".mp3", ".mp4", ".webm",
    };

    private static readonly HashSet<string> AllowedContentTypePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/", "video/", "audio/", "text/",
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "application/zip", "application/json", "application/octet-stream",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
    };

    /// <summary>検証してサニタイズ済みファイル名を返す。不正な場合は ArgumentException。</summary>
    public static string Validate(string fileName, string contentType, long size)
    {
        if (size <= 0 || size > MaxSizeBytes)
        {
            throw new ArgumentException($"ファイルサイズは1バイト〜{MaxSizeBytes / (1024 * 1024)}MBの範囲で指定してください。", nameof(size));
        }

        var sanitized = Sanitize(fileName);
        if (sanitized.Length is 0 or > MaxFileNameLength)
        {
            throw new ArgumentException("ファイル名が不正です。", nameof(fileName));
        }

        var extension = Path.GetExtension(sanitized);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException($"拡張子 {extension} のファイルはアップロードできません。", nameof(fileName));
        }

        if (!AllowedContentTypes.Contains(contentType) &&
            !AllowedContentTypePrefixes.Any(p => contentType.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"Content-Type {contentType} は許可されていません。", nameof(contentType));
        }

        return sanitized;
    }

    /// <summary>パス区切り・制御文字を除去し、ファイル名部分のみを残す。</summary>
    private static string Sanitize(string fileName)
    {
        var name = fileName.Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..].Trim();

        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Where(c => !invalid.Contains(c) && !char.IsControl(c)).ToArray();
        return new string(chars);
    }
}
