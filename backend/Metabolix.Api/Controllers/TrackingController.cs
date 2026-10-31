using System.Globalization;
using System.Text;
using Metabolix.Api.Data;
using Metabolix.Api.Dtos;
using Metabolix.Api.Models;
using Metabolix.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Metabolix.Api.Controllers;

/// <summary>Вес, вода, статистика за период и экспорт.</summary>
[Route("api")]
public class TrackingController(AppDbContext db, WeightService weights) : ApiControllerBase
{
    private const int MaxPeriodDays = 366;
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [HttpGet("weight")]
    public Task<List<WeightDto>> GetWeights(DateOnly? from, DateOnly? to) =>
        WeightsAsync(from ?? DateOnly.MinValue, to ?? DateOnly.MaxValue);

    [HttpPut("weight/{date}")]
    public async Task<WeightDto> SetWeight(DateOnly date, SetWeightRequest r)
    {
        await weights.SetAsync(UserId, date, r.Kg);
        return new WeightDto(date, r.Kg);
    }

    [HttpDelete("weight/{date}")]
    public async Task<IActionResult> DeleteWeight(DateOnly date)
    {
        var record = await db.WeightRecords.FindAsync(UserId, date);
        if (record == null || record.IsDeleted) return Error(404, "Запись не найдена");

        record.IsDeleted = true;
        record.UpdatedAt = record.SyncedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await weights.RefreshProfileWeightAsync(UserId);
        return NoContent();
    }

    [HttpGet("water")]
    public Task<List<WaterDto>> GetWater(DateOnly from, DateOnly to) =>
        db.WaterRecords.AsNoTracking()
            .Where(w => w.UserId == UserId && w.Date >= from && w.Date <= to && !w.IsDeleted)
            .OrderBy(w => w.Date)
            .Select(w => new WaterDto(w.Date, w.Ml))
            .ToListAsync();

    /// <summary>Абсолютное значение за день, а не прибавка: повторный запрос ничего не сломает.</summary>
    [HttpPut("water/{date}")]
    public async Task<WaterDto> SetWater(DateOnly date, SetWaterRequest r)
    {
        var record = await db.WaterRecords.FindAsync(UserId, date);
        if (record == null)
        {
            record = new WaterRecord { UserId = UserId, Date = date };
            db.WaterRecords.Add(record);
        }
        record.Ml = r.Ml;
        record.IsDeleted = false;
        record.UpdatedAt = record.SyncedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return new WaterDto(date, r.Ml);
    }

    [HttpGet("stats")]
    public async Task<ActionResult<StatsDto>> Stats(DateOnly from, DateOnly to)
    {
        if (BadPeriod(from, to) is { } error) return error;

        var logged = await db.DiaryEntries.AsNoTracking()
            .Where(e => e.UserId == UserId && e.Date >= from && e.Date <= to && !e.IsDeleted)
            .GroupBy(e => e.Date)
            .Select(g => new DayStatsDto(
                g.Key,
                g.Sum(e => e.Food.Kcal * e.Grams / 100),
                g.Sum(e => e.Food.Protein * e.Grams / 100),
                g.Sum(e => e.Food.Fat * e.Grams / 100),
                g.Sum(e => e.Food.Carbs * e.Grams / 100)))
            .ToListAsync();

        // Пустые дни тоже нужны графику, но в средние они не входят.
        var byDate = logged.ToDictionary(d => d.Date);
        var days = new List<DayStatsDto>();
        for (var d = from; d <= to; d = d.AddDays(1))
            days.Add(byDate.GetValueOrDefault(d) ?? new DayStatsDto(d, 0, 0, 0, 0));

        var count = Math.Max(logged.Count, 1);
        var average = new MacrosDto
        {
            Kcal = Math.Round(logged.Sum(d => d.Kcal) / count, 1),
            Protein = Math.Round(logged.Sum(d => d.Protein) / count, 1),
            Fat = Math.Round(logged.Sum(d => d.Fat) / count, 1),
            Carbs = Math.Round(logged.Sum(d => d.Carbs) / count, 1),
        };

        var profile = await db.Profiles.FindAsync(UserId);
        var settings = await db.Settings.FindAsync(UserId) ?? new UserSettings();
        return new StatsDto(from, to, days, average, logged.Count, days.Count,
            await WeightsAsync(from, to),
            profile == null ? null : Nutrition.Targets(profile, settings, Today),
            profile == null ? null : Nutrition.Bmi(profile.Weight, profile.Height));
    }

    /// <summary>Экспорт дневника в CSV (п. 5.1.7 ТЗ). BOM и «;» нужны, чтобы русский Excel открыл файл правильно.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(DateOnly from, DateOnly to)
    {
        if (BadPeriod(from, to) is { } error) return error;

        var entries = await db.DiaryEntries.AsNoTracking()
            .Where(e => e.UserId == UserId && e.Date >= from && e.Date <= to && !e.IsDeleted)
            .OrderBy(e => e.Date).ThenBy(e => e.Meal).ThenBy(e => e.UpdatedAt)
            .ToListAsync();

        var csv = new StringBuilder("Дата;Приём пищи;Продукт;Количество;Ккал;Белки;Жиры;Углеводы\r\n");
        foreach (var e in entries)
        {
            var m = Nutrition.ForPortion(e.Food, e.Grams);
            csv.AppendJoin(';', e.Date.ToString("yyyy-MM-dd"), MealLabel(e.Meal), CsvText(e.Food.Name),
                N(e.Grams), N(m.Kcal, 0), N(m.Protein), N(m.Fat), N(m.Carbs)).Append("\r\n");
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"metabolix_{from:yyyy-MM-dd}_{to:yyyy-MM-dd}.csv");
    }

    private Task<List<WeightDto>> WeightsAsync(DateOnly from, DateOnly to) =>
        db.WeightRecords.AsNoTracking()
            .Where(w => w.UserId == UserId && w.Date >= from && w.Date <= to && !w.IsDeleted)
            .OrderBy(w => w.Date)
            .Select(w => new WeightDto(w.Date, w.Kg))
            .ToListAsync();

    private ObjectResult? BadPeriod(DateOnly from, DateOnly to) =>
        from > to || to.DayNumber - from.DayNumber >= MaxPeriodDays
            ? Error(400, $"Период должен быть не длиннее {MaxPeriodDays} дней")
            : null;

    private static string N(double value, int digits = 1) =>
        Math.Round(value, digits).ToString(CultureInfo.InvariantCulture);

    private static string MealLabel(MealType meal) => meal switch
    {
        MealType.Breakfast => "Завтрак",
        MealType.Lunch => "Обед",
        MealType.Dinner => "Ужин",
        _ => "Перекусы",
    };

    // Excel выполняет ячейки, начинающиеся с = + - @, как формулы, поэтому экранируем.
    private static string CsvText(string value)
    {
        var safe = value.Length > 0 && "=+-@".Contains(value[0]) ? "'" + value : value;
        return '"' + safe.Replace("\"", "\"\"") + '"';
    }
}
