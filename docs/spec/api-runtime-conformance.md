# API runtime conformance (TK14)

Матрица доказательств для [#32](https://github.com/EugenyTsoy/ai-for-developers-project-386/issues/32)
по I1, I9–I10 и AC16 [спецификации #18](https://github.com/EugenyTsoy/ai-for-developers-project-386/issues/18).
Это индекс исполняемых HTTP-тестов, не новая схема или отдельный conformance framework.
Все указанные файлы находятся в `tests/backend/CallBooking.Api.Tests/`.

## Операции

Успешные ответы имеют `application/json`. Коллекции проверяются как JSON-массивы,
включая пустой результат. Указаны только применимые утверждённые отказы.

| Операция | Runtime success и shape | Runtime отказы | HTTP-доказательства |
|---|---|---|---|
| `GET /api/event-types` | 200; EventType[] или `[]`; стабильный строковый id, name, целочисленный durationMinutes, optional string description | Не установлены | `EventTypesHttpTests`: `Empty_storage_returns_200_empty_array`, `Creation_returns_201_and_subsequent_list_returns_saved_trimmed_type_with_stable_id`, `Duplicate_names_and_omitted_description_are_allowed_for_every_positive_int32_duration`, `Type_remains_listed_after_all_its_available_slots_are_booked` (AC1, AC3, AC16) |
| `POST /api/event-types` | 201 после сохранения; ровно id/name/durationMinutes и description при наличии; отсутствующее description опущено | 400 | `EventTypesHttpTests`: `Creation_returns_201_and_subsequent_list_returns_saved_trimmed_type_with_stable_id`, `Duplicate_names_and_omitted_description_are_allowed_for_every_positive_int32_duration`, `Supplied_string_description_is_valid`, `Invalid_creation_returns_400_problem_without_saving`, `Missing_body_returns_400_problem_without_saving` (AC1–AC2, AC16) |
| `GET /api/event-types/{eventTypeId}/slots` | 200; Slot[] или `[]`; ровно строковые start/end, UTC ISO-8601 с `Z` | 404 unknown EventType | `SlotsHttpTests`: `Known_type_returns_its_saved_duration_grid_as_UTC_Z_slots`, `Each_saved_type_has_its_own_grid_and_long_duration_is_a_successful_empty_result`, `No_working_days_and_fully_occupied_working_days_are_successful_empty_arrays`, `Unknown_type_returns_contract_404_problem_without_framework_extension_fields`; общий `ReadSlots` проверяет каждый элемент (AC4, AC16) |
| `POST /api/bookings` | 201 после сохранения; только строковые start/end, UTC ISO-8601 с `Z`; без Location | 400 invalid input; 404 unknown EventType; 409 unavailable canonical selection | `BookingsHttpTests`: `Valid_booking_returns_201_only_start_end_without_location_after_saving_trimmed_guest` и группы отказов ниже (AC10–AC13, AC16) |
| `GET /api/bookings/upcoming` | 200; UpcomingBooking[] или `[]`; только eventTypeName/guestName/guestEmail/start/end, все строки; instants UTC ISO-8601 с `Z` | Не установлены | `UpcomingBookingsHttpTests`: `No_bookings_returns_200_empty_array`, `Future_booking_returns_saved_event_type_and_guest_with_only_approved_fields_and_UTC_Z_instants`, `All_event_types_are_sorted_by_absolute_start_independently_of_insertion_order`; общий `ReadUpcoming` проверяет shape и instants каждого элемента. `BookingsHttpTests.Valid_booking_returns_201_only_start_end_without_location_after_saving_trimmed_guest` доказывает последующее HTTP-чтение созданной Booking (AC10, AC15–AC16) |
| `GET /api/calendar` | 200; только строковый timezone, равный настроенному IANA timezone | Не установлены | `CalendarConfigurationTests.Calendar_metadata_returns_only_the_configured_IANA_timezone` (AC4, AC16) |

## Отказы и отсутствие rejected writes

Каждая следующая группа проверяет фактический HTTP status, `application/problem+json`,
обязательные числовой status и непустой строковый title, status = HTTP.
Разрешены только status/title/detail/type/instance; code, errors, traceId и другие
framework/custom fields запрещены проверкой полного набора имён полей.

| Операция / status | Проверки | Отсутствие сохранения |
|---|---|---|
| POST event-types / 400 | `EventTypesHttpTests.Invalid_creation_returns_400_problem_without_saving`: malformed JSON, missing fields, null, неверные primitive types, blank name, duration ≤ 0, fractional/out-of-int32/quoted duration, supplied non-string description; отдельный `Missing_body_returns_400_problem_without_saving` | Общий `AssertRejected` читает через GET пустой массив типов |
| GET slots / 404 | `SlotsHttpTests.Unknown_type_returns_contract_404_problem_without_framework_extension_fields` | Хранилище Booking остаётся пустым |
| POST bookings / 400 | `BookingsHttpTests`: `Malformed_or_schema_invalid_body_returns_400_problem_and_saves_nothing`, `Missing_body_returns_400_problem_and_saves_nothing`, `Unparseable_or_non_Z_wire_timestamp_returns_400_without_saving` (включая `+00:00`), `Invalid_guest_returns_400_and_saves_nothing`, `Off_grid_start_is_400_independently_of_current_availability` | Каждый случай проверяет пустое реальное хранилище Booking |
| POST bookings / 404 | `BookingsHttpTests.Unknown_event_type_returns_404_problem_and_saves_nothing` | Пустое хранилище Booking |
| POST bookings / 409 | `BookingsHttpTests`: `Canonical_start_at_or_before_now_or_outside_booking_window_is_409`, `Formerly_listed_slot_is_rechecked_against_submission_time`, `Formerly_listed_slot_is_rechecked_against_the_current_booking_window`, `Sequential_overlap_of_any_type_rejects_formerly_listed_selection_without_writing` | Пустое хранилище либо полное равенство состоянию до отказа при существующей Booking |

AC14 не заменяется последовательными conformance-тестами: специализированные
contention-доказательства находятся в `BookingContentionTests` (TK7) и проверяются
вместе с остальными критериями при финальной приёмке TK15.

## Boundary и изоляция

Тесты используют `WebApplicationFactory<Program>` и настоящие generated controllers,
handwritten boundary, application/domain и production in-memory storage strategy.
Каждый behavior fixture создаёт отдельный host/store; now заморожен, timezone и
Working Window заданы test configuration. Seeding/inspection идут через test DI seam,
без новых публичных операций и без замены атомарного создания fake-реализацией.

I9 enforcement не основан только на компиляции DTO: invalid-wire cases проходят MVC
binding/validation и проверяют HTTP-отказ и отсутствие записи. Handwritten validation
filters используют model state с generated validation metadata;
`EventTypeRequestJson` выводит строгую обработку primitive types из generated DTO
metadata, а `UtcInstantJsonConverter` проверяет исходный суффикс `Z` до преобразования.
Domain/application обеспечивают trim, guest validation, canonical grid и availability.
Generated-файлы и authored schema в TK14 не изменяются.

## Запуск

```powershell
dotnet test tests/backend/CallBooking.Api.Tests/CallBooking.Api.Tests.csproj
dotnet test tests/backend/CallBooking.Architecture.Tests/CallBooking.Architecture.Tests.csproj
```

Эти проекты уже включены в `.github/workflows/ci.yml`; дополнительный runner,
snapshot baseline или OpenAPI conformance dependency не требуется.
