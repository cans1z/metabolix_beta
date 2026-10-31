using Metabolix.Api.Data;
using Metabolix.Api.Dtos;
using Metabolix.Api.Models;
using Metabolix.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Metabolix.Api.Controllers;

[Route("api/recipes")]
public class RecipesController(AppDbContext db, FileStorage files) : ApiControllerBase
{
    private IQueryable<Recipe> Mine => db.Recipes.Include(r => r.Items).Where(r => r.UserId == UserId);

    [HttpGet]
    public async Task<List<RecipeDto>> List()
    {
        var recipes = await Mine.AsNoTracking().OrderByDescending(r => r.CreatedAt).ToListAsync();
        return recipes.Select(r => r.ToDto()).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RecipeDto>> Get(Guid id)
    {
        var recipe = await Mine.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        return recipe == null ? Error(404, "Блюдо не найдено") : recipe.ToDto();
    }

    [HttpPost]
    public async Task<ActionResult<RecipeDto>> Create(RecipeRequest r)
    {
        var recipe = new Recipe { UserId = UserId, Name = r.Name.Trim(), Items = ToItems(r) };
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = recipe.Id }, recipe.ToDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RecipeDto>> Update(Guid id, RecipeRequest r)
    {
        var recipe = await Mine.FirstOrDefaultAsync(x => x.Id == id);
        if (recipe == null) return Error(404, "Блюдо не найдено");

        recipe.Name = r.Name.Trim();
        recipe.Items.Clear();
        recipe.Items.AddRange(ToItems(r));
        await db.SaveChangesAsync();
        return recipe.ToDto();
    }

    /// <summary>Записи дневника остаются: в них хранится копия блюда на момент добавления.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var recipe = await db.Recipes.FirstOrDefaultAsync(r => r.Id == id && r.UserId == UserId);
        if (recipe == null) return Error(404, "Блюдо не найдено");
        db.Recipes.Remove(recipe);
        await db.SaveChangesAsync();
        files.Delete(recipe.ImageUrl);
        return NoContent();
    }

    [HttpPost("{id:guid}/image")]
    [RequestSizeLimit(FileStorage.MaxBytes + 64 * 1024)]
    public async Task<ActionResult<RecipeDto>> Image(Guid id, IFormFile file)
    {
        var recipe = await Mine.FirstOrDefaultAsync(r => r.Id == id);
        if (recipe == null) return Error(404, "Блюдо не найдено");

        try
        {
            var url = await files.SaveImageAsync(file);
            files.Delete(recipe.ImageUrl);
            recipe.ImageUrl = url;
        }
        catch (InvalidImageException e)
        {
            return Error(400, e.Message);
        }
        await db.SaveChangesAsync();
        return recipe.ToDto();
    }

    private static List<RecipeItem> ToItems(RecipeRequest r) =>
        r.Items.Select((item, i) => new RecipeItem { Position = i, Food = item.Food.ToSnapshot(), Grams = item.Grams }).ToList();
}
