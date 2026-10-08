using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using KARider.Application.Abstractions;
using KARider.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using IAppPasswordHasher = KARider.Application.Abstractions.IPasswordHasher;

namespace KARider.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "KARider.API";

    [Required]
    public string Audience { get; set; } = "KARider.PWA";

    /// <summary>Clave HMAC-SHA256 de al menos 32 caracteres. Solo por variable de entorno o Key Vault.</summary>
    [Required(ErrorMessage = "Configura Jwt__SigningKey (mínimo 32 caracteres) como variable de entorno o user-secret.")]
    [MinLength(32, ErrorMessage = "Jwt__SigningKey debe tener al menos 32 caracteres.")]
    public string SigningKey { get; set; } = string.Empty;

    [Range(5, 120)]
    public int AccessTokenMinutes { get; set; } = 15;
}

public sealed class HashingOptions
{
    public const string SectionName = "Hashing";

    /// <summary>Pepper para HMAC de códigos y refresh tokens. Solo por variable de entorno o Key Vault.</summary>
    [Required(ErrorMessage = "Configura Hashing__Pepper (mínimo 32 caracteres) como variable de entorno o user-secret.")]
    [MinLength(32, ErrorMessage = "Hashing__Pepper debe tener al menos 32 caracteres.")]
    public string Pepper { get; set; } = string.Empty;
}

public static class KARiderClaims
{
    public const string Rol = "role";
    public const string Verificado = "verificado";
    public const string SecurityStamp = "sstamp";
}

internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider time) : ITokenService
{
    private readonly JwtOptions _options = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken CreateAccessToken(Usuario usuario)
    {
        var ahora = time.GetUtcNow();
        var expira = ahora.AddMinutes(_options.AccessTokenMinutes);
        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = ahora.UtcDateTime,
            NotBefore = ahora.UtcDateTime,
            Expires = expira.UtcDateTime,
            SigningCredentials = credenciales,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Email, usuario.CorreoInstitucional),
                new Claim(JwtRegisteredClaimNames.Name, usuario.NombreCompleto),
                new Claim(KARiderClaims.Rol, usuario.Rol.ToString()),
                new Claim(KARiderClaims.Verificado, usuario.CorreoVerificado ? "true" : "false"),
                new Claim(KARiderClaims.SecurityStamp, usuario.SecurityStamp)
            ])
        };

        return new AccessToken(_handler.CreateToken(descriptor), expira);
    }
}

/// <summary>PBKDF2 (HMAC-SHA512, 100k iteraciones) de ASP.NET Core Identity.</summary>
internal sealed class PasswordHasherAdapter : IAppPasswordHasher
{
    private static readonly object Usuario = new();
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(Usuario, password);

    public PasswordVerification Verify(string hashedPassword, string providedPassword) =>
        _hasher.VerifyHashedPassword(Usuario, hashedPassword, providedPassword) switch
        {
            PasswordVerificationResult.Success => PasswordVerification.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordVerification.SuccessRehashNeeded,
            _ => PasswordVerification.Failed
        };
}

internal sealed class HmacSecretHasher(IOptions<HashingOptions> options) : ISecretHasher
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(options.Value.Pepper);

    public string Hash(string value) =>
        Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(value)));

    public bool Verify(string value, string hash) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Hash(value)),
            Encoding.ASCII.GetBytes(hash));

    public string GenerateNumericCode(int digits)
    {
        var max = (int)Math.Pow(10, digits);
        return RandomNumberGenerator.GetInt32(0, max).ToString($"D{digits}", System.Globalization.CultureInfo.InvariantCulture);
    }

    public string GenerateToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
}
