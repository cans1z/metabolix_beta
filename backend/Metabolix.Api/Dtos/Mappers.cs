using Metabolix.Api.Models;
using Metabolix.Api.Services;

namespace Metabolix.Api.Dtos;

public static class Mappers
{
    public static FoodDto ToDto(this Food f) => new()
    {
        Id = f.Id.ToString(),
        Name = f.Name,
        Kcal = f.Kcal,
        Protein = f.Protein,
        Fat = f.Fat,
        Carbs = f.Carbs,
        Unit = f.Unit,
        Portion = f.Portion,
        Barcode = f.Barcode,
        ImageUrl = f.ImageUrl,
        Custom = f.OwnerId != null,
    };

    public static FoodDto ToDto(this FoodSnapshot f) => new()
    {
        Id = f.FoodRef,
        Name = f.Name,
        Kcal = f.Kcal,
        Protein = f.Protein,
        Fat = f.Fat,
        Carbs = f.Carbs,
        Unit = f.Unit,
        Portion = f.Portion,
        Barcode = f.Barcode,
        Custom = f.Custom,
    };

    public static FoodSnapshot ToSnapshot(this FoodDto f) => new()
    {
        FoodRef = f.Id,
        Name = f.Name,
        Kcal = f.Kcal,
        Protein = f.Protein,
        Fat = f.Fat,
        Carbs = f.Carbs,
        Unit = f.Unit,
        Portion = f.Portion,
        Barcode = f.Barcode,
        Custom = f.Custom,
    };

    public static EntryDto ToDto(this DiaryEntry e) =>
        new(e.Id, e.Date, e.Meal, e.Food.ToDto(), e.Grams, Nutrition.ForPortion(e.Food, e.Grams), e.UpdatedAt);

    public static RecipeDto ToDto(this Recipe r)
    {
        var items = r.Items.OrderBy(i => i.Position).ToList();
        var total = Nutrition.Sum(items.Select(i => Nutrition.ForPortion(i.Food, i.Grams)));
        var weight = items.Sum(i => i.Grams);
        double Per100(double v) => weight > 0 ? Math.Round(v / weight * 100, 1) : 0;

        // Блюдо как продукт: удельная ценность на 100 г, порция — всё блюдо.
        var asFood = new FoodDto
        {
            Id = "recipe:" + r.Id,
            Name = r.Name,
            Kcal = Per100(total.Kcal),
            Protein = Per100(total.Protein),
            Fat = Per100(total.Fat),
            Carbs = Per100(total.Carbs),
            Unit = FoodUnit.G,
            Portion = weight,
            ImageUrl = r.ImageUrl,
            Custom = true,
        };

        return new RecipeDto(r.Id, r.Name, r.ImageUrl,
            items.Select(i => new RecipeItemDto { Food = i.Food.ToDto(), Grams = i.Grams }).ToList(),
            weight, total, asFood);
    }

    public static SyncDiaryItem ToSync(this DiaryEntry e) => new()
    {
        Id = e.Id,
        Date = e.Date,
        Meal = e.Meal,
        Food = e.Food.ToDto(),
        Grams = e.Grams,
        UpdatedAt = e.UpdatedAt,
        IsDeleted = e.IsDeleted,
    };

    public static SyncWeightItem ToSync(this WeightRecord w) => new()
    {
        Date = w.Date,
        Kg = w.Kg,
        UpdatedAt = w.UpdatedAt,
        IsDeleted = w.IsDeleted,
    };

    public static SyncWaterItem ToSync(this WaterRecord w) => new()
    {
        Date = w.Date,
        Ml = w.Ml,
        UpdatedAt = w.UpdatedAt,
        IsDeleted = w.IsDeleted,
    };
}
