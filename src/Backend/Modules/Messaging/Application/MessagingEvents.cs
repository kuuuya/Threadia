using Threadia.Contracts.Messaging;

namespace Threadia.Modules.Messaging.Application;

/// <summary>イベント種別と SignalR クライアントメソッド名の対応(SignalR 配信専用)。</summary>
public static class MessagingClientMethods
{
    public static readonly IReadOnlyDictionary<string, string> ByEventType = new Dictionary<string, string>
    {
        [MessagingEventTypes.MessageSent] = "messageSent",
        [MessagingEventTypes.MessageEdited] = "messageEdited",
        [MessagingEventTypes.MessageDeleted] = "messageDeleted",
    };
}
