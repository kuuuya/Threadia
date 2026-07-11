using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Threadia.IntegrationTests;

[Collection("api")]
public class MessagingApiTests(ApiFixture fixture)
{
    private sealed record AuthDto(Guid UserId, string Email, string DisplayName, string Token);

    private sealed record WorkspaceDto(Guid Id, string Name);

    private sealed record ConversationDto(Guid Id, string Type, string? Name, Guid[] MemberIds);

    private sealed record MessageDto(
        Guid Id, Guid ConversationId, long Sequence, Guid SenderId, string ClientMessageId,
        string Content, DateTime CreatedAt, DateTime? EditedAt, bool IsDeleted, Guid[] MentionedUserIds);

    private sealed record MessagePageDto(MessageDto[] Items, bool HasMore);

    private sealed record ReadPositionDto(Guid ConversationId, long LastReadSequence);

    private sealed record UnreadCountDto(Guid ConversationId, long LatestSequence, long LastReadSequence, long UnreadCount);

    private async Task<(HttpClient Client, AuthDto Auth)> RegisterAsync(string displayName)
    {
        var client = fixture.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email, displayName, password = "password-123" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var auth = (await response.Content.ReadFromJsonAsync<AuthDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (client, auth);
    }

    private static async Task<WorkspaceDto> CreateWorkspaceAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/workspaces", new { name = $"ws-{Guid.NewGuid():N}" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<WorkspaceDto>())!;
    }

    private static async Task AddWorkspaceMemberAsync(HttpClient owner, Guid workspaceId, string email)
    {
        var response = await owner.PostAsJsonAsync($"/api/workspaces/{workspaceId}/members", new { email });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private static async Task<ConversationDto> CreateDirectAsync(HttpClient client, Guid workspaceId, Guid otherUserId)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/conversations/direct", new { otherUserId });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ConversationDto>())!;
    }

