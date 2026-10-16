# CallBooking — календарь звонков

[![hexlet-check](https://github.com/EugenyTsoy/ai-for-developers-project-386/actions/workflows/hexlet-check.yml/badge.svg)](https://github.com/EugenyTsoy/ai-for-developers-project-386/actions)

Сервис для записи на звонок: владелец создаёт типы событий и видит предстоящие
бронирования, гость выбирает тип и свободное время и оставляет имя и email.
Сервер проверяет доступность и не допускает пересечения бронирований.

Учебный проект Хекслета: https://ru.hexlet.io/programs/ai-for-developers
Как это должно работать: https://files.hexlet.app/a/2ipc5m

## Публичная демонстрация

**Открыть CallBooking:** https://callbooking-demo.onrender.com

Сервис размещён в Render Free. После 15 минут без запросов он может заснуть;
первое открытие после простоя может занять около минуты.

> **Это демонстрационный сервис, не календарь для реальных записей.**
> Типы событий и бронирования хранятся только в памяти процесса и теряются после
> сна, перезапуска или повторного деплоя. Авторизации нет: любой посетитель может
> создавать типы и читать список бронирований с именами и email.
> **Используйте только синтетические данные**, например `Гость Демо` и
> `demo@example.com`. Не вводите реальные персональные данные.

Для знакомства с приложением откройте «Предстоящие события» (`/events`),
создайте тип события, затем перейдите в «Записаться» (`/booking`) и выберите
свободное время. После сна сервиса типы потребуется создать заново.

## Стек

- **Backend:** C# / .NET 10, ASP.NET Core Web API; слои API, Application, Domain,
  Infrastructure. Хранилище — потокобезопасный `InMemoryCalendarStore`, без БД.
- **Frontend:** React 19, TypeScript, Vite 8, React Router; типизированный
  API-клиент на `openapi-fetch`.
- **API-контракт:** TypeSpec → OpenAPI → TypeScript-типы и C#-артефакты
  через `openapi-typescript` и NSwag.
- **Локальная разработка:** .NET Aspire 13 запускает API и Vite.
- **Контейнер:** multi-stage Docker-сборка React и .NET; один ASP.NET Core
  процесс раздаёт frontend и API на одном origin. Порт задаётся через `PORT`.
- **Тесты:** xUnit, Vitest и React Testing Library; CI — GitHub Actions.

## MCP для AI-агента

В репозитории есть проектная конфигурация OpenCode V2 для Render MCP.
Перед запуском OpenCode задайте `RENDER_API_KEY` в окружении; ключи в Git не храните.
[Короткая инструкция настройки и проверки подключения](docs/agents/mcp.md).
Для запуска приложения MCP не требуется.

## Установка

```bash
git clone https://github.com/EugenyTsoy/ai-for-developers-project-386.git
cd ai-for-developers-project-386
```

### Локальный запуск через Aspire

Нужны **.NET SDK 10** и **Node.js 24** с npm. Команда запускается из корня:

```bash
dotnet run --project src/host/CallBooking.AppHost
```

Откройте адрес Aspire dashboard из вывода команды, затем endpoint ресурса `web`.
Aspire установит npm-зависимости frontend и запустит API и Vite; Vite проксирует
`/api` на API. Дополнительные переменные окружения и БД для этого запуска не нужны.

### Локальный запуск через Docker

Нужен запущенный Docker с поддержкой Linux containers. Устанавливать .NET и Node
на хост для сборки и запуска контейнера не требуется. Из корня репозитория:

```bash
docker build -t callbooking:local .
docker run --rm --name callbooking-local -p 127.0.0.1:8080:8080 callbooking:local
```

Откройте http://localhost:8080. API доступен на том же адресе, например
http://localhost:8080/api/calendar. Остановка — `Ctrl+C`.

Другой порт приложения задаётся при запуске (публикация порта должна совпадать):

```bash
docker run --rm --name callbooking-local -e PORT=10000 -p 127.0.0.1:8080:10000 callbooking:local
```

В этом примере сайт по-прежнему доступен на http://localhost:8080. Контейнер
работает без root и принимает HTTP; публичный HTTPS завершается на стороне Render.
Для локальных запусков также действует ограничение in-memory хранения:
перезапуск приложения очищает данные, Docker volume этого не исправляет.

Подробные сценарии проверки: [Aspire/Vite и real API](docs/testing/mvp-runtime.md),
[Docker, конфликты бронирований и ограничения хранения](docs/testing/docker.md).

---

<details>
<summary>Автоматические тесты Хекслета</summary>

Тесты запускаются на каждый коммит. За запуск отвечает файл `.github/workflows/hexlet-check.yml` — не удаляйте и не переименовывайте ни его, ни репозиторий.

</details>

## О Хекслете

[Хекслет](https://ru.hexlet.io/) — школа программирования: авторские программы обучения с практикой, поддержкой наставников и реальными проектами, которые остаются в резюме. Этот репозиторий — один из таких проектов.
