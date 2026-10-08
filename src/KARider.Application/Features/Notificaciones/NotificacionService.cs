using FluentValidation;
using KARider.Application.Abstractions;
using KARider.Application.Common;
using KARider.Domain.Common;
using KARider.Domain.Entities;
using KARider.Domain.Enums;
using KARider.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace KARider.Application.Features.Notificaciones;

public sealed record NotificacionesQuery(bool SoloNoLeidas = false, int Pagina = 1, int TamanoPagina = 20) : IPaginado;

public sealed record NotificacionDto(
    Guid Id,
    TipoNotificacion Tipo,
    string Titulo,
    string Mensaje,
    Guid? ViajeId,
    Guid? ReservaId,
    bool Leida,
    DateTimeOffset CreadoEn);

public sealed record BandejaNotificacionesDto(int NoLeidas, PagedResult<NotificacionDto> Notificaciones);

public sealed record SuscripcionPushRequest(string Endpoint, string P256dh, string Auth);

public sealed record CancelarSuscripcionPushRequest(string Endpoint);

public sealed record VapidKeyDto(string? PublicKey, bool Habilitado);

public sealed class NotificacionesQueryValidator : AbstractValidator<NotificacionesQuery>
{
    public NotificacionesQueryValidator() => this.ReglasPaginacion();
}

public sealed class SuscripcionPushRequestValidator : AbstractValidator<SuscripcionPushRequest>
{
    public SuscripcionPushRequestValidator()
    {
        RuleFor(x => x.Endpoint)
            .NotEmpty().WithMessage("El endpoint de la suscripción push es obligatorio.")
            .MaximumLength(1000).WithMessage("El endpoint de la suscripción push es demasiado largo.")
            .Must(e => Uri.TryCreate(e, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            .WithMessage("El endpoint de la suscripción push debe ser una URL https.");
        RuleFor(x => x.P256dh)
            .NotEmpty().WithMessage("La clave p256dh de la suscripción es obligatoria.")
            .MaximumLength(200).WithMessage("La clave p256dh no tiene un formato válido.");
        RuleFor(x => x.Auth)
            .NotEmpty().WithMessage("La clave auth de la suscripción es obligatoria.")
            .MaximumLength(100).WithMessage("La clave auth no tiene un formato válido.");
    }
}

public sealed class CancelarSuscripcionPushRequestValidator : AbstractValidator<CancelarSuscripcionPushRequest>
{
    public CancelarSuscripcionPushRequestValidator() =>
        RuleFor(x => x.Endpoint).NotEmpty().WithMessage("El endpoint de la suscripción push es obligatorio.");
}

/// <summary>Bandeja de notificaciones in-app y suscripciones Web Push.</summary>
public sealed class NotificacionService(IApplicationDbContext db, IPushSender push, TimeProvider time)
{
    public async Task<BandejaNotificacionesDto> ListarAsync(Guid usuarioId, NotificacionesQuery query, CancellationToken ct)
    {
        var notificaciones = db.Notificaciones.AsNoTracking().Where(n => n.UsuarioId == usuarioId);
        var noLeidas = await notificaciones.CountAsync(n => !n.Leida, ct);

        if (query.SoloNoLeidas)
        {
            notificaciones = notificaciones.Where(n => !n.Leida);
        }

        var pagina = await notificaciones
            .OrderByDescending(n => n.CreadoEn)
            .Select(n => new NotificacionDto(n.Id, n.Tipo, n.Titulo, n.Mensaje, n.ViajeId, n.ReservaId, n.Leida, n.CreadoEn))
            .ToPagedAsync(query, ct);

        return new BandejaNotificacionesDto(noLeidas, pagina);
    }

    public async Task<Result> MarcarLeidaAsync(Guid usuarioId, Guid notificacionId, CancellationToken ct)
    {
        var notificacion = await db.Notificaciones.FirstOrDefaultAsync(n => n.Id == notificacionId && n.UsuarioId == usuarioId, ct);
        if (notificacion is null)
        {
            return DomainErrors.Notificacion.NoEncontrada;
        }

        notificacion.MarcarLeida(time.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<int> MarcarTodasLeidasAsync(Guid usuarioId, CancellationToken ct)
    {
        var ahora = time.GetUtcNow();
        var pendientes = await db.Notificaciones.Where(n => n.UsuarioId == usuarioId && !n.Leida).ToListAsync(ct);
        foreach (var notificacion in pendientes)
        {
            notificacion.MarcarLeida(ahora);
        }

        await db.SaveChangesAsync(ct);
        return pendientes.Count;
    }

    public async Task<Result> SuscribirAsync(Guid usuarioId, SuscripcionPushRequest request, CancellationToken ct)
    {
        var existente = await db.SuscripcionesPush.FirstOrDefaultAsync(s => s.Endpoint == request.Endpoint, ct);
        if (existente is null)
        {
            db.SuscripcionesPush.Add(SuscripcionPush.Crear(usuarioId, request.Endpoint, request.P256dh, request.Auth));
        }
        else
        {
            // El mismo navegador puede cambiar de cuenta: la suscripción pasa al usuario actual.
            existente.Actualizar(usuarioId, request.P256dh, request.Auth);
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> CancelarSuscripcionAsync(Guid usuarioId, CancelarSuscripcionPushRequest request, CancellationToken ct)
    {
        var suscripcion = await db.SuscripcionesPush.FirstOrDefaultAsync(s => s.Endpoint == request.Endpoint && s.UsuarioId == usuarioId, ct);
        if (suscripcion is not null)
        {
            db.SuscripcionesPush.Remove(suscripcion);
            await db.SaveChangesAsync(ct);
        }

        return Result.Success();
    }

    public VapidKeyDto ObtenerClavePublica() => new(push.PublicKey, push.PublicKey is not null);
}
