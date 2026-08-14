using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace VelvetChess.Server.Auth;

public sealed class SessionOptions
{
    public string Issuer { get; set; } = "VelvetChess";
    public string Audience { get; set; } = "VelvetChess.Clients";
    public string SigningKey { get; set; } = "";
}

public sealed class SessionKey(IConfiguration configuration, IHostEnvironment environment)
{
    private readonly byte[] _bytes = Resolve(configuration["Session:SigningKey"], environment);
    public SymmetricSecurityKey SecurityKey => new(_bytes);

    private static byte[] Resolve(string? configured, IHostEnvironment environment)
    {
        if (!string.IsNullOrWhiteSpace(configured) && Encoding.UTF8.GetByteCount(configured) >= 32) return Encoding.UTF8.GetBytes(configured);
        if (!environment.IsDevelopment()) throw new InvalidOperationException("Session:SigningKey длиной не менее 32 байт обязателен в production.");
        return RandomNumberGenerator.GetBytes(64);
    }
}

public sealed class SessionTokenService(SessionKey key, IConfiguration configuration)
{
    public string Issue(string playerId)
    {
        var issuer = configuration["Session:Issuer"] ?? "VelvetChess";
        var audience = configuration["Session:Audience"] ?? "VelvetChess.Clients";
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, playerId), new Claim(ClaimTypes.NameIdentifier, playerId)]),
            Expires = DateTime.UtcNow.AddMinutes(15), Issuer = issuer, Audience = audience,
            SigningCredentials = new SigningCredentials(key.SecurityKey, SecurityAlgorithms.HmacSha256)
        };
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityTokenHandler().CreateToken(descriptor));
    }

    public static string NewRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public static string HashRefreshToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
