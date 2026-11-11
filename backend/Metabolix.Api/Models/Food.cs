using NpgsqlTypes;

namespace Metabolix.Api.Models;

/// <summary>пищевая ценность на 100 г или 100 мл</summary>
public class Food
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>без владельца продукт доступен всем</summary>
    public Guid? OwnerId { get; set; }

    public required string Name { get; set; }

    /// <summary>название для поиска приводим к нижнему регистру и заменяем ё на е</summary>
    public string SearchName { get; set; } = "";

    public double Kcal { get; set; }
    public double Protein { get; set; }
    public double Fat { get; set; }
    public double Carbs { get; set; }
    public FoodUnit Unit { get; set; }
    public double? Portion { get; set; }
    public string? Barcode { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>база строит поисковый вектор с русским словарём</summary>
    public NpgsqlTsVector SearchVector { get; set; } = null!;
}

/// <summary>снимок продукта сохраняет историю при изменении справочника</summary>
public class FoodSnapshot
{
    /// <summary>ссылка на исходный продукт или рецепт для списка недавних</summary>
    public string? FoodRef { get; set; }
    public string Name { get; set; } = "";
    public double Kcal { get; set; }
    public double Protein { get; set; }
    public double Fat { get; set; }
    public double Carbs { get; set; }
    public FoodUnit Unit { get; set; }
    public double? Portion { get; set; }
    public string? Barcode { get; set; }
    public bool Custom { get; set; }
}

public class FavoriteFood
{
    public Guid UserId { get; set; }
    public Guid FoodId { get; set; }
    public Food Food { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class FavoriteRecipe
{
    public Guid UserId { get; set; }
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Recipe
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public string? ImageUrl { get; set; }
    public List<RecipeItem> Items { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class RecipeItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipeId { get; set; }
    public int Position { get; set; }
    public FoodSnapshot Food { get; set; } = new();
    public double Grams { get; set; }
}