    private static async Task<MessageDto> SendMessageAsync(
        HttpClient client, Guid conversationId, string content, string? clientMessageId = null)
    {
        var response = await client.PostAsJsonAsync($"/api/conversations/{conversationId}/messages",
            new { content, clientMessageId = clientMessageId ?? Guid.NewGuid().ToString("N") });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<MessageDto>())!;
    }

    /// <summary>2ユーザーと Direct Conversation を用意する共通セットアップ。</summary>
    private async Task<(HttpClient Alice, HttpClient Bob, AuthDto BobAuth, Guid WorkspaceId, Guid ConversationId)> SetupDirectConversationAsync()
    {
        var (alice, _) = await RegisterAsync("Alice");
        var (bob, bobAuth) = await RegisterAsync("Bob");
        var workspace = await CreateWorkspaceAsync(alice);
        await AddWorkspaceMemberAsync(alice, workspace.Id, bobAuth.Email);
        var conversation = await CreateDirectAsync(alice, workspace.Id, bobAuth.UserId);
        return (alice, bob, bobAuth, workspace.Id, conversation.Id);
    }

    [DockerFact]
    public async Task 登録からメッセージ送信と履歴取得までの一連の操作が成功する()
    {
        var (alice, bob, _, _, conversationId) = await SetupDirectConversationAsync();

        await SendMessageAsync(alice, conversationId, "こんにちは");
        await SendMessageAsync(bob, conversationId, "やあ");

        var page = (await alice.GetFromJsonAsync<MessagePageDto>(
            $"/api/conversations/{conversationId}/messages"))!;

        page.Items.Should().HaveCount(2);
        page.Items.Select(m => m.Sequence).Should().BeInAscendingOrder();
        page.Items[0].Content.Should().Be("こんにちは");
        page.Items[1].Content.Should().Be("やあ");
    }

    [DockerFact]
    public async Task 同一Conversationへの並行投稿でSequenceが重複しない()
    {
        var (alice, _, _, _, conversationId) = await SetupDirectConversationAsync();

        var tasks = Enumerable.Range(0, 20)
            .Select(i => SendMessageAsync(alice, conversationId, $"message-{i}"))
            .ToArray();
        var messages = await Task.WhenAll(tasks);

        var sequences = messages.Select(m => m.Sequence).ToList();
        sequences.Should().OnlyHaveUniqueItems();
        sequences.Should().BeEquivalentTo(Enumerable.Range(1, 20).Select(i => (long)i));
    }

    [DockerFact]
    public async Task 同じClientMessageIdの再送でMessageが重複しない()
    {
        var (alice, _, _, _, conversationId) = await SetupDirectConversationAsync();
        var clientMessageId = Guid.NewGuid().ToString("N");

        var first = await SendMessageAsync(alice, conversationId, "once", clientMessageId);
        var second = await SendMessageAsync(alice, conversationId, "once", clientMessageId);

        second.Id.Should().Be(first.Id);
        second.Sequence.Should().Be(first.Sequence);

        var page = (await alice.GetFromJsonAsync<MessagePageDto>(
            $"/api/conversations/{conversationId}/messages"))!;
        page.Items.Should().HaveCount(1);
    }

    [DockerFact]
    public async Task LastReadSequenceは後退しない()
    {
        var (alice, _, _, _, conversationId) = await SetupDirectConversationAsync();
        for (var i = 0; i < 5; i++)
        {
            await SendMessageAsync(alice, conversationId, $"m{i}");
        }

        var advanced = await alice.PutAsJsonAsync(
            $"/api/conversations/{conversationId}/read-position", new { lastReadSequence = 5 });
        (await advanced.Content.ReadFromJsonAsync<ReadPositionDto>())!.LastReadSequence.Should().Be(5);

        var regressed = await alice.PutAsJsonAsync(
            $"/api/conversations/{conversationId}/read-position", new { lastReadSequence = 3 });
        (await regressed.Content.ReadFromJsonAsync<ReadPositionDto>())!.LastReadSequence.Should().Be(5);
    }

    [DockerFact]
    public async Task 未読数は最新Sequenceと既読位置の差になる()
    {
        var (alice, bob, _, workspaceId, conversationId) = await SetupDirectConversationAsync();
        for (var i = 0; i < 4; i++)
        {
            await SendMessageAsync(alice, conversationId, $"m{i}");
        }

        await bob.PutAsJsonAsync($"/api/conversations/{conversationId}/read-position", new { lastReadSequence = 1 });

        var unreads = (await bob.GetFromJsonAsync<UnreadCountDto[]>(
            $"/api/workspaces/{workspaceId}/unread-counts"))!;

        var unread = unreads.Single(u => u.ConversationId == conversationId);
        unread.LatestSequence.Should().Be(4);
        unread.UnreadCount.Should().Be(3);
    }

    [DockerFact]
    public async Task 非参加者はMessageを参照も投稿もできない()
    {
        var (alice, _, _, workspaceId, conversationId) = await SetupDirectConversationAsync();
        await SendMessageAsync(alice, conversationId, "secret");

        // ワークスペースには所属するが会話には参加していないユーザー。
        var (charlie, charlieAuth) = await RegisterAsync("Charlie");
        await AddWorkspaceMemberAsync(alice, workspaceId, charlieAuth.Email);

        var read = await charlie.GetAsync($"/api/conversations/{conversationId}/messages");
        read.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var post = await charlie.PostAsJsonAsync($"/api/conversations/{conversationId}/messages",
            new { content = "侵入", clientMessageId = Guid.NewGuid().ToString("N") });
        post.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [DockerFact]
    public async Task afterSequence指定で不足分のMessageを再取得できる()
    {
        var (alice, _, _, _, conversationId) = await SetupDirectConversationAsync();
        for (var i = 0; i < 3; i++)
        {
            await SendMessageAsync(alice, conversationId, $"m{i}");
        }

        // SignalR 切断中に Sequence 1 まで受信していた想定で、以降を取得する。
        var page = (await alice.GetFromJsonAsync<MessagePageDto>(
            $"/api/conversations/{conversationId}/messages?afterSequence=1"))!;

        page.Items.Select(m => m.Sequence).Should().Equal(2, 3);
    }

    [DockerFact]
    public async Task 編集と削除が反映され送信者以外は操作できない()
    {
        var (alice, bob, _, _, conversationId) = await SetupDirectConversationAsync();
        var message = await SendMessageAsync(alice, conversationId, "original");

        // 送信者以外は編集できない。
        var forbidden = await bob.PatchAsJsonAsync($"/api/messages/{message.Id}", new { content = "hijack" });
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var edited = await alice.PatchAsJsonAsync($"/api/messages/{message.Id}", new { content = "edited" });
        edited.StatusCode.Should().Be(HttpStatusCode.OK);
        var editedDto = (await edited.Content.ReadFromJsonAsync<MessageDto>())!;
        editedDto.Content.Should().Be("edited");
        editedDto.EditedAt.Should().NotBeNull();
        editedDto.Sequence.Should().Be(message.Sequence);

        var deleted = await alice.DeleteAsync($"/api/messages/{message.Id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 削除後は本文を返さない。
        var page = (await alice.GetFromJsonAsync<MessagePageDto>(
            $"/api/conversations/{conversationId}/messages"))!;
        var deletedMessage = page.Items.Single(m => m.Id == message.Id);
        deletedMessage.IsDeleted.Should().BeTrue();
        deletedMessage.Content.Should().BeEmpty();
    }

    [DockerFact]
    public async Task 同じ2ユーザー間のDirectConversationは重複作成されない()
    {
        var (alice, _, bobAuth, workspaceId, conversationId) = await SetupDirectConversationAsync();

        var again = await CreateDirectAsync(alice, workspaceId, bobAuth.UserId);

        again.Id.Should().Be(conversationId);
    }

    [DockerFact]
    public async Task ワークスペース非所属ユーザーとはDirectConversationを作成できない()
    {
        var (alice, _) = await RegisterAsync("Alice");
        var (_, strangerAuth) = await RegisterAsync("Stranger");
        var workspace = await CreateWorkspaceAsync(alice);

        var response = await alice.PostAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/conversations/direct", new { otherUserId = strangerAuth.UserId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
