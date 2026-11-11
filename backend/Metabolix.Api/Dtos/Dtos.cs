using System.ComponentModel.DataAnnotations;
using Metabolix.Api.Models;

namespace Metabolix.Api.Dtos;

// required поля обязательны в запросе иначе сервер вернёт 400

public class RegisterRequest
{
    [EmailAddress(ErrorMessage = "Некорректная почта"), MaxLength(254)]
    public required string Email { get; init; }

    [MinLength(6, ErrorMessage = "Пароль не короче 6 символов"), MaxLength(128)]
    public required string Password { get; init; }

    [MaxLength(100)]
    public string? Name { get; init; }

    /// <summary>согласие на обработку персональных данных</summary>
    public required bool AcceptedTerms { get; init; }
}

public class LoginRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
}

public class GoogleLoginRequest
{
    public required string IdToken { get; init; }
    public bool AcceptedTerms { get; init; }
}

public class RefreshRequest
{
    public required string RefreshToken { get; init; }
}

public class ForgotPasswordRequest
{
    [EmailAddress(ErrorMessage = "Некорректная почта")]
    public required string Email { get; init; }
}

public class ResetPasswordRequest
{
    public required string Email { get; init; }

    [RegularExpression(@"^\d{6}$", ErrorMessage = "Код состоит из 6 цифр")]
    public required string Code { get; init; }

    [MinLength(6, ErrorMessage = "Пароль не короче 6 символов"), MaxLength(128)]
    public required string NewPassword { get; init; }
}

public class DeleteAccountRequest
{
    /// <summary>нужен для аккаунтов с паролем</summary>
    public string? Password { get; init; }
}

public record UserDto(Guid Id, string Email, string Name, string? AvatarUrl, bool HasProfile);
public record AuthResponse(string AccessToken, string RefreshToken, int ExpiresIn, UserDto User);

public class ProfileDto
{
    [MaxLength(100)]
    public string? Name { get; init; }
    public required Sex Sex { get; init; }
    public required DateOnly BirthDate { get; init; }

    [Range(100, 250, ErrorMessage = "Рост от 100 до 250 см")]
    public required double Height { get; init; }

    [Range(20, 400, ErrorMessage = "Вес от 20 до 400 кг")]
    public required double Weight { get; init; }

    [Range(20, 400, ErrorMessage = "Целевой вес от 20 до 400 кг")]
    public required double TargetWeight { get; init; }

    [Range(1.2, 1.9, ErrorMessage = "Коэффициент активности от 1,2 до 1,9")]
    public required double Activity { get; init; }
    public required Goal Goal { get; init; }

    /// <summary>дату для записи веса передаёт клиент с учётом своего часового пояса</summary>
    public DateOnly? Today { get; init; }
}

public class MacrosDto
{
    [Range(0, 20000)] public required double Kcal { get; init; }
    [Range(0, 2000)] public required double Protein { get; init; }
    [Range(0, 2000)] public required double Fat { get; init; }
    [Range(0, 2000)] public required double Carbs { get; init; }
}

public record BmiDto(double Value, string Label, double NormFromKg, double NormToKg);
public record NormDto(double Bmr, double Tdee, MacrosDto Auto, MacrosDto Targets, bool Manual, BmiDto Bmi);

public class RatioDto
{
    [Range(0, 100)] public required int Protein { get; init; }
    [Range(0, 100)] public required int Fat { get; init; }
    [Range(0, 100)] public required int Carbs { get; init; }
}

public class RemindersDto
{
    public required bool Meal { get; init; }
    public required bool Water { get; init; }
    public required bool Weight { get; init; }

    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Время в формате ЧЧ:ММ")]
    public required string Time { get; init; }
}

public class SettingsDto
{
    public required Theme Theme { get; init; }
    public required RatioDto Ratio { get; init; }
    public MacrosDto? ManualTargets { get; init; }

    [Range(500, 10000, ErrorMessage = "Норма воды от 500 до 10000 мл")]
    public required int WaterGoal { get; init; }
    public required RemindersDto Reminders { get; init; }
}

