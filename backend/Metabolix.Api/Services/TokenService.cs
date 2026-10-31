using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace Metabolix.Api.Services;

public class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "metabolix";
    public string Audience { get; set; } = "metabolix-clients";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;

    public SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(Key));

    public void Validate(IHostEnvironment env)
    {
        if (Encoding.UTF8.GetByteCount(Key) < 32)
            throw new InvalidOperationException("Jwt:Key не задан или короче 32 байт");
        if (!env.IsDevelopment() && Key.StartsWith("dev-only"))
            throw new InvalidOperationException("Вне режима Development нужен собственный Jwt:Key");
    }
}

/// <summary>
/// Access-токен (JWT) живёт 15 минут и проверяется без обращения к хранилищу.
/// Refresh-токен живёт 30 дней и лежит в Redis, поэтому его можно отозвать: это и есть выход из аккаунта.
/// </summary>
public class TokenService(JwtOptions options, RedisStore store)
{
    private IDatabase Db => store.Db;

    public int AccessTokenSeconds => options.AccessTokenMinutes * 60;

    public string CreateAccessToken(Guid userId)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())]),
            Issuer = options.Issuer,
            Audience = options.Audience,
            Expires = DateTime.UtcNow.AddMinutes(options.AccessTokenMinutes),
            SigningCredentials = new SigningCredentials(options.SigningKey, SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public async Task<string> IssueRefreshTokenAsync(Guid userId)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var hash = Hash(token);
        var ttl = TimeSpan.FromDays(options.RefreshTokenDays);

        // В Redis лежит только хэш: утечка дампа не даст войти в чужой аккаунт.
        await Db.StringSetAsync($"refresh:{hash}", userId.ToString(), ttl);
        await Db.SetAddAsync($"user-tokens:{userId}", hash);
        await Db.KeyExpireAsync($"user-tokens:{userId}", ttl);
        return token;
    }

    /// <summary>
    /// Одноразово забирает refresh-токен. GETDEL атомарен, поэтому один токен нельзя обменять дважды,
    /// даже если два запроса пришли одновременно.
    /// </summary>
    public async Task<Guid?> ConsumeRefreshTokenAsync(string token)
    {
        var hash = Hash(token);
        var value = await Db.StringGetDeleteAsync($"refresh:{hash}");
        if (value.IsNull) return null;

        var userId = Guid.Parse((string)value!);
        await Db.SetRemoveAsync($"user-tokens:{userId}", hash);
        return userId;
    }

    /// <summary>Выход на всех устройствах: после смены пароля и удаления аккаунта.</summary>
    public async Task RevokeAllAsync(Guid userId)
    {
        var hashes = await Db.SetMembersAsync($"user-tokens:{userId}");
        var keys = hashes.Select(h => (RedisKey)$"refresh:{h}").Append($"user-tokens:{userId}").ToArray();
        await Db.KeyDeleteAsync(keys);
    }

    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public static class ClaimsExtensions
{
    public static Guid? UserIdOrNull(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;
}
