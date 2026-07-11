import type { HubConnection } from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useRef, useState, type FormEvent, type ReactNode } from "react";
import { api } from "../../shared/api";
import type { Conversation, DownloadUrl, Message, MessageAttachment, MessagePage, UploadTicket } from "../../shared/types";
import { hasGap, mergeMessages } from "../messages/mergeMessages";

interface ChatPaneProps {
  conversation: Conversation;
  currentUserId: string;
  memberNames: Map<string, string>;
  connection: HubConnection | null;
  connected: boolean;
  onlineUserIds: Set<string>;
}

interface MessageDeletedEvent {
  conversationId: string;
  messageId: string;
  sequence: number;
}

interface PendingAttachment {
  id: string;
  fileName: string;
  uploading: boolean;
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

/** メンション(@表示名)をハイライトして描画する。対象は API で明示された UserId に対応する名前のみ。 */
function renderContent(content: string, mentionNames: string[]): ReactNode {
  if (mentionNames.length === 0) return content;
  const pattern = new RegExp(`@(${mentionNames.map(escapeRegExp).join("|")})`, "g");
  const parts = content.split(pattern);
  return parts.map((part, i) =>
    i % 2 === 1 ? (
      <span key={i} className="mention">
        @{part}
      </span>
    ) : (
      part
    ),
  );
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

export default function ChatPane({
  conversation,
  currentUserId,
  memberNames,
  connection,
  connected,
  onlineUserIds,
}: ChatPaneProps) {
  const queryClient = useQueryClient();
  const [messages, setMessages] = useState<Message[]>([]);
  const [hasMore, setHasMore] = useState(false);
  const [draft, setDraft] = useState("");
  const [mentions, setMentions] = useState<Map<string, string>>(new Map());
  const [showMentionPicker, setShowMentionPicker] = useState(false);
  const [pendingAttachments, setPendingAttachments] = useState<PendingAttachment[]>([]);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editDraft, setEditDraft] = useState("");
  const [error, setError] = useState<string | null>(null);
  const bottomRef = useRef<HTMLDivElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const lastReportedReadRef = useRef(0);

  const conversationId = conversation.id;

  // 初期履歴の読み込み。
  useEffect(() => {
    let cancelled = false;
    setMessages([]);
    setHasMore(false);
    lastReportedReadRef.current = 0;
    api
      .get<MessagePage>(`/api/conversations/${conversationId}/messages?limit=50`)
      .then((page) => {
        if (cancelled) return;
        setMessages(page.items);
        setHasMore(page.hasMore);
      })
      .catch((e) => setError(e instanceof Error ? e.message : "履歴の取得に失敗しました。"));
    return () => {
      cancelled = true;
    };
  }, [conversationId]);

  // 欠番補完。切断からの復帰時にも使用する。
  const fetchAfter = useCallback(
    async (afterSequence: number) => {
      const page = await api.get<MessagePage>(
        `/api/conversations/${conversationId}/messages?afterSequence=${afterSequence}&limit=100`,
      );
      setMessages((current) => mergeMessages(current, page.items));
    },
    [conversationId],
  );

  // SignalR イベントの購読。欠番を検知したら API から不足分を取得する。
  useEffect(() => {
    if (!connection) return;

    const onMessage = (message: Message) => {
      if (message.conversationId !== conversationId) return;
      setMessages((current) => {
        if (hasGap(current, message)) {
          const maxSequence = current[current.length - 1].sequence;
          void fetchAfter(maxSequence).catch(() => undefined);
        }
        return mergeMessages(current, [message]);
      });
    };

    const onMessageEvent = (payload: { conversationId: string; message: Message }) => onMessage(payload.message);

    const onDeleted = (payload: MessageDeletedEvent) => {
      if (payload.conversationId !== conversationId) return;
      setMessages((current) =>
        current.map((m) =>
          m.id === payload.messageId ? { ...m, content: "", isDeleted: true, attachments: [] } : m,
        ),
      );
    };

    connection.on("messageSent", onMessageEvent);
    connection.on("messageEdited", onMessageEvent);
    connection.on("messageDeleted", onDeleted);
    return () => {
      connection.off("messageSent", onMessageEvent);
      connection.off("messageEdited", onMessageEvent);
      connection.off("messageDeleted", onDeleted);
    };
  }, [connection, conversationId, fetchAfter]);

  // 再接続時に不足分を補完する。
  useEffect(() => {
    if (!connected || messages.length === 0) return;
    void fetchAfter(messages[messages.length - 1].sequence).catch(() => undefined);
    // 再接続イベント(connected の変化)のみを契機とする。
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [connected]);

  // 既読位置の送信(最新 Sequence を一定間隔でまとめて送る)。
  useEffect(() => {
    if (messages.length === 0) return;
    const latest = messages[messages.length - 1].sequence;
    if (latest <= lastReportedReadRef.current) return;

    const timer = setTimeout(() => {
      lastReportedReadRef.current = latest;
      void api
        .put(`/api/conversations/${conversationId}/read-position`, { lastReadSequence: latest })
        .then(() => queryClient.invalidateQueries({ queryKey: ["unread-counts"] }))
        .catch(() => undefined);
    }, 800);
    return () => clearTimeout(timer);
  }, [messages, conversationId, queryClient]);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages.length]);

