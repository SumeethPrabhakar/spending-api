# SpendingApi — AI Context & Architecture Guide

This file is the single source of truth for AI coding assistants (Claude Code, Cursor, Copilot, Codeium, Aider).
Follow these rules strictly when generating or modifying code in this project.

---

## Project Purpose

A production-ready REST API for aggregating transaction spending by category.
Processes high-volume transaction data and returns real-time spending insights.

**Use case:** A customer views their monthly spend grouped by category
(Groceries $1,240 · Dining $380 · Transport $95). This API powers that screen.

---

## Architecture: Clean Architecture

```
Api → Application → Domain
Infrastructure → Application → Domain
```

Dependencies only point inward. Domain knows nothing about databases or HTTP.

### Layer Rules

| Layer | Project | Allowed Dependencies | Purpose |
|-------|---------|---------------------|---------|
| Domain | `SpendingApi.Domain` | None | Entities, value objects, domain events, domain rules |
| Application | `SpendingApi.Application` | Domain only | Use cases, feature handlers, abstractions |
| Infrastructure | `SpendingApi.Infrastructure` | Application + Domain | EF Core writes, Dapper reads, caching, idempotency, event dispatch |
| Api | `SpendingApi.Api` | Application + Infrastructure | Minimal API endpoints, middleware, auth, rate limiting |
| Tests | `SpendingApi.Tests` | All layers | xUnit, Testcontainers |

### Hard Rules
- `Domain` has ZERO external dependencies — no NuGet packages, language primitives only
- `Domain` entities use private setters — state changes only through methods
- `Api` never returns domain entities — always map to a DTO/response record
- `Infrastructure` never leaks into `Domain` or `Application`
- No business logic in endpoint handlers — delegate to Application handlers

---

## Actual Folder Structure (as built)

```
spending-api/
├── AI.md                          # this file — AI context for all tools
├── CLAUDE.md                      # Claude Code pointer → AI.md
├── Directory.Build.props          # shared MSBuild settings (nullable, warnings as errors)
├── global.json                    # pins .NET 10 LTS SDK
├── .editorconfig                  # code style rules
├── .gitattributes                 # line ending normalisation
├── SpendingApi.slnx               # solution file
│
├── src/
│   ├── SpendingApi.Domain/
│   │   ├── Primitives/
│   │   │   ├── Error.cs           # Error(Code, Message) — returned instead of exceptions
│   │   │   └── Result.cs          # Result<T> — wraps success or failure
│   │   ├── Entities/
│   │   │   ├── Transaction.cs     # core entity — rich domain model, private setters
│   │   │   └── Category.cs        # reference entity — name + icon
│   │   ├── ValueObjects/
│   │   │   └── Money.cs           # Amount + Currency — immutable record, no negatives
│   │   └── Events/
│   │       ├── IDomainEvent.cs    # marker interface — EventId + OccurredAt
│   │       └── TransactionCreatedEvent.cs  # raised by Transaction.Create()
│   │
│   ├── SpendingApi.Application/
│   │   ├── Abstractions/
│   │   │   ├── IRepository.cs             # generic write — Save, Update, Delete
│   │   │   ├── IReadOnlyRepository.cs     # generic read — GetById, GetAll, FindAsync
│   │   │   ├── ISpecification.cs          # filter + ordering contract
│   │   │   ├── BaseSpecification.cs       # base class — stores criteria, ordering, paging
│   │   │   ├── ITransactionProvider.cs    # Dapper complex query contract
│   │   │   ├── IEventDispatcher.cs        # domain event publishing contract
│   │   │   ├── IIdempotencyStore.cs       # idempotency key storage contract
│   │   │   └── ISpendingSummaryCache.cs   # spending summary cache contract
│   │   ├── SpendingSummary/               # feature folder
│   │   │   ├── GetSpendingSummaryQuery.cs
│   │   │   ├── SpendingSummaryHandler.cs  # cache-aside: check cache → DB → store
│   │   │   ├── SpendingSummaryResponse.cs
│   │   │   └── TransactionsByCustomerAndMonthSpec.cs
│   │   └── CreateTransaction/             # feature folder
│   │       ├── CreateTransactionCommand.cs
│   │       ├── CreateTransactionHandler.cs  # save → dispatch events → invalidate cache
│   │       └── CreateTransactionResponse.cs (inside Command file)
│   │
│   ├── SpendingApi.Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── SpendingDbContext.cs
│   │   │   ├── Configurations/
│   │   │   │   ├── TransactionConfiguration.cs  # EF Core — OwnsOne Money, snake_case, soft delete filter
│   │   │   │   └── CategoryConfiguration.cs
│   │   │   ├── Repositories/
│   │   │   │   ├── TransactionRepository.cs  # EF Core writes + simple reads
│   │   │   │   └── CategoryRepository.cs
│   │   │   └── Providers/
│   │   │       └── TransactionProvider.cs    # Dapper + Polly resilience pipeline
│   │   ├── Caching/
│   │   │   └── InMemorySpendingSummaryCache.cs  # IMemoryCache — swap for Redis in prod
│   │   ├── Idempotency/
│   │   │   └── InMemoryIdempotencyStore.cs  # ConcurrentDictionary + 24h TTL — swap for Redis in prod
│   │   ├── Messaging/
│   │   │   └── EventDispatcher.cs           # publishes domain events (stub → SNS/SQS in prod)
│   │   └── DependencyInjection.cs           # registers all infrastructure services
│   │
│   └── SpendingApi.Api/
│       ├── Endpoints/
│       │   ├── SpendingSummaryEndpoints.cs  # GET spending-summary + POST transactions
│       │   └── CreateTransactionRequest.cs  # API-layer DTO (separate from Application command)
│       ├── Middleware/
│       │   ├── SecurityHeadersMiddleware.cs  # HSTS, CSP, X-Frame-Options, etc.
│       │   ├── CorrelationIdMiddleware.cs    # X-Correlation-ID — uses OTel TraceId
│       │   └── IdempotencyMiddleware.cs      # Idempotency-Key header — cache POST/PUT/PATCH
│       ├── Observability/
│       │   ├── ObservabilityExtensions.cs   # OTel traces + metrics + logs wiring
│       │   └── SpendingMetrics.cs           # custom counters + histograms
│       ├── RateLimiting/
│       │   └── RateLimitingExtensions.cs    # per-customer sliding window, 60 req/min
│       └── Program.cs                       # middleware pipeline + DI wiring
│
└── tests/
    └── SpendingApi.Tests/
```

