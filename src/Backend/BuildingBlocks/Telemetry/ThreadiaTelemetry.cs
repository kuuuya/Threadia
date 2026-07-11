using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Threadia.BuildingBlocks.Telemetry;

/// <summary>アプリ独自の計装ポイント。</summary>
public static class ThreadiaDiagnostics
{
    public const string SourceName = "Threadia";

    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> OutboxProcessed =
        Meter.CreateCounter<long>("threadia.outbox.processed", description: "処理に成功した Outbox イベント数");

    public static readonly Counter<long> OutboxFailed =
        Meter.CreateCounter<long>("threadia.outbox.failed", description: "処理に失敗した Outbox イベント数(リトライ含む)");

    public static readonly Counter<long> OutboxDeadLettered =
        Meter.CreateCounter<long>("threadia.outbox.dead_lettered", description: "リトライ上限を超えた Outbox イベント数");
}

public static class TelemetryExtensions
{
    /// <summary>
    /// OpenTelemetry のトレース・メトリクスを構成する。
    /// Otel:OtlpEndpoint が設定されている場合のみ OTLP エクスポートを有効化する
    /// (未設定でも計装自体は動き、コレクタ導入時に設定だけで出力できる)。
    /// </summary>
    public static IServiceCollection AddThreadiaTelemetry(
        this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        var otlpEndpoint = configuration["Otel:OtlpEndpoint"];

        var builder = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource(ThreadiaDiagnostics.SourceName)
                    .AddSource("Npgsql");

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(ThreadiaDiagnostics.SourceName);

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    metrics.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                }
            });

        _ = builder;
        return services;
    }
}
