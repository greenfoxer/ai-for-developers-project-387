using System.Net.Mail;
using CallCalendar.Api.Domain;
using CallCalendar.Api.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace CallCalendar.Api.Implementation;

/// Реализация сгенерированного IBookingsController (T2/T6/T7).
public sealed class BookingsImplementation(
    IEventTypeStore eventTypes,
    IBookingStore bookings) : IBookingsController
{
    public async Task<ActionResult<Booking>> CreateAsync(NewBooking body)
    {
        // Тело отсутствует/не разобрано (Required.DisallowNull у optional-полей) — 400, а не 500.
        if (body is null)
        {
            return ApiProblem.Create(400, "Validation error", "Тело запроса отсутствует или некорректно.");
        }

        if (string.IsNullOrWhiteSpace(body.Name) || string.IsNullOrWhiteSpace(body.Email) || !IsEmail(body.Email))
        {
            return ApiProblem.Create(400, "Validation error", "name и email (валидный) обязательны (T6).");
        }

        if (!MskClock.TryParseWall(body.Start, out var startWall))
        {
            return ApiProblem.Create(400, "Validation error", "start — wall-clock MSK «yyyy-MM-ddTHH:mm» (T8 Q2).");
        }

        var type = await eventTypes.FindAsync(body.EventTypeId, CancellationToken.None);
        if (type is null)
        {
            return ApiProblem.Create(404, "Not found", $"Тип события {body.EventTypeId} не найден.");
        }

        // Старт обязан быть на 30-минутной сетке, в рабочем дне, в окне 14 дней, строго после now (T3/T4) — иначе 400 (T8).
        var now = MskClock.NowWall();
        var day = startWall.Date;
        var inWindow = day >= now.Date && day <= now.LastWindowDay();
        var onGrid = startWall >= day.DayStart()
            && startWall.AddMinutes(type.DurationMinutes) <= day.DayEnd()
            && (startWall - day.DayStart()).TotalMinutes % BookingWindow.SlotStepMinutes == 0
            && startWall > now
            && inWindow;
        if (!onGrid)
        {
            return ApiProblem.Create(400, "Validation error", "start вне сетки слотов, рабочего дня или окна записи (T3/T4).");
        }

        var startUtc = MskClock.ToUtc(startWall);
        var endUtc = startUtc.AddMinutes(type.DurationMinutes);
        var entity = new BookingEntity
        {
            EventTypeId = type.Id,
            EventType = type,
            StartUtc = startUtc,
            GuestName = body.Name.Trim(),
            GuestEmail = body.Email.Trim(),
            AdditionalInfo = string.IsNullOrWhiteSpace(body.AdditionalInfo) ? null : body.AdditionalInfo,
        };

        // Пересечение полуоткрытых интервалов проверяется в транзакции записи; проигравший — 409 (T2).
        var created = await bookings.TryCreateIfFreeAsync(entity, endUtc, CancellationToken.None);
        if (created is null)
        {
            return ApiProblem.Create(409, "Conflict", "Слот занят: пересечение с существующей записью (T2).");
        }

        return new CreatedResult("/api/bookings", ToDto(created, type));
    }

    public async Task<ActionResult<ICollection<Booking>>> UpcomingAsync()
    {
        var nowUtc = DateTime.UtcNow;
        var entities = await bookings.ListUpcomingAsync(nowUtc, CancellationToken.None);
        ICollection<Booking> dtos = entities.Select(b => ToDto(b, b.EventType!)).ToList();
        return new OkObjectResult(dtos);
    }

    private static Booking ToDto(BookingEntity b, EventTypeEntity type)
    {
        var startWall = MskClock.ToWall(b.StartUtc);
        return new Booking
        {
            Id = b.Id,
            EventTypeId = b.EventTypeId,
            EventTypeName = type.Name,
            Start = MskClock.Format(startWall),
            End = MskClock.Format(startWall.AddMinutes(type.DurationMinutes)), // конец производный (T6)
            Name = b.GuestName,
            Email = b.GuestEmail,
            AdditionalInfo = b.AdditionalInfo,
        };
    }

    private static bool IsEmail(string value)
    {
        try
        {
            _ = new MailAddress(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

}
