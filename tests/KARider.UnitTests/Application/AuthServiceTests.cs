using System.Text.RegularExpressions;
using KARider.Application.Abstractions;
using KARider.Application.Common;
using KARider.Application.Features.Auth;
using KARider.Domain.Entities;
using KARider.Domain.Enums;
using KARider.Infrastructure.Persistence;
using KARider.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KARider.UnitTests.Application;

public sealed partial class AuthServiceTests : IDisposable
{
    private const string Password = "Karider#2026";
    private const string Correo = "carlos.mendoza@uttt.edu.mx";

    private readonly AppDbContext _db;
    private readonly CorreoFalso _correo = new();
    private readonly AuthService _auth;

    public AuthServiceTests()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _db.Carreras.Add(new Carrera { Id = 1, Nombre = "IDGS" });
        _db.SaveChanges();

        var hashing = Options.Create(new HashingOptions { Pepper = new string('p', 32) });
        var jwt = Options.Create(new JwtOptions { SigningKey = new string('k', 32) });

        _auth = new AuthService(
            _db,
            new PasswordHasherAdapter(),
            new HmacSecretHasher(hashing),
            new JwtTokenService(jwt, TimeProvider.System),
            _correo,
            Options.Create(new SeguridadOptions { MaxIntentosFallidos = 3, SegundosEntreCodigos = 10 }),
            TimeProvider.System,
            NullLogger<AuthService>.Instance);
    }

    private static RegistroRequest Registro(string correo = Correo, string matricula = "22300630") =>
        new(Rol.Pasajero, "Carlos Mendoza", matricula, "7731234567", 1, 7, correo, Password, Password, null);

    [Fact]
    public async Task Registro_duplicado_devuelve_error_especifico()
    {
        Assert.True((await _auth.RegistrarAsync(Registro(), default)).IsSuccess);

        var correoDuplicado = await _auth.RegistrarAsync(Registro(matricula: "22300999"), default);
        var matriculaDuplicada = await _auth.RegistrarAsync(Registro(correo: "otro@uttt.edu.mx"), default);

        Assert.Equal("USUARIO_CORREO_DUPLICADO", correoDuplicado.Error.Code);
        Assert.Equal("USUARIO_MATRICULA_DUPLICADA", matriculaDuplicada.Error.Code);
    }

    [Fact]
    public async Task Login_requiere_correo_verificado_y_despues_emite_tokens()
    {
        await _auth.RegistrarAsync(Registro(), default);
        var login = new LoginRequest(Correo, Password, null, false);

        Assert.Equal("AUTH_CORREO_NO_VERIFICADO", (await _auth.LoginAsync(login, "127.0.0.1", default)).Error.Code);

        var verificado = await _auth.VerificarCorreoAsync(new VerificarCorreoRequest(Correo, _correo.UltimoCodigo!), default);
        Assert.True(verificado.IsSuccess);

        var sesion = await _auth.LoginAsync(login, "127.0.0.1", default);
        Assert.True(sesion.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(sesion.Value.AccessToken));
        Assert.Equal("Bearer", sesion.Value.TokenType);

        // Solo se guarda el hash del refresh token.
        Assert.DoesNotContain(_db.RefreshTokens, t => t.TokenHash == sesion.Value.RefreshToken);
    }

    [Fact]
    public async Task Codigo_incorrecto_y_conductor_sin_vehiculo_tienen_codigos_propios()
    {
        await _auth.RegistrarAsync(Registro(), default);

        var codigoMalo = await _auth.VerificarCorreoAsync(new VerificarCorreoRequest(Correo, "000000"), default);
        Assert.Equal(_correo.UltimoCodigo == "000000" ? "AUTH_CORREO_YA_VERIFICADO" : "AUTH_CODIGO_INVALIDO", codigoMalo.Error.Code);

        await _auth.VerificarCorreoAsync(new VerificarCorreoRequest(Correo, _correo.UltimoCodigo!), default);
        var comoConductor = await _auth.LoginAsync(new LoginRequest(Correo, Password, Rol.Conductor, false), null, default);
        Assert.Equal("AUTH_ROL_NO_AUTORIZADO", comoConductor.Error.Code);
    }

    [Fact]
    public async Task Intentos_fallidos_bloquean_la_cuenta()
    {
        await _auth.RegistrarAsync(Registro(), default);
        await _auth.VerificarCorreoAsync(new VerificarCorreoRequest(Correo, _correo.UltimoCodigo!), default);
        var incorrecto = new LoginRequest(Correo, "Incorrecta#1", null, false);

        Assert.Equal("AUTH_CREDENCIALES_INVALIDAS", (await _auth.LoginAsync(incorrecto, null, default)).Error.Code);
        Assert.Equal("AUTH_CREDENCIALES_INVALIDAS", (await _auth.LoginAsync(incorrecto, null, default)).Error.Code);
        Assert.Equal("AUTH_CUENTA_BLOQUEADA", (await _auth.LoginAsync(incorrecto, null, default)).Error.Code);

        // Ni siquiera la contraseña correcta entra mientras dure el bloqueo.
        var correcto = await _auth.LoginAsync(new LoginRequest(Correo, Password, null, false), null, default);
        Assert.Equal("AUTH_CUENTA_BLOQUEADA", correcto.Error.Code);
    }

    [Fact]
    public async Task Reutilizar_un_refresh_token_rotado_revoca_todas_las_sesiones()
    {
        await _auth.RegistrarAsync(Registro(), default);
        await _auth.VerificarCorreoAsync(new VerificarCorreoRequest(Correo, _correo.UltimoCodigo!), default);
        var sesion = (await _auth.LoginAsync(new LoginRequest(Correo, Password, null, true), null, default)).Value;

        var rotada = await _auth.RefrescarAsync(new RefreshRequest(sesion.RefreshToken), null, default);
        Assert.True(rotada.IsSuccess);

        var reutilizada = await _auth.RefrescarAsync(new RefreshRequest(sesion.RefreshToken), null, default);
        Assert.Equal("AUTH_REFRESH_TOKEN_INVALIDO", reutilizada.Error.Code);

        var despues = await _auth.RefrescarAsync(new RefreshRequest(rotada.Value.RefreshToken), null, default);
        Assert.Equal("AUTH_REFRESH_TOKEN_INVALIDO", despues.Error.Code);
    }

    public void Dispose() => _db.Dispose();

    private sealed partial class CorreoFalso : IEmailSender
    {
        public string? UltimoCodigo { get; private set; }

        public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            UltimoCodigo = CodigoRegex().Match(htmlBody).Groups[1].Value;
            return Task.CompletedTask;
        }

        [GeneratedRegex(@"letter-spacing:8px;color:#0284C7"">(\d{6})<")]
        private static partial Regex CodigoRegex();
    }
}