---

## Middleware Pipeline Order (Program.cs)

```
Request in
    │
    ▼  SecurityHeadersMiddleware   → stamps security headers on every response
    ▼  CorrelationIdMiddleware     → OTel TraceId becomes correlation ID, attached to logger scope
    ▼  IdempotencyMiddleware       → intercepts POST/PUT/PATCH, returns cached response on duplicate key
    ▼  UseRateLimiter()            → per-customer sliding window — rejects 429 before JWT work
    ▼  UseAuthentication()         → validates JWT Bearer token
    ▼  UseAuthorization()          → enforces [RequireAuthorization] policies
    ▼  Endpoints
```

Order is intentional:
- Security headers go first — always stamped even on error responses
- Idempotency before rate limiter — duplicate suppression is cheaper than rate counting
- Rate limiter before auth — no JWT validation cost on rejected requests

---

## Request Flow — GET Spending Summary

```
GET /api/v1/customers/{customerId}/spending-summary?year=2026&month=6
    │
    ├── All middleware (see above)
    │
    └── SpendingSummaryEndpoints
            │
            ├── Auth boundary check    → JWT sub must match customerId (403 if not)
            │
            └── SpendingSummaryHandler.HandleAsync(query)
                    │
                    ├── Validate month/year
                    │
                    ├── ISpendingSummaryCache.GetAsync()    ← cache-aside: check first
                    │       hit  → return cached response immediately
                    │       miss → continue to DB
                    │
                    ├── ITransactionProvider.GetByCustomerAndMonthAsync()
                    │       └── Polly pipeline (retry 3×, timeout 10s)
                    │               └── Dapper SQL JOIN transactions + categories
                    │
                    ├── IReadOnlyRepository<Category>.GetAllAsync()
                    │       └── EF Core SELECT * FROM categories
                    │
                    ├── Group by CategoryId → sum → sort by spend
                    │
                    ├── ISpendingSummaryCache.SetAsync()    ← store for 5 minutes
                    │
                    └── Return Result<SpendingSummaryResponse>
```

