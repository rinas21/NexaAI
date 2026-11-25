using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NexaAI.Core.Domain.ValueObjects;

namespace NexaAI.Core.Domain.Entities;

public class User
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    [Required]
    [MaxLength(255)]
    public string Email { get; private set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string PasswordHash { get; private set; } = string.Empty;

    [MaxLength(255)]
    public string? FullName { get; private set; }

    [Required]
    [MaxLength(50)]
    public string Role { get; private set; } = UserRole.User;

    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();
    public ICollection<Document> Documents { get; private set; } = new List<Document>();
    public ICollection<Conversation> Conversations { get; private set; } = new List<Conversation>();
    public ICollection<AgentExecution> AgentExecutions { get; private set; } = new List<AgentExecution>();

    // Private constructor for EF Core
    private User() { }

    public static User Create(string email, string passwordHash, string? fullName = null, string role = UserRole.User)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required", nameof(email));

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required", nameof(passwordHash));

        return new User
        {
            Email = email.ToLowerInvariant(),
            PasswordHash = passwordHash,
            FullName = fullName?.Trim(),
            Role = role
        };
    }

    public void UpdateProfile(string? fullName, string? email = null)
    {
        if (!string.IsNullOrWhiteSpace(fullName))
            FullName = fullName.Trim();

        if (!string.IsNullOrWhiteSpace(email))
            Email = email.ToLowerInvariant();

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("Password hash is required", nameof(newPasswordHash));

        PasswordHash = newPasswordHash;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetRole(string role)
    {
        if (!UserRole.IsValid(role))
            throw new ArgumentException($"Invalid role: {role}", nameof(role));

        Role = role;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool IsAdmin() => Role == UserRole.Admin;
}

public static class UserRole
{
    public const string Admin = "Admin";
    public const string User = "User";

    public static bool IsValid(string role) => role == Admin || role == User;
}