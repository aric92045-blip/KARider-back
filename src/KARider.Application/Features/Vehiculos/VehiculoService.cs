using KARider.Application.Abstractions;
using KARider.Application.Features.Auth;
using KARider.Domain.Common;
using KARider.Domain.Entities;
using KARider.Domain.Enums;
using KARider.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace KARider.Application.Features.Vehiculos;

public sealed record VehiculoDto(Guid Id, string Modelo, string Color, int Anio, string Placas, int Capacidad, bool Verificado);

/// <summary>Administración de los vehículos del conductor (P4, P10 y P15).</summary>
public sealed class VehiculoService(IApplicationDbContext db)
{
    private static readonly EstadoViaje[] EstadosActivos = [EstadoViaje.Borrador, EstadoViaje.Programado, EstadoViaje.EnCurso];

    public async Task<IReadOnlyList<VehiculoDto>> ListarAsync(Guid usuarioId, CancellationToken ct) =>
        await db.Vehiculos.AsNoTracking()
            .Where(v => v.ConductorId == usuarioId && v.Activo)
            .OrderBy(v => v.CreadoEn)
            .Select(v => new VehiculoDto(v.Id, v.Modelo, v.Color, v.Anio, v.Placas, v.Capacidad, v.Verificado))
            .ToListAsync(ct);

    public async Task<Result<VehiculoDto>> RegistrarAsync(Guid usuarioId, VehiculoRequest request, CancellationToken ct)
    {
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId && u.Activo, ct);
        if (usuario is null)
        {
            return DomainErrors.Usuario.NoEncontrado;
        }

        var placas = Vehiculo.NormalizarPlacas(request.Placas);
        if (await db.Vehiculos.AnyAsync(v => v.Placas == placas && v.Activo, ct))
        {
            return DomainErrors.Vehiculo.PlacasDuplicadas;
        }

        var vehiculo = Vehiculo.Crear(usuarioId, request.Modelo, request.Color, request.Anio, request.Placas, request.Capacidad);
        db.Vehiculos.Add(vehiculo);
        usuario.PromoverAConductor();
        await db.SaveChangesAsync(ct);

        return ToDto(vehiculo);
    }

    public async Task<Result<VehiculoDto>> ActualizarAsync(Guid usuarioId, Guid vehiculoId, VehiculoRequest request, CancellationToken ct)
    {
        var vehiculo = await db.Vehiculos.FirstOrDefaultAsync(v => v.Id == vehiculoId && v.ConductorId == usuarioId && v.Activo, ct);
        if (vehiculo is null)
        {
            return DomainErrors.Vehiculo.NoEncontrado;
        }

        var placas = Vehiculo.NormalizarPlacas(request.Placas);
        if (await db.Vehiculos.AnyAsync(v => v.Placas == placas && v.Activo && v.Id != vehiculoId, ct))
        {
            return DomainErrors.Vehiculo.PlacasDuplicadas;
        }

        var maxOfrecidos = await db.Viajes
            .Where(v => v.VehiculoId == vehiculoId && EstadosActivos.Contains(v.Estado))
            .Select(v => (int?)v.AsientosOfrecidos)
            .MaxAsync(ct);
        if (maxOfrecidos > request.Capacidad)
        {
            return DomainErrors.Vehiculo.CapacidadInsuficiente(maxOfrecidos.Value, request.Capacidad);
        }

        vehiculo.Actualizar(request.Modelo, request.Color, request.Anio, request.Placas, request.Capacidad);
        await db.SaveChangesAsync(ct);

        return ToDto(vehiculo);
    }

    public async Task<Result> EliminarAsync(Guid usuarioId, Guid vehiculoId, CancellationToken ct)
    {
        var vehiculo = await db.Vehiculos.FirstOrDefaultAsync(v => v.Id == vehiculoId && v.ConductorId == usuarioId && v.Activo, ct);
        if (vehiculo is null)
        {
            return DomainErrors.Vehiculo.NoEncontrado;
        }

        if (await db.Viajes.AnyAsync(v => v.VehiculoId == vehiculoId && EstadosActivos.Contains(v.Estado), ct))
        {
            return DomainErrors.Vehiculo.ConViajesActivos;
        }

        vehiculo.Desactivar();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static VehiculoDto ToDto(Vehiculo v) => new(v.Id, v.Modelo, v.Color, v.Anio, v.Placas, v.Capacidad, v.Verificado);
}
