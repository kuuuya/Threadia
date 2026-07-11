using Threadia.Contracts.Messaging;

namespace Threadia.Modules.Messaging.Application;

public sealed record MessagePageDto(IReadOnlyList<MessageDto> Items, bool HasMore);

public sealed record UnreadCountDto(Guid ConversationId, long LatestSequence, long LastReadSequence, long UnreadCount);

public sealed record ReadPositionDto(Guid ConversationId, long LastReadSequence, DateTime UpdatedAt);
