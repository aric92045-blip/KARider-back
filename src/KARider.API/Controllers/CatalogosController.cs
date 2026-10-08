using KARider.API.Common;
using KARider.Application.Features.Catalogos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace KARider.API.Controllers;

/// <summary>Catálogos públicos usados en registro, publicación y calificación.</summary>
[Route("api/v1/catalogos")]
[AllowAnonymous]
[OutputCache(PolicyName = "catalogos")]
public sealed class CatalogosController(CatalogoService catalogos) : ApiControllerBase
{
    [HttpGet("carreras")]
    [ProducesResponseType<IReadOnlyList<CarreraDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Carreras(CancellationToken ct) => Ok(await catalogos.ObtenerCarrerasAsync(ct));

    [HttpGet("puntos-encuentro")]
    [ProducesResponseType<IReadOnlyList<PuntoEncuentroDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PuntosEncuentro(CancellationToken ct) => Ok(await catalogos.ObtenerPuntosEncuentroAsync(ct));

    [HttpGet("etiquetas-calificacion")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public IActionResult EtiquetasCalificacion() => Ok(CatalogoService.ObtenerEtiquetasCalificacion());
}
