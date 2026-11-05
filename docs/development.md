# Development guide

## Prerequisites

| Tool     | Version                  | Notes                                                                     |
| -------- | ------------------------ | ------------------------------------------------------------------------- |
| Node.js  | 24 LTS (`.nvmrc`)        | `nvm install && nvm use`. Node 20 is end-of-life and is not supported.    |
| npm      | 11+ (ships with Node 24) | npm workspaces                                                            |
| .NET SDK | 10.0.1xx (`global.json`) | Keep the 10.0 runtime patched; see Troubleshooting.                       |
| OrbStack | Current                  | Provides `docker` and `docker compose` on macOS, including Apple Silicon. |
| DBeaver  | Any recent               | Optional GUI for PostgreSQL. No PostgreSQL install on macOS is needed.    |

Do not install PostgreSQL on macOS itself; the database runs only in OrbStack.

## First-time setup

```bash
git clone <repo-url> sportswear-commerce && cd sportswear-commerce
nvm use                                   # Node 24
npm install                               # all workspaces

cp .env.example .env                      # local infrastructure + API settings (git-ignored)
cp apps/storefront/.env.example apps/storefront/.env.local
cp apps/admin/.env.example apps/admin/.env.local

npm run infra:up                          # PostgreSQL + object storage, waits until healthy
npm run api:secrets                       # copies .env values into dotnet user-secrets
dotnet tool restore                       # dotnet-ef local tool
```

## Daily workflow

Use three terminals:

```bash
npm run infra:up        # 1. once per session (idempotent)
npm run api:watch       # 2. API with hot reload on http://localhost:5080
npm run dev             # 3. storefront :5173 and admin :5174 together
```

Then:

1. Open <http://localhost:5173> (storefront). The footer shows **API healthy** in development.
2. Open <http://localhost:5174> (admin). The dashboard's **System status** card shows API, database
   and object storage.
3. Run `curl http://localhost:5080/api/v1/health/ready`. It should return `"status":"Healthy"`.

## Local URLs and ports

| Service                | URL                                                         | Notes                                       |
| ---------------------- | ----------------------------------------------------------- | ------------------------------------------- |
| Storefront             | <http://localhost:5173>                                     | Vite dev server (`strictPort`)              |
| Admin                  | <http://localhost:5174>                                     | Development only, no authentication yet     |
| API                    | <http://localhost:5080>                                     | `dotnet run` / `dotnet watch`               |
| API liveness           | <http://localhost:5080/api/v1/health>                       |                                             |
| API readiness          | <http://localhost:5080/api/v1/health/ready>                 | Checks database and object storage          |
| OpenAPI document       | <http://localhost:5080/openapi/v1.json>                     | Development only                            |
| API reference (Scalar) | <http://localhost:5080/scalar>                              | Development only                            |
| PostgreSQL             | `localhost:5432` (`POSTGRES_PORT`)                          | Bound to 127.0.0.1                          |
| Object storage S3 API  | <http://localhost:9000> (`S3_API_PORT`)                     | RustFS, bound to 127.0.0.1                  |
| Object storage console | <http://localhost:9001/rustfs/console/> (`S3_CONSOLE_PORT`) | Log in with `S3_ACCESS_KEY`/`S3_SECRET_KEY` |

Production builds served with `vite preview` use ports 4173 (storefront) and 4174 (admin).

## Environment variables

### Root `.env` (copy of `.env.example`, git-ignored)

Docker Compose reads this file directly. `npm run api:secrets` also copies its values into the
API's user secrets.

| Variable                                 | Purpose                                                                       |
| ---------------------------------------- | ----------------------------------------------------------------------------- |
| `ASPNETCORE_ENVIRONMENT`                 | API environment name. Documents the value for container runs.                 |
| `API_URL`, `STOREFRONT_URL`, `ADMIN_URL` | Local URLs. The two frontend URLs become the API's allowed CORS origins.      |
| `POSTGRES_DB`                            | Database name (`sportswear_db`).                                              |
| `POSTGRES_USER`, `POSTGRES_PASSWORD`     | Development database credentials.                                             |
| `POSTGRES_PORT`                          | Host port for PostgreSQL (default 5432).                                      |
| `S3_ENDPOINT`                            | S3 endpoint the API uses (`http://localhost:9000`).                           |
| `S3_REGION`, `S3_BUCKET`                 | Region and bucket name.                                                       |
| `S3_ACCESS_KEY`, `S3_SECRET_KEY`         | Object storage credentials, which are also the local RustFS root credentials. |
| `S3_API_PORT`, `S3_CONSOLE_PORT`         | Host ports for object storage.                                                |

