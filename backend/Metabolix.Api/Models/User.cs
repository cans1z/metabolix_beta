namespace Metabolix.Api.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public string Name { get; set; } = "";

    /// <summary>null — аккаунт создан через Google и пароля пока нет.</summary>
    public string? PasswordHash { get; set; }
    public string? GoogleId { get; set; }
    public string? AvatarUrl { get; set; }

    /// <summary>Когда пользователь согласился на обработку персональных данных (152-ФЗ).</summary>
    public DateTime ConsentAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Анкета. Появляется после первого входа, отсюда считается норма КБЖУ.</summary>
public class Profile
{
    public Guid UserId { get; set; }
    public Sex Sex { get; set; }
    public DateOnly BirthDate { get; set; }
    public double Height { get; set; }
    public double Weight { get; set; }
    public double TargetWeight { get; set; }
    public double Activity { get; set; }
    public Goal Goal { get; set; }
}

public class UserSettings
{
    public Guid UserId { get; set; }
    public Theme Theme { get; set; } = Theme.System;
    public int RatioProtein { get; set; } = 30;
    public int RatioFat { get; set; } = 30;
    public int RatioCarbs { get; set; } = 40;

    /// <summary>Ручная норма. Если задана — заменяет расчётную.</summary>
    public double? ManualKcal { get; set; }
    public double? ManualProtein { get; set; }
    public double? ManualFat { get; set; }
    public double? ManualCarbs { get; set; }

    public int WaterGoal { get; set; } = 2000;
    public bool RemindMeal { get; set; } = true;
    public bool RemindWater { get; set; }
    public bool RemindWeight { get; set; }
    public string RemindTime { get; set; } = "09:00";
}
