using System.ComponentModel.DataAnnotations;
using Metabolix.Api.Data;
using Metabolix.Api.Dtos;
using Metabolix.Api.Models;
using Metabolix.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Metabolix.Api.Controllers;

[Route("api/foods")]
public class FoodsController(
    AppDbContext db,
    FoodSearch search,
    IBarcodeLookup barcodes,
    RedisStore store,
    FileStorage files) : ApiControllerBase
{
    private const string BarcodeRoute = @"barcode/{code:regex(^\d{{8,14}}$)}";
    private static readonly TimeSpan FoundTtl = TimeSpan.FromDays(1);
    private static readonly TimeSpan NotFoundTtl = TimeSpan.FromHours(1);

    /// <summary>Что лежит в кэше по штрихкоду. Food = null значит «точно нет», чтобы не спрашивать внешнюю базу снова.</summary>
    private record CachedBarcode(FoodDto? Food);

    /// <summary>Гостю справочник тоже доступен (п. 5.1.1 ТЗ).</summary>
    [AllowAnonymous]
    [HttpGet]
    public Task<PageDto<FoodDto>> Search(string? q, [Range(1, 1000)] int page = 1, [Range(1, 100)] int pageSize = 20) =>
        search.SearchAsync(q, page, pageSize, User.UserIdOrNull());

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FoodDto>> Get(Guid id)
    {
        var userId = User.UserIdOrNull();
        var food = await db.Foods.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id && (f.OwnerId == null || f.OwnerId == userId));
        return food == null ? Error(404, "Продукт не найден") : food.ToDto();
    }

    /// <summary>Порядок: личная карточка → Redis → общий справочник → Open Food Facts.</summary>
    [AllowAnonymous]
    [HttpGet(BarcodeRoute)]
    public async Task<ActionResult<FoodDto>> ByBarcode(string code, CancellationToken ct)
    {
        const string notFound = "Продукт не найден, можно создать карточку вручную";

        if (User.UserIdOrNull() is { } userId)
        {
            var own = await db.Foods.AsNoTracking().FirstOrDefaultAsync(f => f.OwnerId == userId && f.Barcode == code, ct);
            if (own != null) return own.ToDto();
        }

        var key = $"barcode:{code}";
        var cached = await store.GetJsonAsync<CachedBarcode>(key);
        if (cached != null) return cached.Food == null ? Error(404, notFound) : cached.Food;

        var food = await db.Foods.AsNoTracking().FirstOrDefaultAsync(f => f.OwnerId == null && f.Barcode == code, ct);
        if (food == null)
        {
            // Метод публичный и ходит во внешний сервис, поэтому ограничиваем частоту.
            if (!await store.TryHitAsync($"barcode-ip:{ClientIp}", 30, TimeSpan.FromMinutes(1)))
                return Error(429, "Слишком много запросов, подождите минуту");

            var result = await barcodes.FindAsync(code, ct);
            if (result.Temporary) return Error(503, "Внешняя база продуктов недоступна, попробуйте позже");

            food = result.Food;
            if (food != null)
            {
                db.Foods.Add(food);
                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                {
                    // Такой же штрихкод одновременно сохранил другой запрос — берём его запись.
                    db.Entry(food).State = EntityState.Detached;
                    food = await db.Foods.AsNoTracking().FirstAsync(f => f.OwnerId == null && f.Barcode == code, ct);
                }
            }
        }

        await store.SetJsonAsync(key, new CachedBarcode(food?.ToDto()), food == null ? NotFoundTtl : FoundTtl);
        return food == null ? Error(404, notFound) : food.ToDto();
    }

    /// <summary>Недавние — последние разные продукты из дневника, отдельно не хранятся.</summary>
    [HttpGet("recent")]
    public async Task<List<FoodDto>> Recent()
    {
        var entries = await db.DiaryEntries.AsNoTracking()
            .Where(e => e.UserId == UserId && !e.IsDeleted)
            .OrderByDescending(e => e.UpdatedAt)
            .Take(300)
            .Select(e => e.Food)
            .ToListAsync();
        return entries.DistinctBy(f => f.FoodRef ?? f.Name).Take(30).Select(f => f.ToDto()).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<FoodDto>> Create(FoodDto r)
    {
        var food = new Food { Name = r.Name.Trim(), OwnerId = UserId };
        Apply(food, r);
        db.Foods.Add(food);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = food.Id }, food.ToDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FoodDto>> Update(Guid id, FoodDto r)
    {
        var food = await db.Foods.FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == UserId);
        if (food == null) return Error(404, "Продукт не найден");
        Apply(food, r);
        await db.SaveChangesAsync();
        return food.ToDto();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var food = await db.Foods.FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == UserId);
        if (food == null) return Error(404, "Продукт не найден");
        db.Foods.Remove(food);
        await db.SaveChangesAsync();
        files.Delete(food.ImageUrl);
        return NoContent();
    }

    [HttpPost("{id:guid}/image")]
    [RequestSizeLimit(FileStorage.MaxBytes + 64 * 1024)]
    public async Task<ActionResult<FoodDto>> Image(Guid id, IFormFile file)
    {
        var food = await db.Foods.FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == UserId);
        if (food == null) return Error(404, "Продукт не найден");

        try
        {
            var url = await files.SaveImageAsync(file);
            files.Delete(food.ImageUrl);
            food.ImageUrl = url;
        }
        catch (InvalidImageException e)
        {
            return Error(400, e.Message);
        }
        await db.SaveChangesAsync();
        return food.ToDto();
    }

    private static void Apply(Food food, FoodDto r)
    {
        food.Name = r.Name.Trim();
        food.SearchName = FoodSearch.Normalize(r.Name);
        food.Kcal = r.Kcal;
        food.Protein = r.Protein;
        food.Fat = r.Fat;
        food.Carbs = r.Carbs;
        food.Unit = r.Unit;
        food.Portion = r.Portion;
        food.Barcode = r.Barcode;
    }
}

