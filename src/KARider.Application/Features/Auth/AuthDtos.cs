using KARider.Domain.Enums;

namespace KARider.Application.Features.Auth;

public sealed record VehiculoRequest(string Modelo, string Color, int Anio, string Placas, int Capacidad);

public sealed record RegistroRequest(
    Rol Rol,
    string NombreCompleto,
    string Matricula,
    string Telefono,
    int CarreraId,
    int? Cuatrimestre,
    string CorreoInstitucional,
    string Password,
    string ConfirmarPassword,
    VehiculoRequest? Vehiculo);

public sealed record RegistroResponse(Guid UsuarioId, string CorreoInstitucional, DateTimeOffset CodigoExpiraEn, string Mensaje);

public sealed record VerificarCorreoRequest(string CorreoInstitucional, string Codigo);

public sealed record CorreoRequest(string CorreoInstitucional);

public sealed record LoginRequest(string CorreoInstitucional, string Password, Rol? Rol, bool Recordarme);

public sealed record RefreshRequest(string RefreshToken);

public sealed record RestablecerPasswordRequest(string CorreoInstitucional, string Codigo, string NuevaPassword, string ConfirmarPassword);

public sealed record CambiarPasswordRequest(string PasswordActual, string NuevaPassword, string ConfirmarPassword);

public sealed record UsuarioSesionDto(Guid Id, string NombreCompleto, string CorreoInstitucional, Rol Rol, string? FotoUrl);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiraEn,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiraEn,
    string TokenType,
    UsuarioSesionDto Usuario);
