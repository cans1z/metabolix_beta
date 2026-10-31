using System.Net;
using System.Net.Http.Json;
using Metabolix.Api.Dtos;

namespace Metabolix.Tests;

[Collection("api")]
public class ProfileTests(ApiFixture api)
{
    [Fact]
    public async Task Norm_follows_the_formulas_from_the_spec()
    {
        var user = await api.RegisterAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await user.Http.GetAsync("/api/profile")).StatusCode);

        await user.SaveProfileAsync();
        Assert.True((await user.GetAsync<UserDto>("/api/auth/me")).HasProfile);

        var norm = await user.GetAsync<NormDto>("/api/profile/norm");
        Assert.Equal(1780, norm.Bmr); // 10×80 + 6,25×180 − 5×30 + 5
        Assert.Equal(2136, norm.Tdee); // ×1,2
        Assert.Equal(2136, norm.Targets.Kcal); // поддержание
        Assert.Equal(160, norm.Targets.Protein); // 30% / 4
        Assert.Equal(71, norm.Targets.Fat); // 30% / 9
        Assert.Equal(214, norm.Targets.Carbs); // 40% / 4
        Assert.Equal(24.7, norm.Bmi.Value);
        Assert.False(norm.Manual);
    }

    [Theory]
    [InlineData("height", 50)]
    [InlineData("weight", 1000)]
    [InlineData("activity", 3)]
    public async Task Profile_with_impossible_values_is_rejected(string field, double value)
    {
        var user = await api.RegisterAsync();
        var body = new Dictionary<string, object>
        {
            ["sex"] = "male",
            ["birthDate"] = "2000-01-01",
            ["height"] = 180,
            ["weight"] = 80,
            ["targetWeight"] = 75,
            ["activity"] = 1.2,
            ["goal"] = "lose",
        };
        body[field] = value;
        Assert.Equal(HttpStatusCode.BadRequest, (await user.Http.PutAsJsonAsync("/api/profile", body)).StatusCode);
    }

    [Fact]
    public async Task Settings_ratio_and_manual_targets_change_the_norm()
    {
        var user = await api.RegisterAsync();
        await user.SaveProfileAsync();

        var bad = await user.Http.PutAsJsonAsync("/api/settings", Settings(new { protein = 50, fat = 50, carbs = 50 }, null));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        await user.Http.PutAsJsonAsync("/api/settings", Settings(new { protein = 40, fat = 20, carbs = 40 }, null));
        Assert.Equal(214, (await user.GetAsync<NormDto>("/api/profile/norm")).Targets.Protein); // 40% / 4

        await user.Http.PutAsJsonAsync("/api/settings",
            Settings(new { protein = 40, fat = 20, carbs = 40 }, new { kcal = 1500, protein = 100, fat = 50, carbs = 150 }));
        var norm = await user.GetAsync<NormDto>("/api/profile/norm");
        Assert.True(norm.Manual);
        Assert.Equal(1500, norm.Targets.Kcal);
        Assert.Equal(2136, norm.Auto.Kcal);

        var saved = await user.GetAsync<SettingsDto>("/api/settings");
        Assert.Equal(1500, saved.ManualTargets!.Kcal);
    }

    private static object Settings(object ratio, object? manualTargets) => new
    {
        theme = "dark",
        ratio,
        manualTargets,
        waterGoal = 2500,
        reminders = new { meal = true, water = false, weight = true, time = "08:30" },
    };

    [Fact]
    public async Task Latest_weight_updates_profile_and_earlier_does_not()
    {
        var user = await api.RegisterAsync();
        await user.SaveProfileAsync(weight: 80);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await user.Http.PutAsJsonAsync($"/api/weight/{today.AddDays(-10):yyyy-MM-dd}", new { kg = 90 });
        Assert.Equal(80, (await user.GetAsync<ProfileDto>("/api/profile")).Weight);

        await user.Http.PutAsJsonAsync($"/api/weight/{today.AddDays(1):yyyy-MM-dd}", new { kg = 78 });
        Assert.Equal(78, (await user.GetAsync<ProfileDto>("/api/profile")).Weight);

        await user.Http.DeleteAsync($"/api/weight/{today.AddDays(1):yyyy-MM-dd}");
        Assert.Equal(80, (await user.GetAsync<ProfileDto>("/api/profile")).Weight);
        Assert.Equal(2, (await user.GetAsync<List<WeightDto>>("/api/weight")).Count);
    }

    [Fact]
    public async Task Water_is_set_as_absolute_value()
    {
        var user = await api.RegisterAsync();
        var date = new DateOnly(2026, 4, 1);
        await user.Http.PutAsJsonAsync($"/api/water/{date:yyyy-MM-dd}", new { ml = 500 });
        await user.Http.PutAsJsonAsync($"/api/water/{date:yyyy-MM-dd}", new { ml = 750 });

        Assert.Equal(750, (await user.GetAsync<DayDto>($"/api/diary/{date:yyyy-MM-dd}")).WaterMl);
    }
}

