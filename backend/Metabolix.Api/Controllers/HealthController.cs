using Metabolix.Api.Data;
using Metabolix.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace Metabolix.Api.Controllers;

[Route("api/health")]
public class HealthController(AppDbContext db, RedisStore store) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var postgres = await db.Database.CanConnectAsync();
        var redis = true;
        try
        {
            await store.Db.PingAsync();
        }
        catch (Exception e) when (e is RedisException or RedisTimeoutException)
        {
            redis = false;
        }
        return StatusCode(postgres && redis ? 200 : 503, new { postgres, redis });
    }
}
