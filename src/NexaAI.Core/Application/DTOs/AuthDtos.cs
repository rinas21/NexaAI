using System.ComponentModel.DataAnnotations;

namespace NexaAI.Core.Application.DTOs;

public record RegisterRequest(
    [Required][EmailAddress] string Email,
    [Required][MinLength(8)] string Password,
    [Required][MinLength(1)] string FullName
);

public record LoginRequest(
    [Required][EmailAddress] string Email,
    [Required] string Password
);

public record RefreshTokenRequest(
    [Required] string RefreshToken
);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    UserDto User
);

public record UserDto(
    Guid Id,
    string Email,
    string? FullName,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt
);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required][MinLength(8)] string NewPassword
);

public record UpdateProfileRequest(
    string? FullName,
    [EmailAddress] string? Email
);