# PR #35 — исправления F1 и F2

## Результат

F1 и F2 из `docs/reviews/pr-35-mvp-audit.md` исправлены в рабочем дереве ветки `dev` поверх `2e6d66a035d28fec909ffc983caae1aa17f0b973`. F3 не затронут. Коммиты, push и merge не выполнялись; опубликованный head PR этим прогоном не изменён.

## Изменения и regressions

### F1 — потеря точности start

Причина: `System.Text.Json` отбрасывал дробные разряды после седьмого до проверки сетки. `UtcInstantJsonConverter` теперь отклоняет ненулевой отбрасываемый остаток через существующий путь `400 application/problem+json`. Дополнительные нулевые разряды допускаются: они не меняют instant. Формат ответа не изменён.

В `BookingsHttpTests.cs` добавлены HTTP-проверки с фиксированным clock:

- Ненулевые 8-й, 9-й и 16-й разряды у старта из GET slots: `400`, корректный ProblemDetails, нет Booking, слот остаётся доступным.
- Нулевой хвост длиной 8, 9 и 16 разрядов: `201` и точный сохранённый start.
- Представимый ненулевой tick с дополнительными нулями: по-прежнему off-grid `400` без записи.

До исправления команда `dotnet test tests/backend/CallBooking.Api.Tests/CallBooking.Api.Tests.csproj --filter "FullyQualifiedName~Sub_tick_start|FullyQualifiedName~Exactly_representable"` дала 3 failed / 3 passed: все три sub-tick случая возвращали `Created` вместо `BadRequest`. После исправления они проходят в полном наборе.

### F2 — email до trim

Причина: проверка пустоты использовала trim, а `typeMismatch` — исходный input. `GuestForm` теперь проверяет нормализованный email штатным браузерным email-validator и отправляет нормализованные имя/email. Новый доменный regex не вводился.

В `BookingPage.test.tsx` добавлены сценарии `user.paste` через настоящий компонент и typed client с injectable fetch: U+00A0 вокруг допустимого адреса → один POST с нормализованным телом и confirmation; неверный и пустой после trim адреса → ошибка без POST.

До исправления `npm test -- --run src/BookingPage.test.tsx -t "validates trimmed pasted email"` дал 1 failed / 2 passed: допустимый после trim email отправлял 0 POST вместо 1. После исправления все три проходят. Существующий opt-in runtime-сценарий также использует вставку email с U+00A0 и проверяет сохранённый адрес через upcoming реального API.

## Ревью исправлений

Проверялся только diff исправлений относительно указанного head, не весь PR заново. Два независимых прохода:

- **Standards:** 0 findings. Проверка точности остаётся на transport boundary; Domain/Application и зависимости не менялись. Необоснованных абстракций или расширения scope не обнаружено.
- **Spec:** 0 findings. F1/F2 соответствуют §2.3, §3.2, §5.3, §8.1–8.2; сценарии из аудита покрыты. TypeSpec, OpenAPI, generated controller/schema, маршруты и response DTO не изменялись. F3 и generation tooling не редактировались.

## Проверки

Локально на Windows, 15 октября 2026:

| Команда / проверка | Результат |
|---|---|
| `dotnet test tests/backend/CallBooking.Api.Tests/CallBooking.Api.Tests.csproj --configuration Release` | 245 passed, 1 skipped, 246 total |
| `dotnet test tests/backend/CallBooking.Architecture.Tests/CallBooking.Architecture.Tests.csproj --configuration Release` | 4 passed |
| Frontend `npm test` | 66 passed, 5 файлов |
| Frontend `npm run lint` | Passed |
| Frontend `npm run build` | Passed |
| `dotnet build src/host/CallBooking.AppHost/CallBooking.AppHost.csproj --configuration Release` | Passed; существующее предупреждение ASPIRE010 |
| `dotnet format <project> --verify-no-changes --no-restore` для API, AppHost, обоих test projects, Domain, Application, Infrastructure | Passed |
| Frontend `npm run test:runtime` с `VITE_RUNTIME_WEB_URL=http://localhost:49577` | 2 passed; настоящий API на localhost:5217 через Vite proxy |
| `git -c core.whitespace=cr-at-eol diff --check` | Passed; учтены CRLF C#-файлов |

Первый параллельный build AppHost столкнулся с блокировкой общего output Application (`CS2012`); последовательный повтор прошёл. Первичная проверка formatting нашла смешанные line endings во вставленных строках C#; исправлено штатным `dotnet format whitespace` только в двух изменённых C#-файлах, повторная проверка всех семи проектов успешна. Обычный `git diff --check` после formatting считает CR в CRLF trailing whitespace; вариант с `cr-at-eol` проходит без изменения Git-конфигурации репозитория. Это не изменение или исправление F3. Поднятые для runtime-проверки локальные API и Vite остановлены после прогона.

## Ограничения

- Один существующий timezone-тест пропущен на Windows (IANA-зона без Windows equivalent).
- Runtime проверен через API + Vite и RTL/jsdom, не через Aspire launcher или полноценный browser E2E. AppHost только собран.
- Contract regeneration/drift-check не запускался: контракт и generated-файлы не менялись, известный F3 оставлен отдельной задачей.
- Новый Linux/GitHub CI не запускался; локальные результаты не выдаются за проверку опубликованного PR head.

Итог: локальные проверки и оба ревью исправлений успешны; merge не выполнялся.
