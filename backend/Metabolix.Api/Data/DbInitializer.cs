using Microsoft.EntityFrameworkCore;

namespace Metabolix.Api.Data;

public static class DbInitializer
{
    /// <summary>При старте доводит схему БД до актуальной и заполняет пустой справочник.</summary>
    public static async Task InitAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();

        if (!await db.Foods.AnyAsync(f => f.OwnerId == null))
        {
            db.Foods.AddRange(SeedData.Foods());
            await db.SaveChangesAsync();
        }
    }
}
