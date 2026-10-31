using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Metabolix.Api.Dtos;

namespace Metabolix.Tests;

[Collection("api")]
public class AuthTests(ApiFixture api)
{
    private static string NewEmail() => $"user{Guid.NewGuid():N}@test.ru";

    [Fact]
    public async Task Register_without_consent_is_rejected()
    {
        var response = await api.Anonymous().PostAsJsonAsync("/api/auth/register",
            new { email = NewEmail(), password = "secret123", acceptedTerms = false });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_without_consent_field_is_rejected()
    {
        var response = await api.Anonymous().PostAsJsonAsync("/api/auth/register",
            new { email = NewEmail(), password = "secret123" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_then_login_works_and_email_is_case_insensitive()
    {
        var user = await api.RegisterAsync();

        var login = await api.Anonymous().PostAsJsonAsync("/api/auth/login",
            new { email = user.Email.ToUpperInvariant(), password = TestUser.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var me = await user.GetAsync<UserDto>("/api/auth/me");
        Assert.Equal(user.Email, me.Email);
        Assert.False(me.HasProfile);
    }

    [Fact]
    public async Task Duplicate_email_gets_conflict()
    {
        var user = await api.RegisterAsync();
        var response = await api.Anonymous().PostAsJsonAsync("/api/auth/register",
            new { email = user.Email, password = "another123", acceptedTerms = true });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Too_many_wrong_passwords_are_rate_limited()
    {
        var user = await api.RegisterAsync();
        var client = api.Anonymous();
        for (var i = 0; i < 10; i++)
        {
            var wrong = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "wrong-password" });
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        var blocked = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = TestUser.Password });
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Fact]
    public async Task Refresh_token_works_only_once()
    {
        var user = await api.RegisterAsync();
        var client = api.Anonymous();

        var first = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = user.Auth.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var renewed = (await first.Content.ReadFromJsonAsync<AuthResponse>(ApiFixture.Json))!;
        Assert.NotEqual(user.Auth.RefreshToken, renewed.RefreshToken);

        var second = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = user.Auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token()
    {
        var user = await api.RegisterAsync();
        var client = api.Anonymous();
        await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = user.Auth.RefreshToken });

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = user.Auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Forgot_does_not_reveal_whether_email_exists()
    {
        var response = await api.Anonymous().PostAsJsonAsync("/api/auth/forgot", new { email = NewEmail() });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Password_reset_changes_password_and_ends_sessions()
    {
        var user = await api.RegisterAsync();
        var client = api.Anonymous();
        await client.PostAsJsonAsync("/api/auth/forgot", new { email = user.Email });
        var code = api.Email.CodeFor(user.Email);

        var reset = await client.PostAsJsonAsync("/api/auth/reset", new { email = user.Email, code, newPassword = "brand-new-1" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = TestUser.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "brand-new-1" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = user.Auth.RefreshToken })).StatusCode);

        // Код одноразовый.
        var again = await client.PostAsJsonAsync("/api/auth/reset", new { email = user.Email, code, newPassword = "brand-new-2" });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task Reset_code_burns_after_five_wrong_attempts()
    {
        var user = await api.RegisterAsync();
        var client = api.Anonymous();
        await client.PostAsJsonAsync("/api/auth/forgot", new { email = user.Email });
        var code = api.Email.CodeFor(user.Email);
        var wrong = code == "000000" ? "000001" : "000000";

        for (var i = 0; i < 5; i++)
            await client.PostAsJsonAsync("/api/auth/reset", new { email = user.Email, code = wrong, newPassword = "brand-new-1" });
        await client.PostAsJsonAsync("/api/auth/reset", new { email = user.Email, code = wrong, newPassword = "brand-new-1" });

        var correct = await client.PostAsJsonAsync("/api/auth/reset", new { email = user.Email, code, newPassword = "brand-new-1" });
        Assert.Equal(HttpStatusCode.BadRequest, correct.StatusCode);
    }

    [Fact]
    public async Task Google_login_requires_consent_for_new_user_and_verified_email()
    {
        var client = api.Anonymous();
        var email = NewEmail();
        var subject = Guid.NewGuid().ToString();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/auth/google", new { idToken = "garbage" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/auth/google", new { idToken = $"{subject}|{email}|false", acceptedTerms = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/auth/google", new { idToken = $"{subject}|{email}|true" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/auth/google", new { idToken = $"{subject}|{email}|true", acceptedTerms = true })).StatusCode);

        // Повторный вход согласия уже не требует.
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/auth/google", new { idToken = $"{subject}|{email}|true" })).StatusCode);
    }

    [Fact]
    public async Task Google_login_links_only_verified_email_to_existing_account()
    {
        var user = await api.RegisterAsync();
        var client = api.Anonymous();
        var subject = Guid.NewGuid().ToString();

        var unverified = await client.PostAsJsonAsync("/api/auth/google", new { idToken = $"{subject}|{user.Email}|false" });
        Assert.Equal(HttpStatusCode.Unauthorized, unverified.StatusCode);

        var verified = await client.PostAsJsonAsync("/api/auth/google", new { idToken = $"{subject}|{user.Email}|true" });
        var auth = (await verified.Content.ReadFromJsonAsync<AuthResponse>(ApiFixture.Json))!;
        Assert.Equal(user.Auth.User.Id, auth.User.Id);
    }

    [Fact]
    public async Task Delete_account_removes_everything_and_kills_tokens()
    {
        var user = await api.RegisterAsync();
        await user.SaveProfileAsync();
        await user.AddEntryAsync(new DateOnly(2026, 1, 1));

        var wrong = await user.Http.PostAsJsonAsync("/api/auth/delete-account", new { password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
        var missing = await user.Http.PostAsJsonAsync("/api/auth/delete-account", new { });
        Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);

        var deleted = await user.Http.PostAsJsonAsync("/api/auth/delete-account", new { password = TestUser.Password });
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await user.Http.GetAsync("/api/auth/me")).StatusCode);
        var client = api.Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = user.Auth.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = TestUser.Password })).StatusCode);
    }

    [Theory]
    [InlineData("/api/auth/me")]
    [InlineData("/api/profile")]
    [InlineData("/api/settings")]
    [InlineData("/api/diary/2026-01-01")]
    [InlineData("/api/recipes")]
    [InlineData("/api/favorites")]
    [InlineData("/api/weight")]
    [InlineData("/api/sync/pull")]
    [InlineData("/api/stats?from=2026-01-01&to=2026-01-02")]
    [InlineData("/api/export?from=2026-01-01&to=2026-01-02")]
    public async Task Personal_endpoints_require_token(string url)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous().GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Forged_token_is_rejected()
    {
        var client = api.Anonymous();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            "eyJhbGciOiJub25lIn0.eyJzdWIiOiIwMDAwMDAwMC0wMDAwLTAwMDAtMDAwMC0wMDAwMDAwMDAwMDAifQ.");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }
}
