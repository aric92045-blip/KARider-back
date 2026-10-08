using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace KARider.API.Common;

/// <summary>
/// Ejecuta los validadores de FluentValidation de cada argumento de la acción y responde
/// con errores por campo (nombres en camelCase) usando el formato estándar.
/// </summary>
public sealed class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public const string Code = "VALIDACION_FALLIDA";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errores = new Dictionary<string, List<string>>();

        foreach (var argumento in context.ActionArguments.Values)
        {
            if (argumento is null)
            {
                continue;
            }

            var tipoValidador = typeof(IValidator<>).MakeGenericType(argumento.GetType());
            if (services.GetService(tipoValidador) is not IValidator validador)
            {
                continue;
            }

            var resultado = await validador.ValidateAsync(new ValidationContext<object>(argumento), context.HttpContext.RequestAborted);
            foreach (var falla in resultado.Errors)
            {
                var campo = ToCamelCasePath(falla.PropertyName);
                if (!errores.TryGetValue(campo, out var mensajes))
                {
                    mensajes = [];
                    errores[campo] = mensajes;
                }

                if (!mensajes.Contains(falla.ErrorMessage))
                {
                    mensajes.Add(falla.ErrorMessage);
                }
            }
        }

        if (errores.Count > 0)
        {
            var problem = ApiProblems.Create(
                context.HttpContext,
                StatusCodes.Status400BadRequest,
                Code,
                "La solicitud contiene datos inválidos. Revisa los campos indicados en «errors».",
                errores.ToDictionary(e => e.Key, e => e.Value.ToArray()));

            context.Result = new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status400BadRequest,
                ContentTypes = { ApiProblems.ContentType }
            };
            return;
        }

        await next();
    }

    internal static string ToCamelCasePath(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
