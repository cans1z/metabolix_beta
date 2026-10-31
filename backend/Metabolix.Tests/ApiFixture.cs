using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Metabolix.Api.Dtos;
using Metabolix.Api.Models;
using Metabolix.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Metabolix.Tests;

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFixture>;

/// <summary>Настоящие Postgres и Redis в контейнерах; подменяются только внешние сервисы.</summary>
public class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
    private readonly RedisContainer _redis = new RedisBuilder().WithImage("redis:7.2-alpine").Build();
    private WebApplicationFactory<Program> _factory = null!;

    public FakeEmail Email { get; } = new();
    public FakeBarcodes Barcodes { get; } = new();
    public FakeGoogle Google { get; } = new();
    public string UploadsPath { get; } = Path.Combine(Path.GetTempPath(), "metabolix-tests-" + Guid.NewGuid().ToString("N"));

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        // Program.cs читает настройки ещё до Build(), поэтому передаём их через окружение.
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", _redis.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Key", "test-key-test-key-test-key-test-key-0123456789");
        Environment.SetEnvironmentVariable("Uploads__Path", UploadsPath);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
            host.ConfigureServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.RemoveAll<IBarcodeLookup>();
                services.RemoveAll<IGoogleTokenValidator>();
                services.AddSingleton<IEmailSender>(Email);
                services.AddSingleton<IBarcodeLookup>(Barcodes);
                services.AddSingleton<IGoogleTokenValidator>(Google);
            }));
        _factory.CreateClient().Dispose(); // прогоняет миграции и заполнение справочника
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());
        if (Directory.Exists(UploadsPath)) Directory.Delete(UploadsPath, true);
    }

    public HttpClient Anonymous() => _factory.CreateClient();

    public async Task<TestUser> RegisterAsync()
    {
        var email = $"user{Guid.NewGuid():N}@test.ru";
        var client = Anonymous();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email, password = TestUser.Password, name = "Тест", acceptedTerms = true });
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return new TestUser(client, email, auth);
    }
}

public record TestUser(HttpClient Http, string Email, AuthResponse Auth)
{
    public const string Password = "secret123";

    public async Task<T> GetAsync<T>(string url)
    {
        var response = await Http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(ApiFixture.Json))!;
    }

    /// <summary>Анкета: мужчина 30 лет, 180 см, 80 кг, поддержание, активность 1,2.</summary>
    public async Task SaveProfileAsync(double weight = 80)
    {
        var birth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-30).AddDays(-1);
        var response = await Http.PutAsJsonAsync("/api/profile", new
        {
            sex = "male",
            birthDate = birth,
            height = 180,
            weight,
            targetWeight = 75,
            activity = 1.2,
            goal = "maintain",
        });
        response.EnsureSuccessStatusCode();
    }

    public async Task<EntryDto> AddEntryAsync(DateOnly date, string meal = "breakfast", double grams = 200, Guid? id = null, string name = "Гречка")
    {
        var response = await Http.PostAsJsonAsync("/api/diary", new { id, date, meal, grams, food = Food(name) });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EntryDto>(ApiFixture.Json))!;
    }

    public static object Food(string name = "Гречка", string? id = null) =>
        new { id, name, kcal = 100, protein = 10, fat = 5, carbs = 20, unit = "g" };
}

public class FakeEmail : IEmailSender
{
    private readonly Dictionary<string, string> _last = [];

    public Task SendAsync(string to, string subject, string body)
    {
        lock (_last) _last[to] = body;
        return Task.CompletedTask;
    }

    public string CodeFor(string email)
    {
        lock (_last) return System.Text.RegularExpressions.Regex.Match(_last[email], @"\d{6}").Value;
    }
}

public class FakeBarcodes : IBarcodeLookup
{
    public int Calls;
    public Func<string, BarcodeResult> Handler { get; set; } = _ => new BarcodeResult(null);

    public Task<BarcodeResult> FindAsync(string barcode, CancellationToken ct = default)
    {
        Interlocked.Increment(ref Calls);
        return Task.FromResult(Handler(barcode));
    }
}

public class FakeGoogle : IGoogleTokenValidator
{
    public bool IsConfigured => true;

    /// <summary>Формат токена в тестах: "subject|email|verified".</summary>
    public Task<GoogleUser?> ValidateAsync(string idToken)
    {
        var parts = idToken.Split('|');
        return Task.FromResult(parts.Length == 3 ? new GoogleUser(parts[0], parts[1], "Гугл", parts[2] == "true") : null);
    }
}
