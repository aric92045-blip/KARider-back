using FluentValidation;
using KARider.Application.Abstractions;
using KARider.Application.Common;
using KARider.Application.Features.Notificaciones;
using KARider.Domain.Common;
using KARider.Domain.Entities;
using KARider.Domain.Enums;
using KARider.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace KARider.Application.Features.Calificaciones;

public sealed record CalificarRequest(Guid ViajeId, Guid EvaluadoId, int Estrellas, IReadOnlyList<string>? Etiquetas, string? Comentario);

public sealed record CalificacionDto(
    Guid Id,
    Guid ViajeId,
    Guid EvaluadorId,
    string EvaluadorNombre,
    string EvaluadorCarrera,
    Rol RolEvaluado,
    int Estrellas,
    IReadOnlyList<string> Etiquetas,
    string? Comentario,
    DateTimeOffset CreadoEn);

public sealed record EtiquetaConteoDto(string Etiqueta, int Total);

public sealed record ReputacionDto(
    Guid UsuarioId,
    decimal Promedio,
    int TotalCalificaciones,
    IReadOnlyList<EtiquetaConteoDto> Etiquetas,
    PagedResult<CalificacionDto> Resenas);

public sealed record CalificacionPendienteDto(Guid ViajeId, string Destino, DateTimeOffset FechaSalida, Guid EvaluadoId, string EvaluadoNombre, Rol RolEvaluado);

public sealed class CalificarRequestValidator : AbstractValidator<CalificarRequest>
{
    public CalificarRequestValidator()
    {
        RuleFor(x => x.ViajeId).NotEmpty().WithMessage("El viaje a calificar es obligatorio.");
        RuleFor(x => x.EvaluadoId).NotEmpty().WithMessage("Indica a qué compañero vas a calificar.");
        RuleFor(x => x.Estrellas).InclusiveBetween(1, 5).WithMessage("La calificación debe estar entre 1 y 5 estrellas.");
        RuleFor(x => x.Etiquetas)
            .Must(e => e is null || e.Count <= EtiquetasCalificacion.Todas.Count)
            .WithMessage("Seleccionaste más etiquetas de las disponibles.");
        RuleForEach(x => x.Etiquetas)
            .Must(EtiquetasCalificacion.EsValida)
            .WithMessage((_, etiqueta) => $"La etiqueta «{etiqueta}» no forma parte del catálogo de calificaciones.");
        RuleFor(x => x.Comentario).MaximumLength(500).WithMessage("El comentario no puede exceder 500 caracteres.");
    }
}

/// <summary>Calificación mutua conductor ↔ pasajero y reputación institucional (P13, P14).</summary>
public sealed class CalificacionService(IApplicationDbContext db, Notificador notificador)
{
    public async Task<Result<CalificacionDto>> CalificarAsync(Guid evaluadorId, CalificarRequest request, CancellationToken ct)
    {
        var viaje = await db.Viajes.AsNoTracking()
            .Include(v => v.Reservas)
            .FirstOrDefaultAsync(v => v.Id == request.ViajeId, ct);

        if (viaje is null)
        {
            return DomainErrors.Viaje.NoEncontrado;
        }

        if (viaje.Estado != EstadoViaje.Completado)
        {
            return DomainErrors.Calificacion.ViajeNoCompletado;
        }

        var pasajeros = viaje.Reservas
            .Where(r => r.Estado == EstadoReserva.Completada)
            .Select(r => r.PasajeroId)
            .ToHashSet();

        var esConductor = viaje.ConductorId == evaluadorId;
        if (!esConductor && !pasajeros.Contains(evaluadorId))
        {
            return DomainErrors.Calificacion.NoParticipante;
        }

        if (evaluadorId == request.EvaluadoId)
        {
            return DomainErrors.Calificacion.Autoevaluacion;
        }

        // El conductor califica a sus pasajeros; los pasajeros califican al conductor.
        var evaluadoValido = esConductor ? pasajeros.Contains(request.EvaluadoId) : request.EvaluadoId == viaje.ConductorId;
        if (!evaluadoValido)
        {
            return DomainErrors.Calificacion.EvaluadoInvalido;
        }

        if (await db.Calificaciones.AnyAsync(
                c => c.ViajeId == viaje.Id && c.EvaluadorId == evaluadorId && c.EvaluadoId == request.EvaluadoId, ct))
        {
            return DomainErrors.Calificacion.Duplicada;
        }

        var resultado = Calificacion.Crear(
            viaje.Id,
            evaluadorId,
            request.EvaluadoId,
            esConductor ? Rol.Pasajero : Rol.Conductor,
            request.Estrellas,
            request.Comentario,
            request.Etiquetas ?? []);

        if (resultado.IsFailure)
        {
            return resultado.Error;
        }

        var evaluado = await db.Usuarios.FirstAsync(u => u.Id == request.EvaluadoId, ct);
        var evaluador = await db.Usuarios.AsNoTracking().Include(u => u.Carrera).FirstAsync(u => u.Id == evaluadorId, ct);
        evaluado.AplicarCalificacion(request.Estrellas);

        var calificacion = resultado.Value;
        db.Calificaciones.Add(calificacion);
        notificador.Agregar(
            evaluado.Id,
            TipoNotificacion.CalificacionRecibida,
            "Recibiste una calificación",
            $"{evaluador.NombreCompleto} te calificó con {request.Estrellas} estrella(s) por el viaje a {viaje.Destino}.",
            viaje.Id);

        await db.SaveChangesAsync(ct);
        await notificador.DespacharAsync(ct);

        return new CalificacionDto(
            calificacion.Id,
            calificacion.ViajeId,
            evaluador.Id,
            evaluador.NombreCompleto,
            evaluador.Carrera?.Nombre ?? string.Empty,
            calificacion.RolEvaluado,
            calificacion.Estrellas,
            calificacion.Etiquetas,
            calificacion.Comentario,
            calificacion.CreadoEn);
    }

