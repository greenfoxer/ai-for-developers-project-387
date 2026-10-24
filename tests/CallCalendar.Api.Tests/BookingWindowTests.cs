using System.Net;

namespace CallCalendar.Api.Tests;

/// Окно записи (T3): прошедшие старты отсекаются строго после now (MSK); выходные не исключаются.
/// Верхней границы окна (14 дней) больше нет (#4): запись доступна и на далёкие будущие даты.
public class BookingWindowTests
{
    [Fact]
    public async Task Far_future_day_is_within_window()
    {
        using var app = new ApiTestApp();

        var slots = await app.AvailabilityAsync(ApiTestApp.ThirtyMinTypeId, ApiTestApp.Day(30));

        Assert.NotEmpty(slots);
    }

    [Fact]
    public async Task Far_future_booking_is_accepted()
    {
        // Arrange
        using var app = new ApiTestApp();

        // Act
        var response = await app.BookAsync(ApiTestApp.ThirtyMinTypeId, ApiTestApp.Day(30), 10, 0);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Past_day_offers_nothing()
    {
        using var app = new ApiTestApp();

        var slots = await app.AvailabilityAsync(ApiTestApp.ThirtyMinTypeId, ApiTestApp.Day(-1));

        Assert.Empty(slots);
    }

    [Fact]
    public async Task Today_offers_only_starts_strictly_after_now()
    {
        using var app = new ApiTestApp();
        var nowWall = ApiTestApp.NowWall();

        var slots = await app.AvailabilityAsync(ApiTestApp.ThirtyMinTypeId, ApiTestApp.Day(0));

        // Временной интервал узкий (runner-часы), но любой предложенный старт — строго «после now».
        foreach (var slot in slots)
        {
            var parsed = DateTime.ParseExact(slot, ApiTestApp.WallFormat, System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(parsed > nowWall, $"слот {slot} не позже now {nowWall:O}");
        }
    }

    [Fact]
    public async Task Saturday_is_a_working_day()
    {
        using var app = new ApiTestApp();
        // Первый будущий выходной (суббота или воскресенье) — тоже должен иметь слоты.
        var day = ApiTestApp.Day(1);
        while (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
        {
            day = day.AddDays(1);
        }

        var slots = await app.AvailabilityAsync(ApiTestApp.ThirtyMinTypeId, day);

        Assert.NotEmpty(slots);
    }
}
