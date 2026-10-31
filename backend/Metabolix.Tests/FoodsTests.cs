using System.Net;
using System.Net.Http.Json;
using Metabolix.Api.Dtos;
using Metabolix.Api.Models;
using Metabolix.Api.Services;

namespace Metabolix.Tests;

[Collection("api")]
public class FoodsTests(ApiFixture api)
{
    private async Task<List<string>> SearchAsync(string q, HttpClient? client = null)
    {
        var page = await (client ?? api.Anonymous()).GetFromJsonAsync<PageDto<FoodDto>>(
            "/api/foods?q=" + Uri.EscapeDataString(q), ApiFixture.Json);
        return page!.Items.Select(f => f.Name).ToList();
    }

    [Theory]
    [InlineData("куриная", "Куриная грудка отварная")] // точное слово
    [InlineData("куриной", "Куриная грудка отварная")] // другой падеж
    [InlineData("грудку", "Куриная грудка отварная")]
    [InlineData("гре", "Гречка отварная")] // начало слова
    [InlineData("тушеная", "Говядина тушёная")] // «е» вместо «ё»
    [InlineData("ТВОРОГ", "Творог 5%")] // регистр
    [InlineData("картофль", "Картофель отварной")] // опечатка
    [InlineData("отварная гречка", "Гречка отварная")] // порядок слов
    public async Task Search_understands_russian(string query, string expected)
    {
        Assert.Contains(expected, await SearchAsync(query));
    }

