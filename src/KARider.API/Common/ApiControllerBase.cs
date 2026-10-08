using System.Security.Claims;
using KARider.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KARider.API.Common;

[ApiController]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public abstract class ApiControllerBase : ControllerBase
{
    protected Guid UsuarioId => User.GetUsuarioId();

    protected string? IpCliente => HttpContext.Connection.RemoteIpAddress?.ToString();

    protected ObjectResult ProblemFrom(Error error)
    {
        var problem = ApiProblems.FromError(HttpContext, error);
        return new ObjectResult(problem)
        {
            StatusCode = problem.Status,
            ContentTypes = { ApiProblems.ContentType }
        };
    }

    protected IActionResult FromResult<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : ProblemFrom(result.Error);

    protected IActionResult FromResult(Result result) =>
        result.IsSuccess ? NoContent() : ProblemFrom(result.Error);

    protected IActionResult CreatedFromResult<T>(Result<T> result, Func<T, string> location) =>
        result.IsSuccess ? Created(location(result.Value), result.Value) : ProblemFrom(result.Error);
}

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUsuarioId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out var id)
            ? id
            : throw new UnauthorizedAccessException("El token no contiene un identificador de usuario válido.");
    }
}
