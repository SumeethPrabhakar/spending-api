using System.Diagnostics.Metrics;

namespace SpendingApi.Api.Observability;

public sealed class SpendingMetrics
{
    public const string MeterName = "SpendingApi";

    private readonly Counter<long> _summaryRequestCounter;
    private readonly Histogram<double> _summaryDuration;
    private readonly Counter<long> _summaryFailureCounter;

    public SpendingMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _summaryRequestCounter = meter.CreateCounter<long>(
            "spending_summary_requests_total",
            description: "Total number of spending summary requests");

        _summaryDuration = meter.CreateHistogram<double>(
            "spending_summary_duration_ms",
            unit: "ms",
            description: "Spending summary handler duration in milliseconds");

        _summaryFailureCounter = meter.CreateCounter<long>(
            "spending_summary_failures_total",
            description: "Total number of spending summary failures");
    }

    public void RecordRequest(Guid customerId) =>
        _summaryRequestCounter.Add(1, new KeyValuePair<string, object?>("customer_id", customerId));

    public void RecordDuration(double milliseconds) =>
        _summaryDuration.Record(milliseconds);

    public void RecordFailure(string errorCode) =>
        _summaryFailureCounter.Add(1, new KeyValuePair<string, object?>("error_code", errorCode));
}
