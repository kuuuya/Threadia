using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Threadia.BuildingBlocks.Events;
using Threadia.Contracts.Messaging;
using Threadia.Modules.Attachments.Infrastructure;
using Xunit;

namespace Threadia.IntegrationTests;

/// <summary>通知・検索・添付・Presence の統合テスト(InProcess バス + インメモリストレージ)。</summary>
[Collection("api")]
public class FeatureApiTests(ApiFixture fixture)
{
    private sealed record AuthDto(Guid UserId, string Email, string DisplayName, string Token);

    private sealed record WorkspaceDto(Guid Id, string Name);

    private sealed record ConversationDto(Guid Id, string Type, Guid[] MemberIds);

    private sealed record AttachmentInfoDto(Guid Id, string FileName, string ContentType, long Size);

    private sealed record MessageDtoR(Guid Id, Guid ConversationId, long Sequence, AttachmentInfoDto[] Attachments);

    private sealed record NotificationDto(Guid Id, string Type, Guid ConversationId, Guid MessageId, string Title, string Body);

    private sealed record SearchResultDto(Guid MessageId, Guid ConversationId, string Snippet);

    private sealed record SearchPageDto(SearchResultDto[] Items, bool HasMore);

    private sealed record UploadTicketDto(Guid AttachmentId, string UploadUrl);

    private sealed record PresenceDto(Guid UserId, bool IsOnline);

    private async Task<(HttpClient Client, AuthDto Auth)> RegisterAsync(string displayName)
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = $"{Guid.NewGuid():N}@example.com", displayName, password = "password-123" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var auth = (await response.Content.ReadFromJsonAsync<AuthDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (client, auth);
    }

    /// <summary>alice / bob の Direct と、会話非参加の charlie を用意する。</summary>
    private async Task<(HttpClient Alice, AuthDto AliceAuth, HttpClient Bob, AuthDto BobAuth, HttpClient Charlie, AuthDto CharlieAuth, Guid WorkspaceId, Guid ConversationId)>
        SetupAsync()
    {
        var (alice, aliceAuth) = await RegisterAsync("Alice");
        var (bob, bobAuth) = await RegisterAsync("Bob");
        var (charlie, charlieAuth) = await RegisterAsync("Charlie");

        var ws = (await (await alice.PostAsJsonAsync("/api/workspaces", new { name = $"ws-{Guid.NewGuid():N}" }))
            .Content.ReadFromJsonAsync<WorkspaceDto>())!;
        foreach (var email in new[] { bobAuth.Email, charlieAuth.Email })
        {
            (await alice.PostAsJsonAsync($"/api/workspaces/{ws.Id}/members", new { email })).EnsureSuccessStatusCode();
        }

        var conv = (await (await alice.PostAsJsonAsync(
                $"/api/workspaces/{ws.Id}/conversations/direct", new { otherUserId = bobAuth.UserId }))
            .Content.ReadFromJsonAsync<ConversationDto>())!;

        return (alice, aliceAuth, bob, bobAuth, charlie, charlieAuth, ws.Id, conv.Id);
    }

    private static async Task<MessageDtoR> SendAsync(
        HttpClient client, Guid conversationId, string content,
        Guid[]? mentionedUserIds = null, Guid[]? attachmentIds = null)
    {
        var response = await client.PostAsJsonAsync($"/api/conversations/{conversationId}/messages", new
        {
            content,
            clientMessageId = Guid.NewGuid().ToString("N"),
            mentionedUserIds = mentionedUserIds ?? [],
            attachmentIds = attachmentIds ?? [],
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<MessageDtoR>())!;
    }

    /// <summary>Outbox → Consumer の非同期反映を待つ。</summary>
    private static async Task<T> WaitUntilAsync<T>(Func<Task<T?>> probe, string description, int timeoutSeconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var result = await probe();
            if (result is not null)
            {
                return result;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"待機がタイムアウトしました: {description}");
    }

    [DockerFact]
    public async Task DirectMessageの通知が作成されOutbox再実行でも重複処理されない()
    {
        var (alice, aliceAuth, bob, _, _, _, _, conversationId) = await SetupAsync();
        await SendAsync(alice, conversationId, "こんにちはボブ");

        var notifications = await WaitUntilAsync(async () =>
        {
            var list = await bob.GetFromJsonAsync<NotificationDto[]>("/api/me/notifications");
            return list is { Length: > 0 } ? list : null;
        }, "Direct メッセージ通知の作成");

        notifications.Should().ContainSingle();
        notifications[0].Type.Should().Be("direct_message");
        notifications[0].Body.Should().Contain("こんにちはボブ");

        // 同じイベントを2回配信しても通知は1件しか増えない(ProcessedEvent + 決定的 NotificationId)。
        var syntheticEvent = new IntegrationEvent(
            Guid.NewGuid(),
            MessagingEventTypes.MessageSent,
            JsonSerializer.Serialize(new MessageEventPayload(conversationId, new MessageDto(
                Guid.NewGuid(), conversationId, 999, aliceAuth.UserId, "cm-x", "再配信テスト",
                DateTime.UtcNow, null, false, [], [])), MessagingEventTypes.SerializerOptions),
            DateTime.UtcNow);

        for (var i = 0; i < 2; i++)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var consumer = scope.ServiceProvider.GetServices<IIntegrationEventConsumer>()
                .First(c => c.Name == "notifications");
            await consumer.HandleAsync(syntheticEvent, CancellationToken.None);
        }

        var after = (await bob.GetFromJsonAsync<NotificationDto[]>("/api/me/notifications"))!;
        after.Should().HaveCount(2);
    }

