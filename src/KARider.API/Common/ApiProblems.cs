using System.Diagnostics;
using System.Text.Json;
using KARider.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace KARider.API.Common;

/// <summary>
/// Construye todas las respuestas de error con el mismo formato (RFC 9457 Problem Details):
/// <c>type, title, status, detail, instance, code, traceId, timestamp</c> y <c>errors</c> en validaciones.
/// </summary>
public static class ApiProblems
{
    public const string ContentType = "application/problem+json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static int StatusFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
        ErrorType.Locked => StatusCodes.Status423Locked,
        ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
        _ => StatusCodes.Status400BadRequest
    };

    public static ProblemDetails Create(
        HttpContext context,
        int status,
        string code,
        string detail,
        IDictionary<string, string[]>? errors = null)
    {
        var problem = errors is null ? new ProblemDetails() : new ValidationProblemDetails(errors);
        problem.Status = status;
        problem.Title = TitleFor(status);
        problem.Type = $"urn:karider:error:{code.ToLowerInvariant()}";
        problem.Detail = detail;
        problem.Instance = $"{context.Request.Method} {context.Request.Path}";
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        problem.Extensions["timestamp"] = DateTimeOffset.UtcNow;
        return problem;
    }

    public static ProblemDetails FromError(HttpContext context, Error error) =>
        Create(
            context,
            StatusFor(error.Type),
            error.Code,
            error.Message,
            error.Details?.ToDictionary(kv => kv.Key, kv => kv.Value));

    public static async Task WriteAsync(
        HttpContext context,
        int status,
        string code,
        string detail,
        IDictionary<string, string[]>? errors = null)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        var problem = Create(context, status, code, detail, errors);
        context.Response.StatusCode = status;
        context.Response.ContentType = ContentType;
        await JsonSerializer.SerializeAsync(context.Response.Body, problem, problem.GetType(), JsonOptions, context.RequestAborted);
    }

    private static string TitleFor(int status) => status switch
    {
        400 => "Solicitud inválida",
        401 => "No autenticado",
        403 => "Acceso denegado",
        404 => "Recurso no encontrado",
        405 => "Método no permitido",
        409 => "Conflicto",
        413 => "Contenido demasiado grande",
        415 => "Tipo de contenido no soportado",
        422 => "Regla de negocio incumplida",
        423 => "Recurso bloqueado",
        429 => "Demasiadas solicitudes",
        503 => "Servicio no disponible",
        _ when status >= 500 => "Error del servidor",
        _ => "Error"
    };
}
