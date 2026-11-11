using Metabolix.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Metabolix.Api.Controllers;

/// <summary>для публичных методов явно разрешаем анонимный доступ</summary>
[ApiController]
[Authorize]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>id пользователя берём из подписанного токена</summary>
    protected Guid UserId => User.UserIdOrNull() ?? throw new UnauthorizedAccessException();

    protected ObjectResult Error(int status, string detail) => Problem(detail: detail, statusCode: status);

    protected string ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
