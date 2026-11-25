using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaAI.Core.Application.DTOs;
using NexaAI.Core.Application.Interfaces;

namespace NexaAI.Api.Controllers;

/// <summary>Registration, login, token rotation and user administration.</summary>
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ApiControllerBase
{
    /// <summary>Registers a new user and returns an access/refresh token pair.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await authService.RegisterAsync(request, cancellationToken));
        }
        catch (Exception ex)
        {
            return Failure<AuthResponse>(ex);
        }
    }

    /// <summary>Authenticates a user and returns an access/refresh token pair.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await authService.LoginAsync(request, cancellationToken));
        }
        catch (Exception ex)
        {
            return Failure<AuthResponse>(ex);
        }
    }

    /// <summary>Rotates a refresh token and issues a new access token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await authService.RefreshTokenAsync(request.RefreshToken, cancellationToken));
        }
        catch (Exception ex)
        {
            return Failure<AuthResponse>(ex);
        }
    }

    /// <summary>Revokes a refresh token.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await authService.RevokeTokenAsync(request.RefreshToken, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return Failure(ex);
        }
    }

    /// <summary>Returns the currently authenticated user.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await authService.GetCurrentUserAsync(CurrentUserId, cancellationToken));
        }
        catch (Exception ex)
        {
            return Failure<UserDto>(ex);
        }
    }

    /// <summary>Updates the current user's display name and/or email address.</summary>
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> UpdateMe(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await authService.UpdateProfileAsync(CurrentUserId, request, cancellationToken);
            return Ok(await authService.GetCurrentUserAsync(CurrentUserId, cancellationToken));
        }
        catch (Exception ex)
        {
            return Failure<UserDto>(ex);
        }
    }

    /// <summary>Changes the current user's password and revokes all refresh tokens.</summary>
    [HttpPost("me/change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await authService.ChangePasswordAsync(CurrentUserId, request, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return Failure(ex);
        }
    }

    /// <summary>Lists all users (administrators only).</summary>
    [HttpGet("users")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(IReadOnlyList<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> Users(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await authService.GetAllUsersAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return Failure<IReadOnlyList<UserDto>>(ex);
        }
    }

    /// <summary>Changes a user's role (administrators only).</summary>
    [HttpPut("users/{id:guid}/role")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> UpdateRole(
        Guid id,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await authService.UpdateUserRoleAsync(id, request.Role, cancellationToken));
        }
        catch (Exception ex)
        {
            return Failure<UserDto>(ex);
        }
    }
}

/// <summary>Request body used to change a user's role.</summary>
public sealed record UpdateRoleRequest([Required] string Role);
