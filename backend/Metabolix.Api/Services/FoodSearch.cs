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

    /// <summary>"куриная гру" → "куриная:* &amp; гру:*": каждое слово может быть началом слова в названии.</summary>
    public static string? ToTsQuery(string normalized)
    {
        var words = WordRegex().Matches(normalized).Select(m => m.Value + ":*").ToList();
        return words.Count == 0 ? null : string.Join(" & ", words);
    }

    public async Task<PageDto<FoodDto>> SearchAsync(string? query, int page, int pageSize, Guid? userId)
    {
        var normalized = Normalize(query ?? "");

        // В кэше только общий справочник: он одинаков для всех. Личные карточки читаем из базы.
        var key = $"search:{normalized}:{page}:{pageSize}";
        var result = await store.GetJsonAsync<PageDto<FoodDto>>(key);
        if (result == null)
        {
            result = await QueryAsync(db.Foods.Where(f => f.OwnerId == null), normalized, page, pageSize);
            await store.SetJsonAsync(key, result, CacheTtl);
        }

        if (userId == null || page != 1) return result;

        var own = await QueryAsync(db.Foods.Where(f => f.OwnerId == userId), normalized, 1, pageSize);
        return own.Items.Count == 0
            ? result
            : result with { Items = [.. own.Items, .. result.Items], Total = result.Total + own.Total };
    }

    private static async Task<PageDto<FoodDto>> QueryAsync(IQueryable<Food> source, string normalized, int page, int pageSize)
    {
        var foods = source.AsNoTracking();
        var tsQuery = ToTsQuery(normalized);

        IOrderedQueryable<Food> ordered;
        if (tsQuery == null)
        {
            ordered = foods.OrderBy(f => f.Name);
        }
        else
        {
            // Полнотекстовый поиск со словарём russian понимает падежи, триграммы прощают опечатки.
            ordered = foods
                .Where(f => f.SearchVector.Matches(EF.Functions.ToTsQuery("russian", tsQuery))
                            || EF.Functions.TrigramsWordSimilarity(normalized, f.SearchName) > TypoThreshold)
                .OrderByDescending(f => f.SearchVector.Rank(EF.Functions.ToTsQuery("russian", tsQuery)))
                .ThenByDescending(f => EF.Functions.TrigramsWordSimilarity(normalized, f.SearchName))
                .ThenBy(f => f.Name);
        }

        var total = await ordered.CountAsync();
        var items = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PageDto<FoodDto>(items.Select(f => f.ToDto()).ToList(), page, pageSize, total);
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRegex();
}
