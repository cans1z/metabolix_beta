using System.Security.Cryptography;
using Metabolix.Api.Data;
using Metabolix.Api.Dtos;
using Metabolix.Api.Models;
using Metabolix.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Metabolix.Api.Controllers;

[Route("api/auth")]
public class AuthController(
    AppDbContext db,
    TokenService tokens,
    RedisStore store,
    IPasswordHasher<User> hasher,
    IGoogleTokenValidator google,
    IEmailSender email,
    FileStorage files,
    ILogger<AuthController> log) : ApiControllerBase
{
    private static readonly TimeSpan LoginWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ResetCodeTtl = TimeSpan.FromMinutes(15);
    private const int MaxLoginAttempts = 10;
    private const int MaxResetAttempts = 5;

    private static string NormalizeEmail(string value) => value.Trim().ToLowerInvariant();

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest r)
    {
        if (!r.AcceptedTerms) return Error(400, "Нужно согласие на обработку персональных данных");

        var address = NormalizeEmail(r.Email);
        if (await db.Users.AnyAsync(u => u.Email == address))
            return Error(409, "Пользователь с такой почтой уже есть");

        var user = new User
        {
            Email = address,
            Name = string.IsNullOrWhiteSpace(r.Name) ? address.Split('@')[0] : r.Name.Trim(),
            ConsentAt = DateTime.UtcNow,
        };
        user.PasswordHash = hasher.HashPassword(user, r.Password);
        db.Users.Add(user);

        // Сначала токены: если Redis недоступен, аккаунт не создастся и повторная попытка не упрётся в «почта занята».
        var response = await IssueAsync(user);
        await db.SaveChangesAsync();
        return response;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest r)
    {
        var address = NormalizeEmail(r.Email);

        // Защита от перебора: не больше 10 попыток на пару «адрес + почта» за 15 минут.
        if (!await store.TryHitAsync($"login:{ClientIp}:{address}", MaxLoginAttempts, LoginWindow))
            return Error(429, "Слишком много попыток входа, подождите 15 минут");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == address);
        // Ответ одинаковый, чтобы нельзя было узнать, зарегистрирована ли почта.
        if (user?.PasswordHash == null
            || hasher.VerifyHashedPassword(user, user.PasswordHash, r.Password) == PasswordVerificationResult.Failed)
            return Error(401, "Неверная почта или пароль");

        await store.Db.KeyDeleteAsync($"login:{ClientIp}:{address}");
        return await IssueAsync(user);
    }

    [AllowAnonymous]
    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> Google(GoogleLoginRequest r)
    {
        if (!google.IsConfigured) return Error(503, "Вход через Google не настроен на сервере");

        var payload = await google.ValidateAsync(r.IdToken);
        if (payload == null) return Error(401, "Токен Google не прошёл проверку");

        var user = await db.Users.FirstOrDefaultAsync(u => u.GoogleId == payload.Subject);
        if (user == null)
        {
            // Привязываем к существующему аккаунту только подтверждённую почту, иначе это захват чужого аккаунта.
            if (!payload.EmailVerified) return Error(401, "Почта Google не подтверждена");

            var address = NormalizeEmail(payload.Email);
            user = await db.Users.FirstOrDefaultAsync(u => u.Email == address);
            if (user != null)
            {
                user.GoogleId = payload.Subject;
            }
            else
            {
                if (!r.AcceptedTerms) return Error(400, "Нужно согласие на обработку персональных данных");
                user = new User
                {
                    Email = address,
                    Name = payload.Name ?? address.Split('@')[0],
                    GoogleId = payload.Subject,
                    ConsentAt = DateTime.UtcNow,
                };
                db.Users.Add(user);
            }
        }

        var response = await IssueAsync(user);
        await db.SaveChangesAsync();
        return response;
    }

    /// <summary>Меняет refresh-токен на новую пару. Старый после этого не работает.</summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest r)
    {
        var userId = await tokens.ConsumeRefreshTokenAsync(r.RefreshToken);
        if (userId == null) return Error(401, "Сессия истекла, войдите заново");

        var user = await db.Users.FindAsync(userId.Value);
        if (user == null) return Error(401, "Сессия истекла, войдите заново");

        return await IssueAsync(user);
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest r)
    {
        await tokens.ConsumeRefreshTokenAsync(r.RefreshToken);
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await db.Users.FindAsync(UserId);
        return await ToDtoAsync(user!);
    }

    /// <summary>Всегда отвечает 204: иначе по ответу можно узнать, чья почта зарегистрирована.</summary>
    [AllowAnonymous]
    [HttpPost("forgot")]
    public async Task<IActionResult> Forgot(ForgotPasswordRequest r)
    {
        var address = NormalizeEmail(r.Email);
        if (!await store.TryHitAsync($"forgot:{address}", 3, ResetCodeTtl)
            || !await store.TryHitAsync($"forgot-ip:{ClientIp}", 20, ResetCodeTtl))
            return Error(429, "Слишком много запросов, подождите 15 минут");

        if (!await db.Users.AnyAsync(u => u.Email == address)) return NoContent();

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        await store.Db.StringSetAsync($"reset:{address}", TokenService.Hash(address + code), ResetCodeTtl);
        await store.Db.KeyDeleteAsync($"reset-attempts:{address}");

        try
        {
            await email.SendAsync(address, "Восстановление доступа к Metabolix",
                $"Код для смены пароля: {code}\nОн действует 15 минут. Если вы не запрашивали смену пароля, просто проигнорируйте это письмо.");
        }
        catch (Exception e)
        {
            // Клиенту ошибку не показываем по той же причине, что и выше.
            log.LogError(e, "Не удалось отправить письмо восстановления");
        }
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("reset")]
    public async Task<IActionResult> Reset(ResetPasswordRequest r)
    {
        var address = NormalizeEmail(r.Email);
        var invalid = Error(400, "Неверный или просроченный код");

        var stored = await store.Db.StringGetAsync($"reset:{address}");
        if (stored.IsNull) return invalid;

        // 6 цифр легко перебрать, поэтому после 5 ошибок код сгорает.
        if (!await store.TryHitAsync($"reset-attempts:{address}", MaxResetAttempts, ResetCodeTtl))
        {
            await store.Db.KeyDeleteAsync($"reset:{address}");
            return invalid;
        }
        if (stored != TokenService.Hash(address + r.Code)) return invalid;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == address);
        if (user == null) return invalid;

        user.PasswordHash = hasher.HashPassword(user, r.NewPassword);
        await db.SaveChangesAsync();

        await store.Db.KeyDeleteAsync([$"reset:{address}", $"reset-attempts:{address}"]);
        await tokens.RevokeAllAsync(user.Id);
        return NoContent();
    }

    /// <summary>Полное удаление аккаунта (п. 5.7 ТЗ). Связанные данные удаляет каскад в БД.</summary>
    [HttpPost("delete-account")]
    public async Task<IActionResult> DeleteAccount(DeleteAccountRequest r)
    {
        var user = (await db.Users.FindAsync(UserId))!;
        if (user.PasswordHash != null
            && (r.Password == null
                || hasher.VerifyHashedPassword(user, user.PasswordHash, r.Password) == PasswordVerificationResult.Failed))
            return Error(403, "Неверный пароль");

        var images = await db.Foods.Where(f => f.OwnerId == user.Id).Select(f => f.ImageUrl)
            .Concat(db.Recipes.Where(x => x.UserId == user.Id).Select(x => x.ImageUrl))
            .ToListAsync();
        images.Add(user.AvatarUrl);

        db.Users.Remove(user);
        await db.SaveChangesAsync();

        await tokens.RevokeAllAsync(user.Id);
        images.ForEach(files.Delete);
        return NoContent();
    }

    private async Task<AuthResponse> IssueAsync(User user) => new(
        tokens.CreateAccessToken(user.Id),
        await tokens.IssueRefreshTokenAsync(user.Id),
        tokens.AccessTokenSeconds,
        await ToDtoAsync(user));

    private async Task<UserDto> ToDtoAsync(User user) =>
        new(user.Id, user.Email, user.Name, user.AvatarUrl, await db.Profiles.AnyAsync(p => p.UserId == user.Id));
}
