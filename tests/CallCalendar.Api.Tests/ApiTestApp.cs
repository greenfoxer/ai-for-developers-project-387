using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CallCalendar.Api.Tests;

/// Поднимает CallCalendar.Api на HTTP-шве (WebApplicationFactory) с изолированной
/// SQLite-базой во временной папке (файл на инстанс, EnsureCreated + сид выполняется
/// точкой входа приложения). Время в тестах считается от фактического «сейчас» по MSK —
/// helper независим от MskClock приложения: своя конверсия через TimeZoneInfo.
public sealed class ApiTestApp : IDisposable
{
    public const string WallFormat = "yyyy-MM-ddTHH:mm";

    /// Идентификаторы сид-демо типов событий (Program.cs: создание + просмотр).
    public const int ThirtyMinTypeId = 1;
    public const int SixtyMinTypeId = 2;

    // Как реальный Kiota-клиент: пропущенные optional-поля не отправляются null'ом.
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly TimeZoneInfo Msk = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
    private readonly string _dbPath;
    private readonly WebApplicationFactory<Program> _factory;

    public ApiTestApp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "callcalendar-tests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(Microsoft.AspNetCore.Hosting.WebHostDefaults.EnvironmentKey, "Development");
            builder.UseSetting("ConnectionStrings:sqlite", $"Data Source={_dbPath}");
        });
        Client = _factory.CreateClient();
    }

    public HttpClient Client { get; }

    /// Календарный день (MSK wall-clock) относительно сегодня.
    public static DateTime Day(int offsetFromToday) => NowWall().Date.AddDays(offsetFromToday);

    /// «Сейчас» по Москве — независимая конверсия из UTC.
    public static DateTime NowWall() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Msk);

    public static string Wall(DateTime mskDay, int hour, int minute) =>
        mskDay.Date.AddHours(hour).AddMinutes(minute).ToString(WallFormat, CultureInfo.InvariantCulture);

    public void Dispose()
    {
        _factory.Dispose();
        foreach (var suffix in new[] { "", "-shm", "-wal", "-journal" })
        {
            try
            {
                File.Delete(_dbPath + suffix);
            }
            catch (IOException)
            {
                // файл мог не создаться — неважно
            }
        }
    }

    // --- helpers через контрактные маршруты ---

    public async Task<List<string>> AvailabilityAsync(int eventTypeId, DateTime mskDay)
    {
        var date = mskDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var response = await Client.GetAsync($"/api/event-types/{eventTypeId}/availability?date={date}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<string>>(Json))!;
    }

    public Task<HttpResponseMessage> BookAsync(int eventTypeId, DateTime mskDay, int hour, int minute, string name = "Гость", string email = "guest@example.test", string? additionalInfo = null) =>
        Client.PostAsJsonAsync("/api/bookings", new
        {
            eventTypeId,
            start = Wall(mskDay, hour, minute),
            name,
            email,
            additionalInfo,
        }, Json);

    public Task<HttpResponseMessage> CancelAsync(int bookingId) =>
        Client.PostAsync($"/api/bookings/{bookingId}/cancel", content: null);

    public Task<HttpResponseMessage> CreateEventTypeAsync(string name, int durationMinutes, string? description = null) =>
        Client.PostAsJsonAsync("/api/event-types", new { name, description, durationMinutes }, Json);
}