[Collection("api")]
public class SyncTests(ApiFixture api)
{
    private static readonly DateOnly Day = new(2026, 5, 1);

    private static object Entry(Guid id, double grams, DateTime updatedAt, bool isDeleted = false) => new
    {
        id,
        date = Day,
        meal = "lunch",
        food = TestUser.Food(),
        grams,
        updatedAt,
        isDeleted,
    };

    private static async Task<SyncPushResult> PushAsync(TestUser user, object body)
    {
        var response = await user.Http.PostAsJsonAsync("/api/sync/push", body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SyncPushResult>(ApiFixture.Json))!;
    }

    [Fact]
    public async Task Newer_change_wins_older_is_skipped()
    {
        var user = await api.RegisterAsync();
        var id = Guid.NewGuid();
        var t = DateTime.UtcNow.AddHours(-3);

        Assert.Equal(1, (await PushAsync(user, new { diary = new[] { Entry(id, 100, t) } })).Applied);

        // Телефон долго был офлайн и прислал более старую правку.
        var stale = await PushAsync(user, new { diary = new[] { Entry(id, 999, t.AddMinutes(-30)) } });
        Assert.Equal(1, stale.Skipped);
        Assert.Equal(100, (await user.GetAsync<DayDto>($"/api/diary/{Day:yyyy-MM-dd}")).Entries.Single().Grams);

        Assert.Equal(1, (await PushAsync(user, new { diary = new[] { Entry(id, 200, t.AddMinutes(30)) } })).Applied);
        Assert.Equal(200, (await user.GetAsync<DayDto>($"/api/diary/{Day:yyyy-MM-dd}")).Entries.Single().Grams);

        // Повтор той же отправки (например, после обрыва сети) ничего не меняет.
        Assert.Equal(1, (await PushAsync(user, new { diary = new[] { Entry(id, 200, t.AddMinutes(30)) } })).Skipped);
    }

    [Fact]
    public async Task Cannot_overwrite_other_users_entry()
    {
        var owner = await api.RegisterAsync();
        var stranger = await api.RegisterAsync();
        var entry = await owner.AddEntryAsync(Day);

        var result = await PushAsync(stranger, new { diary = new[] { Entry(entry.Id, 1, DateTime.UtcNow) } });

        Assert.Equal([entry.Id], result.Rejected);
        Assert.Equal(0, result.Applied);
        Assert.Equal(200, (await owner.GetAsync<DayDto>($"/api/diary/{Day:yyyy-MM-dd}")).Entries.Single().Grams);
    }

