namespace KARider.API.Common;

/// <summary>
/// Carga un archivo .env local SOLO en Development para no depender de herramientas externas.
/// No sobrescribe variables ya definidas. En Azure las variables vienen de la configuración de App Service.
/// </summary>
public static class DotEnv
{
    public static void CargarEnDesarrollo()
    {
        var entorno = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                      ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        if (!string.Equals(entorno, "Development", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var archivo = Buscar(Directory.GetCurrentDirectory()) ?? Buscar(AppContext.BaseDirectory);
        if (archivo is null)
        {
            return;
        }

        foreach (var linea in File.ReadAllLines(archivo))
        {
            var texto = linea.Trim();
            if (texto.Length == 0 || texto.StartsWith('#'))
            {
                continue;
            }

            var separador = texto.IndexOf('=', StringComparison.Ordinal);
            if (separador <= 0)
            {
                continue;
            }

            var clave = texto[..separador].Trim();
            var valor = texto[(separador + 1)..].Trim().Trim('"', '\'');
            if (Environment.GetEnvironmentVariable(clave) is null)
            {
                Environment.SetEnvironmentVariable(clave, valor);
            }
        }
    }

    private static string? Buscar(string inicio)
    {
        var directorio = new DirectoryInfo(inicio);
        for (var nivel = 0; directorio is not null && nivel < 6; nivel++, directorio = directorio.Parent)
        {
            var candidato = Path.Combine(directorio.FullName, ".env");
            if (File.Exists(candidato))
            {
                return candidato;
            }
        }

        return null;
    }
}
