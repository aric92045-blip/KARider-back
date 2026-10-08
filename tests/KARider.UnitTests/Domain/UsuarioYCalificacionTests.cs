using KARider.Domain.Entities;
using KARider.Domain.Enums;

namespace KARider.UnitTests.Domain;

public sealed class UsuarioYCalificacionTests
{
    private static Usuario CrearUsuario() =>
        Usuario.Crear("Valeria Gómez", "22300001", "7731234567", " Valeria.Gomez@UTTT.edu.mx ", "hash", Rol.Pasajero, 1, 5);

    [Fact]
    public void Correo_se_normaliza_en_minusculas_sin_espacios() =>
        Assert.Equal("valeria.gomez@uttt.edu.mx", CrearUsuario().CorreoInstitucional);

    [Fact]
    public void Cuenta_se_bloquea_al_alcanzar_el_maximo_de_intentos()
    {
        var usuario = CrearUsuario();
        var ahora = DateTimeOffset.UtcNow;

        for (var i = 0; i < 4; i++)
        {
            usuario.RegistrarIntentoFallido(ahora, 5, TimeSpan.FromMinutes(15));
        }

        Assert.False(usuario.EstaBloqueado(ahora));
        usuario.RegistrarIntentoFallido(ahora, 5, TimeSpan.FromMinutes(15));
        Assert.True(usuario.EstaBloqueado(ahora));
        Assert.False(usuario.EstaBloqueado(ahora.AddMinutes(16)));
    }

    [Fact]
    public void Cambiar_password_rota_el_security_stamp()
    {
        var usuario = CrearUsuario();
        var stamp = usuario.SecurityStamp;
        usuario.CambiarPassword("nuevo");
        Assert.NotEqual(stamp, usuario.SecurityStamp);
    }

    [Fact]
    public void Promedio_de_calificaciones_se_actualiza_incrementalmente()
    {
        var usuario = CrearUsuario();
        usuario.AplicarCalificacion(5);
        usuario.AplicarCalificacion(4);
        usuario.AplicarCalificacion(5);

        Assert.Equal(3, usuario.TotalCalificaciones);
        Assert.Equal(4.67m, usuario.CalificacionPromedio);
    }

    [Fact]
    public void Registrar_vehiculo_convierte_al_pasajero_en_conductor()
    {
        var usuario = CrearUsuario();
        usuario.AgregarVehiculo(Vehiculo.Crear(usuario.Id, "VW Jetta", "Gris", 2020, "ABC-123", 4));
        Assert.Equal(Rol.Conductor, usuario.Rol);
    }

    [Fact]
    public void Calificacion_rechaza_autoevaluacion_y_etiquetas_fuera_de_catalogo()
    {
        var id = Guid.NewGuid();
        Assert.Equal("CALIFICACION_AUTOEVALUACION",
            Calificacion.Crear(Guid.NewGuid(), id, id, Rol.Conductor, 5, null, []).Error.Code);
        Assert.Equal("CALIFICACION_ETIQUETA_INVALIDA",
            Calificacion.Crear(Guid.NewGuid(), id, Guid.NewGuid(), Rol.Conductor, 5, null, ["Grosero"]).Error.Code);
        Assert.Equal("CALIFICACION_ESTRELLAS_INVALIDAS",
            Calificacion.Crear(Guid.NewGuid(), id, Guid.NewGuid(), Rol.Conductor, 6, null, []).Error.Code);
    }

    [Fact]
    public void Calificacion_normaliza_etiquetas_al_nombre_canonico()
    {
        var resultado = Calificacion.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Rol.Conductor, 5, "  Excelente  ", ["puntual", "PUNTUAL", "manejo seguro"]);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(["Puntual", "Manejo seguro"], resultado.Value.Etiquetas);
        Assert.Equal("Excelente", resultado.Value.Comentario);
    }
}
