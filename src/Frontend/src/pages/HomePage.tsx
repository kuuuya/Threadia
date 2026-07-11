import { HubConnectionState } from "@microsoft/signalr";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { useAuth } from "../features/auth/AuthContext";
import ChatPane from "../features/chat/ChatPane";
import { joinConversation, useChatConnection } from "../features/chat/useChatConnection";
import { enablePushNotifications } from "../features/notifications/push";
import { api } from "../shared/api";
import type {
  Conversation,
  SearchPage,
  UnreadCount,
  UserPresence,
  Workspace,
  WorkspaceMember,
} from "../shared/types";

export default function HomePage() {
  const { session, logout } = useAuth();
  const queryClient = useQueryClient();
  const [workspaceId, setWorkspaceId] = useState<string | null>(null);
  const [conversationId, setConversationId] = useState<string | null>(null);
  const [newWorkspaceName, setNewWorkspaceName] = useState("");
  const [inviteEmail, setInviteEmail] = useState("");
  const [groupName, setGroupName] = useState("");
  const [groupMemberIds, setGroupMemberIds] = useState<Set<string>>(new Set());
  const [sidebarError, setSidebarError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState("");
  const [pushStatus, setPushStatus] = useState<string | null>(null);

  const { connection, connected } = useChatConnection(session?.token ?? null);

  const workspacesQuery = useQuery({
    queryKey: ["workspaces"],
    queryFn: () => api.get<Workspace[]>("/api/workspaces"),
  });

  // 最初のワークスペースを自動選択する。
  useEffect(() => {
    if (!workspaceId && workspacesQuery.data && workspacesQuery.data.length > 0) {
      setWorkspaceId(workspacesQuery.data[0].id);
    }
  }, [workspaceId, workspacesQuery.data]);

  const membersQuery = useQuery({
    queryKey: ["members", workspaceId],
    queryFn: () => api.get<WorkspaceMember[]>(`/api/workspaces/${workspaceId}/members?limit=100`),
    enabled: workspaceId !== null,
  });

  const conversationsQuery = useQuery({
    queryKey: ["conversations", workspaceId],
    queryFn: () => api.get<Conversation[]>(`/api/workspaces/${workspaceId}/conversations`),
    enabled: workspaceId !== null,
  });

  const unreadsQuery = useQuery({
    queryKey: ["unread-counts", workspaceId],
    queryFn: () => api.get<UnreadCount[]>(`/api/workspaces/${workspaceId}/unread-counts`),
    enabled: workspaceId !== null,
    refetchInterval: 30_000,
  });

  // オンライン状態は強い整合性を求めず、定期ポーリングで取得する(ADR 0011)。
  const memberIdsKey = (membersQuery.data ?? []).map((m) => m.userId).sort().join(",");
  const presenceQuery = useQuery({
    queryKey: ["presence", workspaceId, memberIdsKey],
    queryFn: () => {
      const params = (membersQuery.data ?? []).map((m) => `userIds=${m.userId}`).join("&");
      return api.get<UserPresence[]>(`/api/workspaces/${workspaceId}/presence?${params}`);
    },
    enabled: workspaceId !== null && (membersQuery.data ?? []).length > 0,
    refetchInterval: 30_000,
  });

  const trimmedSearch = searchQuery.trim();
  const searchResultsQuery = useQuery({
    queryKey: ["search", workspaceId, trimmedSearch],
    queryFn: () =>
      api.get<SearchPage>(`/api/workspaces/${workspaceId}/search?q=${encodeURIComponent(trimmedSearch)}&limit=20`),
    enabled: workspaceId !== null && trimmedSearch.length > 0,
  });

  const memberNames = useMemo(() => {
    const map = new Map<string, string>();
    for (const member of membersQuery.data ?? []) map.set(member.userId, member.displayName);
    return map;
  }, [membersQuery.data]);

  const conversations = useMemo(() => conversationsQuery.data ?? [], [conversationsQuery.data]);
  const selectedConversation = conversations.find((c) => c.id === conversationId) ?? null;

  // 参加中の全会話の SignalR Group に参加する(未読更新のため)。
  useEffect(() => {
    if (!connection || !connected || connection.state !== HubConnectionState.Connected) return;
    for (const conversation of conversations) {
      void joinConversation(connection, conversation.id).catch(() => undefined);
    }
  }, [connection, connected, conversations]);

  // どの会話への新着でも未読数を更新する。
  useEffect(() => {
    if (!connection) return;
    const invalidate = () => void queryClient.invalidateQueries({ queryKey: ["unread-counts"] });
    connection.on("messageSent", invalidate);
    return () => connection.off("messageSent", invalidate);
  }, [connection, queryClient]);

  async function handleCreateWorkspace() {
    const name = newWorkspaceName.trim();
    if (!name) return;
    try {
      const workspace = await api.post<Workspace>("/api/workspaces", { name });
      setNewWorkspaceName("");
      await queryClient.invalidateQueries({ queryKey: ["workspaces"] });
      setWorkspaceId(workspace.id);
      setSidebarError(null);
    } catch (e) {
      setSidebarError(e instanceof Error ? e.message : "作成に失敗しました。");
    }
  }

  async function handleInvite() {
    const email = inviteEmail.trim();
    if (!email || !workspaceId) return;
    try {
      await api.post(`/api/workspaces/${workspaceId}/members`, { email });
      setInviteEmail("");
      await queryClient.invalidateQueries({ queryKey: ["members", workspaceId] });
      setSidebarError(null);
    } catch (e) {
      setSidebarError(e instanceof Error ? e.message : "招待に失敗しました。");
    }
  }

  async function handleStartDirect(otherUserId: string) {
    if (!workspaceId) return;
    try {
      const conversation = await api.post<Conversation>(
        `/api/workspaces/${workspaceId}/conversations/direct`,
        { otherUserId },
      );
      await queryClient.invalidateQueries({ queryKey: ["conversations", workspaceId] });
      setConversationId(conversation.id);
      setSidebarError(null);
    } catch (e) {
      setSidebarError(e instanceof Error ? e.message : "会話の作成に失敗しました。");
    }
  }

  async function handleCreateGroup() {
    const name = groupName.trim();
    if (!name || !workspaceId || groupMemberIds.size === 0) return;
    try {
      const conversation = await api.post<Conversation>(`/api/workspaces/${workspaceId}/conversations/group`, {
        name,
        memberIds: [...groupMemberIds],
      });
      setGroupName("");
      setGroupMemberIds(new Set());
      await queryClient.invalidateQueries({ queryKey: ["conversations", workspaceId] });
      setConversationId(conversation.id);
      setSidebarError(null);
    } catch (e) {
      setSidebarError(e instanceof Error ? e.message : "グループの作成に失敗しました。");
    }
  }

  function conversationLabel(conversation: Conversation): string {
    if (conversation.type === "Group") return `# ${conversation.name}`;
    const otherId = conversation.memberIds.find((id) => id !== session!.userId);
    return memberNames.get(otherId ?? "") ?? "Direct Message";
  }

  function unreadFor(id: string): number {
    return unreadsQuery.data?.find((u) => u.conversationId === id)?.unreadCount ?? 0;
  }

  async function handleEnablePush() {
    try {
      const result = await enablePushNotifications();
      setPushStatus(
        result === "enabled" ? "プッシュ通知を有効化しました" :
        result === "denied" ? "通知がブロックされています" : "このブラウザでは利用できません",
      );
    } catch (e) {
      setPushStatus(e instanceof Error ? e.message : "プッシュ通知の設定に失敗しました");
    }
  }

  const otherMembers = (membersQuery.data ?? []).filter((m) => m.userId !== session!.userId);
  const onlineUserIds = useMemo(
    () => new Set((presenceQuery.data ?? []).filter((p) => p.isOnline).map((p) => p.userId)),
    [presenceQuery.data],
  );

  return (
    <div className="home-layout">
      <aside className="sidebar">
        <div className="sidebar-header">
          <span className="me">{session!.displayName}</span>
          <button onClick={logout}>ログアウト</button>
        </div>

        <section>
          <h3>ワークスペース</h3>
          <select value={workspaceId ?? ""} onChange={(e) => setWorkspaceId(e.target.value || null)}>
            {(workspacesQuery.data ?? []).map((w) => (
              <option key={w.id} value={w.id}>
                {w.name}
              </option>
            ))}
          </select>
          <div className="inline-form">
            <input
              value={newWorkspaceName}
              onChange={(e) => setNewWorkspaceName(e.target.value)}
              placeholder="新しいワークスペース名"
              maxLength={80}
            />
            <button onClick={handleCreateWorkspace}>作成</button>
          </div>
        </section>

        {workspaceId && (
          <>
            <section>
              <h3>メンバー招待</h3>
              <div className="inline-form">
                <input
                  value={inviteEmail}
                  onChange={(e) => setInviteEmail(e.target.value)}
                  placeholder="メールアドレス"
                  type="email"
                />
                <button onClick={handleInvite}>招待</button>
              </div>
            </section>

            <section>
              <h3>会話</h3>
              <ul className="conversation-list">
                {conversations.map((c) => (
                  <li key={c.id}>
                    <button
                      className={c.id === conversationId ? "selected" : ""}
                      onClick={() => setConversationId(c.id)}
                    >
                      {conversationLabel(c)}
                      {unreadFor(c.id) > 0 && <span className="badge">{unreadFor(c.id)}</span>}
                    </button>
                  </li>
                ))}
              </ul>
            </section>

            <section>
              <h3>ダイレクトメッセージ</h3>
              <ul className="member-list">
                {otherMembers.map((m) => (
                  <li key={m.userId}>
                    <button onClick={() => handleStartDirect(m.userId)}>
                      <span>
                        <span className={onlineUserIds.has(m.userId) ? "presence-dot online" : "presence-dot"} />
                        {m.displayName}
                      </span>
                    </button>
                  </li>
                ))}
              </ul>
            </section>

            <section>
              <h3>グループ作成</h3>
              <input
                value={groupName}
                onChange={(e) => setGroupName(e.target.value)}
                placeholder="グループ名"
                maxLength={80}
              />
              <ul className="member-list">
                {otherMembers.map((m) => (
                  <li key={m.userId}>
                    <label>
                      <input
                        type="checkbox"
                        checked={groupMemberIds.has(m.userId)}
                        onChange={(e) => {
                          const next = new Set(groupMemberIds);
                          if (e.target.checked) next.add(m.userId);
                          else next.delete(m.userId);
                          setGroupMemberIds(next);
                        }}
                      />
                      {m.displayName}
                    </label>
                  </li>
                ))}
              </ul>
              <button onClick={handleCreateGroup} disabled={!groupName.trim() || groupMemberIds.size === 0}>
                グループを作成
              </button>
            </section>
          </>
        )}

        <section>
          <h3>通知</h3>
          <button onClick={handleEnablePush}>プッシュ通知を有効化</button>
          {pushStatus && <p className="push-status">{pushStatus}</p>}
        </section>

        {sidebarError && <p className="error">{sidebarError}</p>}
      </aside>

      <main className="main-pane">
        {workspaceId && (
          <div className="search-bar">
            <input
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="メッセージを検索"
              maxLength={100}
            />
            {trimmedSearch.length > 0 && (
              <div className="search-results">
                {(searchResultsQuery.data?.items ?? []).map((result) => (
                  <button
                    key={result.messageId}
                    className="search-result"
                    onClick={() => {
                      setConversationId(result.conversationId);
                      setSearchQuery("");
                    }}
                  >
                    <span className="search-result-meta">
                      {memberNames.get(result.senderId) ?? "不明"} ·{" "}
                      {new Date(result.createdAt).toLocaleString()}
                    </span>
                    <span>{result.snippet}</span>
                  </button>
                ))}
                {searchResultsQuery.data && searchResultsQuery.data.items.length === 0 && (
                  <p className="search-empty">一致するメッセージはありません</p>
                )}
              </div>
            )}
          </div>
        )}
        {selectedConversation ? (
          <ChatPane
            key={selectedConversation.id}
            conversation={selectedConversation}
            currentUserId={session!.userId}
            memberNames={memberNames}
            connection={connection}
            connected={connected}
            onlineUserIds={onlineUserIds}
          />
        ) : (
          <div className="empty-state">会話を選択するか、新しい会話を作成してください。</div>
        )}
      </main>
    </div>
  );
}
