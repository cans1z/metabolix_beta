using System.Text.Json;
using Metabolix.Api.Models;

namespace Metabolix.Api.Services;

/// <summary>Поиск продукта по штрихкоду во внешней базе. null — не найден или база недоступна.</summary>
public interface IBarcodeLookup
{
    /// <returns>Карточка без сохранения в БД; <see cref="BarcodeResult.Temporary"/> — сбой сети, кэшировать «не найдено» нельзя.</returns>
    Task<BarcodeResult> FindAsync(string barcode, CancellationToken ct = default);
}

public record BarcodeResult(Food? Food, bool Temporary = false);

/// <summary>Open Food Facts — открытая база продуктов, бесплатная и без ключа.</summary>
public class OpenFoodFactsLookup(HttpClient http, ILogger<OpenFoodFactsLookup> log) : IBarcodeLookup
{
    public async Task<BarcodeResult> FindAsync(string barcode, CancellationToken ct = default)
    {
        try
        {
            using var response = await http.GetAsync(
                $"api/v2/product/{barcode}.json?fields=product_name,product_name_ru,nutriments,serving_quantity,nutrition_data_per,image_front_url", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return new BarcodeResult(null);
            if (!response.IsSuccessStatusCode) return new BarcodeResult(null, Temporary: true);

            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            return new BarcodeResult(Parse(json.RootElement, barcode));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            log.LogWarning(e, "Open Food Facts недоступен");
            return new BarcodeResult(null, Temporary: true);
        }
    }

    /// <summary>Разбор ответа. Карточки без названия или калорий и с нереальными числами отбрасываем.</summary>
    public static Food? Parse(JsonElement root, string barcode)
    {
        if (!root.TryGetProperty("product", out var product)) return null;

        var name = Text(product, "product_name_ru") ?? Text(product, "product_name");
        if (name == null || !product.TryGetProperty("nutriments", out var n)) return null;
        if (Number(n, "energy-kcal_100g") is not { } kcal) return null;

        var protein = Number(n, "proteins_100g") ?? 0;
        var fat = Number(n, "fat_100g") ?? 0;
        var carbs = Number(n, "carbohydrates_100g") ?? 0;
        if (kcal is < 0 or > 1000 || protein is < 0 or > 100 || fat is < 0 or > 100 || carbs is < 0 or > 100) return null;

        name = name.Length > 200 ? name[..200] : name;
        var image = Text(product, "image_front_url");
        return new Food
        {
            Name = name,
            SearchName = FoodSearch.Normalize(name),
            Kcal = Math.Round(kcal, 1),
            Protein = Math.Round(protein, 1),
            Fat = Math.Round(fat, 1),
            Carbs = Math.Round(carbs, 1),
            Unit = Text(product, "nutrition_data_per") == "100ml" ? FoodUnit.Ml : FoodUnit.G,
            Portion = Number(product, "serving_quantity") is > 0 and <= 20000 and var portion ? portion : null,
            Barcode = barcode,
            ImageUrl = image != null && image.StartsWith("https://") ? image : null,
        };
    }

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!.Trim()
            : null;

    // Open Food Facts отдаёт числа то числом, то строкой.
    private static double? Number(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
        return v.ValueKind == JsonValueKind.String
               && double.TryParse(v.GetString(), System.Globalization.CultureInfo.InvariantCulture, out var d)
            ? d
            : null;
    }
}
