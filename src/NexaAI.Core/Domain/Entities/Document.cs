using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using pgvector;

namespace NexaAI.Core.Domain.Entities;

public class Document
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    [Required]
    [MaxLength(500)]
    public string Title { get; private set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string OriginalFilename { get; private set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ContentType { get; private set; } = string.Empty;

    public long FileSize { get; private set; }

    [Required]
    [MaxLength(1000)]
    public string StoragePath { get; private set; } = string.Empty;

    public Guid? OwnerId { get; private set; }

    [Required]
    [MaxLength(50)]
    public string AccessLevel { get; private set; } = DocumentAccessLevel.Private;

    [Required]
    [MaxLength(50)]
    public string Status { get; private set; } = DocumentStatus.Processing;

    [Column(TypeName = "jsonb")]
    public JsonDocument Metadata { get; private set; } = JsonDocument.Parse("{}");

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public User? Owner { get; private set; }
    public ICollection<DocumentChunk> Chunks { get; private set; } = new List<DocumentChunk>();

    // Private constructor for EF Core
    private Document() { }

    public static Document Create(
        string title,
        string originalFilename,
        string contentType,
        long fileSize,
        string storagePath,
        Guid? ownerId = null,
        string accessLevel = DocumentAccessLevel.Private)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));

        if (string.IsNullOrWhiteSpace(originalFilename))
            throw new ArgumentException("Original filename is required", nameof(originalFilename));

        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type is required", nameof(contentType));

        if (fileSize <= 0)
            throw new ArgumentException("File size must be positive", nameof(fileSize));

        if (string.IsNullOrWhiteSpace(storagePath))
            throw new ArgumentException("Storage path is required", nameof(storagePath));

        if (!DocumentAccessLevel.IsValid(accessLevel))
            throw new ArgumentException($"Invalid access level: {accessLevel}", nameof(accessLevel));

        return new Document
        {
            Title = title.Trim(),
            OriginalFilename = originalFilename.Trim(),
            ContentType = contentType.ToLowerInvariant(),
            FileSize = fileSize,
            StoragePath = storagePath,
            OwnerId = ownerId,
            AccessLevel = accessLevel,
            Status = DocumentStatus.Processing
        };
    }

    public void UpdateStatus(string status, JsonDocument? metadata = null)
    {
        if (!DocumentStatus.IsValid(status))
            throw new ArgumentException($"Invalid status: {status}", nameof(status));

        Status = status;
        if (metadata != null)
            Metadata = metadata;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateMetadata(JsonDocument metadata)
    {
        Metadata = metadata;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateAccessLevel(string accessLevel)
    {
        if (!DocumentAccessLevel.IsValid(accessLevel))
            throw new ArgumentException($"Invalid access level: {accessLevel}", nameof(accessLevel));

        AccessLevel = accessLevel;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool CanAccess(User? user)
    {
        if (user == null)
            return AccessLevel == DocumentAccessLevel.Public;

        if (AccessLevel == DocumentAccessLevel.Public)
            return true;

        if (AccessLevel == DocumentAccessLevel.Private)
            return OwnerId == user.Id || user.IsAdmin();

        if (AccessLevel == DocumentAccessLevel.Team)
            return OwnerId == user.Id || user.IsAdmin(); // Simplified: team = owner + admin

        return false;
    }
}

public static class DocumentStatus
{
    public const string Processing = "Processing";
    public const string Ready = "Ready";
    public const string Failed = "Failed";

    public static bool IsValid(string status) => status == Processing || status == Ready || status == Failed;
}

public static class DocumentAccessLevel
{
    public const string Private = "Private";
    public const string Team = "Team";
    public const string Public = "Public";

    public static bool IsValid(string level) => level == Private || level == Team || level == Public;
}