# Финальная приёмка MVP (TK15)

Индекс доказательств для [#33](https://github.com/EugenyTsoy/ai-for-developers-project-386/issues/33)
по [спецификации #18](https://github.com/EugenyTsoy/ai-for-developers-project-386/issues/18).
Это ссылки на существующие поведенческие проверки, не дублирующий test suite.

## AC1–AC21

Backend-файлы ниже находятся в `tests/backend/CallBooking.Api.Tests/`, frontend —
в `src/frontend/callbooking.web/src/`. Подробные HTTP outcomes, shapes, ошибки и
отсутствие rejected writes перечислены в [runtime conformance matrix](../spec/api-runtime-conformance.md).

| AC | Implementation tickets | Исполняемые доказательства |
|---|---|---|
| AC1 — создание типа | TK2/TK4 (#20/#22) | `StorageSeamTests`, `EventTypesHttpTests`: фактический 201, trim, стабильный id, последующее чтение, одинаковые названия, optional description |
| AC2 — отказ создания типа | TK4/TK14 (#22/#32) | `EventTypeTests`, `EventTypesHttpTests`: malformed/schema-invalid input, 400 ProblemDetails, отсутствие сохранения |
| AC3 — список типов | TK4/TK5/TK14 (#22/#23/#32) | `EventTypesHttpTests`, `SlotsHttpTests`: пустой/полный массив, тип с чрезмерной для окна длительностью или без свободных слотов остаётся видимым |
| AC4 — slots/calendar | TK3/TK5 (#21/#23) | `CalendarConfigurationTests`, `SlotsHttpTests`: IANA timezone, сохранённая длительность, UTC-Z, 200/404, пустая доступность |
| AC5 — рабочее окно/grid | TK5 (#23), ADR-0002 | `AvailabilityTests`, `SlotsHttpTests`: фиксированный интервал начал 30 минут от opening (в том числе 09:15), duration 30/45/60, отбрасывание неполных кандидатов, now не сдвигает grid |
| AC6 — Booking Window/now | TK5/TK6 (#23/#24) | `AvailabilityTests`, `SlotsHttpTests`, `BookingsHttpTests`, `TimeIntervalTests`: 14 календарных дат, строгий now, полное вхождение, допустимое равенство end верхней границе |
| AC7 — overlap | TK5/TK6/TK7 (#23/#24/#25) | `TimeIntervalTests`: half-open, adjacency, midnight; `SlotsHttpTests`: общий busy state; `BookingsHttpTests`: последовательный cross-type overlap/adjacency; `BookingContentionTests`: конкурентные пересечения разных типов и start |
| AC8 — пример 60/30 минут | TK5 (#23) | `AvailabilityTests.Saved_bookings_of_other_types_remove_overlapping_but_not_adjacent_slots`, `SlotsHttpTests.Reads_do_not_reserve_and_see_new_bookings_of_any_type_in_the_same_saved_storage` |
| AC9 — DST | TK5 (#23) | `AvailabilityTests`, `SlotsHttpTests`: invalid boundaries, ambiguous open/close, spring-forward/fall-back, elapsed grid/end, repeated labels; `BookingsHttpTests.Authoritative_duration_derives_end_on_elapsed_timeline_across_DST` |
| AC10 — успешная Booking | TK2/TK6 (#20/#24) | `StorageSeamTests`, `BookingsHttpTests.Valid_booking_returns_201_only_start_end_without_location_after_saving_trimmed_guest`: ровно одна запись, technical id/link, trim, derived end, HTTP readback |
| AC11 — invalid Booking | TK6/TK14 (#24/#32) | `GuestContactTests`, `BookingsHttpTests`: malformed/types/required, blank/invalid guest, non-Z (включая +00:00), off-grid, 400 без записи |
| AC12 — неизвестный тип | TK6 (#24) | `BookingsHttpTests.Unknown_event_type_returns_404_problem_and_saves_nothing` |
| AC13 — stale canonical choice | TK6 (#24) | `BookingsHttpTests`: overlap, start ≤ now, вне Booking Window; ранее показанный выбор перепроверяется при submit, 409 без rejected writes |
| AC14 — contention | TK7 (#25) | `BookingContentionTests`: четыре HTTP-запроса, один 201/три 409, одинаковые и разные типы/start, подтверждённые конкурирующие попытки входа в защищённую операцию; inspection committed state и данных победителя, без привязки к порядку завершения |
| AC15 — upcoming | TK8 (#26) | `UpcomingBookingsHttpTests`: все типы, сортировка по absolute start, строгий now, пустой массив, approved fields |
| AC16 — runtime conformance | TK14 (#32) + TK3–TK8 (#21–#26) | [Матрица шести операций](../spec/api-runtime-conformance.md): реальные статусы/content types, ProblemDetails без custom fields, UTC-Z, optional description, точные confirmation/upcoming shapes |
| AC17 — Guest UI | TK10/TK11/TK13 (#28/#29/#31) | `BookingPage.test.tsx`: loading/error/empty, выбор, guest validation, pending/success, 409/reselect, без automatic POST retry и ложного success при HTTP/transport failure |
| AC18 — Owner UI/navigation | TK9/TK12/TK13 (#27/#30/#31) | `CreateEventTypeForm.test.tsx`, `UpcomingBookingsPage.test.tsx`, `App.test.tsx`: создание/validation/pending/failure/success, upcoming fields/states, BrowserRouter routes/header/возврат на главную |
| AC19 — timezone formatting | TK10/TK11/TK12 (#28/#29/#30) | `BookingPage.test.tsx`, `UpcomingBookingsPage.test.tsx`: CalendarInfo вместо ambient timezone, обозначение timezone, repeated labels с разными UTC offsets, confirmation по серверным start/end |
| AC20 — architecture | TK1 (#19) | Отдельные Domain/Application projects и `tests/backend/CallBooking.Architecture.Tests/DomainBoundaryGuardTests.cs`: production scan и `Guard_detects_artificially_forbidden_reference_in_application_source` (generated/MVC/qualified/alias/static leakage) |
| AC21 — generation/CI | TK15 (#33), существующая toolchain | `npm run generate:contract`, `npm run verify:contract-drift`; clean-checkout build/test с committed artifacts до генерации; scoped porcelain с untracked detection |

## Установленный CI flow

В `.github/workflows/ci.yml` все обязательные suites уже включены без фильтров:

- `backend`: restore/build отдельных API, AppHost и двух test `.csproj` в Release;
  `dotnet test` API project включает domain unit, HTTP integration, storage и contention;
  отдельный architecture project включает positive и negative guard tests;
  затем formatting API/AppHost/tests/Domain/Application/Infrastructure.
- `frontend`: Node 24, `npm ci` → lint → component/adapter/navigation tests →
  build (`tsc -b` + Vite). Предварительная генерация не выполняется.
- `contract-drift`: Node 24 и .NET 10, root `npm ci`, `dotnet tool restore`,
  существующие generation и drift-команды. Drift проверяет `git status --porcelain`
  всех generated directories, а не только diff tracked files.

Все jobs используют бесплатный `ubuntu-latest`; solution с `.esproj` не собирается.
Hexlet autograder, pins/ranges с lockfiles, generator metadata convention и
canonical LF policy не меняются. Повторная реализация CI/generator не нужна.

Opt-in real-runtime proof из TK13 остаётся в [mvp-runtime.md](mvp-runtime.md):
`npm run test:runtime` использует настоящие Vite proxy/API/storage и проверяет
сквозной owner → guest → confirmation → upcoming → updated availability.
Он дополняет, а не заменяет изолированные обязательные suites; browser E2E framework
и обязательный запуск живого стека в CI не добавляются.

## Воспроизведение AC21

На отдельном чистом checkout сначала выполнить backend/frontend команды из CI,
до запуска генерации: так build/test доказывают достаточность committed artifacts.
Для generation job из корня:

```sh
npm ci
dotnet tool restore
npm run generate:contract
npm run verify:contract-drift
git status --porcelain --untracked-files=all -- contract/openapi src/frontend/callbooking.web/src/api/generated src/backend/CallBooking.Api/Generated
```

Последняя команда должна вернуть пустой вывод. Для отрицательной проверки untracked
detection в этом отдельном checkout создать временный файл внутри generated directory,
повторить `npm run verify:contract-drift` и убедиться в ненулевом exit code и `??` для
этого файла. Удалить только созданный probe и повторить команду: exit code снова 0.
Генератор очищает только принадлежащие ему canonical outputs, поэтому не скрывает probe.

## Результаты проверки

Baseline: `792c68bef1cc4ea1ba84ac3a7d685856e93902a8`, отдельные clean checkout;
результаты относятся к этому commit, не к будущему GitHub Actions run.

- Windows, .NET 10.0.400 / Node 24.19.0: restore/build четырёх individual projects
  успешны до generation; API/domain/storage/contention: **238 passed, 1 skipped**;
  architecture: **4 passed**; frontend clean `npm ci` → lint → **63 passed** → build.
- Platform skip — `Calendar_accepts_an_IANA_timezone_without_a_Windows_equivalent`:
  требует системную tzdata (`Antarctica/Troll`). Это не пропуск contention или DST suite.
- Linux (Ubuntu/WSL), .NET 10.0.400 / Node 24.19.0: restore/build четырёх individual
  projects до generation успешны; API/domain/storage/contention: **239 passed,
  0 skipped**; architecture (включая negative guard): **4 passed**;
  frontend clean `npm ci` → lint → **63 passed** → build; formatting семи `.csproj`
  также прошёл в Linux.
- Formatting семи `.csproj` прошёл. AppHost build выдаёт существующее предупреждение
  `ASPIRE010` (`AspireUseCliBundle=false`), без build errors.
- Real-runtime TK13 на локальных API + Vite: **2 passed**, включая direct routes
  и сквозной сценарий общего сохранённого состояния.
- Windows generation успешна, но drift видит `Contract.g.cs` как modified из-за
  OS-native CRLF при `eol=lf`; содержательный `git diff` пуст. Canonical LF policy
  не заменяется Windows-specific workaround.
- **AC21 в Linux подтверждён:** после root `npm ci` и `dotnet tool restore`
  существующие `generate:contract` и `verify:contract-drift` завершились с exit 0;
  scoped porcelain (включая untracked) пуст. Временный untracked probe в generated
  directory дал exit 1 и `??` для probe; после удаления только этого файла drift
  снова дал exit 0. Итоговый `git status --porcelain --untracked-files=all` всего
  checkout пуст. Это локальный Linux proof, не утверждение о запуске GitHub Actions.
- Root tooling `npm ci` сообщает **2 existing audit findings** (moderate/high,
  transitive `@scalar/json-magic`/`undici`); frontend audit — 0. Dependency update
  не входит в TK15; `npm audit fix` и изменение lockfiles не выполнялись.

## Review

- Standards: **0 findings** — нарушения документированных правил и применимые
  baseline code smells в изменении не найдены.
- Spec: **0 findings** — ссылки AC сверены с исполняемыми suites и CI;
  дублирующих tests/workflows или несогласованных product changes нет.
