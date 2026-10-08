using KARider.API.Common;
using KARider.Application.Features.Notificaciones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KARider.API.Controllers;

/// <summary>Bandeja de notificaciones y suscripciones Web Push de la PWA.</summary>
[Route("api/v1/notificaciones")]
public sealed class NotificacionesController(NotificacionService notificaciones) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<BandejaNotificacionesDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar([FromQuery] NotificacionesQuery query, CancellationToken ct) =>
        Ok(await notificaciones.ListarAsync(UsuarioId, query, ct));

    [HttpPost("{id:guid}/leer")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarcarLeida(Guid id, CancellationToken ct) =>
        FromResult(await notificaciones.MarcarLeidaAsync(UsuarioId, id, ct));

    [HttpPost("leer-todas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> MarcarTodas(CancellationToken ct) =>
        Ok(new { marcadas = await notificaciones.MarcarTodasLeidasAsync(UsuarioId, ct) });

    /// <summary>Clave pública VAPID para registrar el service worker de la PWA.</summary>
    [HttpGet("push/clave-publica")]
    [AllowAnonymous]
    [ProducesResponseType<VapidKeyDto>(StatusCodes.Status200OK)]
    public IActionResult ClavePublica() => Ok(notificaciones.ObtenerClavePublica());

    [HttpPost("push/suscripciones")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Suscribir(SuscripcionPushRequest request, CancellationToken ct) =>
        FromResult(await notificaciones.SuscribirAsync(UsuarioId, request, ct));

    [HttpPost("push/suscripciones/cancelar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CancelarSuscripcion(CancelarSuscripcionPushRequest request, CancellationToken ct) =>
        FromResult(await notificaciones.CancelarSuscripcionAsync(UsuarioId, request, ct));
}
