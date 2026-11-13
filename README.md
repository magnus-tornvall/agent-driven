# Agent-driven

Experiments where agent owns the codebase. The TODO app is the vehicle; the product is the
local dev loop around it (see the record in the issue for intent).

## Run it (inner loop)

Prerequisites: Docker, .NET SDK 10, Node 22+, the [Aspire CLI](https://aspire.dev) (13.4.x;
`dotnet tool install Aspire.Cli --version 13.4.0 --tool-path ~/.local/aspire-cli` works where
the install script is not reachable).

```sh
aspire run          # starts PostgreSQL, the API and the web app; prints the dashboard URL
```

Open the `web` URL from the Aspire dashboard (or `aspire describe`). Background use:
`aspire start` / `aspire describe` / `aspire stop`. Nothing is persisted across AppHost runs:
each run has its own PostgreSQL container.

## Layout

| Path | What |
|---|---|
| `src/Todo.AppHost` | Aspire app model: PostgreSQL, API, web |
| `src/Todo.Api` | ASP.NET Core minimal API. Knows the database only by the `ConnectionStrings__tododb` connection string; applies `Migrations/*.sql` itself on start |
| `web` | SolidJS + TypeScript (Vite). Forwards `/api` to `API_URL` |
| `tests/Todo.Api.Tests` | xUnit tests that start the real AppHost model (API + PostgreSQL, no web) |
| `web/e2e` | Playwright browser tests against `BASE_URL` |

API: `GET/POST /api/todos`, `PUT/DELETE /api/todos/{id}`. Health: `/health/live` (process only),
`/health/ready` (includes the database; 503 while it is unreachable).

## Schema changes

Add a new `src/Todo.Api/Migrations/NNN_name.sql`. Files are applied once each, in name order,
and recorded in `schema_migrations`. Never edit an applied file.

## Tests

```sh
dotnet test tests/Todo.Api.Tests        # needs Docker; ~40 s
cd web && npm ci && npx playwright install chromium
BASE_URL=<web url from aspire describe> npx playwright test
```
