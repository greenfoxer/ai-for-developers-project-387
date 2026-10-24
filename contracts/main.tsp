import "@typespec/http";
import "@typespec/openapi";
import "@typespec/openapi3";
import "./models.tsp";
import "./errors.tsp";

using TypeSpec.Http;
using TypeSpec.OpenAPI;

/// API сервиса бронирования календаря (аналог cal.com), курс Hexlet «AI for Developers».
/// Время везде — wall-clock Europe/Moscow (MSK, UTC+3 без DST) в ISO 8601 без offset,
/// минутная точность («2026-10-21T14:30»), зона зафиксирована этим описанием (T8 Q2).
/// Рабочие часы 09:00–18:00, запись доступна на любую будущую дату (T3).
/// Конфликт записи — пересечение полуоткрытых интервалов [start, end) на минуту и более (T2).
@doc("API сервиса бронирования календаря. Время — wall-clock Europe/Moscow (MSK) в ISO 8601 без offset (T8 Q2).")
@service(#{ title: "CallCalendar API" })
@info(#{ version: "1.0.0" })
@server("http://localhost:5000", "Разработка (dev)")
namespace CallCalendar.Api;

/// Типы событий: гостевая витрина и админ-список читают одну операцию (T7).
@tag("EventTypes")
@route("/api/event-types")
interface EventTypes {
  @doc("Список всех типов событий (одна операция для гостя и админа, T7/T8).")
  @get
  list(): EventType[];

  @doc("Создать тип события (T5/T7). name уникален без учёта регистра/пробелов — дубль 409 (T5/T8).")
  @post
  create(@body newEventType: NewEventType):
    {
      @statusCode statusCode: 201;
      @body body: EventType;
    }
    | BadRequest
    | Conflict;

  @doc("Свободные старты слотов типа на дату — список wall-clock datetime «2026-10-21T09:00» (T8). Сервер считает один раз: шаг сетки = durationMinutes, якорь 09:00, старты с start+D<=18:00 (T4); любая будущая дата, сегодняшние старты строго после now по MSK (T3); свободен = нет пересечения полуоткрытых интервалов с записями (T2). Нет такого типа — 404.")
  @get
  @route("/{id}/availability")
  availability(@path id: int32, @query date: plainDate): wallClockDateTime[] | NotFound;
}

/// Записи: гость создаёт запись без аккаунта (T6); отмены/редактирования в v1 нет (T7).
@tag("Bookings")
@route("/api/bookings")
interface Bookings {
  @doc("Создать запись (T6/T8). Старт вне сетки/рабочих часов или некорректные поля — 400; нет такого типа — 404; слот занят (пересечение T2) — 409.")
  @post
  create(@body newBooking: NewBooking):
    {
      @statusCode statusCode: 201;
      @body body: Booking;
    }
    | BadRequest
    | NotFound
    | Conflict;

  @doc("Предстоящие встречи всех типов одним списком: старт строго после now (MSK), по возрастанию старта, без пагинации (T7).")
  @get
  upcoming(): Booking[];
}
