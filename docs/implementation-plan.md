# Implementation plan

## Phase 1: Foundation and infrastructure (complete)

### Deliverables

- npm workspaces monorepo with two independently deployable apps (`apps/storefront` and
  `apps/admin`) and three shared packages (`ui`, `api-client` and `shared-types`).
- **Both apps** are built with React 19, TypeScript (strict), Vite 8, Tailwind CSS v4,
  React Router 8 and TanStack Query 5. They include:
  - Zod validation of environment variables at startup.
  - Lazy-loaded pages.
  - Loading, error and not-found states.
  - Mobile-first responsive shells.
- **Storefront shell:**
  - Public layout and sticky header.
  - A mobile menu drawer below `lg`.
  - A footer with a development-only API status indicator.
  - A home placeholder.
- **Admin shell:**
  - A development-only warning banner.
  - A responsive sidebar: persistent at `lg` and above, a drawer below that.
  - Navigation config for all 11 planned sections. Only Dashboard is enabled.
  - A dashboard with a live system-status card.
- **Design tokens.** Neutral tokens covering colors, typography, spacing, radius, shadows,
  breakpoints and focus, with per-app theme overrides.
- **Shared UI primitives:** `Container`, `Drawer`, `SkipLink`, `Spinner` and `StatusMessage`.
- **`api-client`.** Configurable base URL, typed requests, ProblemDetails/network/parse error
  normalisation and abort signal support. It sends cookie credentials, has a `prepareHeaders`
  hook and never stores tokens.
- **.NET 10 solution** (`Commerce.slnx`) with four projects, central package management, warnings
  as errors and enforced dependency direction. It provides:
  - `GET /api/v1/health` (liveness) and `GET /api/v1/health/ready` (readiness for the database and
    object storage, with no sensitive details).
  - OpenAPI, plus the Scalar API reference in Development only.
  - ProblemDetails with a global exception handler.
  - Structured logging and HTTP logging.
  - Explicit-origin CORS with credentials.
  - Options validated at startup.
- **EF Core infrastructure:** Npgsql, `CommerceDbContext`, the design-time strategy, migration
  commands and the `dotnet-ef` local tool.
- **Object storage infrastructure:** S3 client options and a health check, with RustFS locally.
- **Docker Compose:**
  - PostgreSQL 18 with the `sportswear_db` database.
  - RustFS object storage, plus a one-shot bucket-creation job.
  - Named volumes and health checks, with every port bound to 127.0.0.1.
- **Configuration.** Environment examples and a script that copies `.env` into API user secrets.
- **Tests.**
  - Vitest suites: 14 tests across both apps and the API client.
  - xUnit v3 unit and integration tests: 16, plus one opt-in test against real infrastructure.
- **Documentation:** the README, [architecture.md](architecture.md) and
  [development.md](development.md), which includes DBeaver instructions.

### Acceptance criteria