## Request Flow — POST Create Transaction

```
POST /api/v1/customers/{customerId}/transactions
Idempotency-Key: <uuid>
    │
    ├── IdempotencyMiddleware → duplicate key? return cached 201 immediately
    │
    └── CreateTransactionEndpoint
            │
            ├── Auth boundary check
            │
            └── CreateTransactionHandler.HandleAsync(command)
                    │
                    ├── IReadOnlyRepository<Category>.GetByIdAsync()  → validate category exists
                    ├── Money.Create(amount, currency)                 → validate amount ≥ 0
                    ├── Transaction.Create(...)                        → raises TransactionCreatedEvent
                    ├── IRepository<Transaction>.SaveAsync()           → EF Core INSERT
                    ├── IEventDispatcher.DispatchAsync(events)         → publish domain events
                    ├── ISpendingSummaryCache.InvalidateAsync()        → evict stale summary
                    └── Return Result<CreateTransactionResponse>
```

---

## Key Patterns Used

### Result\<T\> — no exceptions for business failures
```csharp
public async Task<Result<SpendingSummaryResponse>> HandleAsync(query)

return result.IsFailure
    ? Results.Problem(result.Error.Message)
    : Results.Ok(result.Value);
```

### Rich Domain Model — behaviour on entities
```csharp
// State changes through methods only — not public setters
transaction.Settle();
transaction.Reverse();
transaction.SoftDelete();
```

### Two factory methods on Transaction
```csharp
Transaction.Create(...)  // new — raises TransactionCreatedEvent
Transaction.Load(...)    // from DB — no events, just rebuilds state
```

### Repository + Provider split (hybrid ORM)
```csharp
IRepository<Transaction>    // EF Core — writes
ITransactionProvider        // Dapper — complex multi-table reads
IReadOnlyRepository<T>      // EF Core — simple reads
```

### Cache-aside pattern
```csharp
// In SpendingSummaryHandler
var cached = await _cache.GetAsync(customerId, year, month, ct);
if (cached is not null) return cached;               // cache hit — return immediately

var result = await _provider.GetByCustomerAndMonthAsync(...);
// ... compute summary ...
await _cache.SetAsync(customerId, year, month, summary, ct);  // store
return summary;

// In CreateTransactionHandler — after save
await _summaryCache.InvalidateAsync(customerId, year, month, ct);  // evict on write
```

### Idempotency — write-once, return-cached
```csharp
// IdempotencyMiddleware checks Idempotency-Key header on POST/PUT/PATCH
// First call: process + cache response
// Duplicate: return cached response without touching DB
```

### Per-customer rate limiting
```csharp
// Partitioned by JWT sub claim — not by IP
RateLimitPartition.GetSlidingWindowLimiter(customerId, _ => new()
{
    Window = TimeSpan.FromMinutes(1),
    SegmentsPerWindow = 6,
    PermitLimit = 60
});
```

### Resilience pipeline (Polly) on DB reads
```csharp
// TransactionProvider — wraps all Dapper queries
private static readonly ResiliencePipeline Pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 3, BackoffType = Exponential, UseJitter = true })
    .AddTimeout(TimeSpan.FromSeconds(10))
    .Build();
```

### OpenTelemetry — three pillars
```csharp
// Traces  — AddAspNetCoreInstrumentation (every request = one span)
// Metrics — AddRuntimeInstrumentation + custom SpendingMetrics (counter, histogram)
// Logs    — AddOpenTelemetry with IncludeScopes = true (CorrelationId on every line)
```

### Security headers on every response
```csharp
// SecurityHeadersMiddleware stamps:
// X-Content-Type-Options, X-Frame-Options, CSP, HSTS, Referrer-Policy, Permissions-Policy
```

### Soft Delete — never hard delete financial data
```csharp
transaction.SoftDelete();  // sets DeletedAt = DateTime.UtcNow
// EF Core HasQueryFilter(t => t.DeletedAt == null) filters these globally
```

