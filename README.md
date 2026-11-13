# Agent-driven

Experiments where agent owns the codebase. The TODO app is the vehicle; the product is the
local dev loop around it (see the record in the issue for intent).

## Run it (inner loop)

Prerequisites: Docker, .NET SDK 10, Node 22+, and the [Aspire CLI](https://aspire.dev) **13.6.5**, which
is the version the packages in this repo use. A CLI on another machine (for example the owner's Mac)
must match it; `dotnet tool install Aspire.Cli --version 13.6.5 --tool-path ~/.local/aspire-cli`
works where the install script is not reachable. A CLI installed that way is not on `PATH`:

```sh
export PATH=$HOME/.local/aspire-cli:$PATH     # for aspire run / start / describe
scripts/preview doctor                        # checks docker, dotnet, node and aspire are found
```

(`scripts/preview` itself also looks in `~/.local/aspire-cli`.)

```sh
aspire run          # starts PostgreSQL, the API and the web app; prints the dashboard URL
```

Open the `web` URL from the Aspire dashboard (or `aspire describe`). Background use:
`aspire start` / `aspire describe` / `aspire stop`. Nothing is persisted across AppHost runs:
each run has its own PostgreSQL container.

### Reaching it from a browser on another machine

Everything this repo starts listens on loopback (`127.0.0.1` / `::1`) only, never on all
interfaces (`scripts/check-loopback` lists any project listener that is not). From a Mac into the VM,
forward the port over SSH (here `me` is the SSH host name of the VM):

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

A preview is the Aspire app model published (`aspire publish`) as a Docker Compose file and run with
plain `docker compose` under the project name `todo-<branch slug>`; `scripts/preview plan` prints the
exact name. One URL serves the web app and, under `/api`, the API (nginx in the web image forwards `/api`
to the API container). Each preview has its own PostgreSQL and its own images
(`todo-api:<slug>`, `todo-web:<slug>`, built from this worktree's files). Output goes to `.preview/<slug>/`
(ignored by git).

Everything an agent needs afterwards is plain docker compose with that project name:

```sh
docker compose -p todo-<slug> ps
docker compose -p todo-<slug> logs api
docker compose -p todo-<slug> stop postgres      # and: start postgres
docker compose -p todo-<slug> exec web wget -S -O- http://api:8080/health/ready
docker compose -p todo-<slug> down --volumes     # (scripts/preview down also removes the images)
```

The URL port is derived from the branch name, so a branch keeps its port from run to run (one
`LocalForward` line in `~/.ssh/config` per branch is enough). If that port is taken, `scripts/preview`
takes the next free one and says so loudly with the corrected `ssh -L` line, because a line made for
the usual port would then reach nothing, or another preview. Previews of different branches run side by
side. The API's `/health/ready` follows the preview's database: 503 while it is stopped, and 200 again
once it is back, also when it comes back empty (the API re-applies its migrations without a restart).

Two previews side by side, and the browser checks for that (the second URL and a text that only its
branch shows):

```sh
BASE_URL=http://localhost:<port A> OTHER_URL=http://localhost:<port B> CHANGE_TEXT="branch B" \
  npx playwright test e2e/isolation.spec.ts      # from web/
```

Notes: the database `tododb` is created by the postgres image (`POSTGRES_DB`, set only when publishing,
because Aspire creates the database itself only while running); nginx sends the API its own host name
as `Host` (as Vite does in dev); `up` leaves earlier builds' images behind, `down` removes the ones
its containers use. Base images (`node`, `nginx`) are pinned to exact versions in `web/Dockerfile`.

## Layout

| Path | What |
|---|---|
| `src/Todo.AppHost` | Aspire app model: PostgreSQL, API, web |
| `src/Todo.Api` | ASP.NET Core minimal API. Knows the database only by the `ConnectionStrings__tododb` connection string; applies `Migrations/*.sql` itself on start |
| `scripts/preview` | Per-branch preview (Compose) |
| `scripts/check-loopback`, `scripts/check-pins`, `scripts/test-web-image` | Checks: no listener beyond loopback; no floating base images; the web image's nginx |
| `web` | SolidJS + TypeScript (Vite). Forwards `/api` to `API_URL` (Vite in dev, nginx per `web/Dockerfile` in a preview) |
| `tests/Todo.Api.Tests` | xUnit tests that start the real AppHost model (API + PostgreSQL, no web) |
| `web/e2e` | Playwright browser tests against `BASE_URL` |

API: `GET/POST /api/todos`, `PUT/DELETE /api/todos/{id}`. Health: `/health/live` (process only),
`/health/ready` (includes the database schema; 503 while the database is unreachable or empty).

## Schema changes

Add a new `src/Todo.Api/Migrations/NNN_name.sql`. Files are applied once each, in name order,
and recorded in `schema_migrations`. Never edit an applied file.

## Tests

To verify a change, an agent runs both suites: `dotnet test` (API and database, through the real AppHost) and
Playwright (the browser, against a running web URL).

```sh
dotnet test tests/Todo.Api.Tests        # needs Docker; ~20 s; each test that changes the database has its own app

cd web && npm ci
npx playwright install chromium         # once per machine; skip if the browsers are already there
BASE_URL=<web url> npx playwright test
```

Playwright finds its browsers through `PLAYWRIGHT_BROWSERS_PATH`. In the sandbox VM they are installed at
`/opt/ms-playwright` and the variable is already set (`export PLAYWRIGHT_BROWSERS_PATH=/opt/ms-playwright`
if yours is not); elsewhere `npx playwright install chromium` puts them in the default cache, and the
variable need not be set.

`BASE_URL` is the web app's URL, from one of:

- the inner loop: `aspire describe` (the `web` row) after `aspire start`;
- a preview: `scripts/preview up` (or `scripts/preview url`) prints it, e.g. `http://localhost:22835`.

Other checks (no browser): `scripts/test-web-image` (the web image's nginx: upstream `Host`, `/health` is
404, port, follows an API container that gets a new IP), `scripts/check-pins` (no floating base images),
`scripts/check-loopback` (no project listener beyond loopback).

At the preview URL `/health/...` is 404 on purpose: health is the API's and is read inside the network
(`docker compose -p todo-<slug> exec web wget -S -O- http://api:8080/health/ready`).
