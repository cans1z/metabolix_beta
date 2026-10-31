using Metabolix.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Metabolix.Api.Controllers;

/// <summary>По умолчанию всё закрыто токеном; публичные методы помечены [AllowAnonymous].</summary>
[ApiController]
[Authorize]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Id берём только из подписанного токена, а не из запроса: чужие данные так не запросить.</summary>
    protected Guid UserId => User.UserIdOrNull() ?? throw new UnauthorizedAccessException();

    protected ObjectResult Error(int status, string detail) => Problem(detail: detail, statusCode: status);

    protected string ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
