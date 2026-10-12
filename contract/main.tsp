import "@typespec/http";
import "@typespec/openapi3";

using TypeSpec.Http;
using TypeSpec.OpenAPI;

@service(#{ title: "CallBooking API" })
@route("/api")
namespace CallBooking;

// ---------------------------------------------------------------------------
// Public DTOs
// ---------------------------------------------------------------------------

@doc("An event type that guests can book.")
model EventType {
  @doc("Opaque, stable server-assigned identifier.")
  id: string;

  @doc("Required, non-empty after trim.")
  name: string;

  @doc("Optional description; omitted from the output when not provided.")
  description?: string;

  @doc("Positive integer duration in minutes.")
  @minValue(1)
  durationMinutes: int32;
}

@doc("Request body for creating an event type.")
model CreateEventTypeRequest {
  @doc("Required, non-empty after trim.")
  name: string;

  @doc("Optional description.")
  description?: string;

  @doc("Positive integer duration in minutes.")
  @minValue(1)
  durationMinutes: int32;
}

@doc("A bookable time interval.")
model Slot {
  @doc("UTC ISO-8601 instant with Z suffix.")
  start: utcDateTime;

  @doc("UTC ISO-8601 instant with Z suffix; end = start + authoritative duration.")
  end: utcDateTime;
}

@doc("Request body for creating a booking.")
model CreateBookingRequest {
  @doc("Identifier of the selected event type.")
  eventTypeId: string;

  @doc("UTC ISO-8601 instant with Z suffix; the requested slot start.")
  start: utcDateTime;

  @doc("Required, non-empty after trim.")
  name: string;

  @doc("Required valid email address.")
  @format("email")
  email: string;
}

@doc("Minimal confirmation returned after a booking is created.")
model BookingConfirmation {
  @doc("UTC ISO-8601 instant with Z suffix.")
  start: utcDateTime;

  @doc("UTC ISO-8601 instant with Z suffix.")
  end: utcDateTime;
}

@doc("An upcoming booking shown in the owner's list.")
model UpcomingBooking {
  @doc("Name of the booking's event type.")
  eventTypeName: string;

  @doc("Guest name.")
  guestName: string;

  @doc("Guest email.")
  guestEmail: string;

  @doc("UTC ISO-8601 instant with Z suffix.")
  start: utcDateTime;

  @doc("UTC ISO-8601 instant with Z suffix.")
  end: utcDateTime;
}

@doc("Calendar metadata for the single owner.")
model CalendarInfo {
  @doc("IANA timezone identifier from backend configuration, used for all display formatting.")
  timezone: string;
}

@doc("RFC 9457 problem details error body.")
model ProblemDetails {
  @doc("HTTP status code of the error; matches the response status.")
  status: int32;

  @doc("Short, human-readable summary of the problem.")
  title: string;

  @doc("Human-readable explanation specific to this occurrence.")
  detail?: string;

  @doc("URI reference identifying the problem type.")
  type?: url;

  @doc("URI reference identifying this specific occurrence.")
  instance?: url;
}

// ---------------------------------------------------------------------------
// Responses
// ---------------------------------------------------------------------------

@doc("An RFC 9457 problem detail with HTTP 400 Bad Request.")
model BadRequestResponse {
  @statusCode statusCode: 400;

  @header contentType: "application/problem+json";

  @body body: ProblemDetails;
}

@doc("An RFC 9457 problem detail with HTTP 404 Not Found.")
model NotFoundResponse {
  @statusCode statusCode: 404;

  @header contentType: "application/problem+json";

  @body body: ProblemDetails;
}

@doc("An RFC 9457 problem detail with HTTP 409 Conflict.")
model ConflictResponse {
  @statusCode statusCode: 409;

  @header contentType: "application/problem+json";

  @body body: ProblemDetails;
}

model EventTypeCreatedResponse {
  @statusCode statusCode: 201;

  @body body: EventType;
}

model BookingCreatedResponse {
  @statusCode statusCode: 201;

  @body body: BookingConfirmation;
}

// ---------------------------------------------------------------------------
// Operations
// ---------------------------------------------------------------------------

@doc("List all event types.")
@get
@route("/event-types")
op listEventTypes(): EventType[];

@doc("Create a new event type.")
@post
@route("/event-types")
op createEventType(@body request: CreateEventTypeRequest):
  | EventTypeCreatedResponse
  | BadRequestResponse;

@doc("List the available slots for an event type within the booking window.")
@get
@route("/event-types/{eventTypeId}/slots")
op listSlots(@path eventTypeId: string):
  | Slot[]
  | NotFoundResponse;

@doc("Create a booking for an available slot.")
@post
@route("/bookings")
op createBooking(@body request: CreateBookingRequest):
  | BookingCreatedResponse
  | BadRequestResponse
  | NotFoundResponse
  | ConflictResponse;

@doc("List upcoming bookings across all event types.")
@get
@route("/bookings/upcoming")
op listUpcomingBookings(): UpcomingBooking[];

@doc("Get the calendar's authoritative timezone and metadata.")
@get
@route("/calendar")
op getCalendar(): CalendarInfo;