using Metabolix.Api.Data;
using Metabolix.Api.Dtos;
using Metabolix.Api.Models;
using Metabolix.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Metabolix.Api.Controllers;

[Route("api")]
public class ProfileController(AppDbContext db, WeightService weights, FileStorage files) : ApiControllerBase
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [HttpGet("profile")]
    public async Task<ActionResult<ProfileDto>> Get()
    {
        var profile = await db.Profiles.FindAsync(UserId);
        if (profile == null) return Error(404, "Анкета ещё не заполнена");
        var user = await db.Users.FindAsync(UserId);
        return ToDto(profile, user!);
    }

    [HttpPut("profile")]
    public async Task<ActionResult<ProfileDto>> Put(ProfileDto r)
    {
        var age = Nutrition.Age(r.BirthDate, Today);
        if (age is < 10 or > 120) return Error(400, "Проверьте дату рождения");

        var user = (await db.Users.FindAsync(UserId))!;
        if (!string.IsNullOrWhiteSpace(r.Name)) user.Name = r.Name.Trim();

        var profile = await db.Profiles.FindAsync(UserId);
        if (profile == null)
        {
            profile = new Profile { UserId = UserId };
            db.Profiles.Add(profile);
        }
        profile.Sex = r.Sex;
        profile.BirthDate = r.BirthDate;
        profile.Height = r.Height;
        profile.Weight = r.Weight;
        profile.TargetWeight = r.TargetWeight;
        profile.Activity = r.Activity;
        profile.Goal = r.Goal;
        await db.SaveChangesAsync();

        // Вес из анкеты попадает и в историю веса.
        await weights.SetAsync(UserId, r.Today ?? Today, r.Weight);
        return ToDto(profile, user);
    }

    /// <summary>Основной обмен, TDEE, норма КБЖУ и ИМТ.</summary>
    [HttpGet("profile/norm")]
    public async Task<ActionResult<NormDto>> Norm()
    {
        var profile = await db.Profiles.FindAsync(UserId);
        if (profile == null) return Error(404, "Анкета ещё не заполнена");
        var settings = await db.Settings.FindAsync(UserId) ?? new UserSettings();

        return new NormDto(
            Math.Round(Nutrition.Bmr(profile, Today)),
            Math.Round(Nutrition.Tdee(profile, Today)),
            Nutrition.AutoTargets(profile, settings, Today),
            Nutrition.Targets(profile, settings, Today),
            settings.ManualKcal != null,
            Nutrition.Bmi(profile.Weight, profile.Height));
    }

    [HttpPost("profile/avatar")]
    [RequestSizeLimit(FileStorage.MaxBytes + 64 * 1024)]
    public async Task<ActionResult<UserDto>> Avatar(IFormFile file)
    {
        string url;
        try
        {
            url = await files.SaveImageAsync(file);
        }
        catch (InvalidImageException e)
        {
            return Error(400, e.Message);
        }

        var user = (await db.Users.FindAsync(UserId))!;
        files.Delete(user.AvatarUrl);
        user.AvatarUrl = url;
        await db.SaveChangesAsync();
        return new UserDto(user.Id, user.Email, user.Name, user.AvatarUrl, await db.Profiles.FindAsync(UserId) != null);
    }

    [HttpGet("settings")]
    public async Task<ActionResult<SettingsDto>> GetSettings() =>
        ToDto(await db.Settings.FindAsync(UserId) ?? new UserSettings());

    [HttpPut("settings")]
    public async Task<ActionResult<SettingsDto>> PutSettings(SettingsDto r)
    {
        if (r.Ratio.Protein + r.Ratio.Fat + r.Ratio.Carbs != 100)
            return Error(400, "Сумма долей белков, жиров и углеводов должна быть 100%");

        var s = await db.Settings.FindAsync(UserId);
        if (s == null)
        {
            s = new UserSettings { UserId = UserId };
            db.Settings.Add(s);
        }
        s.Theme = r.Theme;
        s.RatioProtein = r.Ratio.Protein;
        s.RatioFat = r.Ratio.Fat;
        s.RatioCarbs = r.Ratio.Carbs;
        s.ManualKcal = r.ManualTargets?.Kcal;
        s.ManualProtein = r.ManualTargets?.Protein;
        s.ManualFat = r.ManualTargets?.Fat;
        s.ManualCarbs = r.ManualTargets?.Carbs;
        s.WaterGoal = r.WaterGoal;
        s.RemindMeal = r.Reminders.Meal;
        s.RemindWater = r.Reminders.Water;
        s.RemindWeight = r.Reminders.Weight;
        s.RemindTime = r.Reminders.Time;
        await db.SaveChangesAsync();
        return ToDto(s);
    }

    private static ProfileDto ToDto(Profile p, User u) => new()
    {
        Name = u.Name,
        Sex = p.Sex,
        BirthDate = p.BirthDate,
        Height = p.Height,
        Weight = p.Weight,
        TargetWeight = p.TargetWeight,
        Activity = p.Activity,
        Goal = p.Goal,
    };

    private static SettingsDto ToDto(UserSettings s) => new()
    {
        Theme = s.Theme,
        Ratio = new RatioDto { Protein = s.RatioProtein, Fat = s.RatioFat, Carbs = s.RatioCarbs },
        ManualTargets = s.ManualKcal == null
            ? null
            : new MacrosDto { Kcal = s.ManualKcal.Value, Protein = s.ManualProtein ?? 0, Fat = s.ManualFat ?? 0, Carbs = s.ManualCarbs ?? 0 },
        WaterGoal = s.WaterGoal,
        Reminders = new RemindersDto { Meal = s.RemindMeal, Water = s.RemindWater, Weight = s.RemindWeight, Time = s.RemindTime },
    };
}
