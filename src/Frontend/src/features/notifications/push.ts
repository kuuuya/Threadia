import { api } from "../../shared/api";

/**
 * Web プッシュ通知を有効化する。
 * 1. 通知許可 → 2. Service Worker 登録 → 3. VAPID 公開鍵で購読 → 4. サーバーへ購読情報を登録
 */
export async function enablePushNotifications(): Promise<"enabled" | "denied" | "unsupported"> {
  if (!("serviceWorker" in navigator) || !("PushManager" in window) || !("Notification" in window)) {
    return "unsupported";
  }

  const permission = await Notification.requestPermission();
  if (permission !== "granted") {
    return "denied";
  }

  const { publicKey } = await api.get<{ publicKey: string | null }>("/api/push/vapid-public-key");
  if (!publicKey) {
    return "unsupported";
  }

  const registration = await navigator.serviceWorker.register("/sw.js");
  const subscription =
    (await registration.pushManager.getSubscription()) ??
    (await registration.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: urlBase64ToUint8Array(publicKey),
    }));

  const json = subscription.toJSON();
  await api.post("/api/me/push-subscriptions", {
    endpoint: subscription.endpoint,
    p256dh: json.keys?.p256dh ?? "",
    auth: json.keys?.auth ?? "",
  });

  return "enabled";
}

function urlBase64ToUint8Array(base64: string): Uint8Array {
  const padding = "=".repeat((4 - (base64.length % 4)) % 4);
  const normalized = (base64 + padding).replace(/-/g, "+").replace(/_/g, "/");
  const raw = atob(normalized);
  return Uint8Array.from(raw, (c) => c.charCodeAt(0));
}
