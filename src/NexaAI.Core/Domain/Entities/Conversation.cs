using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace NexaAI.Core.Domain.Entities;

public class Conversation
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; private set; }

    [MaxLength(500)]
    public string? Title { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public User? User { get; private set; }
    public ICollection<Message> Messages { get; private set; } = new List<Message>();

    // Private constructor for EF Core
    private Conversation() { }

    public static Conversation Create(Guid userId, string? title = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required", nameof(userId));

        return new Conversation
        {
            UserId = userId,
            Title = title?.Trim()
        };
    }

    public void UpdateTitle(string? title)
    {
        Title = title?.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public class Message
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    [Required]
    public Guid ConversationId { get; private set; }

    [Required]
    [MaxLength(50)]
    public string Role { get; private set; } = string.Empty; // user, assistant, system, tool

    [Required]
    public string Content { get; private set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    public JsonDocument Citations { get; private set; } = JsonDocument.Parse("[]");

    [Column(TypeName = "jsonb")]
    public JsonDocument ToolCalls { get; private set; } = JsonDocument.Parse("[]");

    [Column(TypeName = "jsonb")]
    public JsonDocument Metadata { get; private set; } = JsonDocument.Parse("{}");

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    // Navigation property
    public Conversation? Conversation { get; private set; }

    // Private constructor for EF Core
    private Message() { }

    public static Message Create(
        Guid conversationId,
        string role,
        string content,
        JsonDocument? citations = null,
        JsonDocument? toolCalls = null,
        JsonDocument? metadata = null)
    {
        if (conversationId == Guid.Empty)
            throw new ArgumentException("Conversation ID is required", nameof(conversationId));

        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role is required", nameof(role));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content is required", nameof(content));

        var validRoles = new[] { "user", "assistant", "system", "tool" };
        if (!validRoles.Contains(role.ToLowerInvariant()))
            throw new ArgumentException($"Invalid role: {role}. Valid roles: {string.Join(", ", validRoles)}", nameof(role));

        return new Message
        {
            ConversationId = conversationId,
            Role = role.ToLowerInvariant(),
            Content = content,
            Citations = citations ?? JsonDocument.Parse("[]"),
            ToolCalls = toolCalls ?? JsonDocument.Parse("[]"),
            Metadata = metadata ?? JsonDocument.Parse("{}")
        };
    }
}

public static class MessageRole
{
    public const string User = "user";
    public const string Assistant = "assistant";
    public const string System = "system";
    public const string Tool = "tool";
}