    [DockerFact]
    public async Task メンションされたユーザーへmention通知が作成される()
    {
        var (alice, _, _, bobAuth, charlie, charlieAuth, workspaceId, _) = await SetupAsync();

        var group = (await (await alice.PostAsJsonAsync($"/api/workspaces/{workspaceId}/conversations/group",
                new { name = "general", memberIds = new[] { bobAuth.UserId, charlieAuth.UserId } }))
            .Content.ReadFromJsonAsync<ConversationDto>())!;

        await SendAsync(alice, group.Id, "@Charlie 確認お願いします", mentionedUserIds: [charlieAuth.UserId]);

        var notifications = await WaitUntilAsync(async () =>
        {
            var list = await charlie.GetFromJsonAsync<NotificationDto[]>("/api/me/notifications");
            return list is { Length: > 0 } ? list : null;
        }, "メンション通知の作成");

        notifications.Should().ContainSingle();
        notifications[0].Type.Should().Be("mention");
    }

    [DockerFact]
    public async Task 検索結果に権限外のMessageが含まれず削除で除去される()
    {
        var (alice, _, _, _, charlie, _, workspaceId, conversationId) = await SetupAsync();
        var keyword = $"合言葉{Guid.NewGuid():N}";
        var message = await SendAsync(alice, conversationId, $"これは {keyword} を含む秘密のメッセージ");

        // インデックス反映を待って検索できることを確認する。
        var page = await WaitUntilAsync(async () =>
        {
            var result = await alice.GetFromJsonAsync<SearchPageDto>(
                $"/api/workspaces/{workspaceId}/search?q={keyword}");
            return result is { Items.Length: > 0 } ? result : null;
        }, "検索インデックスの反映");

        page.Items.Should().ContainSingle();
        page.Items[0].MessageId.Should().Be(message.Id);

        // 会話非参加の charlie には同じキーワードでもヒットしない。
        var charlieResult = (await charlie.GetFromJsonAsync<SearchPageDto>(
            $"/api/workspaces/{workspaceId}/search?q={keyword}"))!;
        charlieResult.Items.Should().BeEmpty();

        // 削除するとインデックスからも除去される。
        (await alice.DeleteAsync($"/api/messages/{message.Id}")).EnsureSuccessStatusCode();
        await WaitUntilAsync(async () =>
        {
            var result = await alice.GetFromJsonAsync<SearchPageDto>(
                $"/api/workspaces/{workspaceId}/search?q={keyword}");
            return result is { Items.Length: 0 } ? result : null;
        }, "削除メッセージのインデックス除去");
    }

    [DockerFact]
    public async Task 添付ファイルはアップロード完了後のみメッセージへ関連付けられる()
    {
        var (alice, _, bob, _, charlie, _, _, conversationId) = await SetupAsync();

        var ticket = (await (await alice.PostAsJsonAsync($"/api/conversations/{conversationId}/attachments",
                new { fileName = "report.pdf", contentType = "application/pdf", size = 2048 }))
            .Content.ReadFromJsonAsync<UploadTicketDto>())!;

        // アップロード未完了のうちは添付できない。
        var premature = await alice.PostAsJsonAsync($"/api/conversations/{conversationId}/messages", new
        {
            content = "添付します",
            clientMessageId = Guid.NewGuid().ToString("N"),
            attachmentIds = new[] { ticket.AttachmentId },
        });
        premature.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // インメモリストレージへのアップロード完了を模擬する(キーは擬似 URL の末尾)。
        var storage = fixture.Services.GetRequiredService<InMemoryObjectStorage>();
        var key = Uri.UnescapeDataString(ticket.UploadUrl.Split("/upload/")[1]);
        storage.MarkUploaded(key, 2048);

        var message = await SendAsync(alice, conversationId, "レポートを添付します",
            attachmentIds: [ticket.AttachmentId]);

        message.Attachments.Should().ContainSingle();
        message.Attachments[0].FileName.Should().Be("report.pdf");

        // 会話参加者はダウンロード URL を取得できる。
        (await bob.GetAsync($"/api/attachments/{ticket.AttachmentId}/download-url"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // 非参加者には存在自体を秘匿する。
        (await charlie.GetAsync($"/api/attachments/{ticket.AttachmentId}/download-url"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [DockerFact]
    public async Task 禁止された拡張子の添付は拒否される()
    {
        var (alice, _, _, _, _, _, _, conversationId) = await SetupAsync();

        var response = await alice.PostAsJsonAsync($"/api/conversations/{conversationId}/attachments",
            new { fileName = "malware.exe", contentType = "application/octet-stream", size = 100 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [DockerFact]
    public async Task Presence未接続のユーザーはオフラインとして返る()
    {
        var (alice, _, _, bobAuth, _, _, workspaceId, _) = await SetupAsync();

        var presence = (await alice.GetFromJsonAsync<PresenceDto[]>(
            $"/api/workspaces/{workspaceId}/presence?userIds={bobAuth.UserId}"))!;

        presence.Should().ContainSingle();
        presence[0].IsOnline.Should().BeFalse();
    }
}