/// <summary>общий формат продукта для справочника дневника и рецептов</summary>
public class FoodDto
{
    [MaxLength(64)] public string? Id { get; init; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "Укажите название"), MaxLength(200)]
    public required string Name { get; init; }

    [Range(0, 1000)] public required double Kcal { get; init; }
    [Range(0, 100)] public required double Protein { get; init; }
    [Range(0, 100)] public required double Fat { get; init; }
    [Range(0, 100)] public required double Carbs { get; init; }
    public FoodUnit Unit { get; init; }
    [Range(0.1, 20000)] public double? Portion { get; init; }
    [RegularExpression(@"^\d{8,14}$", ErrorMessage = "Штрихкод — от 8 до 14 цифр")] public string? Barcode { get; init; }
    public string? ImageUrl { get; init; }
    public bool Custom { get; init; }
}

public record PageDto<T>(List<T> Items, int Page, int PageSize, int Total);
public record FavoritesDto(List<FoodDto> Foods, List<RecipeDto> Recipes);

public class RecipeItemDto
{
    public required FoodDto Food { get; init; }
    [Range(0.1, 20000)] public required double Grams { get; init; }
}

public class RecipeRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Укажите название"), MaxLength(200)]
    public required string Name { get; init; }

    [MinLength(1, ErrorMessage = "Добавьте хотя бы один продукт"), MaxLength(100)]
    public required List<RecipeItemDto> Items { get; init; }
}

public record RecipeDto(Guid Id, string Name, string? ImageUrl, List<RecipeItemDto> Items,
    double Weight, MacrosDto Total, FoodDto AsFood);

public class CreateEntryRequest
{
    /// <summary>клиентский id защищает от дублей при повторной отправке</summary>
    public Guid? Id { get; init; }
    public required DateOnly Date { get; init; }
    public required MealType Meal { get; init; }
    public required FoodDto Food { get; init; }
    [Range(0.1, 20000)] public required double Grams { get; init; }
}

public class UpdateEntryRequest
{
    [Range(0.1, 20000)] public required double Grams { get; init; }
    public MealType? Meal { get; init; }
    public DateOnly? Date { get; init; }
}

public class CopyMealRequest
{
    public required DateOnly From { get; init; }
    public required MealType Meal { get; init; }
    public required DateOnly To { get; init; }
}

public record EntryDto(Guid Id, DateOnly Date, MealType Meal, FoodDto Food, double Grams, MacrosDto Macros, DateTime UpdatedAt);
public record DayDto(DateOnly Date, List<EntryDto> Entries, MacrosDto Total, MacrosDto? Targets, double? KcalLeft, int WaterMl, int WaterGoal);

public class SetWeightRequest
{
    [Range(20, 400, ErrorMessage = "Вес от 20 до 400 кг")] public required double Kg { get; init; }
}

public class SetWaterRequest
{
    [Range(0, 30000)] public required int Ml { get; init; }
}

public record WeightDto(DateOnly Date, double Kg);
public record WaterDto(DateOnly Date, int Ml);
public record DayStatsDto(DateOnly Date, double Kcal, double Protein, double Fat, double Carbs);
public record StatsDto(DateOnly From, DateOnly To, List<DayStatsDto> Days, MacrosDto Average, int LoggedDays, int TotalDays,
    List<WeightDto> Weights, MacrosDto? Targets, BmiDto? Bmi);

public class SyncDiaryItem
{
    public required Guid Id { get; init; }
    public required DateOnly Date { get; init; }
    public required MealType Meal { get; init; }
    public required FoodDto Food { get; init; }
    [Range(0.1, 20000)] public required double Grams { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public bool IsDeleted { get; init; }
}

public class SyncWeightItem
{
    public required DateOnly Date { get; init; }
    [Range(20, 400)] public required double Kg { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public bool IsDeleted { get; init; }
}

public class SyncWaterItem
{
    public required DateOnly Date { get; init; }
    [Range(0, 30000)] public required int Ml { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public bool IsDeleted { get; init; }
}

public class SyncPushRequest
{
    [MaxLength(2000)] public List<SyncDiaryItem> Diary { get; init; } = [];
    [MaxLength(2000)] public List<SyncWeightItem> Weights { get; init; } = [];
    [MaxLength(2000)] public List<SyncWaterItem> Water { get; init; } = [];
}

/// <param name="Applied">число принятых изменений</param>
/// <param name="Skipped">число пропусков из-за более новой версии на сервере</param>
/// <param name="Rejected">id чужих записей</param>
public record SyncPushResult(int Applied, int Skipped, List<Guid> Rejected);

/// <param name="Cursor">курсор для следующего запроса изменений</param>
public record SyncPullResult(List<SyncDiaryItem> Diary, List<SyncWeightItem> Weights, List<SyncWaterItem> Water, DateTime Cursor);
