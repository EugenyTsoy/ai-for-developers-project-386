# Независимый аудит MVP CallBooking перед merge

**Вердикт: Changes requested.** Найдены два воспроизводимых функциональных нарушения спецификации (P2). Дополнительно подтверждена платформенная проблема проверки генерации (P3, механизм существовал до PR). Дефектов P0/P1, воспроизводимого double booking или ошибки актуальных DST-расчётов не обнаружено. Это результат ограниченного аудита, не доказательство отсутствия любых ошибок.

## Область и независимость

- PR: <https://github.com/EugenyTsoy/ai-for-developers-project-386/pull/35>.
- Base `main`: `33f71809961c4e1214667bee1bb3a574bcbfa9b2`.
- Head `dev`: `2e6d66a035d28fec909ffc983caae1aa17f0b973`.
- Merge-base равен base. Проверялся именно `git diff main...dev`: 122 файла, включая перемещения; связанные файлы контракта изучались и там, где не менялись.
- Источник требований: [docs/spec/callbooking.md](https://github.com/EugenyTsoy/ai-for-developers-project-386/blob/2e6d66a035d28fec909ffc983caae1aa17f0b973/docs/spec/callbooking.md), authored TypeSpec, AGENTS.md, архитектурный ADR и executable configuration.
- Проверены слои API/Application/Domain/Infrastructure, generated boundary, frontend/client/state, timezone formatting, host/proxy, CI/generation и тесты. Backend-инварианты и frontend дополнительно проверялись независимыми параллельными проходами.
- Предыдущие review/acceptance-отчёты не использовались как доказательство корректности. Findings ниже подтверждены кодом и новыми воспроизведениями.
- Исходное рабочее дерево было чистым. Продуктовый код, тесты и конфигурация исходного checkout не редактировались; новый regression-test и перегенерация выполнялись только во временной копии. Единственный добавленный в исходный checkout файл — этот отчёт. Коммитов, публикации review и merge не выполнялось.

### Приоритеты

P0 — критическая авария; P1 — серьёзный блокирующий дефект; P2 — функциональный/контрактный дефект, требующий исправления; P3 — ограниченная, неблокирующая проблема.

| ID | Ось | Приоритет | Finding | Происхождение |
|---|---|---|---|---|
| F1 | Spec | P2 | Потеря точности UTC start позволяет принять off-grid запрос | Новый serializer в diff |
| F2 | Spec | P2 | UI проверяет email до trim и блокирует допустимый ввод | Новый GuestForm в diff |
| F3 | Standards / generation | P3 | На Windows drift-check отклоняет эквивалентный generated artifact | Ранее существовавший механизм, перенесённый в новые пути |

## Spec

### F1 — P2: off-grid start молча округляется и создаёт Booking

**Код:** [UtcInstantJsonConverter.cs:10–19](https://github.com/EugenyTsoy/ai-for-developers-project-386/blob/2e6d66a035d28fec909ffc983caae1aa17f0b973/src/backend/CallBooking.Api/Serialization/UtcInstantJsonConverter.cs#L10-L19); последующая [проверка сетки CreateBooking.cs:38–46](https://github.com/EugenyTsoy/ai-for-developers-project-386/blob/2e6d66a035d28fec909ffc983caae1aa17f0b973/src/backend/CallBooking.Application/Bookings/CreateBooking.cs#L38-L46).

**Требование:** §3.2 задаёт точные канонические старты; §5.3 требует `400` для start вне сетки. Недопустимый запрос не должен создавать Booking (§2.3, §8.2).

`Utf8JsonReader.TryGetDateTimeOffset` принимает дробную часть длиннее семи знаков, но отбрасывает точность меньше одного tick (100 нс). Converter не проверяет потерю данных. До domain доходит уже другой instant, совпадающий с каноническим стартом.

**Воспроизведение на реальном HTTP API:** создать тип на 30 минут, получить его свободные слоты, взять будущий канонический старт и заменить нулевую дробную часть на `.00000001Z`. При текущей конфигурации был получен следующий результат:

```http
POST /api/bookings
Content-Type: application/json

{"eventTypeId":"<созданный id>","start":"2026-10-15T11:00:00.00000001Z","name":"Audit","email":"audit@example.com"}
```

```text
Actual:   201
Body:     {"start":"2026-10-15T11:00:00.0000000Z","end":"2026-10-15T11:30:00.0000000Z"}
Expected: 400 application/problem+json, без сохранения Booking
```

Также воспроизведено с ненулевыми 9-м и 16-м знаками дробной части на других свободных стартах. Дата в примере — фактическая дата запуска; для повторения позднее нужно использовать свежий будущий слот из GET.

**Последствие:** сервер сохраняет не тот instant, который передан клиентом, и нарушает отказ для off-grid input. Это не обход overlap-lock и не доказательство возможности double booking: сохранённый округлённый интервал всё ещё проходит общий lock и overlap-проверку.

**Почему тесты не поймали:** [BookingsHttpTests.cs:135–148](https://github.com/EugenyTsoy/ai-for-developers-project-386/blob/2e6d66a035d28fec909ffc983caae1aa17f0b973/tests/backend/CallBooking.Api.Tests/BookingsHttpTests.cs#L135-L148) проверяет `.0000001Z` — ровно представимый tick. Потеря точности за пределами седьмого знака не проверяется.

**Рекомендация:** на API boundary отклонять start, который нельзя представить без потери значимых цифр; не молча округлять перед grid validation. Добавить HTTP regression на 8+ знаков с ненулевым отбрасываемым остатком, проверить `400` и отсутствие записи. Хвост из одних нулей не следует путать с потерей точности.

### F2 — P2: валидный после trim email не проходит UI

**Код:** [BookingPage.tsx:36–47](https://github.com/EugenyTsoy/ai-for-developers-project-386/blob/2e6d66a035d28fec909ffc983caae1aa17f0b973/src/frontend/callbooking.web/src/BookingPage.tsx#L36-L47), [email input:60–63](https://github.com/EugenyTsoy/ai-for-developers-project-386/blob/2e6d66a035d28fec909ffc983caae1aa17f0b973/src/frontend/callbooking.web/src/BookingPage.tsx#L60-L63). Backend правильно нормализует значение в [GuestContact.cs:17–24](https://github.com/EugenyTsoy/ai-for-developers-project-386/blob/2e6d66a035d28fec909ffc983caae1aa17f0b973/src/backend/CallBooking.Domain/Entities/GuestContact.cs#L17-L24).

**Требование:** §2.3: «Имя и email обрабатываются с trim; обязательны непустые значения и допустимый email-формат».

Пустота проверяется через `email.trim()`, но формат — через `validity.typeMismatch` исходного DOM input. Это разные значения. Обычные ASCII-пробелы email-input удаляет сам, однако неразрывные пробелы U+00A0 остаются.

**Воспроизведение:**

1. Выбрать тип и свободный слот, заполнить имя.
2. Вставить в Email `\u00a0guest@example.test\u00a0` — адрес с неразрывными пробелами по краям.
3. Нажать «Записаться».

```text
Actual UI:   «Введите корректный email.», POST /api/bookings не отправляется.
After trim:  guest@example.test — допустимый адрес.
Same API input on a free slot: 201.
```

**Доказательство:** новый RTL/Vitest regression был запущен во временной копии checkout: реальный `BookingPage`, typed client с injectable fetch, `user.paste` указанного значения и submit. Проверка `expect(posts).toBe(1)` упала: `posts = 0`, alert = `Введите корректный email.`. Отдельный HTTP-прогон подтвердил `201` для того же email. Проверка DOM также показала `typeMismatch=true` до trim и `false` после него.

**Последствие:** UI запрещает часть согласованного допустимого ввода, хотя сервер его принимает. Это реальный сценарий копирования адреса с форматированными пробелами, а не отсутствие дополнительного удобства.

**Почему тесты не поймали:** [BookingPage.test.tsx:263–280](https://github.com/EugenyTsoy/ai-for-developers-project-386/blob/2e6d66a035d28fec909ffc983caae1aa17f0b973/src/frontend/callbooking.web/src/BookingPage.test.tsx#L263-L280) проверяет пустой/неверный ввод, но не адрес, становящийся валидным после Unicode trim.

**Рекомендация:** проверять формат нормализованного значения, согласованного с отправкой/backend, не исходного input. Добавить regression с U+00A0 и успешным POST. Не требуется вводить новый доменный email regex.

## Standards / generation

### F3 — P3: ложный contract drift на Windows при одинаковом содержимом Git blob

**Код:** [generate-contract.mjs:90–99](https://github.com/EugenyTsoy/ai-for-developers-project-386/blob/2e6d66a035d28fec909ffc983caae1aa17f0b973/scripts/generate-contract.mjs#L90-L99), [.gitattributes:1–6](https://github.com/EugenyTsoy/ai-for-developers-project-386/blob/2e6d66a035d28fec909ffc983caae1aa17f0b973/.gitattributes#L1-L6), [verify-contract-drift.mjs:31–45](https://github.com/EugenyTsoy/ai-for-developers-project-386/blob/2e6d66a035d28fec909ffc983caae1aa17f0b973/scripts/verify-contract-drift.mjs#L31-L45).

**Требование:** §7.2 допускает OS-native line endings NSwag, но обещает эквивалентный repository result и чистые generated-пути после regeneration. AGENTS.md также задаёт проверяемый generation workflow.

**Окружение воспроизведения:** Windows, Git `2.54.0.windows.1`, `core.autocrlf=false`, Node `24.19.0`, .NET SDK `10.0.400`, NSwag `14.7.1` из manifest. Отдельный чистый локальный clone точного head SHA, root `npm ci`, `dotnet tool restore`, затем `npm run verify:contract-drift`.

```text
Compilation completed successfully.
[generate:contract] chain complete.
[verify:contract-drift] DRIFT DETECTED in generated contract paths:
M src/backend/CallBooking.Api/Generated/Contract.g.cs
exit = 1

git diff --quiet -- <generated file>   => exit 0
git hash-object <generated file>      => 7cf0fdfdf87a38c9957e672b70deaf270476330b
git rev-parse HEAD:<generated file>   => 7cf0fdfdf87a38c9957e672b70deaf270476330b
git ls-files --eol <generated file>   => i/lf w/crlf attr/text eol=lf
```

NSwag пишет CRLF, а attributes требуют LF. В этом окружении porcelain остаётся dirty даже при одинаковом нормализованном blob. Ошибки генерации типов/контроллеров нет; падает проверка воспроизводимости.

**Происхождение и влияние:** отсутствие post-generation LF normalization и porcelain-check уже были в `main`; PR переносит их на новые пути. Поэтому это не новая runtime-регрессия и не самостоятельный блокер merge. Linux CI данного PR проходит; его успех не опровергает Windows-воспроизведение.

**Рекомендация:** нормализовать generated output к canonical LF до проверки, сохранив обнаружение реальных modified/deleted/untracked файлов. Не маскировать проблему автоматическим staging или отключением drift-check. Полезна отдельная Windows-проверка clean regeneration.

Других подтверждённых нарушений документированных архитектурных границ, влияющих на поведение, не найдено. Стилистические smells в findings не включены.

## Что проверено помимо findings

| Область | Результат и основание |
|---|---|
| Atomic check-and-create | [CreateBooking.cs:30–59](https://github.com/EugenyTsoy/ai-for-developers-project-386/blob/2e6d66a035d28fec909ffc983caae1aa17f0b973/src/backend/CallBooking.Application/Bookings/CreateBooking.cs#L30-L59): авторитетные type/duration, grid, свежий now, window, overlap и запись находятся в одном write scope. |
| Owner-wide concurrency | [InMemoryCalendarStore.cs:30–67](https://github.com/EugenyTsoy/ai-for-developers-project-386/blob/2e6d66a035d28fec909ffc983caae1aa17f0b973/src/backend/CallBooking.Infrastructure/InMemory/InMemoryCalendarStore.cs#L30-L67): один reentrant gate; repository writes используют тот же gate. HTTP-доступного обхода не найдено. Новый реальный HTTP-прогон 20 запросов двух типов на один start: 1×201, 19×409, сохранена одна Booking. |
| Overlap и adjacency | [TimeInterval.cs:21–23](https://github.com/EugenyTsoy/ai-for-developers-project-386/blob/2e6d66a035d28fec909ffc983caae1aa17f0b973/src/backend/CallBooking.Domain/Calendar/TimeInterval.cs#L21-L23): корректная полуоткрытая семантика; сравнение absolute instants не разрывается полуночью. |
| Независимый contention stress | Временный standalone probe с исходниками: 256 пересекающихся операций → 1 Created / 255 Unavailable; 256 операций на два соседних интервала → 2 Created / 254 Unavailable. Это дополнительная проверка, не замена HTTP contention-tests. |
| Timezone / DST | [Availability.cs:34–112](https://github.com/EugenyTsoy/ai-for-developers-project-386/blob/2e6d66a035d28fec909ffc983caae1aa17f0b973/src/backend/CallBooking.Domain/Calendar/Availability.cs#L34-L112): календарное окно, invalid boundary → нет окна, ранний ambiguous open / поздний close, сетка по elapsed timeline. Независимо проверены 3 285 дневных grid в девяти зонах за 2026 год; ошибок локальной даты стартов не воспроизведено. |
| Upcoming | [ListUpcomingBookings.cs:10–16](https://github.com/EugenyTsoy/ai-for-developers-project-386/blob/2e6d66a035d28fec909ffc983caae1aa17f0b973/src/backend/CallBooking.Application/Bookings/ListUpcomingBookings.cs#L10-L16): строго start > now, сортировка и данные сохранённого типа. |
| API boundary | Проверены required/type/null, UTC-Z, 400/404/409, отсутствие guest/id в confirmation и Location. Дополнительные malformed-Unicode probes для известных строковых полей возвращали 400 ProblemDetails. F1 — отдельный обнаруженный пробел. |
| Frontend | Изоляция поздних ответов при выборе типа, запрет двойного submit, 409 → сброс выбора и обновление slots, отсутствие автоматического POST retry, failure/empty/success. Форматирование явно использует calendar timezone и offset; подтверждение не раскрывает guest/id. |
| Связка компонентов | Реальный API + Vite `/api` proxy: оба opt-in runtime-tests прошли, включая создание типа через owner UI, booking через guest UI, owner upcoming и исчезновение занятого слота. Прямые `/`, `/booking`, `/events` возвращали HTML. |
| Архитектура и generation | Внутренние слои не используют transport DTO/HTTP; framework-agnostic frontend client использует generated paths. Generated schema/controller согласованы с authored TypeSpec. Содержимое regen совпало, но Windows porcelain-check упал по F3. |

## Выполненные проверки

| Проверка | Результат |
|---|---|
| `dotnet test tests/backend/CallBooking.Api.Tests/CallBooking.Api.Tests.csproj` | 239 total: 238 passed, 1 skipped |
| `dotnet test tests/backend/CallBooking.Architecture.Tests/CallBooking.Architecture.Tests.csproj` | 4 passed, включая негативную проверку guard |
| Frontend `npm test` | 63 passed / 5 files |
| Frontend `npm run lint` | Passed |
| Frontend `npm run build` | Passed |
| `vitest run --config vitest.runtime.config.ts` против поднятых API + Vite | 2 passed |
| Новый email regression во временной копии | Failed ожидаемым образом: 0 POST вместо 1 |
| Новый precision HTTP probe | Дефект подтверждён: 201 вместо 400 |
| Root clean regeneration во временном clone | Генерация успешна; drift-check failed по F3 |
| GitHub CI точного head SHA | Backend, Frontend, Contract drift и Hexlet — success; PR run [38720065233](https://github.com/EugenyTsoy/ai-for-developers-project-386/actions/runs/38720065233) |

## Пробелы тестирования и ограничения вывода

1. **Необходимые regressions:** precision 8+ знаков с проверкой отсутствия записи (F1), Unicode trim-valid email с успешным submit (F2). Нынешние зелёные тесты не доказывают эти свойства.
2. **Concurrency:** committed HTTP-тесты убедительно создают реальное contention, а не только запускают Task.WhenAll. Для расширения покрытия полезны смешанные conflict/non-conflict интервалы, конкурентные соседние успехи и продвижение clock во время ожидания lock. Текущий код в этих местах выглядит корректным; это не отдельные findings.
3. **DST:** добавить committed fixtures на 30-минутный переход Lord Howe и нецелочасовой base offset. Основные grid-fixtures используют Berlin; start-of-day уже проверяется также Santiago/Havana. Дополнительные probes не заменяют постоянные regressions.
4. Один backend-тест пропущен из-за Windows timezone data для IANA-зоны без Windows equivalent ([WorkingWeekTests.cs:105–109](https://github.com/EugenyTsoy/ai-for-developers-project-386/blob/2e6d66a035d28fec909ffc983caae1aa17f0b973/tests/backend/CallBooking.Api.Tests/WorkingWeekTests.cs#L105-L109)). Локального Linux-прогона не было; успешный Linux CI проверен отдельно.
5. Runtime-прогон использовал API + Vite напрямую, **не Aspire launcher**. Wiring AppHost изучен, его build проходит в CI; фактический запуск Aspire, Docker и production SPA-hosting этим аудитом не подтверждены. Opt-in runtime suite не включён в обычный CI.
6. Не проводились нагрузочный/длительный soak и полноценный browser E2E. Runtime UI-tests работают в jsdom с настоящим HTTP, а не в реальном браузере. Нельзя переносить результат single-process lock на несуществующее multi-instance развёртывание.
7. Отсутствие auth и публичная доступность guest name/email в upcoming — явно принятый риск §1, не новый security finding. In-memory storage и отсутствие гарантий restart-recovery сами по себе не объявляются дефектами. Безопасный automatic retry/exactly-once спецификацией также не обещаны.

## Решение перед merge

**Changes requested:** исправить F1 и F2, добавить regressions и повторить backend/frontend/runtime-проверки. F3 можно устранить отдельно как ограниченную платформенную проблему; он не является новой регрессией PR.

Итого по осям: **Spec — 2 findings, максимум P2; Standards / generation — 1 finding, P3, ранее существовавший.** Критических P0/P1 и воспроизводимых нарушений owner-wide overlap-инварианта в исследованной реализации не найдено.
