using System.ComponentModel.DataAnnotations;

namespace NexaAI.Core.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; private set; }

    [Required]
    [MaxLength(255)]
    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? RevokedAt { get; private set; }

    // Navigation property
    public User? User { get; private set; }

    // Private constructor for EF Core
    private RefreshToken() { }

    public static RefreshToken Create(Guid userId, string tokenHash, DateTimeOffset expiresAt)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required", nameof(userId));

        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash is required", nameof(tokenHash));

        if (expiresAt <= DateTimeOffset.UtcNow)
            throw new ArgumentException("Expiration must be in the future", nameof(expiresAt));

        return new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt
        };
    }

    public void Revoke()
    {
        RevokedAt = DateTimeOffset.UtcNow;
    }

    public bool IsValid => RevokedAt == null && ExpiresAt > DateTimeOffset.UtcNow;
}