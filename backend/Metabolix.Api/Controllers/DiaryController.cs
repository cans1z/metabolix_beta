using Metabolix.Api.Data;
using Metabolix.Api.Dtos;
using Metabolix.Api.Models;
using Metabolix.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Metabolix.Api.Controllers;

[Route("api/diary")]
public class DiaryController(AppDbContext db) : ApiControllerBase
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Всё про один день: записи, итоги, норма, остаток и вода.</summary>
    [HttpGet("{date}")]
    public async Task<DayDto> Day(DateOnly date)
    {
        var entries = await db.DiaryEntries.AsNoTracking()
            .Where(e => e.UserId == UserId && e.Date == date && !e.IsDeleted)
            .OrderBy(e => e.UpdatedAt)
            .ToListAsync();
        var total = Nutrition.Sum(entries.Select(e => Nutrition.ForPortion(e.Food, e.Grams)));

        var profile = await db.Profiles.FindAsync(UserId);
        var settings = await db.Settings.FindAsync(UserId) ?? new UserSettings();
        var targets = profile == null ? null : Nutrition.Targets(profile, settings, Today);

        var water = await db.WaterRecords.AsNoTracking()
            .Where(w => w.UserId == UserId && w.Date == date && !w.IsDeleted)
            .Select(w => w.Ml)
            .FirstOrDefaultAsync();

        return new DayDto(date, entries.Select(e => e.ToDto()).ToList(), total, targets,
            targets == null ? null : targets.Kcal - total.Kcal, water, settings.WaterGoal);
    }

    [HttpPost]
    public async Task<ActionResult<EntryDto>> Create(CreateEntryRequest r)
    {
        if (r.Id is { } id)
        {
            var existing = await db.DiaryEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
            if (existing != null)
            {
                // Клиент повторил запрос после обрыва сети — отвечаем той же записью вместо дубля.
                return existing.UserId == UserId && !existing.IsDeleted
                    ? existing.ToDto()
                    : Error(409, "Запись с таким id уже есть");
            }
        }

        var now = DateTime.UtcNow;
        var entry = new DiaryEntry
        {
            Id = r.Id ?? Guid.NewGuid(),
            UserId = UserId,
            Date = r.Date,
            Meal = r.Meal,
            Food = r.Food.ToSnapshot(),
            Grams = r.Grams,
            UpdatedAt = now,
            SyncedAt = now,
        };
        db.DiaryEntries.Add(entry);
        await db.SaveChangesAsync();
        return StatusCode(201, entry.ToDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EntryDto>> Update(Guid id, UpdateEntryRequest r)
    {
        var entry = await db.DiaryEntries.FirstOrDefaultAsync(e => e.Id == id && e.UserId == UserId && !e.IsDeleted);
        if (entry == null) return Error(404, "Запись не найдена");

        entry.Grams = r.Grams;
        entry.Meal = r.Meal ?? entry.Meal;
        entry.Date = r.Date ?? entry.Date;
        entry.UpdatedAt = entry.SyncedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return entry.ToDto();
    }

    /// <summary>Мягкое удаление: другие устройства узнают о нём при синхронизации.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entry = await db.DiaryEntries.FirstOrDefaultAsync(e => e.Id == id && e.UserId == UserId && !e.IsDeleted);
        if (entry == null) return Error(404, "Запись не найдена");

        entry.IsDeleted = true;
        entry.UpdatedAt = entry.SyncedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Копирует приём пищи на другую дату.</summary>
    [HttpPost("copy")]
    public async Task<ActionResult<List<EntryDto>>> Copy(CopyMealRequest r)
    {
        if (r.From == r.To) return Error(400, "Выберите другую дату");

        var now = DateTime.UtcNow;
        var source = await db.DiaryEntries.AsNoTracking()
            .Where(e => e.UserId == UserId && e.Date == r.From && e.Meal == r.Meal && !e.IsDeleted)
            .ToListAsync();
        var copies = source.Select(e => new DiaryEntry
        {
            UserId = UserId,
            Date = r.To,
            Meal = e.Meal,
            Food = e.Food,
            Grams = e.Grams,
            UpdatedAt = now,
            SyncedAt = now,
        }).ToList();

        db.DiaryEntries.AddRange(copies);
        await db.SaveChangesAsync();
        return copies.Select(e => e.ToDto()).ToList();
    }
}
