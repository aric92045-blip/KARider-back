using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace KARider.API.Common;

/// <summary>
/// Último nivel de defensa: traduce excepciones a respuestas con código específico
/// y nunca expone detalles internos fuera de Development.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment) : IExceptionHandler
{
    // Índices únicos (ver migraciones) → mensaje de negocio específico.
    private static readonly Dictionary<string, (string Code, string Message)> RestriccionesUnicas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ix_usuarios_correo_institucional"] = ("USUARIO_CORREO_DUPLICADO", "Ya existe una cuenta registrada con este correo institucional."),
        ["ix_usuarios_matricula"] = ("USUARIO_MATRICULA_DUPLICADA", "Ya existe una cuenta registrada con esta matrícula UTTT."),
        ["ix_vehiculos_placas"] = ("VEHICULO_PLACAS_DUPLICADAS", "Ya existe un vehículo activo registrado con estas placas."),
        ["ix_reservas_viaje_id_pasajero_id"] = ("RESERVA_DUPLICADA", "Ya tienes una reserva activa en este viaje."),
        ["ix_calificaciones_viaje_id_evaluador_id_evaluado_id"] = ("CALIFICACION_DUPLICADA", "Ya calificaste a este compañero para este viaje."),
        ["ix_reservas_folio"] = ("RESERVA_FOLIO_DUPLICADO", "No se pudo generar un folio único para la reserva. Intenta de nuevo.")
    };

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            // El cliente cerró la conexión; no hay a quién responder.
            context.Response.StatusCode = 499;
            return true;
        }

        var (status, code, detail) = Traducir(exception);

        if (status >= 500)
        {
            logger.LogError(exception, "Error no controlado ({Code}) en {Method} {Path}", code, context.Request.Method, context.Request.Path);
        }
        else
        {
            logger.LogWarning(exception, "Solicitud rechazada ({Code}) en {Method} {Path}", code, context.Request.Method, context.Request.Path);
        }

        if (context.Response.HasStarted)
        {
            return false;
        }

        var problem = ApiProblems.Create(context, status, code, detail);
        if (environment.IsDevelopment())
        {
            problem.Extensions["detalleTecnico"] = exception.ToString();
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = ApiProblems.ContentType;
        await context.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, ApiProblems.ContentType, cancellationToken);
        return true;
    }

    private static (int Status, string Code, string Detail) Traducir(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => (
            StatusCodes.Status409Conflict,
            "CONCURRENCIA_CONFLICTO",
            "Otro usuario modificó este recurso al mismo tiempo (por ejemplo, el último asiento disponible). Consulta de nuevo e inténtalo otra vez."),

        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg } =>
            RestriccionesUnicas.TryGetValue(pg.ConstraintName ?? string.Empty, out var conocida)
                ? (StatusCodes.Status409Conflict, conocida.Code, conocida.Message)
                : (StatusCodes.Status409Conflict, "REGISTRO_DUPLICADO", "Ya existe un registro con los mismos datos únicos."),

        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } } => (
            StatusCodes.Status409Conflict,
            "REFERENCIA_INVALIDA",
            "La operación hace referencia a un registro que no existe o que está en uso por otros datos."),

        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.CheckViolation } } => (
            StatusCodes.Status422UnprocessableEntity,
            "RESTRICCION_DATOS_VIOLADA",
            "Los datos enviados no cumplen las reglas de integridad del sistema."),

        NpgsqlException or RetryLimitExceededException or TimeoutException => (
            StatusCodes.Status503ServiceUnavailable,
            "BASE_DATOS_NO_DISPONIBLE",
            "El servicio de datos no está disponible temporalmente. Intenta de nuevo en unos segundos."),

        BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } => (
            StatusCodes.Status413PayloadTooLarge,
            "CARGA_DEMASIADO_GRANDE",
            "El cuerpo de la solicitud excede el tamaño máximo permitido (1 MB)."),

        BadHttpRequestException bad => (
            bad.StatusCode,
            "SOLICITUD_MAL_FORMADA",
            "La solicitud HTTP no tiene un formato válido."),

        UnauthorizedAccessException => (
            StatusCodes.Status401Unauthorized,
            "AUTH_TOKEN_INVALIDO",
            "El token de acceso no es válido. Inicia sesión nuevamente."),

        _ => (
            StatusCodes.Status500InternalServerError,
            "ERROR_INTERNO",
            "Ocurrió un error inesperado en el servidor. Si el problema persiste, comparte el traceId con soporte.")
    };
}