    [Theory]
    [InlineData("'; DROP TABLE \"Foods\"; --")]
    [InlineData("&|!:*()")]
    [InlineData("   ")]
    public async Task Search_survives_special_characters(string query)
    {
        var response = await api.Anonymous().GetAsync("/api/foods?q=" + Uri.EscapeDataString(query));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Search_pages_through_catalog()
    {
        var client = api.Anonymous();
        var first = (await client.GetFromJsonAsync<PageDto<FoodDto>>("/api/foods?pageSize=10", ApiFixture.Json))!;
        var second = (await client.GetFromJsonAsync<PageDto<FoodDto>>("/api/foods?pageSize=10&page=2", ApiFixture.Json))!;

        Assert.True(first.Total > 100);
        Assert.Equal(10, first.Items.Count);
        Assert.Empty(first.Items.Select(f => f.Id).Intersect(second.Items.Select(f => f.Id)));
    }

    [Fact]
    public async Task Custom_food_is_visible_only_to_its_owner()
    {
        var owner = await api.RegisterAsync();
        var stranger = await api.RegisterAsync();
        var name = "Батончик" + Guid.NewGuid().ToString("N")[..8];

        var created = await owner.Http.PostAsJsonAsync("/api/foods", TestUser.Food(name));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var food = (await created.Content.ReadFromJsonAsync<FoodDto>(ApiFixture.Json))!;
        Assert.True(food.Custom);

        Assert.Contains(name, await SearchAsync(name, owner.Http));
        Assert.DoesNotContain(name, await SearchAsync(name, stranger.Http));
        Assert.DoesNotContain(name, await SearchAsync(name));

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Http.GetAsync($"/api/foods/{food.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Http.DeleteAsync($"/api/foods/{food.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.Http.PutAsJsonAsync($"/api/foods/{food.Id}", TestUser.Food("Взлом"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Http.PutAsync($"/api/favorites/foods/{food.Id}", null)).StatusCode);
    }

    [Fact]
    public async Task Catalog_food_cannot_be_changed_by_users()
    {
        var user = await api.RegisterAsync();
        var page = (await user.GetAsync<PageDto<FoodDto>>("/api/foods?q=творог"));
        var id = page.Items.First(f => !f.Custom).Id;

        Assert.Equal(HttpStatusCode.NotFound, (await user.Http.PutAsJsonAsync($"/api/foods/{id}", TestUser.Food("Взлом"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.Http.DeleteAsync($"/api/foods/{id}")).StatusCode);
    }

    [Fact]
    public async Task Invalid_food_is_rejected()
    {
        var user = await api.RegisterAsync();
        var response = await user.Http.PostAsJsonAsync("/api/foods", new { name = "Х", kcal = -5, protein = 1, fat = 1, carbs = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Barcode_finds_catalog_food_without_external_call()
    {
        var calls = api.Barcodes.Calls;
        var food = await api.Anonymous().GetFromJsonAsync<FoodDto>("/api/foods/barcode/4600000000011", ApiFixture.Json);
        Assert.Equal("Творог 5%", food!.Name);
        Assert.Equal(calls, api.Barcodes.Calls);
    }

    [Fact]
    public async Task Barcode_import_is_saved_and_cached()
    {
        const string code = "5901234123457";
        var calls = api.Barcodes.Calls;
        api.Barcodes.Handler = c => new BarcodeResult(new Food
        {
            Name = "Импорт",
            SearchName = "импорт",
            Kcal = 100,
            Protein = 1,
            Fat = 2,
            Carbs = 3,
            Barcode = c,
        });

        var client = api.Anonymous();
        Assert.Equal("Импорт", (await client.GetFromJsonAsync<FoodDto>($"/api/foods/barcode/{code}", ApiFixture.Json))!.Name);
        Assert.Equal("Импорт", (await client.GetFromJsonAsync<FoodDto>($"/api/foods/barcode/{code}", ApiFixture.Json))!.Name);
        Assert.Equal(calls + 1, api.Barcodes.Calls);
        Assert.Contains("Импорт", await SearchAsync("импорт"));
    }

    [Fact]
    public async Task Missing_barcode_is_remembered_but_network_failure_is_not()
    {
        var client = api.Anonymous();

        const string missing = "1111111111111";
        var calls = api.Barcodes.Calls;
        api.Barcodes.Handler = _ => new BarcodeResult(null);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/foods/barcode/{missing}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/foods/barcode/{missing}")).StatusCode);
        Assert.Equal(calls + 1, api.Barcodes.Calls);

        const string flaky = "2222222222222";
        api.Barcodes.Handler = _ => new BarcodeResult(null, Temporary: true);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync($"/api/foods/barcode/{flaky}")).StatusCode);
        api.Barcodes.Handler = _ => new BarcodeResult(null);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/foods/barcode/{flaky}")).StatusCode);
    }

    [Fact]
    public async Task Bad_barcode_format_is_not_routed()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await api.Anonymous().GetAsync("/api/foods/barcode/abc")).StatusCode);
    }

    [Fact]
    public async Task Personal_barcode_card_stays_private()
    {
        const string code = "3333333333333";
        api.Barcodes.Handler = _ => new BarcodeResult(null);
        var owner = await api.RegisterAsync();
        var created = await owner.Http.PostAsJsonAsync("/api/foods", new { name = "Моё", kcal = 1, protein = 1, fat = 1, carbs = 1, barcode = code });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        Assert.Equal("Моё", (await owner.GetAsync<FoodDto>($"/api/foods/barcode/{code}")).Name);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Anonymous().GetAsync($"/api/foods/barcode/{code}")).StatusCode);
    }

    [Fact]
    public async Task Favorites_add_is_idempotent_and_remove_works()
    {
        var user = await api.RegisterAsync();
        var id = (await user.GetAsync<PageDto<FoodDto>>("/api/foods?q=банан")).Items[0].Id;

        Assert.Equal(HttpStatusCode.NoContent, (await user.Http.PutAsync($"/api/favorites/foods/{id}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await user.Http.PutAsync($"/api/favorites/foods/{id}", null)).StatusCode);
        Assert.Single((await user.GetAsync<FavoritesDto>("/api/favorites")).Foods);

        await user.Http.DeleteAsync($"/api/favorites/foods/{id}");
        Assert.Empty((await user.GetAsync<FavoritesDto>("/api/favorites")).Foods);
    }

    [Fact]
    public async Task Upload_accepts_real_image_only_and_serves_it()
    {
        var user = await api.RegisterAsync();

        var text = new MultipartFormDataContent { { new StringContent("not an image"), "file", "photo.png" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await user.Http.PostAsync("/api/profile/avatar", text)).StatusCode);

        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
        var image = new MultipartFormDataContent { { new ByteArrayContent(png), "file", "x.exe" } };
        var response = await user.Http.PostAsync("/api/profile/avatar", image);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var url = (await response.Content.ReadFromJsonAsync<UserDto>(ApiFixture.Json))!.AvatarUrl!;
        Assert.EndsWith(".png", url);
        Assert.Equal(png, await api.Anonymous().GetByteArrayAsync(url));
    }
}
