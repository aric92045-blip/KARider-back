using KARider.Application.Abstractions;
using KARider.Application.Common;
using KARider.Domain.Common;
using KARider.Domain.Entities;
using KARider.Domain.Enums;
using KARider.Domain.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KARider.Application.Features.Auth;

/// <summary>
/// Registro, verificación institucional, inicio de sesión y ciclo de vida de sesiones (pantallas P2, P3 y P4).
/// </summary>
public sealed class AuthService(
    IApplicationDbContext db,
    IPasswordHasher passwordHasher,
    ISecretHasher secretHasher,
    ITokenService tokenService,
    IEmailSender emailSender,
    IOptions<SeguridadOptions> seguridadOptions,
    TimeProvider time,
    ILogger<AuthService> logger)
{
    private const string MensajeGenericoCorreo =
        "Si el correo pertenece a una cuenta válida, recibirás un código en los próximos minutos.";

    private readonly SeguridadOptions _seguridad = seguridadOptions.Value;

    public async Task<Result<RegistroResponse>> RegistrarAsync(RegistroRequest request, CancellationToken ct)
    {
        var correo = Usuario.NormalizarCorreo(request.CorreoInstitucional);
        var matricula = request.Matricula.Trim();

        if (await db.Usuarios.AnyAsync(u => u.CorreoInstitucional == correo, ct))
        {
            return DomainErrors.Usuario.CorreoDuplicado;
        }

        if (await db.Usuarios.AnyAsync(u => u.Matricula == matricula, ct))
        {
            return DomainErrors.Usuario.MatriculaDuplicada;
        }

        if (!await db.Carreras.AnyAsync(c => c.Id == request.CarreraId && c.Activa, ct))
        {
            return DomainErrors.Usuario.CarreraInvalida;
        }

        if (request.Rol == Rol.Conductor && request.Vehiculo is null)
        {
            return DomainErrors.Vehiculo.RequeridoParaConductor;
        }

        if (request.Vehiculo is not null)
        {
            var placas = Vehiculo.NormalizarPlacas(request.Vehiculo.Placas);
            if (await db.Vehiculos.AnyAsync(v => v.Placas == placas && v.Activo, ct))
            {
                return DomainErrors.Vehiculo.PlacasDuplicadas;
            }
        }

        var usuario = Usuario.Crear(
            request.NombreCompleto,
            matricula,
            request.Telefono,
            correo,
            passwordHasher.Hash(request.Password),
            Rol.Pasajero,
            request.CarreraId,
            request.Cuatrimestre);

        db.Usuarios.Add(usuario);

        if (request.Vehiculo is { } v)
        {
            var vehiculo = Vehiculo.Crear(usuario.Id, v.Modelo, v.Color, v.Anio, v.Placas, v.Capacidad);
            db.Vehiculos.Add(vehiculo);
            usuario.PromoverAConductor();
        }

        var (codigo, expiraEn) = await GenerarCodigoAsync(usuario.Id, PropositoCodigo.VerificarCorreo, ct);
        await db.SaveChangesAsync(ct);

        await EnviarCodigoAsync(usuario, codigo, "verificar tu correo institucional en KARider", ct);
        logger.LogInformation("Usuario {UsuarioId} registrado como {Rol}", usuario.Id, usuario.Rol);

        return new RegistroResponse(
            usuario.Id,
            usuario.CorreoInstitucional,
            expiraEn,
            "Cuenta creada. Te enviamos un código de 6 dígitos a tu correo institucional para verificarla.");
    }

    public async Task<Result<MensajeResponse>> VerificarCorreoAsync(VerificarCorreoRequest request, CancellationToken ct)
    {
        var correo = Usuario.NormalizarCorreo(request.CorreoInstitucional);
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.CorreoInstitucional == correo, ct);
        if (usuario is null)
        {
            // No revelamos si el correo existe.
            return DomainErrors.Auth.CodigoInvalido;
        }

        if (usuario.CorreoVerificado)
        {
            return DomainErrors.Auth.CorreoYaVerificado;
        }

        var validacion = await ValidarCodigoAsync(usuario.Id, PropositoCodigo.VerificarCorreo, request.Codigo, ct);
        if (validacion.IsFailure)
        {
            return validacion.Error;
        }

        usuario.MarcarCorreoVerificado(time.GetUtcNow());
        await db.SaveChangesAsync(ct);

        return new MensajeResponse("Tu correo institucional fue verificado. Ya puedes iniciar sesión.");
    }

    public async Task<Result<MensajeResponse>> ReenviarCodigoVerificacionAsync(CorreoRequest request, CancellationToken ct)
    {
        var correo = Usuario.NormalizarCorreo(request.CorreoInstitucional);
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.CorreoInstitucional == correo, ct);

        if (usuario is { CorreoVerificado: false, Activo: true } && !await CodigoRecienteAsync(usuario.Id, PropositoCodigo.VerificarCorreo, ct))
        {
            var (codigo, _) = await GenerarCodigoAsync(usuario.Id, PropositoCodigo.VerificarCorreo, ct);
            await db.SaveChangesAsync(ct);
            await EnviarCodigoAsync(usuario, codigo, "verificar tu correo institucional en KARider", ct);
        }

        return new MensajeResponse(MensajeGenericoCorreo);
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, string? ip, CancellationToken ct)
    {
        var ahora = time.GetUtcNow();
        var correo = Usuario.NormalizarCorreo(request.CorreoInstitucional);
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.CorreoInstitucional == correo, ct);

        if (usuario is null)
        {
            logger.LogWarning("Intento de inicio de sesión con correo no registrado desde {Ip}", ip);
            return DomainErrors.Auth.CredencialesInvalidas;
        }

        if (usuario.EstaBloqueado(ahora))
        {
            return DomainErrors.Auth.CuentaBloqueada(usuario.BloqueadoHasta!.Value, ahora);
        }

        var verificacion = passwordHasher.Verify(usuario.PasswordHash, request.Password);
        if (verificacion == PasswordVerification.Failed)
        {
            usuario.RegistrarIntentoFallido(ahora, _seguridad.MaxIntentosFallidos, TimeSpan.FromMinutes(_seguridad.MinutosBloqueo));
            await db.SaveChangesAsync(ct);

            if (usuario.EstaBloqueado(ahora))
            {
                logger.LogWarning("Cuenta {UsuarioId} bloqueada por intentos fallidos desde {Ip}", usuario.Id, ip);
                return DomainErrors.Auth.CuentaBloqueada(usuario.BloqueadoHasta!.Value, ahora);
            }

            return DomainErrors.Auth.CredencialesInvalidas;
        }

        if (!usuario.Activo)
        {
            return DomainErrors.Auth.CuentaInactiva;
        }

        if (!usuario.CorreoVerificado)
        {
            return DomainErrors.Auth.CorreoNoVerificado;
        }

        if (request.Rol == Rol.Conductor && !usuario.EsConductor)
        {
            return DomainErrors.Auth.RolNoAutorizado;
        }

        if (verificacion == PasswordVerification.SuccessRehashNeeded)
        {
            usuario.RehashPassword(passwordHasher.Hash(request.Password));
        }

        usuario.ReiniciarIntentosFallidos();
        var (respuesta, _) = EmitirSesion(usuario, request.Recordarme, ip, ahora);
        await db.SaveChangesAsync(ct);

        return respuesta;
    }

    public async Task<Result<AuthResponse>> RefrescarAsync(RefreshRequest request, string? ip, CancellationToken ct)
    {
        var ahora = time.GetUtcNow();
        var hash = secretHasher.Hash(request.RefreshToken);
        var token = await db.RefreshTokens
            .Include(t => t.Usuario)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (token?.Usuario is null)
        {
            return DomainErrors.Auth.RefreshTokenInvalido;
        }

        if (token.RevocadoEn is not null)
        {
            // Reutilización de un token ya rotado: posible robo. Se cierran todas las sesiones.
            logger.LogWarning("Reutilización de refresh token detectada para {UsuarioId} desde {Ip}", token.UsuarioId, ip);
            await RevocarSesionesAsync(token.UsuarioId, ahora, ct);
            await db.SaveChangesAsync(ct);
            return DomainErrors.Auth.RefreshTokenInvalido;
        }

        if (!token.EstaActivo(ahora))
        {
            return DomainErrors.Auth.RefreshTokenInvalido;
        }

        if (!token.Usuario.Activo)
        {
            return DomainErrors.Auth.CuentaInactiva;
        }

        var (respuesta, nuevoToken) = EmitirSesion(token.Usuario, token.Recordarme, ip, ahora);
        token.Revocar(ahora, nuevoToken.Id);
        await db.SaveChangesAsync(ct);

        return respuesta;
    }

    public async Task<Result> LogoutAsync(Guid usuarioId, RefreshRequest request, CancellationToken ct)
    {
        var hash = secretHasher.Hash(request.RefreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.UsuarioId == usuarioId, ct);
        if (token is not null)
        {
            token.Revocar(time.GetUtcNow());
            await db.SaveChangesAsync(ct);
        }

        return Result.Success();
    }

    public async Task<Result<MensajeResponse>> SolicitarRestablecimientoAsync(CorreoRequest request, CancellationToken ct)
    {
        var correo = Usuario.NormalizarCorreo(request.CorreoInstitucional);
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.CorreoInstitucional == correo, ct);

        if (usuario is { Activo: true } && !await CodigoRecienteAsync(usuario.Id, PropositoCodigo.RestablecerPassword, ct))
        {
            var (codigo, _) = await GenerarCodigoAsync(usuario.Id, PropositoCodigo.RestablecerPassword, ct);
            await db.SaveChangesAsync(ct);
            await EnviarCodigoAsync(usuario, codigo, "restablecer tu contraseña de KARider", ct);
        }

        return new MensajeResponse(MensajeGenericoCorreo);
    }

    public async Task<Result<MensajeResponse>> RestablecerPasswordAsync(RestablecerPasswordRequest request, CancellationToken ct)
    {
        var correo = Usuario.NormalizarCorreo(request.CorreoInstitucional);
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.CorreoInstitucional == correo, ct);
        if (usuario is null)
        {
            return DomainErrors.Auth.CodigoInvalido;
        }

        var validacion = await ValidarCodigoAsync(usuario.Id, PropositoCodigo.RestablecerPassword, request.Codigo, ct);
        if (validacion.IsFailure)
        {
            return validacion.Error;
        }

        var ahora = time.GetUtcNow();
        usuario.CambiarPassword(passwordHasher.Hash(request.NuevaPassword));

        // Restablecer con un código enviado al correo institucional también prueba la propiedad del correo.
        if (!usuario.CorreoVerificado)
        {
            usuario.MarcarCorreoVerificado(ahora);
        }

        await RevocarSesionesAsync(usuario.Id, ahora, ct);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Contraseña restablecida para {UsuarioId}", usuario.Id);

        return new MensajeResponse("Tu contraseña fue actualizada. Inicia sesión con tu nueva contraseña.");
    }

    public async Task<Result<MensajeResponse>> CambiarPasswordAsync(Guid usuarioId, CambiarPasswordRequest request, CancellationToken ct)
    {
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct);
        if (usuario is null)
        {
            return DomainErrors.Usuario.NoEncontrado;
        }

        if (passwordHasher.Verify(usuario.PasswordHash, request.PasswordActual) == PasswordVerification.Failed)
        {
            return DomainErrors.Auth.PasswordActualIncorrecta;
        }

        usuario.CambiarPassword(passwordHasher.Hash(request.NuevaPassword));
        await RevocarSesionesAsync(usuario.Id, time.GetUtcNow(), ct);
        await db.SaveChangesAsync(ct);

        return new MensajeResponse("Contraseña actualizada. Por seguridad se cerraron tus sesiones en otros dispositivos.");
    }

    private (AuthResponse Respuesta, RefreshToken Token) EmitirSesion(Usuario usuario, bool recordarme, string? ip, DateTimeOffset ahora)
    {
        var access = tokenService.CreateAccessToken(usuario);
        var refreshRaw = secretHasher.GenerateToken();
        var dias = recordarme ? _seguridad.DiasRefreshTokenRecordarme : _seguridad.DiasRefreshToken;
        var refresh = RefreshToken.Crear(usuario.Id, secretHasher.Hash(refreshRaw), ahora.AddDays(dias), ip, recordarme);
        db.RefreshTokens.Add(refresh);

        var respuesta = new AuthResponse(
            access.Token,
            access.ExpiraEn,
            refreshRaw,
            refresh.ExpiraEn,
            "Bearer",
            new UsuarioSesionDto(usuario.Id, usuario.NombreCompleto, usuario.CorreoInstitucional, usuario.Rol, usuario.FotoUrl));

        return (respuesta, refresh);
    }

    private async Task RevocarSesionesAsync(Guid usuarioId, DateTimeOffset ahora, CancellationToken ct)
    {
        var activos = await db.RefreshTokens
            .Where(t => t.UsuarioId == usuarioId && t.RevocadoEn == null)
            .ToListAsync(ct);
        foreach (var token in activos)
        {
            token.Revocar(ahora);
        }
    }

    private async Task<(string Codigo, DateTimeOffset ExpiraEn)> GenerarCodigoAsync(Guid usuarioId, PropositoCodigo proposito, CancellationToken ct)
    {
        var ahora = time.GetUtcNow();

        // Solo el código más reciente es válido.
        var anteriores = await db.CodigosVerificacion
            .Where(c => c.UsuarioId == usuarioId && c.Proposito == proposito && c.UsadoEn == null)
            .ToListAsync(ct);
        foreach (var anterior in anteriores)
        {
            anterior.MarcarUsado(ahora);
        }

        var codigo = secretHasher.GenerateNumericCode(6);
        var expiraEn = ahora.AddMinutes(_seguridad.MinutosExpiracionCodigo);
        db.CodigosVerificacion.Add(CodigoVerificacion.Crear(usuarioId, proposito, secretHasher.Hash(codigo), expiraEn));
        return (codigo, expiraEn);
    }

    private async Task<bool> CodigoRecienteAsync(Guid usuarioId, PropositoCodigo proposito, CancellationToken ct)
    {
        var limite = time.GetUtcNow().AddSeconds(-_seguridad.SegundosEntreCodigos);
        return await db.CodigosVerificacion
            .AnyAsync(c => c.UsuarioId == usuarioId && c.Proposito == proposito && c.CreadoEn > limite, ct);
    }

    private async Task<Result> ValidarCodigoAsync(Guid usuarioId, PropositoCodigo proposito, string codigo, CancellationToken ct)
    {
        var ahora = time.GetUtcNow();
        var registro = await db.CodigosVerificacion
            .Where(c => c.UsuarioId == usuarioId && c.Proposito == proposito && c.UsadoEn == null)
            .OrderByDescending(c => c.CreadoEn)
            .FirstOrDefaultAsync(ct);

        if (registro is null)
        {
            return DomainErrors.Auth.CodigoInvalido;
        }

        if (registro.EstaExpirado(ahora))
        {
            return DomainErrors.Auth.CodigoExpirado;
        }

        if (registro.Intentos >= _seguridad.MaxIntentosCodigo)
        {
            return DomainErrors.Auth.CodigoIntentosExcedidos;
        }

        registro.RegistrarIntento();
        if (!secretHasher.Verify(codigo, registro.CodigoHash))
        {
            await db.SaveChangesAsync(ct);
            return registro.Intentos >= _seguridad.MaxIntentosCodigo
                ? DomainErrors.Auth.CodigoIntentosExcedidos
                : DomainErrors.Auth.CodigoInvalido;
        }

        registro.MarcarUsado(ahora);
        return Result.Success();
    }

    private async Task EnviarCodigoAsync(Usuario usuario, string codigo, string accion, CancellationToken ct)
    {
        try
        {
            var html = EmailTemplates.Codigo(usuario.NombreCompleto, codigo, accion, _seguridad.MinutosExpiracionCodigo);
            await emailSender.SendAsync(usuario.CorreoInstitucional, $"KARider · Tu código para {accion}", html, ct);
        }
#pragma warning disable CA1031 // El usuario puede solicitar reenvío; no se interrumpe el flujo
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogError(ex, "No se pudo enviar el correo con código al usuario {UsuarioId}", usuario.Id);
        }
    }
}
