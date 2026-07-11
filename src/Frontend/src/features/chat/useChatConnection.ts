import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import { useEffect, useRef, useState } from "react";

/**
 * SignalR 接続を管理する。SignalR は通知手段でありデータの正本ではないため、
 * 切断・再接続時の取りこぼしは呼び出し側が afterSequence API で補完する。
 */
export function useChatConnection(token: string | null): {
  connection: HubConnection | null;
  connected: boolean;
} {
  const connectionRef = useRef<HubConnection | null>(null);
  const [connection, setConnection] = useState<HubConnection | null>(null);
  const [connected, setConnected] = useState(false);

  useEffect(() => {
    if (!token) return;

    const hub = new HubConnectionBuilder()
      .withUrl("/hubs/chat", { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connectionRef.current = hub;
    setConnection(hub);

    hub.onreconnected(() => setConnected(true));
    hub.onreconnecting(() => setConnected(false));
    hub.onclose(() => setConnected(false));

    let cancelled = false;
    hub
      .start()
      .then(() => {
        if (!cancelled) setConnected(true);
      })
      .catch((e) => console.error("SignalR 接続に失敗しました", e));

    // Presence の TTL を維持する heartbeat。切断イベントだけに依存しない。
    const heartbeat = setInterval(() => {
      if (hub.state === HubConnectionState.Connected) {
        void hub.invoke("Heartbeat").catch(() => undefined);
      }
    }, 30_000);

    return () => {
      cancelled = true;
      clearInterval(heartbeat);
      setConnected(false);
      setConnection(null);
      void hub.stop();
    };
  }, [token]);

  return { connection: connection ?? connectionRef.current, connected };
}

export async function joinConversation(connection: HubConnection, conversationId: string): Promise<void> {
  if (connection.state === HubConnectionState.Connected) {
    await connection.invoke("JoinConversation", conversationId);
  }
}
