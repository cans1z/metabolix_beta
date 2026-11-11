using System.Text.RegularExpressions;
using Metabolix.Api.Data;
using Metabolix.Api.Dtos;
using Metabolix.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Metabolix.Api.Services;

public partial class FoodSearch(AppDbContext db, RedisStore store)
{
    private const double TypoThreshold = 0.4;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public static string Normalize(string text) => text.Trim().ToLowerInvariant().Replace('ё', 'е');

    /// <summary>ищем каждое слово по началу</summary>
    public static string? ToTsQuery(string normalized)
    {
        var words = WordRegex().Matches(normalized).Select(m => m.Value + ":*").ToList();
        return words.Count == 0 ? null : string.Join(" & ", words);
    }

    public async Task<PageDto<FoodDto>> SearchAsync(string? query, int page, int pageSize, Guid? userId)
    {
        var normalized = Normalize(query ?? "");

        // общие и личные продукты листаем вместе
        if (userId is { } ownerId)
            return await QueryAsync(db.Foods.Where(f => f.OwnerId == null || f.OwnerId == ownerId),
                normalized, page, pageSize);

        var key = $"search:{normalized}:{page}:{pageSize}";
        var result = await store.GetJsonAsync<PageDto<FoodDto>>(key);
        if (result == null)
        {
            result = await QueryAsync(db.Foods.Where(f => f.OwnerId == null), normalized, page, pageSize);
            await store.SetJsonAsync(key, result, CacheTtl);
        }

        return result;
    }

    private static async Task<PageDto<FoodDto>> QueryAsync(IQueryable<Food> source, string normalized, int page, int pageSize)
    {
        var foods = source.AsNoTracking();
        var tsQuery = ToTsQuery(normalized);

        var matching = foods;
        if (tsQuery != null)
        {
            // русский словарь учитывает падежи а триграммы помогают с опечатками
            matching = foods.Where(f => f.SearchVector.Matches(EF.Functions.ToTsQuery("russian", tsQuery))
                                        || EF.Functions.TrigramsWordSimilarity(normalized, f.SearchName) > TypoThreshold);
        }

        var ordered = matching.OrderByDescending(f => f.OwnerId != null);
        if (tsQuery != null)
            ordered = ordered.ThenByDescending(f => f.SearchVector.Rank(EF.Functions.ToTsQuery("russian", tsQuery)))
                .ThenByDescending(f => EF.Functions.TrigramsWordSimilarity(normalized, f.SearchName));

        var total = await matching.CountAsync();
        var items = await ordered.ThenBy(f => f.Name).ThenBy(f => f.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PageDto<FoodDto>(items.Select(f => f.ToDto()).ToList(), page, pageSize, total);
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRegex();
}
