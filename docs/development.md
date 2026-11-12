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

npm run infra:up                          # PostgreSQL, object storage and Mailpit
npm run api:secrets                       # copies .env values into dotnet user-secrets
dotnet tool restore                       # dotnet-ef local tool
npm run api:migrate                       # creates the identity tables in sportswear_db
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
   Register a customer from **Sign in**. Verification mail appears in Mailpit.
2. Open <http://localhost:5174> (admin). Anonymous visitors land on the sign-in page. A customer
   session is refused with **Access denied**. An administrator reaches the dashboard.
3. Run `curl http://localhost:5080/api/v1/health/ready`. It should return `"status":"Healthy"`.
4. Publish the homepage in the admin, then reload <http://localhost:5173>. Shop is
   <http://localhost:5173/shop>. Listings send `priceBasis=from`. The storefront is client-rendered.
5. Bag is <http://localhost:5173/cart> and checkout is <http://localhost:5173/checkout>. A placed
   order opens <http://localhost:5173/order/confirmation> and says the reservation lasts 30 minutes.
   Signed-in history is <http://localhost:5173/account/orders>. Admin orders are
   <http://localhost:5174/orders>. Shipping is 149 TRY and free from 2500 TRY.

## Local URLs and ports

| Service                | URL                                                         | Notes                                        |
| ---------------------- | ----------------------------------------------------------- | -------------------------------------------- |
| Storefront             | <http://localhost:5173>                                     | Vite dev server (`strictPort`)               |
| Admin                  | <http://localhost:5174>                                     | Sign-in required. Not a public site.         |
| API                    | <http://localhost:5080>                                     | `dotnet run` / `dotnet watch`                |
| Sitemap                | <http://localhost:5080/api/v1/sitemap.xml>                  | Active categories, collections, and products |
| API liveness           | <http://localhost:5080/api/v1/health>                       |                                              |
| API readiness          | <http://localhost:5080/api/v1/health/ready>                 | Checks database and object storage           |
| OpenAPI document       | <http://localhost:5080/openapi/v1.json>                     | Development only                             |
| API reference (Scalar) | <http://localhost:5080/scalar>                              | Development only                             |
| PostgreSQL             | `localhost:5432` (`POSTGRES_PORT`)                          | Bound to 127.0.0.1                           |
| Object storage S3 API  | <http://localhost:9000> (`S3_API_PORT`)                     | RustFS, bound to 127.0.0.1                   |
| Object storage console | <http://localhost:9001/rustfs/console/> (`S3_CONSOLE_PORT`) | Log in with `S3_ACCESS_KEY`/`S3_SECRET_KEY`  |
| Mailpit                | <http://localhost:8025> (`MAILPIT_UI_PORT`)                 | Local mailbox. SMTP is `localhost:1025`.     |

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
| `SMTP_HOST`, `SMTP_PORT`, `SMTP_FROM`    | Local SMTP. Defaults to Mailpit on `localhost:1025`.                          |
| `MAILPIT_SMTP_PORT`, `MAILPIT_UI_PORT`   | Host ports for Mailpit (1025 and 8025).                                       |
| `BOOTSTRAP_ADMIN`                        | `true` only while creating the first administrator. Then set it back.         |
| `ADMIN_EMAIL`, `ADMIN_PASSWORD`          | Required when `BOOTSTRAP_ADMIN=true`. Never commit real values.               |

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
   - `PublicUrls:StorefrontUrl`, `Smtp:*`
   - `Identity:SeedRoles` (`true` locally) and `Identity:BootstrapAdmin`
   - `Identity:AdminEmail` and `Identity:AdminPassword` only while bootstrap is enabled
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

| Command                                                                | Purpose                                                             |
| ---------------------------------------------------------------------- | ------------------------------------------------------------------- |
| `npm run infra:up`                                                     | Start PostgreSQL, object storage and Mailpit, and create the bucket |
| `npm run infra:ps`                                                     | Show service status and health                                      |
| `npm run infra:logs`                                                   | Follow logs                                                         |
| `npm run infra:down`                                                   | Stop containers. Data is kept in named volumes.                     |
| `docker compose down --volumes`                                        | Stop containers and **delete all local data**                       |
| `docker compose exec postgres psql -U sportswear_dev -d sportswear_db` | `psql` inside the container                                         |

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

