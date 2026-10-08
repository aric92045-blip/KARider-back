using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using KARider.API.Common;
using KARider.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace KARider.API.Extensions;

public sealed class CorsSettings
{
    public const string SectionName = "Cors";
    public const string PolicyName = "pwa";

    /// <summary>Orígenes del frontend PWA. En Azure: Cors__AllowedOrigins__0, Cors__AllowedOrigins__1…</summary>
    public string[] AllowedOrigins { get; set; } = [];
}

public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimiting";
    public const string AuthPolicy = "auth";

    [Range(10, 10_000)]
    public int SolicitudesPorMinuto { get; set; } = 120;

    [Range(3, 100)]
    public int SolicitudesAuthPorMinuto { get; set; } = 10;
}

public static class AuthorizationPolicies
{
    public const string Conductor = "Conductor";
}

public static class ApiServiceExtensions
{
    public static WebApplicationBuilder AddApiServices(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        // No revelar la tecnología del servidor; el resto de límites viene de la sección "Kestrel".
        builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

        services.AddControllers(options =>
            {
                options.Filters.Add<ValidationFilter>();
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
            })
            .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = SolicitudMalFormada);

        // El documento OpenAPI (Swagger) usa estas opciones: los enums se documentan como texto, igual que la API.
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));

        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddHttpContextAccessor();

        AddSeguridad(services, configuration);
        AddRendimiento(services, configuration);
        AddObservabilidad(builder);

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            // Azure App Service / contenedores terminan TLS en un proxy frontal.
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return builder;
    }

    private static void AddSeguridad(IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey.PadRight(32))),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Name,
                    RoleClaimType = KARiderClaims.Rol
                };
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        var (code, detail) = context.AuthenticateFailure switch
                        {
                            SecurityTokenExpiredException => ("AUTH_TOKEN_EXPIRADO", "Tu sesión expiró. Renueva el token con /api/v1/auth/refresh o inicia sesión de nuevo."),
                            not null => ("AUTH_TOKEN_INVALIDO", "El token de acceso no es válido o fue alterado. Inicia sesión nuevamente."),
                            null => ("AUTH_TOKEN_REQUERIDO", "Debes iniciar sesión para acceder a este recurso (encabezado Authorization: Bearer <token>).")
                        };
                        context.Response.Headers.WWWAuthenticate = "Bearer";
                        await ApiProblems.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, code, detail);
                    }
                };
            });

        services.AddAuthorizationBuilder()
            // Seguro por defecto: todo endpoint exige sesión de un usuario con correo verificado,
            // salvo los marcados explícitamente con [AllowAnonymous].
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(KARiderClaims.Verificado, "true")
                .Build())
            .SetDefaultPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(KARiderClaims.Verificado, "true")
                .Build())
            .AddPolicy(AuthorizationPolicies.Conductor, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(KARiderClaims.Verificado, "true")
                .RequireRole("Conductor", "Administrador"));
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProblemAuthorizationResultHandler>();

        services.AddOptions<RateLimitSettings>()
            .Bind(configuration.GetSection(RateLimitSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        var limites = configuration.GetSection(RateLimitSettings.SectionName).Get<RateLimitSettings>() ?? new RateLimitSettings();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                        ?? context.Connection.RemoteIpAddress?.ToString()
                        ?? "anonimo",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limites.SolicitudesPorMinuto,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
            // Protección contra fuerza bruta en login, registro y códigos.
            options.AddPolicy(RateLimitSettings.AuthPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "anonimo",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limites.SolicitudesAuthPorMinuto,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
            options.OnRejected = async (context, _) =>
            {
                var segundos = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)
                    ? (int)Math.Ceiling(retry.TotalSeconds)
                    : 60;
                context.HttpContext.Response.Headers.RetryAfter = segundos.ToString(CultureInfo.InvariantCulture);
                await ApiProblems.WriteAsync(
                    context.HttpContext,
                    StatusCodes.Status429TooManyRequests,
                    "LIMITE_SOLICITUDES_EXCEDIDO",
                    $"Realizaste demasiadas solicitudes. Intenta de nuevo en {segundos} segundos.");
            };
        });
    }

    private static void AddRendimiento(IServiceCollection services, IConfiguration configuration)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });
        services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

        services.AddOutputCache(options =>
            options.AddPolicy("catalogos", policy => policy.Expire(TimeSpan.FromMinutes(10))));

        // Escalamiento vertical: hilos mínimos configurables para absorber picos (ej. salida de clases).
        var minHilos = configuration.GetValue<int?>("Rendimiento:MinWorkerThreads");
        if (minHilos is > 0)
        {
            ThreadPool.GetMinThreads(out _, out var io);
            ThreadPool.SetMinThreads(minHilos.Value, io);
        }
    }

    private static void AddObservabilidad(WebApplicationBuilder builder)
    {
        // Application Insights solo si Azure define la cadena de conexión.
        if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        {
            builder.Services.AddOpenTelemetry().UseAzureMonitor();
        }
    }

    private static ObjectResult SolicitudMalFormada(ActionContext context)
    {
        var errores = context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToDictionary(
                e => string.IsNullOrEmpty(e.Key) ? "body" : ValidationFilter.ToCamelCasePath(e.Key.TrimStart('$', '.')),
                e => new[]
                {
                    string.IsNullOrEmpty(e.Key) || e.Key is "$" or "request"
                        ? "El cuerpo de la solicitud está vacío o no es un JSON válido."
                        : $"El valor enviado para «{e.Key.TrimStart('$', '.')}» no tiene el tipo o formato esperado."
                });

        var problem = ApiProblems.Create(
            context.HttpContext,
            StatusCodes.Status400BadRequest,
            "SOLICITUD_MAL_FORMADA",
            "No fue posible interpretar la solicitud. Verifica que el JSON sea válido y que cada campo tenga el tipo correcto.",
            errores);

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { ApiProblems.ContentType }
        };
    }
}
