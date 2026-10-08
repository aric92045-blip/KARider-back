using KARider.Application.Abstractions;
using KARider.Domain.Entities;
using KARider.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KARider.Application.Features.Notificaciones;

/// <summary>
/// Registra notificaciones en la misma unidad de trabajo del caso de uso y, una vez guardadas,
/// las despacha como Web Push. Un fallo de push nunca hace fallar la operación de negocio.
/// </summary>
public sealed class Notificador(IApplicationDbContext db, IPushSender push, ILogger<Notificador> logger)
{
    private readonly List<Notificacion> _pendientes = [];

    public void Agregar(
        Guid usuarioId,
        TipoNotificacion tipo,
        string titulo,
        string mensaje,
        Guid? viajeId = null,
        Guid? reservaId = null)
    {
        var notificacion = Notificacion.Crear(usuarioId, tipo, titulo, mensaje, viajeId, reservaId);
        db.Notificaciones.Add(notificacion);
        _pendientes.Add(notificacion);
    }

    public async Task DespacharAsync(CancellationToken ct)
    {
        if (_pendientes.Count == 0 || push.PublicKey is null)
        {
            _pendientes.Clear();
            return;
        }

        try
        {
            var usuarios = _pendientes.Select(n => n.UsuarioId).Distinct().ToList();
            var suscripciones = await db.SuscripcionesPush
                .Where(s => usuarios.Contains(s.UsuarioId))
                .ToListAsync(ct);

            var expirados = new HashSet<string>();
            foreach (var notificacion in _pendientes)
            {
                var destino = suscripciones.Where(s => s.UsuarioId == notificacion.UsuarioId).ToList();
                if (destino.Count == 0)
                {
                    continue;
                }

                var mensaje = new PushMessage(
                    notificacion.Titulo,
                    notificacion.Mensaje,
                    notificacion.Tipo.ToString(),
                    notificacion.ViajeId,
                    notificacion.ReservaId);

                foreach (var endpoint in await push.SendAsync(destino, mensaje, ct))
                {
                    expirados.Add(endpoint);
                }
            }

            if (expirados.Count > 0)
            {
                db.SuscripcionesPush.RemoveRange(suscripciones.Where(s => expirados.Contains(s.Endpoint)));
                await db.SaveChangesAsync(ct);
            }
        }
#pragma warning disable CA1031 // El envío push es best-effort: se registra y no interrumpe el flujo
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogWarning(ex, "No se pudieron despachar {Total} notificaciones push", _pendientes.Count);
        }
        finally
        {
            _pendientes.Clear();
        }
    }
}
