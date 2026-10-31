using System.Net;
using System.Net.Http.Json;
using System.Text;
using Metabolix.Api.Dtos;

namespace Metabolix.Tests;

[Collection("api")]
public class DiaryTests(ApiFixture api)
{
    private static readonly DateOnly Day = new(2026, 3, 10);

    [Fact]
    public async Task Day_shows_totals_targets_and_left()
    {
        var user = await api.RegisterAsync();
        await user.SaveProfileAsync();
        await user.AddEntryAsync(Day, grams: 200);
        await user.AddEntryAsync(Day, "lunch", grams: 100);
        await user.AddEntryAsync(Day.AddDays(1), grams: 500);

        var day = await user.GetAsync<DayDto>($"/api/diary/{Day:yyyy-MM-dd}");

        Assert.Equal(2, day.Entries.Count);
        Assert.Equal(300, day.Total.Kcal); // 100 ккал на 100 г × 300 г
        Assert.Equal(30, day.Total.Protein);
        Assert.Equal(2136, day.Targets!.Kcal);
        Assert.Equal(1836, day.KcalLeft);
    }

    [Fact]
    public async Task Repeated_create_with_same_id_does_not_duplicate()
    {
        var user = await api.RegisterAsync();
        var id = Guid.NewGuid();
        await user.AddEntryAsync(Day, id: id);
        await user.AddEntryAsync(Day, id: id);

        Assert.Single((await user.GetAsync<DayDto>($"/api/diary/{Day:yyyy-MM-dd}")).Entries);
    }

    [Fact]
    public async Task Other_users_entries_are_untouchable()
    {
        var owner = await api.RegisterAsync();
        var stranger = await api.RegisterAsync();
        var entry = await owner.AddEntryAsync(Day);

        Assert.Empty((await stranger.GetAsync<DayDto>($"/api/diary/{Day:yyyy-MM-dd}")).Entries);
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.Http.PutAsJsonAsync($"/api/diary/{entry.Id}", new { grams = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Http.DeleteAsync($"/api/diary/{entry.Id}")).StatusCode);

        var hijack = await stranger.Http.PostAsJsonAsync("/api/diary",
            new { id = entry.Id, date = Day, meal = "lunch", grams = 1, food = TestUser.Food() });
        Assert.Equal(HttpStatusCode.Conflict, hijack.StatusCode);

        var day = await owner.GetAsync<DayDto>($"/api/diary/{Day:yyyy-MM-dd}");
        Assert.Equal(200, day.Entries.Single().Grams);
    }

    [Fact]
    public async Task Update_and_delete_work()
    {
        var user = await api.RegisterAsync();
        var entry = await user.AddEntryAsync(Day);

        var updated = await user.Http.PutAsJsonAsync($"/api/diary/{entry.Id}", new { grams = 50, meal = "dinner" });
        var dto = (await updated.Content.ReadFromJsonAsync<EntryDto>(ApiFixture.Json))!;
        Assert.Equal(50, dto.Grams);
        Assert.Equal(50, dto.Macros.Kcal);

        Assert.Equal(HttpStatusCode.NoContent, (await user.Http.DeleteAsync($"/api/diary/{entry.Id}")).StatusCode);
        Assert.Empty((await user.GetAsync<DayDto>($"/api/diary/{Day:yyyy-MM-dd}")).Entries);
        Assert.Equal(HttpStatusCode.NotFound, (await user.Http.DeleteAsync($"/api/diary/{entry.Id}")).StatusCode);
    }

    [Fact]
    public async Task Copy_meal_copies_only_that_meal()
    {
        var user = await api.RegisterAsync();
        await user.AddEntryAsync(Day);
        await user.AddEntryAsync(Day, name: "Яблоко");
        await user.AddEntryAsync(Day, "lunch");
        var target = Day.AddDays(5);

        var response = await user.Http.PostAsJsonAsync("/api/diary/copy", new { from = Day, meal = "breakfast", to = target });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var copied = await user.GetAsync<DayDto>($"/api/diary/{target:yyyy-MM-dd}");
        Assert.Equal(2, copied.Entries.Count);
        Assert.Equal(3, (await user.GetAsync<DayDto>($"/api/diary/{Day:yyyy-MM-dd}")).Entries.Count);

        var same = await user.Http.PostAsJsonAsync("/api/diary/copy", new { from = Day, meal = "breakfast", to = Day });
        Assert.Equal(HttpStatusCode.BadRequest, same.StatusCode);
    }

    [Fact]
    public async Task History_survives_deleting_the_food()
    {
        var user = await api.RegisterAsync();
        var created = await user.Http.PostAsJsonAsync("/api/foods", TestUser.Food("Удаляемый"));
        var food = (await created.Content.ReadFromJsonAsync<FoodDto>(ApiFixture.Json))!;

        var entry = await user.Http.PostAsJsonAsync("/api/diary", new { date = Day, meal = "lunch", grams = 100, food }, ApiFixture.Json);
        Assert.Equal(HttpStatusCode.Created, entry.StatusCode);
        await user.Http.DeleteAsync($"/api/foods/{food.Id}");

        var day = await user.GetAsync<DayDto>($"/api/diary/{Day:yyyy-MM-dd}");
        Assert.Equal("Удаляемый", day.Entries.Single().Food.Name);
    }