[Route("api/favorites")]
public class FavoritesController(AppDbContext db) : ApiControllerBase
{
    [HttpGet]
    public async Task<FavoritesDto> Get()
    {
        var foods = await db.FavoriteFoods.AsNoTracking()
            .Where(x => x.UserId == UserId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.Food)
            .ToListAsync();
        var recipes = await db.FavoriteRecipes.AsNoTracking()
            .Where(x => x.UserId == UserId)
            .OrderByDescending(x => x.CreatedAt)
            .Include(x => x.Recipe).ThenInclude(r => r.Items)
            .ToListAsync();
        return new FavoritesDto(foods.Select(f => f.ToDto()).ToList(), recipes.Select(x => x.Recipe.ToDto()).ToList());
    }

    [HttpPut("foods/{id:guid}")]
    public async Task<IActionResult> AddFood(Guid id)
    {
        if (!await db.Foods.AnyAsync(f => f.Id == id && (f.OwnerId == null || f.OwnerId == UserId)))
            return Error(404, "Продукт не найден");
        if (await db.FavoriteFoods.FindAsync(UserId, id) == null)
        {
            db.FavoriteFoods.Add(new FavoriteFood { UserId = UserId, FoodId = id });
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    [HttpDelete("foods/{id:guid}")]
    public async Task<IActionResult> RemoveFood(Guid id)
    {
        await db.FavoriteFoods.Where(x => x.UserId == UserId && x.FoodId == id).ExecuteDeleteAsync();
        return NoContent();
    }

    [HttpPut("recipes/{id:guid}")]
    public async Task<IActionResult> AddRecipe(Guid id)
    {
        if (!await db.Recipes.AnyAsync(x => x.Id == id && x.UserId == UserId))
            return Error(404, "Блюдо не найдено");
        if (await db.FavoriteRecipes.FindAsync(UserId, id) == null)
        {
            db.FavoriteRecipes.Add(new FavoriteRecipe { UserId = UserId, RecipeId = id });
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    [HttpDelete("recipes/{id:guid}")]
    public async Task<IActionResult> RemoveRecipe(Guid id)
    {
        await db.FavoriteRecipes.Where(x => x.UserId == UserId && x.RecipeId == id).ExecuteDeleteAsync();
        return NoContent();
    }
}
