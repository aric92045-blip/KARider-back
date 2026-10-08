using KARider.API.Common;
using KARider.API.Extensions;
using KARider.Application.Common;
using KARider.Application.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KARider.API.Controllers;

/// <summary>Registro con correo institucional, verificación, inicio de sesión y sesiones (P2, P3, P4).</summary>
[Route("api/v1/auth")]
[EnableRateLimiting(RateLimitSettings.AuthPolicy)]
public sealed class AuthController(AuthService auth) : ApiControllerBase
{
    /// <summary>Crea una cuenta de pasajero o conductor (con vehículo) y envía el código de verificación.</summary>
    [HttpPost("registro")]
    [AllowAnonymous]
    [ProducesResponseType<RegistroResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Registrar(RegistroRequest request, CancellationToken ct) =>
        CreatedFromResult(await auth.RegistrarAsync(request, ct), _ => "/api/v1/perfil");

    [HttpPost("verificar-correo")]
    [AllowAnonymous]
    [ProducesResponseType<MensajeResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> VerificarCorreo(VerificarCorreoRequest request, CancellationToken ct) =>
        FromResult(await auth.VerificarCorreoAsync(request, ct));

    [HttpPost("reenviar-codigo")]
    [AllowAnonymous]
    [ProducesResponseType<MensajeResponse>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ReenviarCodigo(CorreoRequest request, CancellationToken ct)
    {
        var resultado = await auth.ReenviarCodigoVerificacionAsync(request, ct);
        return resultado.IsSuccess ? Accepted(resultado.Value) : ProblemFrom(resultado.Error);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status423Locked)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct) =>
        FromResult(await auth.LoginAsync(request, IpCliente, ct));

    /// <summary>Rota el refresh token y emite un nuevo access token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Refrescar(RefreshRequest request, CancellationToken ct) =>
        FromResult(await auth.RefrescarAsync(request, IpCliente, ct));

    [HttpPost("olvide-contrasena")]
    [AllowAnonymous]
    [ProducesResponseType<MensajeResponse>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> OlvideContrasena(CorreoRequest request, CancellationToken ct)
    {
        var resultado = await auth.SolicitarRestablecimientoAsync(request, ct);
        return resultado.IsSuccess ? Accepted(resultado.Value) : ProblemFrom(resultado.Error);
    }

    [HttpPost("restablecer-contrasena")]
    [AllowAnonymous]
    [ProducesResponseType<MensajeResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RestablecerContrasena(RestablecerPasswordRequest request, CancellationToken ct) =>
        FromResult(await auth.RestablecerPasswordAsync(request, ct));

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct) =>
        FromResult(await auth.LogoutAsync(UsuarioId, request, ct));

    [HttpPost("cambiar-contrasena")]
    [ProducesResponseType<MensajeResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CambiarContrasena(CambiarPasswordRequest request, CancellationToken ct) =>
        FromResult(await auth.CambiarPasswordAsync(UsuarioId, request, ct));
}
