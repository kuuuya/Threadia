using System.Text.Json;

namespace Threadia.Contracts.Messaging;

/// <summary>
/// Messaging モジュールが Outbox 経由で発行するイベントの契約。
/// Notifications / Search / Attachments はこの型でペイロードを解釈する。
/// </summary>
public static class MessagingEventTypes
{
    public const string MessageSent = "message.sent";
    public const string MessageEdited = "message.edited";
    public const string MessageDeleted = "message.deleted";

    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
}

public sealed record MessageAttachmentDto(Guid Id, string FileName, string ContentType, long Size);

public sealed record MessageDto(
    Guid Id,
    Guid ConversationId,
    long Sequence,
    Guid SenderId,
    string ClientMessageId,
    string Content,
    DateTime CreatedAt,
    DateTime? EditedAt,
    bool IsDeleted,
    IReadOnlyList<Guid> MentionedUserIds,
    IReadOnlyList<MessageAttachmentDto> Attachments);

/// <summary>message.sent / message.edited のペイロード。</summary>
public sealed record MessageEventPayload(Guid ConversationId, MessageDto Message);

/// <summary>message.deleted のペイロード。削除後は本文を配信しない。</summary>
public sealed record MessageDeletedPayload(Guid ConversationId, Guid MessageId, long Sequence);