### Auth Boundary — JWT sub must match URL customerId
```csharp
var authenticatedCustomerId = GetCustomerIdFromToken(httpContext);
if (authenticatedCustomerId != customerId)
    return Results.Problem(statusCode: 403);
```

---

## Tech Stack (as built)

| Concern | Choice | Notes |
|---------|--------|-------|
| Runtime | .NET 10 LTS | |
| API style | Minimal API | No controllers |
| ORM | EF Core 10 + Dapper | EF Core writes, Dapper complex reads |
| Database | PostgreSQL | Via Npgsql |
| Auth | JWT Bearer | Validate `sub` == customerId on every endpoint |
| Error pattern | `Result<T>` | No exceptions for business failures |
| Error responses | `Results.Problem` (RFC 7807) | Consistent ProblemDetails format |
| Caching | `IMemoryCache` → Redis | Cache-aside, 5-min TTL, invalidated on write |
| Idempotency | In-memory → Redis | `Idempotency-Key` header, 24h TTL |
| Rate limiting | Built-in `AddRateLimiter` | Per-customer sliding window, 60 req/min |
| Resilience | Polly v8 | Retry 3×, exponential backoff + jitter, 10s timeout |
| Observability | OpenTelemetry | Traces + metrics + logs, OTLP exporter in prod |
| Correlation | OTel TraceId → X-Correlation-ID | Same ID in traces and logs |
| Security | SecurityHeadersMiddleware | HSTS, CSP, X-Frame-Options, nosniff |
| Domain events | `IDomainEvent` | Raised in Domain, dispatched in Application |
| API docs | Swagger (dev only) | |
| Health | `/health` | For load balancer checks |

---

## Production Swap Guide

These are the only lines to change when moving from dev to production:

| Dev | Production | File |
|-----|-----------|------|
| `InMemoryIdempotencyStore` | `RedisIdempotencyStore` | `DependencyInjection.cs` |
| `InMemorySpendingSummaryCache` | `RedisSpendingSummaryCache` | `DependencyInjection.cs` |
| `ConsoleExporter` | `OtlpExporter` | `ObservabilityExtensions.cs` (auto via env) |
| `EventDispatcher` (stub) | SNS/SQS publisher | `DependencyInjection.cs` |

---

## Naming Conventions

```
Classes, Methods, Properties  →  PascalCase
Private fields                →  _camelCase
Local variables               →  camelCase
Interfaces                    →  I + PascalCase
Repositories (EF Core)        →  [Entity]Repository
Providers (Dapper)            →  [Entity]Provider
Handlers                      →  [Feature]Handler
Queries                       →  [Feature]Query / Get[Feature]Query
Commands                      →  [Action][Entity]Command
Responses                     →  [Feature]Response
Specs                         →  [Description]Spec
Events                        →  [Entity][Action]Event
Caches                        →  [Entity]Cache (impl) / I[Entity]Cache (abstraction)
```

---

## Banking-Specific Rules (non-negotiable)

- **Idempotency** — all write endpoints must accept `Idempotency-Key` header
- **Audit fields** — all DB tables must have `created_at`, `updated_at`, `deleted_at`
- **Soft delete** — use `deleted_at` — never hard delete financial data
- **No PII in logs** — never log card numbers, CVV, account numbers, or full customer details
- **Auth boundary** — always validate JWT `sub` claim matches the requested customer ID
- **AUD currency** — all monetary values stored as `decimal(18,2)`
- **Parameterised queries only** — never concatenate SQL strings
- **Global query filter** — EF Core `HasQueryFilter` applied to all entities for soft delete
- **No negative amounts** — `Money.Create` rejects negative values
- **HTTPS only** — HSTS header enforced via `SecurityHeadersMiddleware`

---

## What NOT to Generate

- No `AutoMapper` — map manually
- No `MediatR` — use handlers directly
- No `[ApiController]` or controller classes — Minimal API only
- No hardcoded connection strings — use `IConfiguration`
- No `Console.WriteLine` — use `ILogger<T>`
- No raw `catch (Exception e)` swallowing
- No public setters on domain entities
- No domain logic in Infrastructure or Api layers
- No `AllowAnyOrigin` CORS — use explicit allowed origins
- No Swagger in production — dev-only guard in `Program.cs`
