using MediatR;

namespace PeopleHQ.Application.Auth;

public record LoginCommand(string Email, string Password) : IRequest<AuthResult>;
public record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResult>;
public record LogoutCommand(Guid UserId) : IRequest;

public record SignupCommand(string OrgName, string Subdomain, string AdminName, string AdminEmail, string AdminPassword)
    : IRequest<SignupResult>;

public record EnableMfaCommand(Guid UserId) : IRequest<EnableMfaResult>;
public record VerifyMfaCommand(Guid UserId, string Secret, string Code) : IRequest<bool>;

public record ForgotPasswordCommand(string Email) : IRequest;
public record ResetPasswordCommand(string Email, string Token, string NewPassword) : IRequest<bool>;

public record AuthResult(bool Succeeded, string? AccessToken, string? RefreshToken, string? Error);
public record SignupResult(bool Succeeded, Guid? TenantId, string? Error);
public record EnableMfaResult(string Secret, string OtpAuthUri);
