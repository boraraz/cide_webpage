# Sportswear Commerce

A premium sportswear e-commerce platform with a minimal, editorial shopping experience.

The monorepo contains a mobile-first customer **storefront**, a separate **admin** dashboard, and an
**ASP.NET Core 10** REST API backed by **PostgreSQL** and **S3-compatible object storage**.

> **Status: Phase 6 (commerce engine).** Customers can browse, add to a bag, check out, and place an
> order that reserves stock. Payment is not collected, and an order cannot be marked paid. The
> storefront is client-rendered, so crawlers that do not run JavaScript do not see product HTML. See
> [docs/implementation-plan.md](docs/implementation-plan.md).
>
> The admin app is still a development tool and must not be deployed as a public site.

## Stack

| Area           | Technology                                                                                                                     |
| -------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| Frontends      | React 19, TypeScript (strict), Vite 8, Tailwind CSS v4, React Router 8, TanStack Query 5, React Hook Form, Zod, Lucide         |
| Shared         | npm workspaces: `@commerce/ui`, `@commerce/content`, `@commerce/api-client`, `@commerce/shared-types` (generated from OpenAPI) |
| Backend        | .NET 10, ASP.NET Core minimal APIs, EF Core with Npgsql, OpenAPI with Scalar, ProblemDetails                                   |
| Infrastructure | PostgreSQL 18, RustFS (S3-compatible) and Mailpit via Docker Compose on OrbStack                                               |
| Quality        | ESLint, Prettier, Vitest with Testing Library, xUnit v3                                                                        |

## Quick start

Prerequisites: Node 24 (`nvm use`), .NET 10 SDK, and OrbStack (or another Docker runtime).

```bash
nvm use && npm install
cp .env.example .env
cp apps/storefront/.env.example apps/storefront/.env.local
cp apps/admin/.env.example apps/admin/.env.local

npm run infra:up       # PostgreSQL, object storage and Mailpit
npm run api:secrets    # .env -> dotnet user-secrets for the API
npm run api:migrate    # apply EF Core migrations to the local database
npm run api:watch      # API on http://localhost:5080   (terminal 2)
npm run dev            # storefront :5173, admin :5174  (terminal 3)
```

Check that everything is running:

```bash
curl http://localhost:5080/api/v1/health/ready
# {"status":"Healthy","checks":[{"name":"database","status":"Healthy"},{"name":"object-storage","status":"Healthy"}]}
```

| App        | URL                            |
| ---------- | ------------------------------ |
| Storefront | <http://localhost:5173>        |
| Admin      | <http://localhost:5174>        |
| API docs   | <http://localhost:5080/scalar> |

To browse PostgreSQL with DBeaver, use `localhost:5432`, database `sportswear_db`, and the
credentials from `.env`. See [the DBeaver guide](docs/development.md#postgresql-with-dbeaver).

## Common commands

```bash
npm run verify        # format check, lint, typecheck, frontend + backend tests, builds
npm run test          # frontend tests
npm run api:test      # backend tests
npm run build         # build both apps
npm run api:types     # regenerate API types (API must be running)
npm run infra:down    # stop local infrastructure (data is kept)
```

## Documentation

- [Architecture](docs/architecture.md): system design, dependency rules, responsive design
  system and module boundaries.
- [Development guide](docs/development.md): setup, environment variables, commands, DBeaver
  and troubleshooting.
- [Implementation plan](docs/implementation-plan.md): completed phases and what comes next.
