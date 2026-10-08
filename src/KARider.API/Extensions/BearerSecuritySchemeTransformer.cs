using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace KARider.API.Extensions;

/// <summary>Documenta el esquema JWT Bearer y la información general en el documento OpenAPI.</summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "KARider API",
            Version = "v1",
            Description = "API de viajes compartidos para la comunidad UTTT. Errores en formato Problem Details con campo «code» (ver docs/errores.md)."
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Access token obtenido en /api/v1/auth/login"
        };

        var requisito = new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme { Reference = new OpenApiReference { Id = "Bearer", Type = ReferenceType.SecurityScheme } }] = []
        };

        foreach (var operacion in document.Paths.Values.SelectMany(p => p.Operations.Values))
        {
            operacion.Security.Add(requisito);
        }

        return Task.CompletedTask;
    }
}