    public async Task<Result<ReputacionDto>> ObtenerReputacionAsync(Guid usuarioId, PaginacionQuery query, CancellationToken ct)
    {
        var usuario = await db.Usuarios.AsNoTracking()
            .Where(u => u.Id == usuarioId && u.Activo)
            .Select(u => new { u.Id, u.CalificacionPromedio, u.TotalCalificaciones })
            .FirstOrDefaultAsync(ct);

        if (usuario is null)
        {
            return DomainErrors.Usuario.NoEncontrado;
        }

        var recibidas = db.Calificaciones.AsNoTracking().Where(c => c.EvaluadoId == usuarioId);

        // Las etiquetas por usuario son pocas: se agrupan en memoria (GroupBy sobre arreglos no es traducible).
        var etiquetas = (await recibidas.Select(c => c.Etiquetas).ToListAsync(ct))
            .SelectMany(e => e)
            .GroupBy(e => e)
            .Select(g => new EtiquetaConteoDto(g.Key, g.Count()))
            .OrderByDescending(e => e.Total)
            .ToList();

        var resenas = await recibidas
            .OrderByDescending(c => c.CreadoEn)
            .Select(c => new CalificacionDto(
                c.Id,
                c.ViajeId,
                c.EvaluadorId,
                c.Evaluador!.NombreCompleto,
                c.Evaluador.Carrera!.Nombre,
                c.RolEvaluado,
                c.Estrellas,
                c.Etiquetas,
                c.Comentario,
                c.CreadoEn))
            .ToPagedAsync(query, ct);

        return new ReputacionDto(usuario.Id, usuario.CalificacionPromedio, usuario.TotalCalificaciones, etiquetas, resenas);
    }

    public async Task<IReadOnlyList<CalificacionPendienteDto>> ObtenerPendientesAsync(Guid usuarioId, CancellationToken ct)
    {
        // Como conductor: pasajeros de viajes completados que aún no califiqué.
        var comoConductor = await db.Reservas.AsNoTracking()
            .Where(r => r.Estado == EstadoReserva.Completada && r.Viaje!.ConductorId == usuarioId)
            .Where(r => !db.Calificaciones.Any(c => c.ViajeId == r.ViajeId && c.EvaluadorId == usuarioId && c.EvaluadoId == r.PasajeroId))
            .Select(r => new CalificacionPendienteDto(r.ViajeId, r.Viaje!.Destino, r.Viaje.FechaSalida, r.PasajeroId, r.Pasajero!.NombreCompleto, Rol.Pasajero))
            .ToListAsync(ct);

        // Como pasajero: conductores de viajes completados que aún no califiqué.
        var comoPasajero = await db.Reservas.AsNoTracking()
            .Where(r => r.Estado == EstadoReserva.Completada && r.PasajeroId == usuarioId)
            .Where(r => !db.Calificaciones.Any(c => c.ViajeId == r.ViajeId && c.EvaluadorId == usuarioId && c.EvaluadoId == r.Viaje!.ConductorId))
            .Select(r => new CalificacionPendienteDto(r.ViajeId, r.Viaje!.Destino, r.Viaje.FechaSalida, r.Viaje.ConductorId, r.Viaje.Conductor!.NombreCompleto, Rol.Conductor))
            .ToListAsync(ct);

        return comoConductor.Concat(comoPasajero).OrderByDescending(p => p.FechaSalida).ToList();
    }
}
