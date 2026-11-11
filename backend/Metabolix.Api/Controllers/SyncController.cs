using Metabolix.Api.Data;
using Metabolix.Api.Dtos;
using Metabolix.Api.Models;
using Metabolix.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Metabolix.Api.Controllers;

/// <summary>сначала принимаем изменения клиента затем отдаём серверные</summary>
[Route("api/sync")]
public class SyncController(AppDbContext db, WeightService weights) : ApiControllerBase
{
    /// <summary>повторяем последние изменения на случай незавершённых транзакций</summary>
    private static readonly TimeSpan CursorOverlap = TimeSpan.FromSeconds(5);

    [HttpPost("push")]
    public async Task<SyncPushResult> Push(SyncPushRequest r)
    {
        var now = DateTime.UtcNow;
        var applied = 0;
        var skipped = 0;
        var rejected = new List<Guid>();

        var ids = r.Diary.Select(x => x.Id).ToList();
        // проверяем владельца даже у записей с чужим id
        var entries = await db.DiaryEntries.Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id);
        foreach (var item in r.Diary)
        {
            var updatedAt = ClampToNow(item.UpdatedAt, now);
            if (entries.TryGetValue(item.Id, out var entry))
            {
                if (entry.UserId != UserId)
                {
                    rejected.Add(item.Id);
                    continue;
                }
                if (updatedAt <= entry.UpdatedAt)
                {
                    skipped++;
                    continue;
                }
            }
            else
            {
                entry = new DiaryEntry { Id = item.Id, UserId = UserId };
                entries[item.Id] = entry;
                db.DiaryEntries.Add(entry);
            }

            entry.Date = item.Date;
            entry.Meal = item.Meal;
            entry.Food = item.Food.ToSnapshot();
            entry.Grams = item.Grams;
            entry.IsDeleted = item.IsDeleted;
            entry.UpdatedAt = updatedAt;
            entry.SyncedAt = now;
            applied++;
        }

        var weightRecords = await db.WeightRecords.Where(x => x.UserId == UserId).ToDictionaryAsync(x => x.Date);
        foreach (var item in r.Weights)
        {
            var record = Upsert(weightRecords, item.Date, item.UpdatedAt, now, db.WeightRecords, ref applied, ref skipped);
            if (record == null) continue;
            record.Kg = item.Kg;
            record.IsDeleted = item.IsDeleted;
        }

        var waterRecords = await db.WaterRecords.Where(x => x.UserId == UserId).ToDictionaryAsync(x => x.Date);
        foreach (var item in r.Water)
        {
            var record = Upsert(waterRecords, item.Date, item.UpdatedAt, now, db.WaterRecords, ref applied, ref skipped);
            if (record == null) continue;
            record.Ml = item.Ml;
            record.IsDeleted = item.IsDeleted;
        }

        await db.SaveChangesAsync();
        if (r.Weights.Count > 0) await weights.RefreshProfileWeightAsync(UserId);
        return new SyncPushResult(applied, skipped, rejected);
    }

    /// <summary>без курсора отдаём всю историю кроме удалённых записей</summary>
    [HttpGet("pull")]
    public async Task<SyncPullResult> Pull(DateTime? since)
    {
        var cursor = DateTime.UtcNow - CursorOverlap;
        var from = since?.ToUniversalTime();

        var diary = await Changes(db.DiaryEntries.Where(x => x.UserId == UserId), from).ToListAsync();
        var weightItems = await Changes(db.WeightRecords.Where(x => x.UserId == UserId), from).ToListAsync();
        var water = await Changes(db.WaterRecords.Where(x => x.UserId == UserId), from).ToListAsync();

        return new SyncPullResult(
            diary.Select(x => x.ToSync()).ToList(),
            weightItems.Select(x => x.ToSync()).ToList(),
            water.Select(x => x.ToSync()).ToList(),
            cursor);
    }

    private static IQueryable<T> Changes<T>(IQueryable<T> source, DateTime? since) where T : class, ISyncable =>
        since == null
            ? source.AsNoTracking().Where(x => !x.IsDeleted)
            : source.AsNoTracking().Where(x => x.SyncedAt > since);

    /// <summary>пропускаем правку если на сервере версия новее</summary>
    private TRecord? Upsert<TRecord>(Dictionary<DateOnly, TRecord> records, DateOnly date, DateTime clientTime, DateTime now,
        DbSet<TRecord> set, ref int applied, ref int skipped) where TRecord : class, ISyncable, new()
    {
        var updatedAt = ClampToNow(clientTime, now);
        if (records.TryGetValue(date, out var record))
        {
            if (updatedAt <= record.UpdatedAt)
            {
                skipped++;
                return null;
            }
        }
        else
        {
            record = new TRecord();
            switch (record)
            {
                case WeightRecord w:
                    w.UserId = UserId;
                    w.Date = date;
                    break;
                case WaterRecord w:
                    w.UserId = UserId;
                    w.Date = date;
                    break;
            }
            records[date] = record;
            set.Add(record);
        }

        record.UpdatedAt = updatedAt;
        record.SyncedAt = now;
        applied++;
        return record;
    }

    // ограничиваем время серверным чтобы сбитые часы не блокировали новые правки
    // округляем до точности постгреса чтобы повторный запрос не считался новой правкой
    private static DateTime ClampToNow(DateTime clientTime, DateTime now)
    {
        var utc = clientTime.ToUniversalTime();
        var time = utc > now ? now : utc;
        return new DateTime(time.Ticks - time.Ticks % 10, DateTimeKind.Utc);
    }
}