The port is bound to `127.0.0.1`, so only this Mac can connect. After `npm run api:migrate` the
identity tables (`users`, `roles`, `auth_sessions`, and the token and security-event tables) are
visible here. Do not drop the `postgres-data` volume to "reset" a migration.

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

### EF Core migrations

`npm run api:migrate` applies pending migrations to the local database, including `Catalog`,
`AdminContent`, and `Commerce` (`20261112221658_Commerce`). The API never migrates on startup. Do not drop the Postgres volume. If
migrations are pending, the role and catalog
seeders log a warning and skip. Do not drop the Postgres volume to upgrade.

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet tool run dotnet-ef migrations add <Name> \
  --project backend/src/Commerce.Infrastructure \
  --startup-project backend/src/Commerce.Api \
  --output-dir Persistence/Migrations
npm run api:migrate
```

### Accounts, sessions and the first administrator

Customers register at <http://localhost:5173/register>. The duplicate-email response says the
account exists. Password reset and verification requests always answer the same way, whether or
not the address is registered. A customer can sign in before verifying their email. Email addresses
cannot be changed yet. There is no per-account lockout beyond the IP rate limits.

The session cookie is not marked `Secure` on local HTTP, and its name is `cide-session`. Production
uses `__Host-cide-session` with `Secure`. HTTPS redirection and HSTS are off in Development and
Testing. `AllowedHosts` stays `localhost` until a real hostname is configured.

Mailpit shows the verification and reset links. The API does not log those links.

To create the first administrator:

1. Set `BOOTSTRAP_ADMIN=true`, `ADMIN_EMAIL` and `ADMIN_PASSWORD` in `.env`. The password must meet
   the same rules as a customer password (at least 10 characters, a letter and a number).
2. Run `npm run api:secrets` and start the API once. Confirm the user in DBeaver. The command does
   not print the password. If that email already exists, the admin role is added and the password
   is left unchanged.
3. Set `BOOTSTRAP_ADMIN=false`, run `npm run api:secrets` again, and restart the API.

Production refuses to start when `Identity:BootstrapAdmin` is true. No migration contains a password.
Role names `customer` and `admin` are seeded when `Identity:SeedRoles` is true.

### Catalog seed

`Catalog:SeedSampleData` is false in `appsettings.json` and true only in Development. On startup the
seeder inserts Women, Men, Unisex, nested Tops and Bottoms, the New Arrivals and Training Essentials
collections, and three sample products when those slugs are missing. It never updates a row that
already exists. Production cannot enable the seed by leaving the Development file behind.

Sample product images are metadata only. A public or admin media GET returns 404 until a file is
uploaded through `POST /api/v1/admin/products/{id}/media`.

### Catalog endpoints

Public, anonymous, no CSRF:

- `GET /api/v1/categories` and `GET /api/v1/categories/{slug}`
- `GET /api/v1/collections` and `GET /api/v1/collections/{slug}`
- `GET /api/v1/products` with `page`, `pageSize` (default 24, max 48), `category`, `collection`,
  `color`, `size`, `minPrice`, `maxPrice`, `available`, `q` and `sort`
  (`newest`, `price_asc`, `price_desc`)
- `GET /api/v1/products/{slug}`
- `GET /api/v1/media/{id}`

Admin, role `admin`, CSRF on POST, PATCH and DELETE:

- Categories and collections under `/api/v1/admin/categories` and `/api/v1/admin/collections`
- Products under `/api/v1/admin/products`, including `publish` and `archive`. Status is not patched.
- Options, option values and variants under the product, `/api/v1/admin/options`,
  `/api/v1/admin/option-values` and `/api/v1/admin/variants`
- `GET /api/v1/admin/inventory`, movements, and `POST …/adjustments` with header `Idempotency-Key`
- Media upload, update, delete and admin stream

Prices in JSON are decimal strings such as `"1299.00"` with `"currency": "TRY"`. They are the
tax-inclusive amount. The client must not turn them into binary floating-point values.

## Tests

| Command                                   | What runs                                                                                                          |
| ----------------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| `npm run test`                            | Frontend Vitest suites (jsdom)                                                                                     |
| `npm run api:test`                        | xUnit v3 unit and integration tests. Catalog and identity tests start their own PostgreSQL containers.             |
| `COMMERCE_INFRA_TESTS=1 npm run api:test` | Also runs readiness and one `catalog-tests/` object round trip against local RustFS. It does not empty the bucket. |
| `npm run test:all`                        | Frontend and backend tests                                                                                         |
| `npm run verify`                          | Format check, lint, typecheck, tests and builds: the pre-commit gate                                               |

The backend uses Microsoft.Testing.Platform, configured in `global.json`. Use
`dotnet test --solution backend/Commerce.slnx`. Do not pass `--nologo`: the test host treats it as
its own flag and reports that zero tests ran. Identity integration tests use Testcontainers and do
not connect to `sportswear_db`. `COMMERCE_INFRA_TESTS=1` is the only opt-in check against the local
database and RustFS.

## Responsive checks

Base styles target mobile. Check layout changes at 375, 768, 1024 and 1440px using the browser's
device toolbar, and confirm that:

- The page never scrolls horizontally (`document.documentElement.scrollWidth <= innerWidth`).
- Navigation is usable by touch: the drawer below `lg`, inline navigation or the sidebar above it.
- Interactive controls are at least 44px tall.

See [architecture.md](architecture.md#design-system-and-responsive-strategy) for the patterns.

## Troubleshooting

| Symptom                                                         | Fix                                                                                                                                                                                                      |
| --------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Set POSTGRES_PASSWORD in .env` from Docker Compose             | Create `.env`: `cp .env.example .env`.                                                                                                                                                                   |
| `Cannot connect to the Docker daemon`                           | Start OrbStack and check that `docker context show` prints `orbstack`.                                                                                                                                   |
| `port is already allocated` (5432, 9000, 9001, 1025 or 8025)    | Change the matching `*_PORT` in `.env`, then rerun `npm run infra:up` and `npm run api:secrets`.                                                                                                         |
| DBeaver: `password authentication failed`                       | The volume keeps the credentials it was created with. Use the original values, or reset with `docker compose down --volumes` (this deletes data).                                                        |
| DBeaver: `sportswear_db` not listed                             | Edit the connection, open the **PostgreSQL** tab and enable **Show all databases**.                                                                                                                      |
| API exits with `ConnectionStrings:Database is not configured`   | Run `npm run api:secrets`.                                                                                                                                                                               |
| Readiness reports `database` or `object-storage` as `Unhealthy` | Run `npm run infra:ps`. Make sure the containers are healthy and that the user secrets match `.env`.                                                                                                     |
| Browser console shows a CORS error                              | The app's origin must match `STOREFRONT_URL` or `ADMIN_URL` exactly. Rerun `npm run api:secrets`.                                                                                                        |
| App shows `Invalid ... environment configuration`               | Create `apps/<app>/.env.local` from its `.env.example`.                                                                                                                                                  |
| `npm install` or Vite fails with an engine or syntax error      | Run `nvm use`. Node 24 is required.                                                                                                                                                                      |
| NuGet restore is slow or fails with NU1507                      | The repository's `nuget.config` uses only nuget.org. Make sure no global config overrides it.                                                                                                            |
| `dotnet test` mentions VSTest or MTP                            | Use `dotnet test --solution backend/Commerce.slnx`. `global.json` selects Microsoft.Testing.Platform.                                                                                                    |
| .NET runtime is older than the 10.0 packages                    | This machine may still be on SDK 10.0.101 while packages are 10.0.14. Install the latest .NET 10 SDK from <https://dotnet.microsoft.com/download/dotnet/10.0>. The build does not require the newer SDK. |
| API log says the identity seed was skipped                      | Migrations are pending. Run `npm run api:migrate` and restart the API.                                                                                                                                   |
| `Identity:BootstrapAdmin must be disabled in Production`        | Expected if that flag is true outside Development. Turn it off.                                                                                                                                          |
| Registration succeeds but no mail arrives                       | Open Mailpit at <http://localhost:8025> and confirm `npm run infra:ps` shows `mail` healthy.                                                                                                             |
