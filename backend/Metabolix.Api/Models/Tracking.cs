namespace Metabolix.Api.Models;

/// <summary>
/// Поля для офлайн-синхронизации. Записи не удаляются физически, иначе другое устройство
/// не узнает об удалении.
/// </summary>
public interface ISyncable
{
    /// <summary>Когда запись изменили на клиенте. По нему решаются конфликты: новее — побеждает.</summary>
    DateTime UpdatedAt { get; set; }

    /// <summary>Когда изменение дошло до сервера. По нему клиент забирает новое (курсор pull).</summary>
    DateTime SyncedAt { get; set; }

    bool IsDeleted { get; set; }
}

public class DiaryEntry : ISyncable
{
    /// <summary>Генерирует клиент: так повторная отправка той же записи не создаст дубль.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public MealType Meal { get; set; }
    public FoodSnapshot Food { get; set; } = new();
    public double Grams { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime SyncedAt { get; set; }
    public bool IsDeleted { get; set; }
}

public class WeightRecord : ISyncable
{
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public double Kg { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime SyncedAt { get; set; }
    public bool IsDeleted { get; set; }
}

public class WaterRecord : ISyncable
{
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public int Ml { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime SyncedAt { get; set; }
    public bool IsDeleted { get; set; }
}
