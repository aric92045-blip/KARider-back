using KARider.API.Common;
using KARider.API.Extensions;
using KARider.Application;
using KARider.Infrastructure;

// En Development carga el archivo .env de la raíz del repositorio (no se usa en Azure).
DotEnv.CargarEnDesarrollo();

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// Servicios
// ============================================================================
builder.Services.AddApplication(builder.Configuration);    // casos de uso y validaciones
builder.Services.AddInfrastructure(builder.Configuration); // PostgreSQL, JWT, correo, push
builder.AddApiServices();                                  // controladores, autenticación, rate limiting, compresión

// ---------------------------------------------------------------------------
// CORS: orígenes permitidos para el frontend PWA.
//   appsettings / variables de entorno: Cors__AllowedOrigins__0, Cors__AllowedOrigins__1…
//   En Development también se acepta cualquier puerto de localhost para pruebas.
// ---------------------------------------------------------------------------
var origenesPermitidos = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var esDesarrollo = builder.Environment.IsDevelopment();

builder.Services.AddCors(options => options.AddPolicy(CorsSettings.PolicyName, policy =>
{
    if (esDesarrollo)
    {
        policy.SetIsOriginAllowed(origen =>
            origenesPermitidos.Contains(origen, StringComparer.OrdinalIgnoreCase) ||
            (Uri.TryCreate(origen, UriKind.Absolute, out var uri) && uri.IsLoopback));
    }
    else
    {
        policy.WithOrigins(origenesPermitidos);
    }

    policy
        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
        .WithHeaders("Authorization", "Content-Type", "Accept")
        .WithExposedHeaders("Retry-After", "Location")
        .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
}));

// ---------------------------------------------------------------------------
// Swagger: documento OpenAPI (/openapi/v1.json) + interfaz Swagger UI (/swagger).
//   Activo en Development; en otros entornos con Swagger__Habilitado=true.
// ---------------------------------------------------------------------------
var swaggerHabilitado = builder.Configuration.GetValue("Swagger:Habilitado", esDesarrollo);
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var app = builder.Build();

await app.Services.AplicarMigracionesSiEstaHabilitadoAsync();

// ============================================================================
// Pipeline HTTP (el orden importa)
// ============================================================================
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages(PipelineExtensions.EscribirErrorSinCuerpoAsync);

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseMiddleware<SecurityHeadersMiddleware>();

// Los tokens viajan en las respuestas de /auth: no se comprimen para mitigar ataques tipo BREACH.
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api/v1/auth"),
    branch => branch.UseResponseCompression());

if (swaggerHabilitado)
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "KARider API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "KARider API · Swagger";
        options.EnablePersistAuthorization(); // conserva el token del botón «Authorize» al recargar
        options.EnableTryItOutByDefault();
        options.DisplayRequestDuration();
        options.EnableFilter();
    });
}

app.UseRouting();
app.UseCors(CorsSettings.PolicyName);   // después de UseRouting y antes de la autenticación
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseOutputCache();

app.MapEndpointsDeSalud();
app.MapControllers();

await app.RunAsync();

/// <summary>Expuesto para pruebas de integración con WebApplicationFactory.</summary>
public partial class Program;