| Criterion                                               | Status | Evidence                                                                                 |
| ------------------------------------------------------- | ------ | ---------------------------------------------------------------------------------------- |
| Monorepo structure is established                       | Done   | See [architecture.md](architecture.md#monorepo-structure)                                |
| npm workspaces function correctly                       | Done   | `npm install` and workspace scripts                                                      |
| Storefront runs locally                                 | Done   | `npm run dev`, then <http://localhost:5173>                                              |
| Admin runs locally                                      | Done   | `npm run dev`, then <http://localhost:5174>                                              |
| Both React applications build successfully              | Done   | `npm run build`                                                                          |
| Both React applications pass TypeScript checks          | Done   | `npm run typecheck`                                                                      |
| Shared workspace imports resolve correctly              | Done   | Both apps import `@commerce/ui`, `@commerce/api-client` and `@commerce/shared-types`     |
| ASP.NET Core 10 API starts successfully                 | Done   | `npm run api:run`                                                                        |
| Backend projects compile with correct dependencies      | Done   | `npm run api:build` and `DependencyDirectionTests`                                       |
| `/api/v1/health` returns a successful response          | Done   | `curl`, plus `HealthEndpointTests`                                                       |
| PostgreSQL starts through Docker Compose                | Done   | `npm run infra:up` reports it healthy                                                    |
| Backend database connectivity is verified               | Done   | `/api/v1/health/ready` reports `database: Healthy`; `COMMERCE_INFRA_TESTS=1` test passes |
| S3-compatible development storage starts successfully   | Done   | Healthy container; bucket created; readiness reports `object-storage: Healthy`           |
| Environment variables are documented                    | Done   | `.env.example` files and [development.md](development.md#environment-variables)          |
| Real secrets are excluded from Git                      | Done   | `.gitignore`; credentials live only in `.env` and user secrets                           |
| CORS supports the configured development origins        | Done   | `CorsTests`, plus a live preflight check for 5173 and 5174                               |
| Backend test projects compile                           | Done   | `npm run api:test`                                                                       |
| Initial smoke tests pass                                | Done   | `npm run test` and `npm run api:test`                                                    |
| Responsive shells verified at 375/768/1024/1440px       | Done   | No horizontal overflow at any width; touch targets at least 44px; drawers work           |
| Documentation reflects the implemented project          | Done   | This folder                                                                              |
| No Phase 2+ business functionality has been implemented | Done   | No entities, migrations or business endpoints                                            |

**Manual step for the developer:** connect with DBeaver using
[these instructions](development.md#postgresql-with-dbeaver). The same host, port, database and
credentials were verified from the command line (`psql` and the API readiness check).

### Deviations from the original brief

| Brief                                       | Implemented                              | Reason                                                                                                                               |
| ------------------------------------------- | ---------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------ |
| MinIO for local S3                          | RustFS 1.0.4                             | MinIO no longer publishes container images (`minio/minio` is gone from Docker Hub and Quay). RustFS is S3-compatible and Apache-2.0. |
| Separate `sportswear-commerce/` root folder | The repository root is the monorepo root | Avoids an extra level of nesting.                                                                                                    |
| Empty `hooks/` folders                      | Not created                              | Created only when a cross-feature hook exists.                                                                                       |
| Zustand, React Hook Form                    | Deferred                                 | No client state or forms yet. Added with the cart and the first form respectively.                                                   |
| Latest TypeScript (7.x)                     | TypeScript 5.9.3                         | typescript-eslint and openapi-typescript do not support TypeScript 7 yet.                                                            |
| Node 20 (initially planned)                 | Node 24 LTS                              | Node 20 is end-of-life. Current Vite, Vitest, React Router and jsdom require Node 22 or newer.                                       |

## Deferred functionality (out of scope for Phase 1)

- **Commerce:** product CRUD and variants, inventory logic, category and collection CRUD.
- **Accounts:** delivered in Phase 2 (see below). Email change is still deferred.
- **Shopping:** cart, checkout, orders, payments, shipping calculations, reviews and wishlists.
- **Content and media:** the homepage block editor, media upload endpoints, presigned URLs and
  image processing.
- **Dashboard and deployment:** real dashboard statistics, and production deployment (Dockerfiles,
  CI/CD, hosting).

## Phase 2: Identity (complete)

Custom identity on `PasswordHasher<User>`. The schema is not ASP.NET Core Identity's `AspNet*`
tables. Sessions are opaque server-side cookies. There is no JWT and no refresh endpoint.

### Delivered

- Snake_case identity schema and migration `InitialIdentity`, applied with `npm run api:migrate`.
  The API does not migrate on startup.
- Customer registration, login, logout, logout-all, profile, password change, email verification
  and password reset. Unverified customers may sign in. Email cannot be changed.
- Separate admin login. `GET /api/v1/admin/auth/me` requires the `admin` role. A customer session
  receives 403.
- CSRF header `X-CSRF-TOKEN`, kept in module memory. IP rate limits. Security-event log.
- Mailpit in Compose, SMTP sender, idempotent role seed, and an opt-in admin bootstrap that
  Production refuses.
- Storefront account pages and a protected admin shell. React Hook Form and shared `Button` /
  `TextField` primitives.
- Identity integration tests on Testcontainers PostgreSQL. They do not use `sportswear_db`.

### Accepted limits

- Registration 409 reveals that an email is already registered. Reset and verification requests do not.
- No per-account lockout beyond IP rate limits.
- Email change is not implemented. Verification is not required for login.
- `AllowedHosts` remains `localhost` until a deployment hostname exists.
- Session reuse detection applies to rotated session tokens. Logout does not revoke the whole family.

### Not in this phase

Cart, wishlist, orders, payments, checkout, reviews, CMS and admin product management. Guest
checkout must stay possible later: there is no orders table, and a future order can store a
nullable user id.

## Phase 3: Catalog and inventory API (complete)

Catalog rules live in `Commerce.Domain/Catalog`. Use cases live in `Commerce.Application/Catalog`.
EF Core and S3 stay in Infrastructure. Endpoints are minimal APIs under `/api/v1`. There is no
MediatR and no generic repository. Storefront listing, product pages and admin catalog screens are
not part of this phase. Admin catalog navigation stays disabled.

### Delivered

- Categories, collections, products (`Draft`, `Active`, `Archived`), per-product options and values,
  variants, balances, movements, media metadata and slug history. Migration `Catalog` does not alter
  identity tables. A PostgreSQL trigger rejects category cycles. The API still does not migrate on
  startup. Upgrade the existing database with `npm run api:migrate`. Do not drop the Postgres volume.
- Prices exist only on variants: `numeric(12,2)`, tax-inclusive TRY, JSON decimal strings. Active
  variants must be priced above zero. Compare-at is null or strictly greater. Currency is `TRY`.
- Color and size are option names. Public filters accept only `color` and `size`. Other axes are
  stored and returned on the product, and are not filter parameters yet.
- Stock adjustments lock `inventory_balances` with `SELECT … FOR UPDATE`. `reserved` stays 0.
  `Idempotency-Key` (16–80 characters) is required. Phase 6 will add `inventory_reservations` and a
  30-minute release worker under the same lock. No reservation endpoint ships now.
- Private bucket. Admin upload and both public and admin downloads are proxied by the API. JPEG,
  PNG and WebP only, 5 MiB maximum, object key `catalog/{productId}/{mediaId}.{ext}`.
- Public reads are anonymous. Drafts, archived products and inactive categories or collections are
  hidden. Lists include `available` and omit exact stock. Search uses a stored `tsvector` with the
  `turkish` configuration. Sort is `newest`, `price_asc` or `price_desc`, tied by product id.
- Admin routes require the `admin` role. Customers receive 403. Anonymous callers receive 401.
  POST, PATCH and DELETE require `X-CSRF-TOKEN`. Media uploads are limited to 30 per hour per IP.
- `Catalog:SeedSampleData` is false by default and true in Development. The seeder inserts missing
  sample slugs and never updates an existing row. Sample images are metadata only until a real upload.
- OpenAPI types and `@commerce/api-client` catalog calls. No new storefront or admin pages.

### Accepted limits

- No product hard delete. Archive is the lifecycle.
- Category and collection delete returns 409 while children or assignments exist.
- Public search ignores queries shorter than two characters.
- Slug history resolves the current resource. It does not emit HTTP 301.
- Sample seed media has no object bytes, so `GET /api/v1/media/{id}` is 404 until an admin upload.

### Not in this phase

Cart, checkout, coupons, a tax engine, reservations, payments, reviews, Redis, Elasticsearch, and
admin or storefront catalog screens.

## Phase 4: Admin catalog and homepage CMS (complete)

The admin can manage categories, collections, products, variants, stock and the homepage. The
customer storefront still does not render the catalogue or the homepage.

### Delivered

- Dashboard counts, product search, product editing, variant generation, inventory, movement
  history, and image upload through the existing private bucket.
- `products.version` increments in the same transaction as merchandising changes. A matching
  `expectedVersion` is an atomic compare-and-swap. Omitting it remains last-write-wins. Stock
  adjustments do not change the product version.
- Homepage revisions are insert-only. PostgreSQL triggers reject updates and deletes.
  `content_pages` is the only place that stores the draft pointer, published pointer and
  `published_at`. Public media is visible only when the published revision still references it.
- Public homepage and public media responses are proxied by the API. Phase 4 serves the original
  image. Derivative widths are specified for Phase 5 and are not generated.

### Accepted limits

- Omitting `expectedVersion` is last-write-wins. Stock adjustments do not take a product version.
- Phase 4 serves original images only. Existing product images keep null dimensions until re-upload.
- Movement history derives the previous on-hand quantity. It is not stored.
- Sample seed images have no bytes.
- Orders, reviews, settings and the media library stay disabled.
- The customer storefront does not render the homepage.

### Not in this phase

Customer listing and product pages, cart, checkout, orders, payments, shipping, wishlist, reviews,
promotions, analytics, a multi-page CMS, multi-language, multi-currency, Redis and Elasticsearch.

## Later prerequisites

1. **Production configuration.**
   - Set `AllowedHosts`, `Cors__AllowedOrigins__*`, the connection string, storage credentials and
     SMTP via environment variables or a secret store.
   - Configure forwarded headers behind the TLS proxy.
   - Keep `Identity:BootstrapAdmin` false.
2. **Observability.** Consider OpenTelemetry for tracing and metrics once real traffic exists.

## Architectural decisions

| Decision                                                         | Rationale                                                                                    |
| ---------------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| Modular monolith, one database                                   | Simplest architecture a single developer can operate; module boundaries kept in code.        |
| Minimal APIs, no MediatR or CQRS or repositories                 | Fewer abstractions; EF Core `DbContext` is already a unit of work.                           |
| Shared packages ship TypeScript source                           | No build step or watch process; Vite compiles them directly.                                 |
| Types generated from OpenAPI                                     | One source of truth for contracts; no hand-written DTO duplicates.                           |
| Cookie sessions, no JWT or token storage                         | Logout and password changes revoke server-side sessions. The browser never stores the token. |
| Custom identity schema, `PasswordHasher<User>` only              | The required session, token and security-event tables do not match `AspNet*` Identity.       |
| Explicit-origin CORS with credentials                            | Required for cookie auth; wildcards rejected at startup.                                     |
| Options validated on start                                       | Misconfiguration fails fast with an actionable message.                                      |
| Readiness separate from liveness                                 | Orchestrators can restart crashed processes without flapping on dependency outages.          |
| Dev secrets in `dotnet user-secrets`                             | Keeps credentials out of the repository and out of `appsettings` files.                      |
| Native API and Vite in development; Docker for dependencies only | Fastest hot reload. Dockerizing the apps later remains straightforward.                      |
| Repository `nuget.config` with source mapping                    | Reproducible restores, independent of developer-global NuGet feeds.                          |
| Tailwind v4 CSS-first tokens                                     | No JS config; tokens are plain CSS variables that each app can override.                     |
| Native `<dialog>` for drawers                                    | Built-in focus trap, Escape and inert background without extra dependencies.                 |
