using KARider.Application.Abstractions;
using KARider.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KARider.Application.Features.Catalogos;

public sealed record CarreraDto(int Id, string Nombre);

public sealed record PuntoEncuentroDto(int Id, string Nombre, string Descripcion, double Latitud, double Longitud);

public sealed class CatalogoService(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<CarreraDto>> ObtenerCarrerasAsync(CancellationToken ct) =>
        await db.Carreras.AsNoTracking()
            .Where(c => c.Activa)
            .OrderBy(c => c.Nombre)
            .Select(c => new CarreraDto(c.Id, c.Nombre))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PuntoEncuentroDto>> ObtenerPuntosEncuentroAsync(CancellationToken ct) =>
        await db.PuntosEncuentro.AsNoTracking()
            .Where(p => p.Activo)
            .OrderBy(p => p.Id)
            .Select(p => new PuntoEncuentroDto(p.Id, p.Nombre, p.Descripcion, p.Latitud, p.Longitud))
            .ToListAsync(ct);

    public static IReadOnlyList<string> ObtenerEtiquetasCalificacion() => EtiquetasCalificacion.Todas;
}
