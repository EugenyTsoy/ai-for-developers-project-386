# Submission Audit: пакет для независимой проверки

Дата: 2026-10-16. База: `origin/main` = `68fd7d3bd7082b95b87f394eb5cffec77e3f9f63`.
Ветка: `feat/submission-audit-booking-grid`.

Материалы предыдущей сессии Sol High недоступны. Основание реализации — текущая
спецификация и явные требования владельца в этой сессии. Это свидетельства
исполнителя, а не заключение независимого аудитора.

## Изменения и границы

- Спецификация редакции 2, GLOSSARY и ADR-0002 фиксируют новый интервал начал.
  Исторические Resolution/completeness audit редакции 1 не переписаны.
- `Availability.GetSlotGrid`: `openInstant + k × 30 минут`; конец определяется
  сохранённой длительностью EventType, весь интервал помещается в рабочее окно.
  Общая функция используется listing и повторной проверкой POST.
- Кандидаты могут пересекаться между собой. Сохранённые Booking не могут:
  существующий атомарный owner-wide check-and-create не изменён.
- BookingPage: тип → нативный `input[type=date]` → времена выбранной локальной
  даты → гость. Дата не выбрана автоматически. Границы контрола — первая/последняя
  дата доступных слотов; промежуточные даты без доступности имеют empty-state.
  Смена даты/типа сбрасывает слот и форму; pending блокирует выбор; 409 сохраняет
  дату, сбрасывает слот и обновляет availability без повторного POST.
- TypeSpec обновлён первым; OpenAPI, C# и TS перегенерированы. Поля DTO, пути и
  статусы не менялись: изменена доменная семантика и её документация.
- Миграций, совместимости со старой сеткой, переходного режима и новых endpoint-ов нет.
  Merge и redeploy Render не выполнялись.

## Матрица доказательств

| Область | Проверка | Результат |
|---|---|---|
| Backend Windows, Release | `dotnet test tests/backend/CallBooking.Api.Tests/CallBooking.Api.Tests.csproj --configuration Release` | 256 passed, 1 существующий Windows-skip IANA-зоны без Windows-эквивалента |
| Backend Linux/amd64, .NET SDK 10 | Те же тесты в одноразовом SDK-контейнере с копией backend sources/tests | 257/257, без skip |
| Architecture | `dotnet test tests/backend/CallBooking.Architecture.Tests/CallBooking.Architecture.Tests.csproj --configuration Release` | 4/4 на Windows и Linux |
| Frontend | `npm ci`, `npm run lint`, `npm test`, `npm run build` в frontend directory | 67/67; lint, TypeScript и Vite build успешны |
| AppHost | Release build отдельного `.csproj` | Успех; существующее ASPIRE010 (`AspireUseCliBundle=false`) |
| Форматирование | `dotnet format <project> --verify-no-changes` для API, Domain, Application, Infrastructure, AppHost и обоих test projects | Успех для всех 7 проектов |
| Контракт | Root `npm ci`, `dotnet tool restore`, `npm run generate:contract` | Успех; pinned TypeSpec 1.17.0 / NSwag 14.7.1 |
| Contract drift | После commit: `npm run generate:contract`, `npm run verify:contract-drift`, `git status --short` | PASS: все три generated-пути чистые; весь checkout чистый |
| Docker | `docker build -t callbooking:submission-audit .` | Multi-stage production build успешен; dependency layers могли использовать cache |
| Runtime, UTC | `node scripts/check-container.mjs http://localhost:18086` | SPA/assets/404, calendar, типы 30/45/60, сетка, 400, booking/upcoming, race/replay 409 — PASS |
| Runtime, другая конфигурация | Тот же скрипт на 18087: `PORT=9090`, `Europe/Moscow`, открытие всех рабочих дней 09:15 | PASS; старты привязаны к открытию, не к круглому часу |
| UI + реальный API | `VITE_RUNTIME_WEB_URL=http://localhost:18086 npm run test:runtime`, повтор на 18087 | 3/3 на каждой конфигурации; 45 минут, выбор даты, сохранение, 409/refresh |
| Production bundle в Chromium | Создан тип 45 минут, до даты 0 времён, выбрана дата 20.10.2026, затем 09:30–10:15 UTC, отправлен гость, показаны confirmation без PII и upcoming | PASS; дата установлена через native setter + DOM events из-за ограничения browser harness для date input; popup открылся, взаимодействие с его OS-календарём не подтверждено |
| Контейнерные ограничения | `docker exec ... id`, `dotnet --list-sdks`; запуск с `PORT=invalid` | UID 1654 (app), SDK отсутствует; неверный PORT останавливает запуск |
| Финальный образ | Повторный Docker build, check-container на 18088 и UI runtime suite | PASS; UI 3/3; JS bundle `index-DLoNBUhR.js` |
| Restart | После финального runtime-прогона: 5 типов, 3 Booking; restart того же контейнера | Обе коллекции вернули `200 []`, потеря in-memory данных подтверждена |

Regression coverage: 30/45/60 и открытие 09:15; новый 09:45 разрешён, старый
45-минутный 10:00 off-grid; неполный край → 400; канонический занятый выбор → 409;
45-минутная бронь блокирует следующий 30-минутный старт, но не создаёт старт на
своём конце. Детерминированный contention удерживает реальную критическую секцию:
разные типы/старты, включая 45/30, дают одного победителя в обоих порядках.
DST fixtures проверяют spring-forward/fall-back на timeline, invalid/ambiguous
границы, повторные метки и offsets; UI проверяет календарную дату, отличающуюся
от UTC и ambient timezone, смену даты, пустую дату, reset типа и сохранение даты
после 409.

## Ограничения и находки вне изменения

- Root `npm audit`: 2 уязвимые транзитивные зависимости (1 high, 1 moderate):
  `@typespec/openapi3 → @scalar/json-magic@0.13.5 → undici@7.24.4`.
  Это существующие dev-tools, не зависимости финального runtime-образа.
  Lockfile не изменён; отдельное обновление с проверкой генерации остаётся задачей.
  Frontend `npm ci` сообщает 0 vulnerabilities.
- Первая параллельная проверка AppHost совпала с очисткой generated directory
  генератором и упала на отсутствующем C# contract. После завершения генерации
  последовательная сборка и тесты прошли. Генерацию нельзя запускать одновременно
  со сборкой в одном checkout. Ошибки CRLF после patch исправлены `dotnet format`.
- Запуск Aspire и его dashboard не выполнялся: проверены AppHost build и Docker
  runtime. Нет утверждения о production TLS/proxy или нескольких репликах.
- Хранилище по-прежнему in-memory, lock только внутри одного процесса;
  authentication отсутствует. Использовались только синтетические гости на
  одноразовых локальных контейнерах. Постоянное хранение и безопасность не менялись.

## Повтор независимым аудитором

1. На чистом checkout PR выполнить команды `.github/workflows/ci.yml` (не `.slnx`).
2. После restore выполнить генерацию, затем `npm run verify:contract-drift`.
   Не запускать генерацию параллельно с build/test в том же checkout.
3. Выполнить Docker-команды из `docs/testing/docker.md` и opt-in UI runtime suite.
4. Особо сверить ADR-0002, §3/§5 спецификации, `AvailabilityTests`,
   `SlotsHttpTests`, `BookingsHttpTests`, `BookingContentionTests`,
   `BookingPage.test.tsx`; generated изменения должны быть только производными.
