import { describe, expect, it } from "vitest";
import type { Message } from "../../shared/types";
import { hasGap, mergeMessages } from "./mergeMessages";

function message(sequence: number, overrides?: Partial<Message>): Message {
  return {
    id: `id-${sequence}`,
    conversationId: "c1",
    sequence,
    senderId: "u1",
    clientMessageId: `cm-${sequence}`,
    content: `message ${sequence}`,
    createdAt: "2026-07-11T00:00:00Z",
    editedAt: null,
    isDeleted: false,
    mentionedUserIds: [],
    attachments: [],
    ...overrides,
  };
}

describe("mergeMessages", () => {
  it("Sequence昇順に統合する(受信順に依存しない)", () => {
    const merged = mergeMessages([message(1), message(3)], [message(2)]);
    expect(merged.map((m) => m.sequence)).toEqual([1, 2, 3]);
  });

  it("同じidのメッセージは新しい内容で置き換える(編集イベント)", () => {
    const edited = message(1, { content: "edited", editedAt: "2026-07-11T01:00:00Z" });
    const merged = mergeMessages([message(1), message(2)], [edited]);
    expect(merged).toHaveLength(2);
    expect(merged[0].content).toBe("edited");
  });

  it("削除イベントで本文が空になった状態を反映する", () => {
    const deleted = message(1, { content: "", isDeleted: true });
    const merged = mergeMessages([message(1)], [deleted]);
    expect(merged[0].isDeleted).toBe(true);
    expect(merged[0].content).toBe("");
  });

  it("重複受信しても件数が増えない", () => {
    const merged = mergeMessages([message(1)], [message(1)]);
    expect(merged).toHaveLength(1);
  });
});

describe("hasGap", () => {
  it("連続したSequenceは欠番なし", () => {
    expect(hasGap([message(1), message(2)], message(3))).toBe(false);
  });

  it("Sequenceが飛んでいる場合は欠番あり", () => {
    expect(hasGap([message(1), message(2)], message(5))).toBe(true);
  });

  it("既存メッセージがない場合は欠番なしとして扱う", () => {
    expect(hasGap([], message(10))).toBe(false);
  });
});
