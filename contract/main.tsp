import "@typespec/http";
import "@typespec/openapi";
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

@doc("A bookable interval: start = working-window open instant + k * 30 elapsed minutes (integer k >= 0), independently of event duration; end = start + authoritative duration. The entire interval must fit the working window and the 14-calendar-date booking window, start strictly after now, and not overlap any booking across event types. DST boundaries: invalid open/close closes the date; ambiguous open uses the earlier instant and close the later instant. Grid arithmetic follows the UTC timeline.")
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

  @doc("UTC ISO-8601 instant with Z suffix; start must belong to the fixed 30-elapsed-minute grid anchored at working-window opening and the full event duration must fit that window (otherwise HTTP 400). A canonical selection that is occupied, at/before now or outside the current booking window returns HTTP 409. Availability and owner-wide non-overlap are checked atomically with creation.")
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
@tag("EventTypes")
@operationId("EventTypes_List")
@get
@route("/event-types")
op listEventTypes(): EventType[];

@doc("Create a new event type.")
@tag("EventTypes")
@operationId("EventTypes_Create")
@post
@route("/event-types")
op createEventType(@body request: CreateEventTypeRequest):
  | EventTypeCreatedResponse
  | BadRequestResponse;

@doc("List available slots within the next 14 calendar dates including today in the calendar timezone. Starts use a fixed 30-elapsed-minute interval from working-window opening; duration comes from the selected event type. This snapshot does not reserve slots. Clients group starts by calendar-local date before time selection and distinguish repeated DST labels by UTC offset.")
@tag("EventTypes")
@operationId("EventTypes_Slots")
@get
@route("/event-types/{eventTypeId}/slots")
op listSlots(@path eventTypeId: string):
  | Slot[]
  | NotFoundResponse;

@doc("Create a booking for an available slot.")
@tag("Bookings")
@operationId("Bookings_Create")
@post
@route("/bookings")
op createBooking(@body request: CreateBookingRequest):
  | BookingCreatedResponse
  | BadRequestResponse
  | NotFoundResponse
  | ConflictResponse;

@doc("List upcoming bookings across all event types.")
@tag("Bookings")
@operationId("Bookings_Upcoming")
@get
@route("/bookings/upcoming")
op listUpcomingBookings(): UpcomingBooking[];

@doc("Get the calendar's authoritative timezone and metadata.")
@tag("Calendar")
@operationId("Calendar_Get")
@get
@route("/calendar")
op getCalendar(): CalendarInfo;
