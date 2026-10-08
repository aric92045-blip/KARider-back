using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace KARider.API.Common;

/// <summary>Encabezados de seguridad recomendados por OWASP para APIs.</summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Cross-Origin-Resource-Policy"] = "same-site";

            // La UI de documentación (solo Development) necesita scripts; el resto de la API no sirve HTML.
            var esDocumentacion = context.Request.Path.StartsWithSegments("/swagger") ||
                                  context.Request.Path.StartsWithSegments("/openapi");
            if (!esDocumentacion)
            {
                headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            }

            // Respuestas con datos personales no deben guardarse en cachés intermedias.
            if (context.Request.Headers.Authorization.Count > 0 && !headers.ContainsKey("Cache-Control"))
            {
                headers.CacheControl = "no-store";
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}

/// <summary>
/// Responde 403 con un código específico según el requisito que falló
/// (ej. una acción solo para conductores) en lugar de un 403 vacío.
/// </summary>
public sealed class ProblemAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            var requisitos = authorizeResult.AuthorizationFailure?.FailedRequirements ?? [];
            var (code, detail) = requisitos.Any(r => r is Microsoft.AspNetCore.Authorization.Infrastructure.RolesAuthorizationRequirement)
                ? ("AUTH_ROL_CONDUCTOR_REQUERIDO", "Esta acción requiere una cuenta de conductor con un vehículo registrado. Si acabas de registrar tu vehículo, renueva tu sesión.")
                : requisitos.Any(r => r is Microsoft.AspNetCore.Authorization.Infrastructure.ClaimsAuthorizationRequirement)
                    ? ("AUTH_CORREO_NO_VERIFICADO", "Debes verificar tu correo institucional para usar esta función.")
                    : ("AUTH_PERMISO_DENEGADO", "No tienes permisos para realizar esta acción.");

            await ApiProblems.WriteAsync(context, StatusCodes.Status403Forbidden, code, detail);
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
