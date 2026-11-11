namespace Metabolix.Api.Models;

/// <summary>отметки удаления сохраняем для синхронизации между устройствами</summary>
public interface ISyncable
{
    /// <summary>время правки на клиенте для разрешения конфликтов</summary>
    DateTime UpdatedAt { get; set; }

    /// <summary>время получения правки сервером для курсора синхронизации</summary>
    DateTime SyncedAt { get; set; }

    bool IsDeleted { get; set; }
}

public class DiaryEntry : ISyncable
{
    /// <summary>клиентский id защищает от дублей при повторной отправке</summary>
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
