using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi;
using SpendingApi.Api.Endpoints;
using SpendingApi.Api.Middleware;
using SpendingApi.Api.Observability;
using SpendingApi.Api.RateLimiting;
using SpendingApi.Application.CreateTransaction;
using SpendingApi.Application.SpendingSummary;
using SpendingApi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddInfrastructure(connectionString);

// Application handlers
builder.Services.AddScoped<SpendingSummaryHandler>();
builder.Services.AddScoped<CreateTransactionHandler>();

// Observability — traces, metrics, logs
builder.Services.AddObservability(builder.Configuration, builder.Environment);
builder.Services.AddSingleton<SpendingMetrics>();
builder.Logging.AddObservabilityLogging(builder.Environment);

// Rate limiting — per-customer sliding window, 60 req/min
builder.Services.AddApiRateLimiting();

// Auth
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        if (builder.Environment.IsDevelopment())
        {
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuers = ["spending-api-dev", "spending-api-test"],
                ValidateAudience = true,
                ValidAudience = "spending-api",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                    System.Text.Encoding.UTF8.GetBytes("dev-signing-key-at-least-32-chars!!")),
                ValidateLifetime = true
            };
        }
    });

builder.Services.AddAuthorization();

// Global exception handler — returns RFC 7807 ProblemDetails, never leaks stack traces
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste your JWT token. Get one from GET /dev/token/{customerId}"
    });
    options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer", doc), [] }
    });
});

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Middleware pipeline — order matters
app.UseExceptionHandler();                            // must be outermost — catches everything below
if (!app.Environment.IsDevelopment())
    app.UseMiddleware<SecurityHeadersMiddleware>();   // stamps security headers on every response
app.UseMiddleware<CorrelationIdMiddleware>();      // must be early — sets up logging scope
app.UseMiddleware<IdempotencyMiddleware>();        // before auth — short-circuit duplicates cheaply
app.UseRateLimiter();                             // before auth — reject over-limit before JWT work
app.UseAuthentication();
app.UseAuthorization();

// Endpoints
app.MapSpendingSummaryEndpoints();
app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapDevEndpoints();
}

app.Run();
