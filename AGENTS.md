# AGENTS.md

Hexlet educational call-booking project. Prefer executable configuration and workflows over `README.md`, which is currently a course template stub.

## Structure

- `src/CallBooking.AppHost/` — .NET Aspire orchestration entrypoint for the full local stack.
- `src/CallBooking.Api/` — ASP.NET Core Web API.
- `src/callbooking.web/` — React + TypeScript + Vite frontend; Visual Studio represents it as a JavaScript `.esproj`.
- `tests/CallBooking.Api.Tests/` — xUnit integration tests using `WebApplicationFactory<Program>`.

## Commands

Run the full stack:

    dotnet run --project src/CallBooking.AppHost

Run backend tests:

    dotnet test tests/CallBooking.Api.Tests/CallBooking.Api.Tests.csproj

Run only smoke tests:

    dotnet test tests/CallBooking.Api.Tests/CallBooking.Api.Tests.csproj --filter "FullyQualifiedName~SmokeTests"

Frontend, from `src/callbooking.web`:

    npm ci
    npm run lint
    npm run build
    npm run dev

## CI contract

- Backend CI restores, builds, formats, and tests individual `.csproj` files.
- Do not restore/build `CallBooking.slnx` in Linux CI: it contains a Visual Studio JavaScript `.esproj`, whose SDK is unavailable there.
- Frontend CI runs `npm ci -> npm run lint -> npm run build`.
- Mirror `.github/workflows/ci.yml` before pushing when changing build/test tooling.

## Repository rules

- `.github/workflows/hexlet-check.yml` is the Hexlet autograder; do not edit, rename, or delete it.
- `.github/workflows/release-please.yml` releases from `main`; use Conventional Commits.
- .NET projects target `net10.0`; frontend CI uses Node 24. No `global.json` pins the .NET SDK.
- `.editorconfig` requires C# 4-space indentation, Allman braces, and file-scoped namespaces.
- Aspire runs the frontend through `AddViteApp`; Node must be installed, and Aspire runs an npm installer resource before starting Vite.

## Agent skills

### Issue tracker

Issues live in GitHub Issues (via the `gh` CLI). See `docs/agents/issue-tracker.md`.

### Triage labels

The default five triage roles (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `GLOSSARY.md` + `docs/adr/` at the repo root. See `docs/agents/domain.md`.