    [Fact]
    public async Task Recent_lists_distinct_foods_newest_first()
    {
        var user = await api.RegisterAsync();
        await user.AddEntryAsync(Day, name: "Первый");
        await user.AddEntryAsync(Day, name: "Второй");
        await user.AddEntryAsync(Day, name: "Первый");

        var recent = await user.GetAsync<List<FoodDto>>("/api/foods/recent");
        Assert.Equal(["Первый", "Второй"], recent.Select(f => f.Name));
    }

    [Fact]
    public async Task Stats_average_ignores_empty_days()
    {
        var user = await api.RegisterAsync();
        await user.SaveProfileAsync();
        await user.AddEntryAsync(Day, grams: 100);
        await user.AddEntryAsync(Day.AddDays(2), grams: 300);

        var stats = await user.GetAsync<StatsDto>($"/api/stats?from={Day:yyyy-MM-dd}&to={Day.AddDays(6):yyyy-MM-dd}");

        Assert.Equal(7, stats.TotalDays);
        Assert.Equal(7, stats.Days.Count);
        Assert.Equal(2, stats.LoggedDays);
        Assert.Equal(200, stats.Average.Kcal);
        Assert.Equal(300, stats.Days[2].Kcal);
        Assert.Equal(0, stats.Days[1].Kcal);
        Assert.Equal("Норма", stats.Bmi!.Label);
    }

    [Fact]
    public async Task Too_long_period_is_rejected()
    {
        var user = await api.RegisterAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await user.Http.GetAsync("/api/stats?from=2020-01-01&to=2026-01-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.Http.GetAsync("/api/stats?from=2026-02-01&to=2026-01-01")).StatusCode);
    }

    [Fact]
    public async Task Export_produces_excel_friendly_csv()
    {
        var user = await api.RegisterAsync();
        await user.AddEntryAsync(Day, name: "=Формула \"в кавычках\"");

        var response = await user.Http.GetAsync($"/api/export?from={Day:yyyy-MM-dd}&to={Day:yyyy-MM-dd}");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var text = Encoding.UTF8.GetString(bytes);

        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.Contains("\"'=Формула \"\"в кавычках\"\"\"", text);
        Assert.Contains("2026-03-10;Завтрак;", text);
    }

    [Fact]
    public async Task Recipe_totals_and_replacing_items_work()
    {
        var user = await api.RegisterAsync();
        var created = await user.Http.PostAsJsonAsync("/api/recipes", new
        {
            name = "Каша",
            items = new[]
            {
                new { food = TestUser.Food("Крупа"), grams = 100 },
                new { food = TestUser.Food("Молоко"), grams = 300 },
            },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var recipe = (await created.Content.ReadFromJsonAsync<RecipeDto>(ApiFixture.Json))!;
        Assert.Equal(400, recipe.Weight);
        Assert.Equal(400, recipe.Total.Kcal);
        Assert.Equal(100, recipe.AsFood.Kcal);
        Assert.Equal("recipe:" + recipe.Id, recipe.AsFood.Id);

        var updated = await user.Http.PutAsJsonAsync($"/api/recipes/{recipe.Id}", new
        {
            name = "Каша 2",
            items = new[] { new { food = TestUser.Food("Крупа"), grams = 50 } },
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var saved = await user.GetAsync<RecipeDto>($"/api/recipes/{recipe.Id}");
        Assert.Equal("Каша 2", saved.Name);
        Assert.Equal(50, saved.Items.Single().Grams);

        Assert.Equal(HttpStatusCode.NoContent, (await user.Http.PutAsync($"/api/favorites/recipes/{recipe.Id}", null)).StatusCode);
        Assert.Single((await user.GetAsync<FavoritesDto>("/api/favorites")).Recipes);
    }

    [Fact]
    public async Task Recipe_needs_items_and_is_private()
    {
        var owner = await api.RegisterAsync();
        var stranger = await api.RegisterAsync();

        var empty = await owner.Http.PostAsJsonAsync("/api/recipes", new { name = "Пусто", items = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        var created = await owner.Http.PostAsJsonAsync("/api/recipes", new { name = "Моё", items = new[] { new { food = TestUser.Food(), grams = 100 } } });
        var recipe = (await created.Content.ReadFromJsonAsync<RecipeDto>(ApiFixture.Json))!;

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Http.GetAsync($"/api/recipes/{recipe.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Http.DeleteAsync($"/api/recipes/{recipe.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Http.PutAsync($"/api/favorites/recipes/{recipe.Id}", null)).StatusCode);
        Assert.Empty(await stranger.GetAsync<List<RecipeDto>>("/api/recipes"));
    }
}
