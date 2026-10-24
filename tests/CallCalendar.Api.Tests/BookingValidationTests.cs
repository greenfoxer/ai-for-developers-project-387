using System.Net;
using System.Net.Http.Json;

namespace CallCalendar.Api.Tests;

/// Валидация создания Записи (T6/T8): поля гостя, формат времени, сетка/окно старта,
/// неизвестный тип события — 400/404 в problem+json; некорректное тело — 400, не 500.
public class BookingValidationTests
{
    [Fact]
    public async Task Empty_name_is_rejected_with_400()
    {
        using var app = new ApiTestApp();
        var tomorrow = ApiTestApp.Day(1);

        var response = await app.BookAsync(ApiTestApp.ThirtyMinTypeId, tomorrow, 10, 0, name: "  ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Invalid_email_is_rejected_with_400()
    {
        using var app = new ApiTestApp();
        var tomorrow = ApiTestApp.Day(1);

        var response = await app.BookAsync(ApiTestApp.ThirtyMinTypeId, tomorrow, 10, 0, email: "не-email");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Start_off_the_type_grid_is_rejected_with_400()
    {
        using var app = new ApiTestApp();
        var tomorrow = ApiTestApp.Day(1);

        // 30-минутный тип: сетка 09:00, 09:30… — 10:10 вне сетки.
        var response = await app.BookAsync(ApiTestApp.ThirtyMinTypeId, tomorrow, 10, 10);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Start_outside_working_hours_is_rejected_with_400()
    {
        using var app = new ApiTestApp();
        var tomorrow = ApiTestApp.Day(1);

        // 17:45 для 30-минутного типа: не влезает в 18:00.
        var response = await app.BookAsync(ApiTestApp.ThirtyMinTypeId, tomorrow, 17, 45);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Past_start_is_rejected_with_400()
    {
        using var app = new ApiTestApp();

        // Вчера в 10:00 — формально на сетке, но в прошлом (T3) → 400.
        var response = await app.BookAsync(ApiTestApp.ThirtyMinTypeId, ApiTestApp.Day(-1), 10, 0);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Malformed_wall_clock_start_is_rejected_with_400()
    {
        using var app = new ApiTestApp();

        var response = await app.Client.PostAsJsonAsync("/api/bookings", new
        {
            eventTypeId = ApiTestApp.ThirtyMinTypeId,
            start = "завтра в десять",
            name = "Гость",
            email = "guest@example.test",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Missing_or_null_body_is_400_not_500()
    {
        using var app = new ApiTestApp();
        // Старт в окне и на сетке: если null в optional-поле почему-то пройдёт биндинг,
        // запись вернёт 201 и тест это увидит (контракт: optional, но не null).
        var validStart = ApiTestApp.Wall(ApiTestApp.Day(1), 10, 0);

        var empty = await app.Client.PostAsync("/api/bookings",
            new StringContent("null", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        var explicitNull = await app.Client.PostAsync("/api/bookings",
            new StringContent($$"""{"eventTypeId":1,"start":"{{validStart}}","name":"Гость","email":"g@e.test","additionalInfo":null}""",
                System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, explicitNull.StatusCode);
    }

    [Fact]
    public async Task Booking_for_unknown_event_type_is_404()
    {
        using var app = new ApiTestApp();
        var tomorrow = ApiTestApp.Day(1);

        var response = await app.BookAsync(999, tomorrow, 10, 0);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Availability_for_unknown_event_type_is_404()
    {
        using var app = new ApiTestApp();
        var date = ApiTestApp.Day(1).ToString("yyyy-MM-dd");

        var response = await app.Client.GetAsync($"/api/event-types/999/availability?date={date}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
