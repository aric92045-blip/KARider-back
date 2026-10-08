using KARider.API.Common;
using KARider.Application.Features.Auth;
using KARider.Application.Features.Vehiculos;
using Microsoft.AspNetCore.Mvc;

namespace KARider.API.Controllers;

/// <summary>Vehículos del usuario. Registrar el primero convierte la cuenta en conductor.</summary>
[Route("api/v1/vehiculos")]
public sealed class VehiculosController(VehiculoService vehiculos) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<VehiculoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken ct) => Ok(await vehiculos.ListarAsync(UsuarioId, ct));

    [HttpPost]
    [ProducesResponseType<VehiculoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Registrar(VehiculoRequest request, CancellationToken ct) =>
        CreatedFromResult(await vehiculos.RegistrarAsync(UsuarioId, request, ct), v => $"/api/v1/vehiculos/{v.Id}");

    [HttpPut("{id:guid}")]
    [ProducesResponseType<VehiculoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Actualizar(Guid id, VehiculoRequest request, CancellationToken ct) =>
        FromResult(await vehiculos.ActualizarAsync(UsuarioId, id, request, ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct) =>
        FromResult(await vehiculos.EliminarAsync(UsuarioId, id, ct));
}
