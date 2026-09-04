using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Application.Auth;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly ISender _sender;
    public AuthController(ISender sender) => _sender = sender;

    [HttpPost("signup")]
    [AllowAnonymous]
    public async Task<IActionResult> Signup(SignupCommand command)
    {
        var result = await _sender.Send(command);
        if (!result.Succeeded) return Problem(title: "Signup failed", statusCode: 400, detail: result.Error);
        return Created($"/api/v1/tenants/{result.TenantId}", new { tenantId = result.TenantId });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginCommand command)
    {
        var result = await _sender.Send(command);
        if (!result.Succeeded) return Problem(title: "Invalid credentials", statusCode: 401, detail: result.Error);

        SetRefreshCookie(result.RefreshToken!);
        return Ok(new { accessToken = result.AccessToken });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh()
    {
        var refreshToken = Request.Cookies["refresh_token"];
        if (refreshToken is null) return Problem(title: "Missing refresh token", statusCode: 401);

        var result = await _sender.Send(new RefreshTokenCommand(refreshToken));
        if (!result.Succeeded) return Problem(title: "Invalid refresh token", statusCode: 401, detail: result.Error);

        SetRefreshCookie(result.RefreshToken!);
        return Ok(new { accessToken = result.AccessToken });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        await _sender.Send(new LogoutCommand(userId));
        Response.Cookies.Delete("refresh_token");
        return NoContent();
    }

    [HttpPost("mfa/enable")]
    public async Task<IActionResult> EnableMfa()
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _sender.Send(new EnableMfaCommand(userId));
        return Ok(result);
    }

    [HttpPost("mfa/verify")]
    public async Task<IActionResult> VerifyMfa(VerifyMfaRequestBody body)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var verified = await _sender.Send(new VerifyMfaCommand(userId, body.Secret, body.Code));
        return verified ? NoContent() : Problem(title: "Invalid MFA code", statusCode: 400);
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordCommand command)
    {
        await _sender.Send(command);
        return NoContent(); // never reveal whether the email exists
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordCommand command)
    {
        var succeeded = await _sender.Send(command);
        return succeeded ? NoContent() : Problem(title: "Password reset failed", statusCode: 400);
    }

    private void SetRefreshCookie(string token) =>
        Response.Cookies.Append("refresh_token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
        });
}

public record VerifyMfaRequestBody(string Secret, string Code);
