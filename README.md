# SpendingApi

A production-ready .NET 10 Minimal API for aggregating and summarising transaction spending by category.

**Use case:** Given a customer ID, year, and month — return total spend grouped by category, ordered by highest spend.

---

## Tech Stack

| Concern | Choice |
|---|---|
| Runtime | .NET 10 LTS |
| API style | Minimal API (no controllers) |
| ORM | EF Core 10 (writes) + Dapper (complex reads) |
| Database | PostgreSQL |
| Auth | JWT Bearer |
| Caching | IMemoryCache → Redis in production |
| Idempotency | In-memory → Redis in production |
| Rate limiting | Built-in ASP.NET Core RateLimiter |
| Resilience | Polly v8 — retry + timeout |
| Observability | OpenTelemetry — traces, metrics, logs |

---

## Architecture

Clean Architecture — dependencies point inward only.

```
┌─────────────────────────────────────────┐
│  API  (Endpoints, Middleware, Program)  │
├─────────────────────────────────────────┤
│  Application  (Handlers, Abstractions)  │
├─────────────────────────────────────────┤
│  Domain  (Entities, Value Objects)      │
├─────────────────────────────────────────┤
│  Infrastructure  (EF Core, Dapper,      │
│                   Cache, Idempotency)   │
└─────────────────────────────────────────┘
```

See [AI.md](./AI.md) for full architecture rules, folder structure, request flows, and naming conventions.

---

## Key Patterns

- **Rich Domain Model** — entities have behaviour, private setters, static factory methods
- **Result\<T\>** — no exceptions for business failures; callers always check `IsSuccess`
- **Hybrid ORM** — EF Core for writes and simple reads; Dapper for complex multi-table queries
- **Specification pattern** — filter logic encapsulated in typed spec classes
- **Cache-aside** — check cache → DB on miss → store; invalidated on write
- **Idempotency** — `Idempotency-Key` header; duplicate POSTs return cached response
- **Per-customer rate limiting** — sliding window partitioned by JWT `sub` claim
- **Resilience pipeline** — retry 3× with exponential backoff + jitter, 10s timeout
- **OpenTelemetry** — traces + metrics + logs; OTLP in prod, console in dev
- **Soft delete** — `deleted_at` timestamp; financial records never hard-deleted
- **Auth boundary** — JWT `sub` claim validated against URL customer ID on every endpoint

---

## API Endpoints

```
GET  /api/v1/customers/{customerId}/spending-summary?year=2026&month=6
     → SpendingSummaryResponse  (200 | 400 | 401 | 403 | 429)

POST /api/v1/customers/{customerId}/transactions
     Idempotency-Key: <uuid>
     → CreateTransactionResponse  (201 | 400 | 401 | 403 | 429)

GET  /health
     → HealthReport
```

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL 15+](https://www.postgresql.org/download/)
- (Optional) [Docker](https://www.docker.com/) — for running PostgreSQL locally

---

## Local Setup

### 1. Clone and restore

```bash
git clone <repo-url>
cd spending-api
dotnet restore
```

### 2. Start PostgreSQL (Docker)

```bash
docker run -d \
  --name spending-postgres \
  -e POSTGRES_DB=spending_api_dev \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=postgres \
  -p 5432:5432 \
  postgres:15
```

### 3. Configure

Copy and edit the connection string in `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=spending_api_dev;Username=postgres;Password=postgres"
  }
}
```

### 4. Apply migrations

```bash
dotnet ef database update --project src/SpendingApi.Infrastructure --startup-project src/SpendingApi.Api
```

### 5. Run

```bash
dotnet run --project src/SpendingApi.Api
```

Swagger UI available at: `http://localhost:5000/swagger`

---

## Project Structure

```
src/
├── SpendingApi.Domain/          # Entities, Value Objects, Domain Events
├── SpendingApi.Application/     # Use case handlers, abstractions (interfaces)
├── SpendingApi.Infrastructure/  # EF Core, Dapper, cache, idempotency store
└── SpendingApi.Api/             # Endpoints, middleware, observability, rate limiting

tests/
└── SpendingApi.Tests/           # xUnit + Testcontainers integration tests
```

---

## Running Tests

```bash
dotnet test
```

Integration tests use Testcontainers — Docker must be running.

---

## Configuration Reference

| Key | Description | Default |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string | required |
| `OpenTelemetry:Endpoint` | OTLP exporter endpoint | `http://localhost:4317` |
| `Authentication:Schemes:Bearer:Authority` | JWT authority URL | required in prod |
| `Authentication:Schemes:Bearer:Audience` | JWT audience | required in prod |

---

## Production Checklist

- [ ] Swap `InMemoryIdempotencyStore` → Redis-backed store
- [ ] Swap `InMemorySpendingSummaryCache` → Redis-backed cache
- [ ] Set JWT `Authority` and `Audience` in configuration
- [ ] Set `OpenTelemetry:Endpoint` to your collector
- [ ] Ensure PostgreSQL connection uses SSL (`SslMode=Require`)
- [ ] Swagger is disabled in production (guarded by `IsDevelopment()`)
