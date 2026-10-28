namespace CallCalendar.Api.Domain;

/// Сущность типа события (T5): id — автонумерация, name уникален (проверка нормализацией),
/// durationMinutes > 0. Типы неизменяемы (T7) — правок/удалений нет.
public sealed class EventTypeEntity
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public int DurationMinutes { get; set; }
}

/// Жизненный цикл записи: запланированная занимает слот (T2), отменённая слот освобождает
/// и остаётся видимой в списке отменённых; удаления записей нет.
public enum BookingStatus
{
    Scheduled,
    Cancelled,
}

/// Сущность записи (T6): старт храним в UTC (практика T8 Q2), конец производный;
/// контакты гостя плоские, email не уникален.
public sealed class BookingEntity
{
    public int Id { get; set; }

    public int EventTypeId { get; set; }

    public EventTypeEntity? EventType { get; set; }

    /// <summary>Старт в UTC; wall-clock MSK показывается только на границе контракта.</summary>
    public DateTime StartUtc { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Scheduled;

    public required string GuestName { get; set; }

    public required string GuestEmail { get; set; }

    public string? AdditionalInfo { get; set; }
}
