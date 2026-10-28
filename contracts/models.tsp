import "@typespec/http";

using TypeSpec.Http;

namespace CallCalendar.Api;

/// Время в контракте — wall-clock Europe/Moscow без offset, минутная точность (T8 Q2):
/// «2026-10-21T14:30». Зона зафиксирована описанием сервиса, смещение UTC+3 без DST.
@pattern("^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}$")
scalar wallClockDateTime extends string;

/// Тип события (T5): id — целое с автонумерацией, непрозрачный ключ маршрута.
model EventType {
  @doc("Идентификатор типа события (T5).")
  id: int32;

  @doc("Название; обязательно, уникально без учёта регистра и обрамляющих пробелов (T5).")
  @minLength(1)
  name: string;

  @doc("Описание; опционально (T5).")
  description?: string;

  @doc("Длительность в минутах, > 0; определяет длительность слота (T4/T5).")
  @minValue(1)
  durationMinutes: int32;
}

/// Создание типа события (POST /api/event-types, T8).
model NewEventType {
  @doc("Название; обязательно, непустое (T5).")
  @minLength(1)
  name: string;

  @doc("Описание; опционально (T5).")
  description?: string;

  @doc("Длительность в минутах, > 0 (T5).")
  @minValue(1)
  durationMinutes: int32;
}

/// Запись (T6): id и eventTypeId — целые, конец производный от длительности типа,
/// контакты гостя хранятся в записи, email не уникален.
model Booking {
  @doc("Идентификатор записи (целое, автонумерация).")
  id: int32;

  @doc("Идентификатор типа события.")
  eventTypeId: int32;

  @doc("Название типа события — для отображения в списке встреч (T7).")
  eventTypeName: string;

  @doc("Старт, wall-clock MSK (T8 Q2).")
  start: wallClockDateTime;

  @doc("Конец — производный: start + durationMinutes типа (T6); типы неизменяемы (T7).")
  end: wallClockDateTime;

  @doc("Имя гостя; обязательно (T6).")
  @minLength(1)
  name: string;

  @doc("Email гостя; обязателен, не уникален (T6).")
  @minLength(1)
  @format("email")
  email: string;

  @doc("Дополнительная информация гостя о будущем звонке; опционально (T6).")
  additionalInfo?: string;
}

/// Создание записи (POST /api/bookings, T8): guest-поля плоские (T6).
model NewBooking {
  @doc("Идентификатор типа события; неизвестный → 404 (T8).")
  eventTypeId: int32;

  @doc("Старт, wall-clock MSK; в сетке слотов типа (T4) и после now (T3) — иначе 400 (T8).")
  start: wallClockDateTime;

  @doc("Имя гостя; обязательно (T6).")
  @minLength(1)
  name: string;

  @doc("Email гостя; обязателен, формат email (T6/T8).")
  @minLength(1)
  @format("email")
  email: string;

  @doc("Дополнительная информация; опционально (T6).")
  additionalInfo?: string;
}
