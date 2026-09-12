# Contapop Technical Stack

Contapop is a web application for freelance account management. It uses a TypeScript single-page application, an ASP.NET Core API, and .NET Aspire to run its local distributed environment.

## Existing Baseline

| Area | Technology | Version / configuration | Purpose |
| --- | --- | --- | --- |
| Frontend | React | 19.2 | User interface |
| Frontend language | TypeScript | 5.9 | Type-safe frontend development with strict compiler options |
| Frontend build and dev server | Vite | 8 | Development server, API proxy, and production bundle |
| Frontend quality | ESLint, TypeScript ESLint, React Hooks rules | ESLint 9 | Static analysis and React Hooks validation |
| Backend | ASP.NET Core Minimal APIs | .NET 10 | HTTP API and static-file hosting in production |
| Backend language | C# | .NET 10 | Domain and application logic |
| API contract | OpenAPI | ASP.NET Core OpenAPI | Development-time API documentation and contract generation |
| Local orchestration | .NET Aspire | 13.5 | Starts, connects, and observes the frontend, API, and infrastructure |
| Cache | Redis | Aspire-managed | ASP.NET Core output caching |
| Observability | OpenTelemetry with OTLP exporter support | OpenTelemetry 1.15 | Logs, metrics, and distributed traces |
| Service reliability | .NET service discovery and standard HTTP resilience | Microsoft.Extensions 10.8 | Service-to-service discovery, retries, and resilience policies |
| Health checks | ASP.NET Core Health Checks | .NET 10 | Readiness at `/health` and liveness at `/alive` during development |
| Packaging | Node.js and npm; .NET SDK | Node.js 20.19+ or 22.12+; .NET 10 | Dependency management and builds |

## Product Stack To Apply

The following additions are the selected stack for the product features. They are not yet present in the application skeleton.

| Area | Technology | Purpose |
| --- | --- | --- |
| Primary database | PostgreSQL | Durable relational storage for users, clients, invoices, expenses, payments, and accounting periods. It supports transactional integrity and reporting queries needed by financial data. |
| Data access and migrations | Entity Framework Core with the Npgsql provider | Typed data access, schema migrations, and PostgreSQL integration for the ASP.NET Core API. |
| Authentication and authorization | ASP.NET Core Identity with cookie authentication | Secure user accounts and session management. Use role and policy authorization for account-owner and future collaborator access. |
| Validation | FluentValidation | Explicit server-side validation for commands and API input models. |
| API design | ASP.NET Core Minimal APIs with versioned route groups | Keep the existing lightweight API approach while organizing endpoints by business capability, such as `/api/invoices` and `/api/expenses`. |
| API client and server state | TanStack Query | Fetching, caching, mutation state, retries, and invalidation in React. The API contract remains the source of truth. |
| Client routing | React Router | URL-based navigation for dashboard, clients, invoices, expenses, and settings. |
| Forms | React Hook Form with Zod | Performant, accessible forms with shared client-side validation schemas where appropriate. Server-side validation remains required. |
| UI and accessibility | CSS Modules or the existing component-scoped CSS, plus Radix UI primitives where needed | Preserve the current Vite/CSS approach while using accessible primitives for complex controls such as dialogs, menus, and selects. |
| Date and monetary values | Native `Intl` APIs and integer minor currency units | Format values using the user's locale and store currency amounts as integer cents (or the relevant minor unit), never floating-point values. |
| Tests: backend | xUnit, FluentAssertions, and Testcontainers for PostgreSQL | Unit and integration coverage against a real disposable PostgreSQL instance. |
| Tests: frontend | Vitest, React Testing Library, and MSW | Component, interaction, and API-boundary tests without depending on a live backend. |
| End-to-end tests | Playwright | Validate critical workflows: sign-in, creating a client, issuing an invoice, recording an expense, and viewing balances. |
| Database operations | Aspire PostgreSQL integration for local development; managed PostgreSQL in production | Run the development database alongside the API and Redis. Production database backups, encryption, and upgrades are managed by the hosting provider. |
| Object storage | Azure Blob Storage; Aspire Azure Storage integration with Azurite locally | Store private invoice attachment bytes outside PostgreSQL while Billing retains authoritative attachment metadata and tenant ownership. |
| Deployment | Containerized ASP.NET Core API with the Vite build published to `wwwroot` | Align with the existing `PublishWithContainerFiles` configuration and deploy the API as a single web workload. |
| CI | GitHub Actions | Restore dependencies, run linting and tests, build the frontend and backend, and publish deployable artifacts. |

## Architecture Guidelines

- Keep the React application in `src/frontend` and use the Vite `/api` proxy during development.
- Keep API endpoints under `/api`; the ASP.NET Core server serves the built frontend from `wwwroot` in production.
- Structure backend code by business capability: clients, invoices, expenses, payments, reporting, and identity.
- Treat PostgreSQL as the system of record. Redis is only a cache and must not hold the only copy of accounting data.
- Use UTC timestamps for persisted events and dates without a time component for invoice and accounting dates.
- Protect financial data with authorization checks at every API endpoint and audit significant state changes.
- Do not expose secrets in source control. Keep local values in ignored `.env` files or .NET user secrets; use the deployment platform's secret store in production.
- Enable OpenTelemetry export in deployed environments through `OTEL_EXPORTER_OTLP_ENDPOINT` and monitor health-check failures, request latency, and error rates.

## Implementation Order

1. Add PostgreSQL to the Aspire AppHost and configure EF Core, migrations, and a local development database.
2. Implement identity, authorization policies, and the core domain data model.
3. Replace the weather-forecast starter endpoint and UI with authenticated client, invoice, expense, and payment workflows.
4. Add frontend routing, API state management, validated forms, and reusable accessible UI primitives.
5. Add unit, integration, and end-to-end coverage before automating CI and deployment.
