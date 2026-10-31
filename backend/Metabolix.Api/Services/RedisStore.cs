using System.Text.Json;
using StackExchange.Redis;

namespace Metabolix.Api.Services;

/// <summary>
/// Обёртка над Redis для кэша и счётчиков. Если Redis недоступен, поиск идёт в базу напрямую,
/// а лимиты пропускают запрос: падение кэша не должно класть всё приложение.
/// </summary>
public class RedisStore(IConnectionMultiplexer redis, ILogger<RedisStore> log)
{
    /// <summary>Для данных, без которых не обойтись (токены, коды). Без соединения сразу ошибка, а клиент получает 503.</summary>
    public IDatabase Db => redis.IsConnected
        ? redis.GetDatabase()
        : throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis недоступен");

    public async Task<T?> GetJsonAsync<T>(string key) where T : class
    {
        // Без соединения не ждём таймаута: поиск должен укладываться в 2 секунды (п. 5.2 ТЗ).
        if (!redis.IsConnected) return null;
        try
        {
            var value = await Db.StringGetAsync(key);
            return value.IsNull ? null : JsonSerializer.Deserialize<T>((string)value!);
        }
        catch (Exception e) when (e is RedisException or RedisTimeoutException)
        {
            log.LogWarning(e, "Redis недоступен, читаем без кэша");
            return null;
        }
    }

    public async Task SetJsonAsync<T>(string key, T value, TimeSpan ttl)
    {
        if (!redis.IsConnected) return;
        try
        {
            await Db.StringSetAsync(key, JsonSerializer.Serialize(value), ttl);
        }
        catch (Exception e) when (e is RedisException or RedisTimeoutException)
        {
            log.LogWarning(e, "Redis недоступен, значение не закэшировано");
        }
    }

    /// <summary>Считает попытку в окне времени. false — лимит исчерпан.</summary>
    public async Task<bool> TryHitAsync(string key, int limit, TimeSpan window)
    {
        if (!redis.IsConnected) return true;
        try
        {
            var count = await Db.StringIncrementAsync(key);
            if (count == 1) await Db.KeyExpireAsync(key, window);
            return count <= limit;
        }
        catch (Exception e) when (e is RedisException or RedisTimeoutException)
        {
            log.LogWarning(e, "Redis недоступен, лимит не проверен");
            return true;
        }
    }
}
