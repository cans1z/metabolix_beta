using Metabolix.Api.Data;
using Metabolix.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Metabolix.Api.Services;

/// <summary>Вес хранится по датам, а текущий вес в анкете — это всегда самая поздняя запись.</summary>
public class WeightService(AppDbContext db)
{
    public async Task SetAsync(Guid userId, DateOnly date, double kg)
    {
        var now = DateTime.UtcNow;
        var record = await db.WeightRecords.FindAsync(userId, date);
        if (record == null)
        {
            record = new WeightRecord { UserId = userId, Date = date };
            db.WeightRecords.Add(record);
        }
        record.Kg = kg;
        record.IsDeleted = false;
        record.UpdatedAt = now;
        record.SyncedAt = now;
        await db.SaveChangesAsync();
        await RefreshProfileWeightAsync(userId);
    }

    public async Task RefreshProfileWeightAsync(Guid userId)
    {
        var latest = await db.WeightRecords
            .Where(w => w.UserId == userId && !w.IsDeleted)
            .OrderByDescending(w => w.Date)
            .Select(w => (double?)w.Kg)
            .FirstOrDefaultAsync();
        if (latest == null) return;

        var profile = await db.Profiles.FindAsync(userId);
        if (profile == null || profile.Weight == latest) return;
        profile.Weight = latest.Value;
        await db.SaveChangesAsync();
    }
}