  function addMention(userId: string, name: string) {
    setMentions((current) => new Map(current).set(userId, name));
    setDraft((current) => `${current}${current.endsWith(" ") || current.length === 0 ? "" : " "}@${name} `);
    setShowMentionPicker(false);
  }

  async function handleFilesSelected(files: FileList | null) {
    if (!files) return;
    setError(null);
    for (const file of Array.from(files)) {
      const placeholder: PendingAttachment = { id: crypto.randomUUID(), fileName: file.name, uploading: true };
      setPendingAttachments((current) => [...current, placeholder]);
      try {
        const contentType = file.type || "application/octet-stream";
        // 1. メタデータ登録 + 署名付き URL 取得 → 2. ストレージへ直接 PUT
        const ticket = await api.post<UploadTicket>(`/api/conversations/${conversationId}/attachments`, {
          fileName: file.name,
          contentType,
          size: file.size,
        });
        const putResponse = await fetch(ticket.uploadUrl, {
          method: "PUT",
          headers: { "Content-Type": contentType },
          body: file,
        });
        if (!putResponse.ok) throw new Error(`アップロードに失敗しました (${putResponse.status})`);
        setPendingAttachments((current) =>
          current.map((a) => (a.id === placeholder.id ? { id: ticket.attachmentId, fileName: file.name, uploading: false } : a)),
        );
      } catch (e) {
        setPendingAttachments((current) => current.filter((a) => a.id !== placeholder.id));
        setError(e instanceof Error ? e.message : "アップロードに失敗しました。");
      }
    }
    if (fileInputRef.current) fileInputRef.current.value = "";
  }

  async function handleSend(event: FormEvent) {
    event.preventDefault();
    const content = draft.trim();
    if (!content || pendingAttachments.some((a) => a.uploading)) return;
    setError(null);
    try {
      // 表示文字列に残っているメンションだけを対象にする(削除された @名前 は送らない)。
      const mentionedUserIds = [...mentions.entries()]
        .filter(([, name]) => content.includes(`@${name}`))
        .map(([userId]) => userId);

      // ClientMessageId により再送してもサーバー側で重複登録されない。
      const message = await api.post<Message>(`/api/conversations/${conversationId}/messages`, {
        content,
        clientMessageId: crypto.randomUUID(),
        mentionedUserIds,
        attachmentIds: pendingAttachments.map((a) => a.id),
      });
      setMessages((current) => mergeMessages(current, [message]));
      setDraft("");
      setMentions(new Map());
      setPendingAttachments([]);
    } catch (e) {
      setError(e instanceof Error ? e.message : "送信に失敗しました。");
    }
  }

  async function handleLoadOlder() {
    if (messages.length === 0) return;
    const page = await api.get<MessagePage>(
      `/api/conversations/${conversationId}/messages?beforeSequence=${messages[0].sequence}&limit=50`,
    );
    setMessages((current) => mergeMessages(current, page.items));
    setHasMore(page.hasMore);
  }

  async function handleEditSubmit(messageId: string) {
    const content = editDraft.trim();
    if (!content) return;
    try {
      const updated = await api.patch<Message>(`/api/messages/${messageId}`, { content });
      setMessages((current) => mergeMessages(current, [updated]));
      setEditingId(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "編集に失敗しました。");
    }
  }

  async function handleDelete(messageId: string) {
    try {
      await api.delete(`/api/messages/${messageId}`);
      setMessages((current) =>
        current.map((m) => (m.id === messageId ? { ...m, content: "", isDeleted: true, attachments: [] } : m)),
      );
    } catch (e) {
      setError(e instanceof Error ? e.message : "削除に失敗しました。");
    }
  }

  async function handleDownload(attachment: MessageAttachment) {
    try {
      const { url } = await api.get<DownloadUrl>(`/api/attachments/${attachment.id}/download-url`);
      window.open(url, "_blank", "noopener");
    } catch (e) {
      setError(e instanceof Error ? e.message : "ダウンロードに失敗しました。");
    }
  }

