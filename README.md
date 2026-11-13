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

### Reaching it from a browser on another machine

Everything listens on `127.0.0.1` inside the machine it runs on, nothing on `0.0.0.0`. From a Mac
into the VM, forward the port over SSH (here `me` is the SSH host name of the VM):

```sh
ssh -L 8080:localhost:<web port> me      # then open http://localhost:8080
ssh -L 8081:localhost:<dashboard port> me  # the Aspire dashboard (https, with the login token from aspire run)
```

`aspire run` / `aspire describe` print the web and dashboard ports; they change each run.
`scripts/preview up` prints its own ready-made `ssh -L` line.

## Preview per branch

```sh
scripts/preview up            # current branch: builds from the files in this worktree, starts, prints the URL
scripts/preview up other-branch   # a named branch (the files built are still this worktree's)
scripts/preview url|status|plan
scripts/preview down          # removes that branch's containers, database and images
```

A preview is the Aspire app model published as a Docker Compose project (`aspire deploy`), named
`todo-<branch>`. One URL serves the web app and, under `/api`, the API (nginx in the web image
forwards `/api` to the API container). Each preview has its own PostgreSQL.

The URL port is derived from the branch name, so a branch keeps its port from run to run (one
`LocalForward` line in `~/.ssh/config` per branch is enough). Previews of different branches run side by
side. `docker compose -p todo-<branch> stop postgres` / `start postgres` stops and starts a preview's
database; the API's `/health/ready` follows it (inside the network:
`docker compose -p todo-<branch> exec web wget -S -O- http://api:8080/health/ready`).

Two previews side by side, and the browser checks for that (the second URL and a text that only its
branch shows):

```sh
BASE_URL=http://localhost:<port A> OTHER_URL=http://localhost:<port B> CHANGE_TEXT="branch B" \
  npx playwright test e2e/isolation.spec.ts      # from web/
```

Notes: Aspire names the Compose project `aspire-todo-<branch>-<hash>` (the script finds it); the
database `tododb` is created by the postgres image (`POSTGRES_DB`, set only when publishing); `up`
leaves earlier builds' images behind, `down` removes the ones its containers use.

Preview images are built for the machine's own architecture. That uses an evaluation-only Aspire API
(`ASPIREPIPELINES003`, acknowledged in `AppHost.cs`); without it Aspire builds `linux/amd64` images.

## Layout

| Path | What |
|---|---|
| `src/Todo.AppHost` | Aspire app model: PostgreSQL, API, web |
| `src/Todo.Api` | ASP.NET Core minimal API. Knows the database only by the `ConnectionStrings__tododb` connection string; applies `Migrations/*.sql` itself on start |
| `scripts/preview` | Per-branch preview (Compose) |
| `web` | SolidJS + TypeScript (Vite). Forwards `/api` to `API_URL` (Vite in dev, nginx per `web/Dockerfile` in a preview) |
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
