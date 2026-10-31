using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Metabolix.Api.Data;
using Metabolix.Api.Models;
using Metabolix.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using Scalar.AspNetCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
jwt.Validate(builder.Environment);
builder.Services.AddSingleton(jwt);

// База и Redis
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(config.GetConnectionString("Postgres")));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var options = ConfigurationOptions.Parse(config.GetConnectionString("Redis")!);
    options.AbortOnConnectFail = false; // приложение стартует и без Redis, а подключится, когда он поднимется
    options.AsyncTimeout = 1000; // локальный Redis отвечает за миллисекунды; 5 секунд по умолчанию — слишком долго
    return ConnectionMultiplexer.Connect(options);
});

// Наши сервисы
builder.Services.AddSingleton<RedisStore>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<FileStorage>();
builder.Services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<FoodSearch>();
builder.Services.AddScoped<WeightService>();
if (string.IsNullOrEmpty(config["Smtp:Host"]))
    builder.Services.AddSingleton<IEmailSender, LogEmailSender>();
else
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddHttpClient<IBarcodeLookup, OpenFoodFactsLookup>(http =>
{
    http.BaseAddress = new Uri("https://world.openfoodfacts.org/");
    http.Timeout = TimeSpan.FromSeconds(4);
    http.DefaultRequestHeaders.UserAgent.ParseAdd("Metabolix/0.1 (student project)");
});

// Проверка токена на каждом запросе
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = jwt.SigningKey,
        ClockSkew = TimeSpan.FromSeconds(30),
    };
    o.Events = new JwtBearerEvents
    {
        // Токен удалённого пользователя ещё до 15 минут выглядит валидным — отсекаем его здесь.
        OnTokenValidated = async ctx =>
        {
            var id = ctx.Principal!.UserIdOrNull();
            var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            if (id == null || !await db.Users.AnyAsync(u => u.Id == id))
                ctx.Fail("Пользователь удалён");
        },
    };
});
builder.Services.AddAuthorization();

builder.Services.AddControllers().AddJsonOptions(o =>
    // enum в JSON строкой: "breakfast", а не 0. Так же, как у клиентов.
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)));
builder.Services.AddProblemDetails();

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(config.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddOpenApi(o => o.AddDocumentTransformer((doc, _, _) =>
{
    doc.Components ??= new OpenApiComponents();
    doc.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
    };
    doc.SecurityRequirements.Add(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
    });
    return Task.CompletedTask;
}));

var app = builder.Build();

await DbInitializer.InitAsync(app.Services);

// За nginx настоящий IP клиента приходит в заголовке. Без прокси заголовку верить нельзя: его подделают и обойдут лимиты.
if (config.GetValue<bool>("Proxy:Trust"))
{
    var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto };
    forwarded.KnownNetworks.Clear();
    forwarded.KnownProxies.Clear();
    app.UseForwardedHeaders(forwarded);
}

app.UseExceptionHandler(errors => errors.Run(async ctx =>
{
    var error = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, detail) = error switch
    {
        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }
            => (409, "Такая запись уже существует"),
        RedisException or RedisTimeoutException
            => (503, "Сервис временно недоступен, попробуйте позже"),
        _ => (500, "Внутренняя ошибка сервера"),
    };
    ctx.Response.StatusCode = status;
    await ctx.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
    {
        HttpContext = ctx,
        ProblemDetails = { Status = status, Detail = detail },
    });
}));

if (app.Environment.IsDevelopment())
{
    // Документация и «песочница» запросов: /scalar/v1
    app.MapOpenApi();
    app.MapScalarApiReference();
}

var storage = app.Services.GetRequiredService<FileStorage>();
Directory.CreateDirectory(storage.Root);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storage.Root),
    RequestPath = FileStorage.UrlPrefix.TrimEnd('/'),
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