  const otherMemberId = conversation.memberIds.find((id) => id !== currentUserId);
  const title =
    conversation.type === "Group"
      ? `# ${conversation.name}`
      : (memberNames.get(otherMemberId ?? "") ?? "Direct Message");
  const showPresenceDot = conversation.type === "Direct" && otherMemberId !== undefined;

  return (
    <section className="chat-pane">
      <header className="chat-header">
        <h2>
          {showPresenceDot && (
            <span className={onlineUserIds.has(otherMemberId) ? "presence-dot online" : "presence-dot"} />
          )}
          {title}
        </h2>
        <span className={connected ? "status online" : "status offline"}>{connected ? "接続中" : "未接続"}</span>
      </header>

      <div className="message-list">
        {hasMore && (
          <button className="load-older" onClick={handleLoadOlder}>
            さらに読み込む
          </button>
        )}
        {messages.map((message) => (
          <div key={message.id} className={`message ${message.senderId === currentUserId ? "own" : ""}`}>
            <div className="message-meta">
              <strong>{memberNames.get(message.senderId) ?? "退出済みユーザー"}</strong>
              <time>{new Date(message.createdAt).toLocaleTimeString()}</time>
              {message.editedAt && !message.isDeleted && <em>(編集済み)</em>}
            </div>
            {message.isDeleted ? (
              <p className="deleted">このメッセージは削除されました</p>
            ) : editingId === message.id ? (
              <div className="edit-row">
                <input value={editDraft} onChange={(e) => setEditDraft(e.target.value)} maxLength={4000} />
                <button onClick={() => handleEditSubmit(message.id)}>保存</button>
                <button onClick={() => setEditingId(null)}>取消</button>
              </div>
            ) : (
              <>
                <p>
                  {renderContent(
                    message.content,
                    message.mentionedUserIds
                      .map((id) => memberNames.get(id))
                      .filter((name): name is string => name !== undefined),
                  )}
                </p>
                {message.attachments.length > 0 && (
                  <div className="attachment-list">
                    {message.attachments.map((attachment) => (
                      <button key={attachment.id} className="attachment-chip" onClick={() => handleDownload(attachment)}>
                        📎 {attachment.fileName} ({formatSize(attachment.size)})
                      </button>
                    ))}
                  </div>
                )}
              </>
            )}
            {message.senderId === currentUserId && !message.isDeleted && editingId !== message.id && (
              <div className="message-actions">
                <button
                  onClick={() => {
                    setEditingId(message.id);
                    setEditDraft(message.content);
                  }}
                >
                  編集
                </button>
                <button onClick={() => handleDelete(message.id)}>削除</button>
              </div>
            )}
          </div>
        ))}
        <div ref={bottomRef} />
      </div>

      {error && <p className="error">{error}</p>}

      {pendingAttachments.length > 0 && (
        <div className="pending-attachments">
          {pendingAttachments.map((attachment) => (
            <span key={attachment.id} className="attachment-chip">
              {attachment.uploading ? "⏳" : "📎"} {attachment.fileName}
              {!attachment.uploading && (
                <button
                  type="button"
                  className="remove-chip"
                  onClick={() => setPendingAttachments((current) => current.filter((a) => a.id !== attachment.id))}
                >
                  ×
                </button>
              )}
            </span>
          ))}
        </div>
      )}

      <form className="composer" onSubmit={handleSend}>
        <div className="composer-tools">
          <button type="button" title="メンション" onClick={() => setShowMentionPicker((v) => !v)}>
            @
          </button>
          <button type="button" title="ファイルを添付" onClick={() => fileInputRef.current?.click()}>
            📎
          </button>
          <input
            ref={fileInputRef}
            type="file"
            multiple
            hidden
            onChange={(e) => void handleFilesSelected(e.target.files)}
          />
        </div>
        {showMentionPicker && (
          <div className="mention-picker">
            {conversation.memberIds
              .filter((id) => id !== currentUserId)
              .map((id) => (
                <button key={id} type="button" onClick={() => addMention(id, memberNames.get(id) ?? "unknown")}>
                  @{memberNames.get(id) ?? "unknown"}
                </button>
              ))}
          </div>
        )}
        <input
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          placeholder="メッセージを入力"
          maxLength={4000}
        />
        <button type="submit" disabled={!draft.trim() || pendingAttachments.some((a) => a.uploading)}>
          送信
        </button>
      </form>
    </section>
  );
}
