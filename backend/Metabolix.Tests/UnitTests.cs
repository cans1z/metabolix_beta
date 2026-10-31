using System.Text.Json;
using Metabolix.Api.Models;
using Metabolix.Api.Services;

namespace Metabolix.Tests;

public class NutritionUnitTests
{
    private static readonly DateOnly Today = new(2026, 6, 15);

    private static Profile Person(Sex sex, Goal goal) => new()
    {
        Sex = sex,
        BirthDate = new DateOnly(1996, 6, 15),
        Height = 170,
        Weight = 60,
        Activity = 1.55,
        Goal = goal,
    };

    [Fact]
    public void Age_counts_birthday()
    {
        Assert.Equal(30, Nutrition.Age(new DateOnly(1996, 6, 15), Today));
        Assert.Equal(29, Nutrition.Age(new DateOnly(1996, 6, 16), Today));
    }

    [Fact]
    public void Bmr_differs_by_sex()
    {
        Assert.Equal(1517.5, Nutrition.Bmr(Person(Sex.Male, Goal.Maintain), Today)); // 600 + 1062,5 − 150 + 5
        Assert.Equal(1351.5, Nutrition.Bmr(Person(Sex.Female, Goal.Maintain), Today)); // 600 + 1062,5 − 150 − 161
    }

    [Theory]
    [InlineData(Goal.Lose, 0.85)]
    [InlineData(Goal.Maintain, 1.0)]
    [InlineData(Goal.Gain, 1.10)]
    public void Goal_adjusts_calories(Goal goal, double factor)
    {
        var p = Person(Sex.Male, goal);
        Assert.Equal(Math.Round(1517.5 * 1.55 * factor), Nutrition.AutoTargets(p, new UserSettings(), Today).Kcal);
    }

    [Theory]
    [InlineData(50, "Дефицит массы")]
    [InlineData(70, "Норма")]
    [InlineData(80, "Избыточная масса")]
    [InlineData(100, "Ожирение")]
    public void Bmi_labels(double weight, string label) => Assert.Equal(label, Nutrition.Bmi(weight, 175).Label);
}

public class FoodSearchUnitTests
{
    [Fact]
    public void Normalize_folds_case_and_yo() => Assert.Equal("тушёная".Replace('ё', 'е'), FoodSearch.Normalize("  ТушЁная "));

    [Theory]
    [InlineData("гречка", "гречка:*")]
    [InlineData("курица гриль", "курица:* & гриль:*")]
    [InlineData("a&b|c!", "a:* & b:* & c:*")]
    [InlineData("':*()", null)]
    public void TsQuery_strips_operators(string input, string? expected) => Assert.Equal(expected, FoodSearch.ToTsQuery(input));
}

public class OpenFoodFactsUnitTests
{
    private static Food? Parse(string json) => OpenFoodFactsLookup.Parse(JsonDocument.Parse(json).RootElement, "4601234567890");

    [Fact]
    public void Parses_product_preferring_russian_name()
    {
        var food = Parse("""
            { "product": {
                "product_name": "Kefir", "product_name_ru": "Кефир", "nutrition_data_per": "100ml", "serving_quantity": "250",
                "image_front_url": "https://images.openfoodfacts.org/x.jpg",
                "nutriments": { "energy-kcal_100g": 53, "proteins_100g": "2.9", "fat_100g": 2.5, "carbohydrates_100g": 4 } } }
            """)!;

        Assert.Equal("Кефир", food.Name);
        Assert.Equal("кефир", food.SearchName);
        Assert.Equal(FoodUnit.Ml, food.Unit);
        Assert.Equal(53, food.Kcal);
        Assert.Equal(2.9, food.Protein);
        Assert.Equal(250, food.Portion);
    }

    [Theory]
    [InlineData("""{ "status": 0 }""")]
    [InlineData("""{ "product": { "nutriments": { "energy-kcal_100g": 100 } } }""")]
    [InlineData("""{ "product": { "product_name": "Без калорий", "nutriments": { } } }""")]
    [InlineData("""{ "product": { "product_name": "Мусор", "nutriments": { "energy-kcal_100g": 99999 } } }""")]
    public void Rejects_incomplete_or_absurd_cards(string json) => Assert.Null(Parse(json));

    [Fact]
    public void Ignores_non_https_image()
    {
        var food = Parse("""
            { "product": { "product_name": "X", "image_front_url": "http://evil.example/x.jpg",
              "nutriments": { "energy-kcal_100g": 1 } } }
            """)!;
        Assert.Null(food.ImageUrl);
    }
}
