import type { Message } from "../../shared/types";

/**
 * メッセージリストへ新着・編集・削除を統合する。
 * - Sequence 昇順を維持する(SignalR で先に受信しても順序はサーバー採番に従う)
 * - 同じ id は新しい内容で置き換える(編集・削除イベント)
 * - クライアント時刻には依存しない
 */
export function mergeMessages(existing: Message[], incoming: Message[]): Message[] {
  const byId = new Map<string, Message>();
  for (const message of existing) byId.set(message.id, message);
  for (const message of incoming) byId.set(message.id, message);
  return [...byId.values()].sort((a, b) => a.sequence - b.sequence);
}

/**
 * 受信済みの最大 Sequence と新着メッセージの間に欠番があるかを判定する。
 * 欠番がある場合、クライアントは afterSequence API で不足分を取得する。
 */
export function hasGap(messages: Message[], incoming: Message): boolean {
  if (messages.length === 0) return false;
  const maxSequence = messages[messages.length - 1].sequence;
  return incoming.sequence > maxSequence + 1;
}
