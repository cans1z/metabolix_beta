using NpgsqlTypes;

namespace Metabolix.Api.Models;

/// <summary>Карточка продукта. Пищевая ценность — на 100 г / 100 мл.</summary>
public class Food
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>null — общий продукт из справочника, иначе личная карточка пользователя.</summary>
    public Guid? OwnerId { get; set; }

    public required string Name { get; set; }

    /// <summary>Название для поиска: нижний регистр и «е» вместо «ё».</summary>
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

    /// <summary>Генерируется самой БД из SearchName со словарём russian.</summary>
    public NpgsqlTsVector SearchVector { get; set; } = null!;
}

/// <summary>
/// Копия продукта внутри записи дневника или рецепта. Правка или удаление карточки
/// не должны менять уже записанную историю.
/// </summary>
public class FoodSnapshot
{
    /// <summary>Id исходного продукта; у блюд вида "recipe:{guid}". Нужен для списка «недавние».</summary>
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