    [Fact]
    public async Task Clock_ahead_does_not_block_later_changes()
    {
        var user = await api.RegisterAsync();
        var id = Guid.NewGuid();
        await PushAsync(user, new { diary = new[] { Entry(id, 100, DateTime.UtcNow.AddYears(5)) } });

        await Task.Delay(50);
        await user.Http.PutAsJsonAsync($"/api/diary/{id}", new { grams = 150 });

        var pull = await user.GetAsync<SyncPullResult>("/api/sync/pull");
        Assert.True(pull.Diary.Single().UpdatedAt <= DateTime.UtcNow.AddSeconds(1));
        Assert.Equal(150, pull.Diary.Single().Grams);
    }

    [Fact]
    public async Task Pull_returns_only_new_changes_including_deletions()
    {
        var user = await api.RegisterAsync();
        var kept = await user.AddEntryAsync(Day, name: "Остался");
        var removed = await user.AddEntryAsync(Day, name: "Удалён");
        await user.Http.DeleteAsync($"/api/diary/{removed.Id}");

        await Task.Delay(TimeSpan.FromSeconds(6)); // курсор сдвинут на 5 секунд назад, иначе эти записи придут повторно
        var first = await user.GetAsync<SyncPullResult>("/api/sync/pull");
        Assert.Equal(kept.Id, first.Diary.Single().Id); // первая загрузка — без удалённых
        var cursor = first.Cursor.ToString("O");

        var unchanged = await user.GetAsync<SyncPullResult>("/api/sync/pull?since=" + Uri.EscapeDataString(cursor));
        Assert.Empty(unchanged.Diary);

        await user.Http.DeleteAsync($"/api/diary/{kept.Id}");
        await user.Http.PutAsJsonAsync($"/api/water/{Day:yyyy-MM-dd}", new { ml = 300 });

        var changes = await user.GetAsync<SyncPullResult>("/api/sync/pull?since=" + Uri.EscapeDataString(cursor));
        Assert.True(changes.Diary.Single().IsDeleted);
        Assert.Equal(300, changes.Water.Single().Ml);
    }

    [Fact]
    public async Task Offline_change_is_visible_to_other_device_even_with_old_timestamp()
    {
        var user = await api.RegisterAsync();
        var cursor = (await user.GetAsync<SyncPullResult>("/api/sync/pull")).Cursor.ToString("O");
        await Task.Delay(TimeSpan.FromSeconds(6));

        // Запись сделана офлайн вчера, а до сервера дошла только сейчас.
        var id = Guid.NewGuid();
        await PushAsync(user, new { diary = new[] { Entry(id, 100, DateTime.UtcNow.AddDays(-1)) } });

        var pull = await user.GetAsync<SyncPullResult>("/api/sync/pull?since=" + Uri.EscapeDataString(cursor));
        Assert.Equal(id, pull.Diary.Single().Id);
    }

    [Fact]
    public async Task Weight_and_water_sync_and_profile_follows_latest_weight()
    {
        var user = await api.RegisterAsync();
        await user.SaveProfileAsync(weight: 80);
        var future = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);
        var now = DateTime.UtcNow;

        var result = await PushAsync(user, new
        {
            weights = new[] { new { date = future, kg = 76.5, updatedAt = now, isDeleted = false } },
            water = new[] { new { date = Day, ml = 1250, updatedAt = now, isDeleted = false } },
        });

        Assert.Equal(2, result.Applied);
        Assert.Equal(76.5, (await user.GetAsync<ProfileDto>("/api/profile")).Weight);
        Assert.Equal(1250, (await user.GetAsync<DayDto>($"/api/diary/{Day:yyyy-MM-dd}")).WaterMl);

        var older = await PushAsync(user, new
        {
            water = new[] { new { date = Day, ml = 1, updatedAt = now.AddHours(-1), isDeleted = false } },
        });
        Assert.Equal(1, older.Skipped);
    }

    [Fact]
    public async Task Sync_item_with_invalid_data_is_rejected()
    {
        var user = await api.RegisterAsync();
        var response = await user.Http.PostAsJsonAsync("/api/sync/push",
            new { diary = new[] { Entry(Guid.NewGuid(), -5, DateTime.UtcNow) } });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
