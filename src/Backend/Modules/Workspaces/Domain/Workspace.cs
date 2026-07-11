namespace Threadia.Modules.Workspaces.Domain;

public sealed class Workspace
{
    public const int MaxNameLength = 80;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public Guid CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Workspace()
    {
    }

    public static Workspace Create(Guid id, string name, Guid createdBy, DateTime utcNow)
    {
        var trimmed = name.Trim();
        if (trimmed.Length is 0 or > MaxNameLength)
        {
            throw new ArgumentException($"ワークスペース名は1〜{MaxNameLength}文字で指定してください。", nameof(name));
        }

        return new Workspace
        {
            Id = id,
            Name = trimmed,
            CreatedBy = createdBy,
            CreatedAt = utcNow,
        };
    }
}
