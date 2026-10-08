using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace KARider.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Pagina, int TamanoPagina, int Total)
{
    public int TotalPaginas => TamanoPagina == 0 ? 0 : (int)Math.Ceiling(Total / (double)TamanoPagina);

    public bool TieneSiguiente => Pagina < TotalPaginas;
}

public interface IPaginado
{
    int Pagina { get; }

    int TamanoPagina { get; }
}

public static class PaginacionExtensions
{
    public const int TamanoMaximo = 50;

    public static async Task<PagedResult<T>> ToPagedAsync<T>(
        this IQueryable<T> query, IPaginado paginado, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((paginado.Pagina - 1) * paginado.TamanoPagina)
            .Take(paginado.TamanoPagina)
            .ToListAsync(ct);
        return new PagedResult<T>(items, paginado.Pagina, paginado.TamanoPagina, total);
    }

    public static void ReglasPaginacion<T>(this AbstractValidator<T> validator) where T : IPaginado
    {
        validator.RuleFor(x => x.Pagina)
            .GreaterThanOrEqualTo(1).WithMessage("La página debe ser mayor o igual a 1.");
        validator.RuleFor(x => x.TamanoPagina)
            .InclusiveBetween(1, TamanoMaximo).WithMessage($"El tamaño de página debe estar entre 1 y {TamanoMaximo}.");
    }
}

public sealed record PaginacionQuery(int Pagina = 1, int TamanoPagina = 20) : IPaginado;

public sealed class PaginacionQueryValidator : AbstractValidator<PaginacionQuery>
{
    public PaginacionQueryValidator() => this.ReglasPaginacion();
}

public sealed record MensajeResponse(string Mensaje);
