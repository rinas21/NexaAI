using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NexaAI.Core.Application.Common;
using NexaAI.Core.Application.Configuration;
using NexaAI.Core.Application.DTOs;
using NexaAI.Core.Application.Interfaces;
using NexaAI.Core.Domain.Entities;
using NexaAI.Core.Infrastructure.Persistence;

namespace NexaAI.Core.Application.Services;

/// <summary>User management and JWT/refresh-token authentication.</summary>
public sealed class AuthService(
    AppDbContext db,
    IOptions<JwtOptions> jwtOptions,
    ILogger<AuthService> logger) : IAuthService
{
    private const int WorkFactor = 11;

    /// <inheritdoc />
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new ConflictException("An account with this email address already exists.");
        }

        var user = User.Create(email, BCrypt.Net.BCrypt.HashPassword(request.Password, WorkFactor), request.FullName);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Registered user {UserId} ({Email})", user.Id, email);
        return await IssueTokensAsync(user, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Use the same failure for unknown users and bad passwords to avoid user enumeration.
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        return await IssueTokensAsync(user, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AuthResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(refreshToken);
        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (stored?.User is null || !stored.IsValid || !stored.User.IsActive)
        {
            throw new UnauthorizedException("The refresh token is invalid or has expired.");
        }

        // Rotate the token: revoke the presented token and issue a fresh pair.
        stored.Revoke();
        await db.SaveChangesAsync(cancellationToken);

        return await IssueTokensAsync(stored.User, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RevokeTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
        if (stored is null || !stored.IsValid)
        {
            return;
        }

        stored.Revoke();
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FindAsync([userId], cancellationToken)
                   ?? throw new NotFoundException("User not found.");
        return ToDto(user);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserDto>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await db.Users.OrderBy(u => u.CreatedAt).ToListAsync(cancellationToken);
        return users.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<UserDto> UpdateUserRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!UserRole.IsValid(role))
        {
            throw new ArgumentException($"Invalid role: {role}", nameof(role));
        }

        var user = await db.Users.FindAsync([userId], cancellationToken)
                   ?? throw new NotFoundException("User not found.");

        if (user.Role == UserRole.Admin && role != UserRole.Admin &&
            await db.Users.CountAsync(u => u.Role == UserRole.Admin && u.IsActive, cancellationToken) <= 1)
        {
            throw new ConflictException("At least one active administrator must remain.");
        }

        user.SetRole(role);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("User {UserId} role changed to {Role}", userId, role);
        return ToDto(user);
    }

    /// <inheritdoc />
    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FindAsync([userId], cancellationToken)
                   ?? throw new NotFoundException("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new UnauthorizedException("The current password is incorrect.");
        }

        user.ChangePassword(BCrypt.Net.BCrypt.HashPassword(request.NewPassword, WorkFactor));
        await RevokeAllTokensAsync(userId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FindAsync([userId], cancellationToken)
                   ?? throw new NotFoundException("User not found.");

        var email = request.Email?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(email) &&
            await db.Users.AnyAsync(u => u.Email == email && u.Id != userId, cancellationToken))
        {
            throw new ConflictException("An account with this email address already exists.");
        }

        user.UpdateProfile(request.FullName, email);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken cancellationToken)
    {
        var options = jwtOptions.Value;
        var (accessToken, expiresAt) = CreateAccessToken(user, options);
        var refreshToken = GenerateRefreshToken();

        db.RefreshTokens.Add(RefreshToken.Create(user.Id, HashToken(refreshToken), DateTimeOffset.UtcNow.AddDays(options.RefreshExpiryDays)));
        await db.SaveChangesAsync(cancellationToken);

        return new AuthResponse(accessToken, refreshToken, expiresAt, ToDto(user));
    }

    private static (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user, JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Secret) || Encoding.UTF8.GetByteCount(options.Secret) < 32)
        {
            throw new InvalidOperationException("Jwt:Secret must be configured with at least 32 bytes.");
        }

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(options.ExpiryMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new("role", user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        if (!string.IsNullOrWhiteSpace(user.FullName))
        {
            claims.Add(new Claim(ClaimTypes.Name, user.FullName));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = DateTime.UtcNow,
            NotBefore = DateTime.UtcNow,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret)),
                SecurityAlgorithms.HmacSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        return (handler.WriteToken(handler.CreateToken(descriptor)), expiresAt);
    }

    private async Task RevokeAllTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        var tokens = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.Revoke();
        }
    }

    private static string GenerateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static UserDto ToDto(User user) =>
        new(user.Id, user.Email, user.FullName, user.Role, user.IsActive, user.CreatedAt);
}
