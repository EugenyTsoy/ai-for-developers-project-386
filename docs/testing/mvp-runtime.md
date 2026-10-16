# Проверка MVP с real API (#31)

## Локальный runtime

Из корня репозитория:

```sh
dotnet run --project src/host/CallBooking.AppHost
```

Открыть endpoint ресурса `web` в Aspire dashboard. Frontend обращается к
относительным `/api/...` через существующий typed client. Vite проксирует эти
запросы на HTTP endpoint `api`, переданный Aspire в серверную переменную
`API_BASE_URL`; frontend ждёт запуска API. Для API выбран launch profile `http`,
чтобы proxy не отправлял браузер на отдельный HTTPS origin. В браузер backend URL не передаётся,
поэтому CORS не требуется. `VITE_API_BASE_URL` для этого setup задавать не нужно.

Без Aspire можно запустить в разных терминалах:

```sh
dotnet run --project src/backend/CallBooking.Api --launch-profile http
```

```sh
cd src/frontend/callbooking.web
npm ci
npm run dev
```

По умолчанию Vite использует `http://localhost:5200` из API launch profile
`http`. Если API запущен по другому адресу, перед запуском Vite задать
`API_BASE_URL` (PowerShell: `$env:API_BASE_URL = 'http://localhost:5201'`).
Это серверная настройка proxy, не второй HTTP-клиент.

Vite обслуживает BrowserRouter routes `/`, `/booking`, `/events`, включая
прямое открытие и обновление вложенного URL. В этой dev-topology API не хостит
frontend. Production-сборка в одном контейнере и ASP.NET SPA fallback описаны в
[docker.md](docker.md). `vite preview` не является полным runtime с API proxy.

## Воспроизводимая автоматическая проверка

При запущенном стеке из `src/frontend/callbooking.web`:

```sh
npm run test:runtime
```

По умолчанию проверяется `http://localhost:49567`. Если endpoint `web` другой,
перед запуском задать `VITE_RUNTIME_WEB_URL`, например в PowerShell:

```powershell
$env:VITE_RUNTIME_WEB_URL = 'http://localhost:49568'
npm run test:runtime
```

Это отдельная opt-in проверка на существующих Vitest/RTL/jsdom, не browser E2E
framework. Она рендерит настоящий `App`, выполняет действия пользователя и
использует typed client с реальным fetch к Vite proxy и работающему API.
HTTP-ответы и storage не подменяются.

Проверяются прямые запросы к трём client routes и полный путь:

1. На `/events` создать тип с уникальным названием, описанием и длительностью 45 минут.
2. Через общую шапку перейти к записи и загрузить созданный тип и его слоты.
3. Выбрать дату через календарь (в timezone CalendarInfo), затем доступное время,
   ввести имя/email и получить подтверждение. До выбора даты времена не показаны;
   смена даты сбрасывает слот и форму гостя.
4. Увидеть подтверждённое время и timezone без данных гостя в confirmation.
5. Перейти к upcoming и увидеть сохранённые название, имя, email, start–end.
6. Повторно загрузить availability через API и UI: забронированный слот отсутствует.
7. Вернуться на главную через логотип.

Нужен свободный 45-минутный слот более чем через час в текущем Booking Window;
стандартная конфигурация календаря его предоставляет на свежем storage.
Проверка использует живое время/расписание и **создаёт реальные тестовые данные**
в локальном in-memory storage. Перезапуск API очищает их; запускать на локальном
стеке без параллельных бронирований. Новые seeding/reset endpoints не добавлены.

Детерминированные проверки loading/error/empty/success, HTTP против transport
failure, ожидания успешного POST и timezone/offset остаются в обычном `npm test`;
они не требуют запущенного стека. Backend tests используют фиксированный now.

Для проверки настоящего браузера повторить шаги 1–7 на endpoint `web`, затем
обновить `/booking` и `/events`: страницы и общая шапка должны сохраниться.
