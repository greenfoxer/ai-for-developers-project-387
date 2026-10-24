using CallCalendar.Api.Domain;
using CallCalendar.Api.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace CallCalendar.Api.Implementation;

/// Реализация сгенерированного IEventTypesController (NSwag partial-контроллер делегирует сюда).
/// Бизнес-правила: T3 (рабочие часы), T4 (сетка слотов), T2 (пересечение), T5 (поля/дубли).
public sealed class EventTypesImplementation(
    IEventTypeStore eventTypes,
    IBookingStore bookings) : IEventTypesController
{
    public async Task<ActionResult<ICollection<EventType>>> ListAsync()
    {
        var entities = await eventTypes.ListAsync(CancellationToken.None);
        ICollection<EventType> dtos = entities.Select(ToDto).ToList();
        return new OkObjectResult(dtos);
    }

    public async Task<ActionResult<EventType>> CreateAsync(NewEventType body)
    {
        // См. BookingsImplementation: некорректное/отсутствующее тело — 400 (T8).
        if (body is null)
        {
            return ApiProblem.Create(400, "Validation error", "Тело запроса отсутствует или некорректно.");
        }

        if (string.IsNullOrWhiteSpace(body.Name) || body.DurationMinutes <= 0)
        {
            return ApiProblem.Create(400, "Validation error", "name непустой и durationMinutes > 0 обязательны (T5).");
        }

        if (await eventTypes.NameExistsAsync(body.Name, CancellationToken.None))
        {
            // Дубль названия — 409 с общим кодом занятости (T8 Q3), различается detail.
            return ApiProblem.Create(409, "Conflict", "Тип события с таким названием уже существует (T5).");
        }

        var entity = new EventTypeEntity
        {
            Name = body.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(body.Description) ? null : body.Description,
            DurationMinutes = body.DurationMinutes,
        };
        await eventTypes.AddAsync(entity, CancellationToken.None);
        return new CreatedResult($"/api/event-types/{entity.Id}", ToDto(entity));
    }

    public async Task<ActionResult<ICollection<string>>> AvailabilityAsync(int id, DateTimeOffset date)
    {
        var type = await eventTypes.FindAsync(id, CancellationToken.None);
        if (type is null)
        {
            return ApiProblem.Create(404, "Not found", $"Тип события {id} не найден.");
        }

        var day = date.Date; // календарный день запроса (wall-clock MSK, T8 Q2)
        var now = MskClock.NowWall();

        // Сетка стартов: якорь 09:00, шаг = durationMinutes; слот предлагается,
        // если start+D <= 18:00 и start > now (T3/T4). Верхней границы окна нет (#4).
        var existing = await bookings.ListAllWithTypesAsync(CancellationToken.None);
        var starts = new List<string>();
        for (var start = day.DayStart(); start.AddMinutes(type.DurationMinutes) <= day.DayEnd(); start = start.AddMinutes(type.DurationMinutes))
        {
            if (start <= now)
            {
                continue;
            }

            var endWall = start.AddMinutes(type.DurationMinutes);
            var free = !existing.Any(b =>
            {
                var bStart = MskClock.ToWall(b.StartUtc);
                var bEnd = bStart.AddMinutes(b.EventType!.DurationMinutes);
                // Полуоткрытые интервалы, пересечение на минуту и более; стык в стык разрешён (T2).
                return bStart < endWall && start < bEnd;
            });

            if (free)
            {
                starts.Add(MskClock.Format(start));
            }
        }

        ICollection<string> result = starts;
        return new OkObjectResult(result);
    }

    private static EventType ToDto(EventTypeEntity e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Description = e.Description,
        DurationMinutes = e.DurationMinutes,
    };

}
