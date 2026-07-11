using Threadia.Contracts.Messaging;

namespace Threadia.Modules.Attachments.PublicApi;

/// <summary>
/// Messaging モジュールへ公開する添付ファイル連携インターフェース。
/// 完了前(ストレージ未アップロード)の Attachment を Message へ関連付けない。
/// </summary>
public interface IMessageAttachments
{
    /// <summary>
    /// 添付候補を検証する(送信者本人のアップロード・同一会話・未関連付け・ストレージへのアップロード完了)。
    /// 不正な場合は ValidationException を送出する。
    /// </summary>
    Task<IReadOnlyList<MessageAttachmentDto>> ValidateAttachableAsync(
        Guid conversationId, Guid senderId, IReadOnlyCollection<Guid> attachmentIds, CancellationToken ct);

    /// <summary>検証済みの添付を Message へ関連付ける。</summary>
    Task BindToMessageAsync(Guid messageId, IReadOnlyCollection<Guid> attachmentIds, CancellationToken ct);

    /// <summary>Message ID ごとの添付一覧(履歴取得時のエンリッチ用)。</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<MessageAttachmentDto>>> GetByMessageIdsAsync(
        IReadOnlyCollection<Guid> messageIds, CancellationToken ct);
}
