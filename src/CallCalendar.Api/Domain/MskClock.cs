using System.Globalization;

namespace CallCalendar.Api.Domain;

/// Wall-clock Europe/Moscow (T8 Q2): контракт оперирует строками «2026-10-21T14:30» без offset,
/// хранилище — UTC; конверсия только здесь, на границах. MSK = UTC+3 без DST (фиксирован в T8).
public static class MskClock
{
    public const string WallFormat = "yyyy-MM-ddTHH:mm";

    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");

    /// Текущее wall-clock MSK (границы now/окна считаются по нему).
    public static DateTime NowWall() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

    /// Wall-clock MSK -> UTC для хранения.
    public static DateTime ToUtc(DateTime mskWall) =>
        TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(mskWall, DateTimeKind.Unspecified), Zone, TimeZoneInfo.Utc);

    /// UTC из хранилища -> wall-clock MSK.
    public static DateTime ToWall(DateTime utc) =>
        TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(utc, DateTimeKind.Unspecified), TimeZoneInfo.Utc, Zone);

    public static string Format(DateTime mskWall) => mskWall.ToString(WallFormat, CultureInfo.InvariantCulture);

    public static bool TryParseWall(string? value, out DateTime mskWall) =>
        DateTime.TryParseExact(value, WallFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out mskWall);
}

/// Рабочие границы дня (T3): 09:00–18:00 MSK, выходные не исключаются.
/// Верхней границы окна (14 дней) больше нет: по issue #4 запись доступна на любую будущую дату.
public static class BookingWindow
{
    public const int WorkStartHour = 9;
    public const int WorkEndHour = 18;

    public static DateTime DayStart(this DateTime date) => date.Date.AddHours(WorkStartHour);

    public static DateTime DayEnd(this DateTime date) => date.Date.AddHours(WorkEndHour);
}
