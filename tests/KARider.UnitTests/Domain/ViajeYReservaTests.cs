using KARider.Domain.Entities;
using KARider.Domain.Enums;

namespace KARider.UnitTests.Domain;

public sealed class ViajeYReservaTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 9, 20, 18, 0, 0, TimeSpan.Zero);

    private static Vehiculo CrearVehiculo(Guid conductorId, int capacidad = 4) =>
        Vehiculo.Crear(conductorId, "Nissan Versa", "Azul", 2022, "hnx-421-c", capacidad);

    private static Viaje CrearViaje(Guid conductorId, int asientos = 3, decimal gasto = 160m)
    {
        var resultado = Viaje.Crear(
            conductorId, CrearVehiculo(conductorId), 1, "Pachuca", null, null,
            Ahora.AddHours(2), asientos, gasto, null, ["Actopan Centro"], publicar: true, Ahora);
        Assert.True(resultado.IsSuccess);
        return resultado.Value;
    }

    [Theory]
    [InlineData(160, 3, 40)]
    [InlineData(100, 1, 50)]
    [InlineData(100, 2, 33.33)]
    public void CalcularAporte_divide_entre_pasajeros_mas_conductor(decimal gasto, int asientos, decimal esperado) =>
        Assert.Equal(esperado, Viaje.CalcularAporte(gasto, asientos));

    [Fact]
    public void Crear_con_fecha_pasada_falla_con_codigo_especifico()
    {
        var conductor = Guid.NewGuid();
        var resultado = Viaje.Crear(conductor, CrearVehiculo(conductor), 1, "Tula", null, null,
            Ahora.AddMinutes(-1), 2, 100, null, [], true, Ahora);

        Assert.True(resultado.IsFailure);
        Assert.Equal("VIAJE_FECHA_PASADA", resultado.Error.Code);
    }

    [Fact]
    public void Crear_con_mas_asientos_que_la_capacidad_falla()
    {
        var conductor = Guid.NewGuid();
        var resultado = Viaje.Crear(conductor, CrearVehiculo(conductor, capacidad: 2), 1, "Tula", null, null,
            Ahora.AddHours(1), 3, 100, null, [], true, Ahora);

        Assert.Equal("VEHICULO_CAPACIDAD_INSUFICIENTE", resultado.Error.Code);
    }

    [Fact]
    public void Crear_normaliza_fecha_a_utc_y_placas_a_mayusculas()
    {
        var conductor = Guid.NewGuid();
        var vehiculo = CrearVehiculo(conductor);
        var salidaLocal = new DateTimeOffset(2026, 9, 20, 14, 30, 0, TimeSpan.FromHours(-6));
        var viaje = Viaje.Crear(conductor, vehiculo, 1, "Tula", null, null, salidaLocal, 2, 90, null, [], true, Ahora).Value;

        Assert.Equal(TimeSpan.Zero, viaje.FechaSalida.Offset);
        Assert.Equal("HNX-421-C", vehiculo.Placas);
    }

    [Fact]
    public void Reserva_aparta_asientos_y_rechazo_los_libera()
    {
        var viaje = CrearViaje(Guid.NewGuid());
        var reserva = Reserva.Crear(viaje, Guid.NewGuid(), 2, Ahora).Value;

        Assert.Equal(1, viaje.AsientosDisponibles);
        Assert.Equal(80m, reserva.MontoAporte);
        Assert.StartsWith("KAR-UTTT-2026-", reserva.Folio, StringComparison.Ordinal);

        Assert.True(reserva.Rechazar(viaje, Ahora).IsSuccess);
        Assert.Equal(3, viaje.AsientosDisponibles);
        Assert.Equal(EstadoReserva.Rechazada, reserva.Estado);
    }

    [Fact]
    public void Reserva_sin_cupo_suficiente_indica_asientos_restantes()
    {
        var viaje = CrearViaje(Guid.NewGuid(), asientos: 1);
        var resultado = Reserva.Crear(viaje, Guid.NewGuid(), 2, Ahora);

        Assert.Equal("VIAJE_SIN_CUPO", resultado.Error.Code);
        Assert.Contains("1 asiento", resultado.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void No_se_puede_reservar_un_viaje_propio()
    {
        var conductor = Guid.NewGuid();
        var resultado = Reserva.Crear(CrearViaje(conductor), conductor, 1, Ahora);

        Assert.Equal("RESERVA_VIAJE_PROPIO", resultado.Error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Reserva_con_asientos_fuera_de_rango_falla(int asientos)
    {
        var resultado = Reserva.Crear(CrearViaje(Guid.NewGuid()), Guid.NewGuid(), asientos, Ahora);
        Assert.Equal("RESERVA_ASIENTOS_INVALIDOS", resultado.Error.Code);
    }

    [Fact]
    public void Borrador_no_acepta_reservas()
    {
        var conductor = Guid.NewGuid();
        var borrador = Viaje.Crear(conductor, CrearVehiculo(conductor), 1, "Tula", null, null,
            Ahora.AddHours(1), 2, 100, null, [], publicar: false, Ahora).Value;

        Assert.Equal(EstadoViaje.Borrador, borrador.Estado);
        Assert.Equal("VIAJE_NO_DISPONIBLE", Reserva.Crear(borrador, Guid.NewGuid(), 1, Ahora).Error.Code);
    }

    [Fact]
    public void Transicion_invalida_de_estado_es_rechazada()
    {
        var viaje = CrearViaje(Guid.NewGuid());
        var resultado = viaje.CambiarEstado(EstadoViaje.Completado, Ahora);

        Assert.Equal("VIAJE_TRANSICION_INVALIDA", resultado.Error.Code);
    }

    [Fact]
    public void Editar_cupos_no_puede_superar_capacidad_considerando_reservados()
    {
        var viaje = CrearViaje(Guid.NewGuid(), asientos: 3);
        Reserva.Crear(viaje, Guid.NewGuid(), 2, Ahora);

        var resultado = viaje.Actualizar(1, Ahora.AddHours(3), cuposLibres: 3, capacidadVehiculo: 4, Ahora);

        Assert.Equal("VIAJE_CUPOS_INVALIDOS", resultado.Error.Code);
        Assert.True(viaje.Actualizar(1, Ahora.AddHours(3), cuposLibres: 2, capacidadVehiculo: 4, Ahora).IsSuccess);
        Assert.Equal(4, viaje.AsientosOfrecidos);
    }

    [Fact]
    public void Ubicacion_solo_se_comparte_en_viajes_activos()
    {
        var viaje = CrearViaje(Guid.NewGuid());
        Assert.True(viaje.ActualizarUbicacion(20.08, -99.34, Ahora).IsSuccess);

        viaje.CambiarEstado(EstadoViaje.Cancelado, Ahora);
        Assert.Null(viaje.UbicacionLatitud);
        Assert.Equal("VIAJE_UBICACION_NO_PERMITIDA", viaje.ActualizarUbicacion(20.08, -99.34, Ahora).Error.Code);
    }
}
