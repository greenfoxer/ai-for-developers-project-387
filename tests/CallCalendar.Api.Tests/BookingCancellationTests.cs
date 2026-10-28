using System.Net;
using System.Net.Http.Json;

namespace CallCalendar.Api.Tests;

/// Отмена записей: запись уходит из предстоящих в отменённые, слот освобождается (T2)
/// для availability и новых записей; повторная отмена — 409, неизвестный id — 404.
public class BookingCancellationTests
{
    [Fact]
    public async Task Cancel_moves_booking_from_upcoming_to_cancelled()
    {
        using var app = new ApiTestApp();
        var tomorrow = ApiTestApp.Day(1);
        var created = await app.BookAsync(ApiTestApp.ThirtyMinTypeId, tomorrow, 10, 0);
        var booking = await created.Content.ReadFromJsonAsync<Booking>(ApiTestApp.Json);

        // Act: отмена запланированной записи.
        var cancelResponse = await app.CancelAsync(booking!.Id);

        // Assert: 200, в предстоящих пусто, в отменённых ровно эта запись.
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<Booking>(ApiTestApp.Json);
        Assert.Equal(booking.Id, cancelled!.Id);

        var upcoming = await app.Client.GetFromJsonAsync<List<Booking>>("/api/bookings", ApiTestApp.Json);
        Assert.Empty(upcoming!);

        var cancelledList = await app.Client.GetFromJsonAsync<List<Booking>>("/api/bookings/cancelled", ApiTestApp.Json);
        var item = Assert.Single(cancelledList!);
        Assert.Equal(booking.Id, item.Id);
        Assert.Equal(ApiTestApp.Wall(tomorrow, 10, 0), item.Start);
        Assert.Equal(ApiTestApp.Wall(tomorrow, 10, 30), item.End);
    }

    [Fact]
    public async Task Cancelled_slot_is_available_and_bookable_again()
    {
        using var app = new ApiTestApp();
        var tomorrow = ApiTestApp.Day(1);
        var created = await app.BookAsync(ApiTestApp.ThirtyMinTypeId, tomorrow, 11, 0);
        var booking = await created.Content.ReadFromJsonAsync<Booking>(ApiTestApp.Json);
        Assert.DoesNotContain(ApiTestApp.Wall(tomorrow, 11, 0), await app.AvailabilityAsync(ApiTestApp.ThirtyMinTypeId, tomorrow));

        // Act: отмена освобождает слот.
        await app.CancelAsync(booking!.Id);

        // Assert: слот снова виден в availability и в него можно записаться (T2).
        Assert.Contains(ApiTestApp.Wall(tomorrow, 11, 0), await app.AvailabilityAsync(ApiTestApp.ThirtyMinTypeId, tomorrow));
        var rebooked = await app.BookAsync(ApiTestApp.ThirtyMinTypeId, tomorrow, 11, 0);
        Assert.Equal(HttpStatusCode.Created, rebooked.StatusCode);
    }

    [Fact]
    public async Task Overlapping_slot_of_another_type_is_free_after_cancellation()
    {
        using var app = new ApiTestApp();
        var tomorrow = ApiTestApp.Day(1);

        // Arrange: 60-минутная запись 12:00–13:00 отменяется — интервал перестаёт блокировать (T2).
        var created = await app.BookAsync(ApiTestApp.SixtyMinTypeId, tomorrow, 12, 0);
        var booking = await created.Content.ReadFromJsonAsync<Booking>(ApiTestApp.Json);
        Assert.Equal(HttpStatusCode.Conflict, (await app.BookAsync(ApiTestApp.ThirtyMinTypeId, tomorrow, 12, 30)).StatusCode);

        // Act + Assert: после отмены запись внутри освободившегося интервала проходит.
        await app.CancelAsync(booking!.Id);
        Assert.Equal(HttpStatusCode.Created, (await app.BookAsync(ApiTestApp.ThirtyMinTypeId, tomorrow, 12, 30)).StatusCode);
    }

    [Fact]
    public async Task Second_cancel_returns_409_problem_json()
    {
        using var app = new ApiTestApp();
        var tomorrow = ApiTestApp.Day(1);
        var created = await app.BookAsync(ApiTestApp.ThirtyMinTypeId, tomorrow, 14, 0);
        var booking = await created.Content.ReadFromJsonAsync<Booking>(ApiTestApp.Json);

        // Act: повторная отмена той же записи.
        await app.CancelAsync(booking!.Id);
        var second = await app.CancelAsync(booking.Id);

        // Assert: 409 с полным телом problem+json, запись остаётся в отменённых в единственном экземпляре.
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
        var problem = await second.Content.ReadFromJsonAsync<ProblemDetails>(ApiTestApp.Json);
        Assert.Equal(409, problem!.Status);
        var cancelledList = await app.Client.GetFromJsonAsync<List<Booking>>("/api/bookings/cancelled", ApiTestApp.Json);
        Assert.Single(cancelledList!);
    }

    [Fact]
    public async Task Cancel_of_unknown_booking_returns_404()
    {
        using var app = new ApiTestApp();

        var response = await app.CancelAsync(9999);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
