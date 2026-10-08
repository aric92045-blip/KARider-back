using System.Text.Json;
using KARider.API.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KARider.API.Extensions;

/// <summary>Piezas reutilizadas por el pipeline definido en Program.cs.</summary>
public static class PipelineExtensions
{
    /// <summary>/health/live (el proceso responde) y /health/ready (PostgreSQL disponible).</summary>
    public static WebApplication MapEndpointsDeSalud(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = EscribirSaludAsync
        }).AllowAnonymous().DisableRateLimiting();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = EscribirSaludAsync
        }).AllowAnonymous().DisableRateLimiting();

        return app;
    }

    /// <summary>Da formato estándar a respuestas de error sin cuerpo (404 de ruta, 405, 415…).</summary>
    public static Task EscribirErrorSinCuerpoAsync(StatusCodeContext statusContext)
    {
        var context = statusContext.HttpContext;
        var (code, detail) = context.Response.StatusCode switch
        {
            404 => ("RECURSO_NO_ENCONTRADO", $"La ruta «{context.Request.Path}» no existe en la API."),
            405 => ("METODO_NO_PERMITIDO", $"El método {context.Request.Method} no está permitido para esta ruta."),
            415 => ("TIPO_CONTENIDO_NO_SOPORTADO", "Envía el cuerpo de la solicitud como application/json."),
            401 => ("AUTH_TOKEN_REQUERIDO", "Debes iniciar sesión para acceder a este recurso."),
            403 => ("AUTH_PERMISO_DENEGADO", "No tienes permisos para realizar esta acción."),
            _ => ("ERROR_HTTP", "La solicitud no pudo completarse.")
        };

        return ApiProblems.WriteAsync(context, context.Response.StatusCode, code, detail);
    }

    private static Task EscribirSaludAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var cuerpo = new
        {
            estado = report.Status.ToString(),
            duracionMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            componentes = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString())
        };
        return JsonSerializer.SerializeAsync(context.Response.Body, cuerpo, cancellationToken: context.RequestAborted);
    }
}
