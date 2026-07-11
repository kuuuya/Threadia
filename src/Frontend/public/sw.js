// Web Push 用 Service Worker。
// notificationId を tag に使い、同じ通知の重複表示を防ぐ(at-least-once 配信のため)。
self.addEventListener("push", (event) => {
  if (!event.data) return;
  let payload;
  try {
    payload = event.data.json();
  } catch {
    return;
  }

  event.waitUntil(
    self.registration.showNotification(payload.title ?? "Threadia", {
      body: payload.body ?? "",
      tag: payload.notificationId ?? undefined,
      data: { conversationId: payload.conversationId },
    }),
  );
});

self.addEventListener("notificationclick", (event) => {
  event.notification.close();
  event.waitUntil(
    self.clients.matchAll({ type: "window", includeUncontrolled: true }).then((clients) => {
      const existing = clients.find((c) => "focus" in c);
      if (existing) return existing.focus();
      return self.clients.openWindow("/");
    }),
  );
});