Avoid `;` in `POSTGRES_PASSWORD`, because the value is embedded in a connection string.

### API configuration

The API runs natively, so it does not read `.env` itself. It reads its configuration from these
layers, where later layers override earlier ones:

1. `appsettings.json`: secure defaults (no CORS origins, `AllowedHosts=localhost`, JSON logs).
2. `appsettings.Development.json`: local CORS origins, readable logs and `ForcePathStyle`.
3. **User secrets** (Development only), written by `npm run api:secrets`:
   - `ConnectionStrings:Database`
   - `ObjectStorage:*`
   - `Cors:AllowedOrigins:*`
4. Environment variables, using `__` as the separator (for example `ConnectionStrings__Database`
   or `Cors__AllowedOrigins__0`).

To inspect the secrets, run `dotnet user-secrets list --project backend/src/Commerce.Api`.

### Frontend `.env.local` (per app, git-ignored)

| Variable            | Purpose                                                                   |
| ------------------- | ------------------------------------------------------------------------- |
| `VITE_API_BASE_URL` | API origin. Validated with Zod at startup; the app fails fast if invalid. |

Every `VITE_` variable is compiled into public JavaScript. **Never** put secrets, database
credentials or S3 keys in frontend environment files.

## Docker commands

| Command                                                                | Purpose                                                                    |
| ---------------------------------------------------------------------- | -------------------------------------------------------------------------- |
| `npm run infra:up`                                                     | Start PostgreSQL and object storage, wait until healthy, create the bucket |
| `npm run infra:ps`                                                     | Show service status and health                                             |
| `npm run infra:logs`                                                   | Follow logs                                                                |
| `npm run infra:down`                                                   | Stop containers. Data is kept in named volumes.                            |
| `docker compose down --volumes`                                        | Stop containers and **delete all local data**                              |
| `docker compose exec postgres psql -U sportswear_dev -d sportswear_db` | `psql` inside the container                                                |

Named volumes: `sportswear-commerce_postgres-data` and `sportswear-commerce_object-storage-data`.

## PostgreSQL with DBeaver

1. Make sure the database is running and healthy: `npm run infra:up && npm run infra:ps`.
2. In DBeaver choose **Database > New Database Connection > PostgreSQL**, then **Next**.
3. On the **Main** tab:

   | Field          | Value                            |
   | -------------- | -------------------------------- |
   | Connect by     | Host                             |
   | Host           | `localhost`                      |
   | Port           | `5432` (or your `POSTGRES_PORT`) |
   | Database       | `sportswear_db`                  |
   | Authentication | Database Native                  |
   | Username       | `POSTGRES_USER` from `.env`      |
   | Password       | `POSTGRES_PASSWORD` from `.env`  |

4. Let DBeaver download the PostgreSQL JDBC driver if it asks.
5. Click **Test Connection**. It should report the server as PostgreSQL 18. Then click **Finish**.
6. Optional: name the connection "Sportswear (local)" and set its type to **Development**.

The port is bound to `127.0.0.1`, so only this Mac can connect. Phase 1 has no tables yet; Phase 2
migrations will create them.

## Object storage console

Open <http://localhost:9001/rustfs/console/> and sign in with `S3_ACCESS_KEY` and `S3_SECRET_KEY`. The
`sportswear-media` bucket is created automatically by the `object-storage-init` job.

## Frontend commands

| Command                    | Purpose                                                 |
| -------------------------- | ------------------------------------------------------- |
| `npm run dev`              | Run storefront and admin together                       |
| `npm run dev:storefront`   | Storefront only                                         |
| `npm run dev:admin`        | Admin only                                              |
| `npm run build`            | Production build of both apps (`apps/*/dist`)           |
| `npm run build:storefront` | Storefront build only                                   |
| `npm run build:admin`      | Admin build only                                        |
| `npm run typecheck`        | `tsc` across all workspaces                             |
| `npm run lint`             | ESLint (flat config at the repository root)             |
| `npm run lint:fix`         | ESLint with automatic fixes                             |
| `npm run format`           | Prettier (with Tailwind class sorting)                  |
| `npm run format:check`     | Check formatting without writing                        |
| `npm run test`             | Vitest suites: storefront, admin, api-client            |
| `npm run api:types`        | Regenerate `packages/shared-types` from the running API |

Run `npm run api:types` while the API is running whenever an endpoint contract changes, and commit
the generated file.

