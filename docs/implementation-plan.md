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
- **Accounts:** customer registration and login, admin authentication.
- **Shopping:** cart, checkout, orders, payments, shipping calculations, reviews and wishlists.
- **Content and media:** the homepage block editor, media upload endpoints, presigned URLs and
  image processing.
- **Dashboard and deployment:** real dashboard statistics, and production deployment (Dockerfiles,
  CI/CD, hosting).

## Phase 2 prerequisites and next steps

1. **Identity and admin authentication.**
   - Add ASP.NET Core Identity with HttpOnly, Secure, SameSite cookie authentication.
   - Add antiforgery/CSRF protection, wired through `api-client`'s `prepareHeaders`.
   - Protect the admin with a route guard, then remove the "development only" restriction.
2. **Database conventions.** Decide on table naming before the first migration (for example
   snake_case through `EFCore.NamingConventions`). Then add the first migration under
   `Persistence/Migrations`.
3. **First catalogue module.** Domain entities, Application services and `/api/v1` endpoints,
   then regenerate `shared-types`.
4. **Frontend forms.** Add React Hook Form (with the Zod resolver) for the first admin forms.
5. **Production configuration.**
   - Set `AllowedHosts`, `Cors__AllowedOrigins__*`, the connection string and storage credentials
     via environment variables or a secret store.
   - Configure forwarded headers behind the TLS proxy.
6. **Observability.** Consider OpenTelemetry for tracing and metrics once real traffic exists.

## Architectural decisions

| Decision                                                         | Rationale                                                                             |
| ---------------------------------------------------------------- | ------------------------------------------------------------------------------------- |
| Modular monolith, one database                                   | Simplest architecture a single developer can operate; module boundaries kept in code. |
| Minimal APIs, no MediatR or CQRS or repositories                 | Fewer abstractions; EF Core `DbContext` is already a unit of work.                    |
| Shared packages ship TypeScript source                           | No build step or watch process; Vite compiles them directly.                          |
| Types generated from OpenAPI                                     | One source of truth for contracts; no hand-written DTO duplicates.                    |
| Cookie-ready API client, no token storage                        | Compatible with HttpOnly cookie auth; avoids tokens in `localStorage`.                |
| Explicit-origin CORS with credentials                            | Required for cookie auth; wildcards rejected at startup.                              |
| Options validated on start                                       | Misconfiguration fails fast with an actionable message.                               |
| Readiness separate from liveness                                 | Orchestrators can restart crashed processes without flapping on dependency outages.   |
| Dev secrets in `dotnet user-secrets`                             | Keeps credentials out of the repository and out of `appsettings` files.               |
| Native API and Vite in development; Docker for dependencies only | Fastest hot reload. Dockerizing the apps later remains straightforward.               |
| Repository `nuget.config` with source mapping                    | Reproducible restores, independent of developer-global NuGet feeds.                   |
| Tailwind v4 CSS-first tokens                                     | No JS config; tokens are plain CSS variables that each app can override.              |
| Native `<dialog>` for drawers                                    | Built-in focus trap, Escape and inert background without extra dependencies.          |
