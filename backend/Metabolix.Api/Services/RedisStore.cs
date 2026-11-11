using System.Text.Json;
using StackExchange.Redis;

namespace Metabolix.Api.Services;

/// <summary>при сбое кэша поиск читает из базы а ограничения запросов пропускаются</summary>
public class RedisStore(IConnectionMultiplexer redis, ILogger<RedisStore> log)
{
    /// <summary>для токенов и кодов восстановления соединение с редисом обязательно</summary>
    public IDatabase Db => redis.IsConnected
        ? redis.GetDatabase()
        : throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis недоступен");

    public async Task<T?> GetJsonAsync<T>(string key) where T : class
    {
        // при обрыве соединения сразу читаем из базы
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

    /// <summary>считаем попытки за заданное время</summary>
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
