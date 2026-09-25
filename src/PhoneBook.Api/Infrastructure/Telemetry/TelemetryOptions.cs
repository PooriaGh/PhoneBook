namespace PhoneBook.Api.Infrastructure.Telemetry;

/// <summary>Configuration section <c>Telemetry</c> (feature 002, FR-015).</summary>
public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    /// <summary>OTLP gRPC endpoint, for example <c>http://localhost:4317</c>. Empty disables export.</summary>
    public string? OtlpEndpoint { get; set; }
}
