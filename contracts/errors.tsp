import "@typespec/http";

using TypeSpec.Http;

namespace CallCalendar.Api;

/// Ошибки API — application/problem+json (RFC 9457), коды 400/404/409 (T8 Q3).
model ProblemDetails {
  @doc("URI-идентификатор типа проблемы.")
  type?: string;

  @doc("Краткое, человекочитаемое описание типа проблемы.")
  title?: string;

  @doc("HTTP-код статуса проблемы.")
  status?: int32;

  @doc("Детали конкретной проблемы (например, какой слот занят).")
  detail?: string;
}

/// 400 — валидация запроса (T8 Q3): пустые name/email, неверный формат email,
/// durationMinutes <= 0, старт вне сетки/рабочих часов.
alias BadRequest = {
  @statusCode statusCode: 400;
  @header("Content-Type") contentType: "application/problem+json";
  @body body: ProblemDetails;
};

/// 404 — неизвестный тип события (T8 Q3).
alias NotFound = {
  @statusCode statusCode: 404;
  @header("Content-Type") contentType: "application/problem+json";
  @body body: ProblemDetails;
};

/// 409 — занятость слота с пересечением полуоткрытых интервалов (T2)
/// или дубль названия типа (T5); различаются detail, код общий (T8 Q3).
alias Conflict = {
  @statusCode statusCode: 409;
  @header("Content-Type") contentType: "application/problem+json";
  @body body: ProblemDetails;
};
