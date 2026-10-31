using Metabolix.Api.Dtos;
using Metabolix.Api.Models;

namespace Metabolix.Api.Services;

/// <summary>Формулы из раздела 5.5.1 ТЗ. Те же, что в веб- и мобильном клиенте.</summary>
public static class Nutrition
{
    public static int Age(DateOnly birthDate, DateOnly today)
    {
        var age = today.Year - birthDate.Year;
        if (birthDate > today.AddYears(-age)) age--;
        return age;
    }

    /// <summary>Основной обмен по формуле Миффлина — Сан Жеора.</summary>
    public static double Bmr(Profile p, DateOnly today) =>
        10 * p.Weight + 6.25 * p.Height - 5 * Age(p.BirthDate, today) + (p.Sex == Sex.Male ? 5 : -161);

    public static double Tdee(Profile p, DateOnly today) => Bmr(p, today) * p.Activity;

    /// <summary>Поправка к TDEE: −15% при похудении, +10% при наборе.</summary>
    private static double GoalFactor(Goal goal) => goal switch
    {
        Goal.Lose => 0.85,
        Goal.Gain => 1.10,
        _ => 1.0,
    };

    public static MacrosDto AutoTargets(Profile p, UserSettings s, DateOnly today)
    {
        var kcal = Tdee(p, today) * GoalFactor(p.Goal);
        return new MacrosDto
        {
            Kcal = Math.Round(kcal),
            Protein = Math.Round(kcal * s.RatioProtein / 100 / 4),
            Fat = Math.Round(kcal * s.RatioFat / 100 / 9),
            Carbs = Math.Round(kcal * s.RatioCarbs / 100 / 4),
        };
    }

    public static MacrosDto Targets(Profile p, UserSettings s, DateOnly today) =>
        s.ManualKcal is { } kcal
            ? new MacrosDto { Kcal = kcal, Protein = s.ManualProtein ?? 0, Fat = s.ManualFat ?? 0, Carbs = s.ManualCarbs ?? 0 }
            : AutoTargets(p, s, today);

    public static MacrosDto ForPortion(FoodSnapshot f, double grams) => new()
    {
        Kcal = f.Kcal * grams / 100,
        Protein = f.Protein * grams / 100,
        Fat = f.Fat * grams / 100,
        Carbs = f.Carbs * grams / 100,
    };

    public static MacrosDto Sum(IEnumerable<MacrosDto> items)
    {
        double kcal = 0, protein = 0, fat = 0, carbs = 0;
        foreach (var m in items)
        {
            kcal += m.Kcal;
            protein += m.Protein;
            fat += m.Fat;
            carbs += m.Carbs;
        }
        return new MacrosDto { Kcal = kcal, Protein = protein, Fat = fat, Carbs = carbs };
    }

    public static BmiDto Bmi(double weight, double height)
    {
        var h2 = height / 100 * (height / 100);
        var value = weight / h2;
        var label = value switch
        {
            < 18.5 => "Дефицит массы",
            < 25 => "Норма",
            < 30 => "Избыточная масса",
            _ => "Ожирение",
        };
        return new BmiDto(Math.Round(value, 1), label, Math.Round(18.5 * h2, 1), Math.Round(24.9 * h2, 1));
    }
}
