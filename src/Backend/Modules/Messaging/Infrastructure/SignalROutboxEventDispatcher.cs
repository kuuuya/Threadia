using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Threadia.Modules.Messaging.Application;
using Threadia.Modules.Messaging.Endpoints;

namespace Threadia.Modules.Messaging.Infrastructure;

public sealed class SignalROutboxEventDispatcher(IHubContext<ChatHub> hubContext) : IOutboxEventDispatcher
{
    public async Task DispatchAsync(string type, string payload, CancellationToken ct)
    {
        if (!MessagingClientMethods.ByEventType.TryGetValue(type, out var clientMethod))
        {
            throw new InvalidOperationException($"未知のイベント種別です: {type}");
        }

        using var document = JsonDocument.Parse(payload);
        var conversationId = document.RootElement.GetProperty("conversationId").GetGuid();

        await hubContext.Clients
            .Group(ChatHub.ConversationGroup(conversationId))
            .SendAsync(clientMethod, document.RootElement.Clone(), ct);
    }
}