## Backend commands

| Command               | Equivalent                                        |
| --------------------- | ------------------------------------------------- |
| `npm run api:restore` | `dotnet restore backend/Commerce.slnx`            |
| `npm run api:build`   | `dotnet build backend/Commerce.slnx`              |
| `npm run api:run`     | `dotnet run --project backend/src/Commerce.Api`   |
| `npm run api:watch`   | `dotnet watch --project backend/src/Commerce.Api` |
| `npm run api:test`    | `dotnet test --solution backend/Commerce.slnx`    |
| `npm run api:secrets` | `./scripts/setup-api-secrets.sh`                  |

### EF Core migrations (from Phase 2)

```bash
dotnet tool restore
dotnet ef migrations add <Name> \
  --project backend/src/Commerce.Infrastructure \
  --startup-project backend/src/Commerce.Api \
  --output-dir Persistence/Migrations
dotnet ef database update \
  --project backend/src/Commerce.Infrastructure \
  --startup-project backend/src/Commerce.Api
dotnet ef dbcontext info \
  --project backend/src/Commerce.Infrastructure \
  --startup-project backend/src/Commerce.Api
```

## Tests

| Command                                   | What runs                                                               |
| ----------------------------------------- | ----------------------------------------------------------------------- |
| `npm run test`                            | Frontend Vitest suites (jsdom)                                          |
| `npm run api:test`                        | xUnit v3 unit and integration tests. They don't need Docker.            |
| `COMMERCE_INFRA_TESTS=1 npm run api:test` | Also runs the readiness test against real PostgreSQL and object storage |
| `npm run test:all`                        | Frontend and backend tests                                              |
| `npm run verify`                          | Format check, lint, typecheck, tests and builds: the pre-commit gate    |

The backend uses Microsoft.Testing.Platform, configured in `global.json`. Use
`dotnet test --solution`, not `dotnet test <path>`.

## Responsive checks

Base styles target mobile. Check layout changes at 375, 768, 1024 and 1440px using the browser's
device toolbar, and confirm that:

- The page never scrolls horizontally (`document.documentElement.scrollWidth <= innerWidth`).
- Navigation is usable by touch: the drawer below `lg`, inline navigation or the sidebar above it.
- Interactive controls are at least 44px tall.

See [architecture.md](architecture.md#design-system-and-responsive-strategy) for the patterns.

## Troubleshooting

| Symptom                                                         | Fix                                                                                                                                               |
| --------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Set POSTGRES_PASSWORD in .env` from Docker Compose             | Create `.env`: `cp .env.example .env`.                                                                                                            |
| `Cannot connect to the Docker daemon`                           | Start OrbStack and check that `docker context show` prints `orbstack`.                                                                            |
| `port is already allocated` (5432, 9000 or 9001)                | Change `POSTGRES_PORT`, `S3_API_PORT` or `S3_CONSOLE_PORT` in `.env`, then rerun `npm run infra:up` and `npm run api:secrets`.                    |
| DBeaver: `password authentication failed`                       | The volume keeps the credentials it was created with. Use the original values, or reset with `docker compose down --volumes` (this deletes data). |
| DBeaver: `sportswear_db` not listed                             | Edit the connection, open the **PostgreSQL** tab and enable **Show all databases**.                                                               |
| API exits with `ConnectionStrings:Database is not configured`   | Run `npm run api:secrets`.                                                                                                                        |
| Readiness reports `database` or `object-storage` as `Unhealthy` | Run `npm run infra:ps`. Make sure the containers are healthy and that the user secrets match `.env`.                                              |
| Browser console shows a CORS error                              | The app's origin must match `STOREFRONT_URL` or `ADMIN_URL` exactly. Rerun `npm run api:secrets`.                                                 |
| App shows `Invalid ... environment configuration`               | Create `apps/<app>/.env.local` from its `.env.example`.                                                                                           |
| `npm install` or Vite fails with an engine or syntax error      | Run `nvm use`. Node 24 is required.                                                                                                               |
| NuGet restore is slow or fails with NU1507                      | The repository's `nuget.config` uses only nuget.org. Make sure no global config overrides it.                                                     |
| `dotnet test` mentions VSTest or MTP                            | Use `dotnet test --solution backend/Commerce.slnx`. `global.json` selects Microsoft.Testing.Platform.                                             |
| .NET runtime is older than the 10.0 packages                    | Install the latest .NET 10 SDK from <https://dotnet.microsoft.com/download/dotnet/10.0>.                                                          |
