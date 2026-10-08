using System.Net;

namespace KARider.Application.Features.Auth;

internal static class EmailTemplates
{
    public static string Codigo(string nombre, string codigo, string accion, int minutos)
    {
        var nombreSeguro = WebUtility.HtmlEncode(nombre);
        return $"""
            <div style="font-family:Arial,Helvetica,sans-serif;max-width:480px;margin:auto;color:#1D2D44">
              <h2 style="color:#1D2D44">KARider · UTTT</h2>
              <p>Hola {nombreSeguro},</p>
              <p>Usa el siguiente código para {accion}:</p>
              <p style="font-size:32px;font-weight:bold;letter-spacing:8px;color:#0284C7">{codigo}</p>
              <p>El código vence en {minutos} minutos y solo puede usarse una vez.</p>
              <p style="font-size:12px;color:#64748b">Si no solicitaste este código, ignora este correo. Nunca compartas tu código con nadie.</p>
            </div>
            """;
    }
}
