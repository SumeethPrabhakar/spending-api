using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;

namespace SpendingApi.Api.Observability;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var serviceName = "SpendingApi";
        var serviceVersion = "1.0.0";

        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService(serviceName, serviceVersion: serviceVersion)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = environment.EnvironmentName.ToLower(),
                ["service.team"] = "spending"
            });

        services.AddOpenTelemetry()

            // TRACES — track request flow across services
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation(opts =>
                    {
                        opts.RecordException = true;
                        // never record auth headers — PII risk
                        opts.Filter = ctx =>
                            !ctx.Request.Path.StartsWithSegments("/health");
                    })
                    .AddHttpClientInstrumentation();

                if (environment.IsDevelopment())
                    tracing.AddConsoleExporter();
                else
                    tracing.AddOtlpExporter(opts =>
                        opts.Endpoint = new Uri(
                            configuration["OpenTelemetry:Endpoint"]
                            ?? "http://localhost:4317"));
            })

            // METRICS — measure system behaviour
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(SpendingMetrics.MeterName);

                if (environment.IsDevelopment())
                    metrics.AddConsoleExporter();
                else
                    metrics.AddOtlpExporter(opts =>
                        opts.Endpoint = new Uri(
                            configuration["OpenTelemetry:Endpoint"]
                            ?? "http://localhost:4317"));
            });

        return services;
    }

    public static ILoggingBuilder AddObservabilityLogging(
        this ILoggingBuilder logging,
        IWebHostEnvironment environment)
    {
        logging.AddOpenTelemetry(opts =>
        {
            opts.IncludeFormattedMessage = true;
            opts.IncludeScopes = true;

            if (!environment.IsDevelopment())
                opts.AddOtlpExporter();
        });

        return logging;
    }
}
