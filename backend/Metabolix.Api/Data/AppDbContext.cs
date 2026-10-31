using Metabolix.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Metabolix.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<UserSettings> Settings => Set<UserSettings>();
    public DbSet<Food> Foods => Set<Food>();
    public DbSet<FavoriteFood> FavoriteFoods => Set<FavoriteFood>();
    public DbSet<FavoriteRecipe> FavoriteRecipes => Set<FavoriteRecipe>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<DiaryEntry> DiaryEntries => Set<DiaryEntry>();
    public DbSet<WeightRecord> WeightRecords => Set<WeightRecord>();
    public DbSet<WaterRecord> WaterRecords => Set<WaterRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Расширение Postgres для поиска с опечатками.
        b.HasPostgresExtension("pg_trgm");

        b.Entity<User>(e =>
        {
            e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.GoogleId).IsUnique();
        });

        b.Entity<Profile>(e =>
        {
            e.HasKey(x => x.UserId);
            e.HasOne<User>().WithOne().HasForeignKey<Profile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Sex).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Goal).HasConversion<string>().HasMaxLength(16);
        });

        b.Entity<UserSettings>(e =>
        {
            e.HasKey(x => x.UserId);
            e.HasOne<User>().WithOne().HasForeignKey<UserSettings>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Theme).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.RemindTime).HasMaxLength(5);
        });

        b.Entity<Food>(e =>
        {
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.SearchName).HasMaxLength(200);
            e.Property(x => x.Barcode).HasMaxLength(32);
            e.Property(x => x.Unit).HasConversion<string>().HasMaxLength(8);

            e.HasGeneratedTsVectorColumn(x => x.SearchVector, "russian", x => new { x.SearchName })
                .HasIndex(x => x.SearchVector)
                .HasMethod("GIN");
            e.HasIndex(x => x.SearchName).HasMethod("GIN").HasOperators("gin_trgm_ops");

            // Один штрихкод — одна общая карточка; личные у разных пользователей могут совпадать.
            e.HasIndex(x => x.Barcode).IsUnique().HasFilter("\"OwnerId\" IS NULL AND \"Barcode\" IS NOT NULL")
                .HasDatabaseName("IX_Foods_GlobalBarcode");
            e.HasIndex(x => new { x.OwnerId, x.Barcode }).IsUnique().HasFilter("\"OwnerId\" IS NOT NULL AND \"Barcode\" IS NOT NULL")
                .HasDatabaseName("IX_Foods_OwnerBarcode");
        });

        b.Entity<FavoriteFood>(e =>
        {
            e.HasKey(x => new { x.UserId, x.FoodId });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Food).WithMany().HasForeignKey(x => x.FoodId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<FavoriteRecipe>(e =>
        {
            e.HasKey(x => new { x.UserId, x.RecipeId });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Recipe).WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Recipe>(e =>
        {
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.UserId);
            e.Property(x => x.Name).HasMaxLength(200);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RecipeItem>(e =>
        {
            // Id задаём сами: иначе EF примет новый ингредиент за существующий и попробует его обновить.
            e.Property(x => x.Id).ValueGeneratedNever();
            MapSnapshot(e.OwnsOne(x => x.Food));
        });

        b.Entity<DiaryEntry>(e =>
        {
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Meal).HasConversion<string>().HasMaxLength(16);
            MapSnapshot(e.OwnsOne(x => x.Food));
            e.HasIndex(x => new { x.UserId, x.Date });
            e.HasIndex(x => new { x.UserId, x.SyncedAt });
        });

        b.Entity<WeightRecord>(e =>
        {
            e.HasKey(x => new { x.UserId, x.Date });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.SyncedAt });
        });

        b.Entity<WaterRecord>(e =>
        {
            e.HasKey(x => new { x.UserId, x.Date });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.SyncedAt });
        });
    }

    private static void MapSnapshot<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.OwnedNavigationBuilder<T, FoodSnapshot> f)
        where T : class
    {
        f.Property(x => x.Unit).HasConversion<string>().HasMaxLength(8);
        f.Property(x => x.Name).HasMaxLength(200);
        f.Property(x => x.FoodRef).HasMaxLength(64);
        f.Property(x => x.Barcode).HasMaxLength(32);
    }
}
