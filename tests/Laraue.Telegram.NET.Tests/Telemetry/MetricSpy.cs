using System.Diagnostics.Metrics;
using Laraue.Telegram.NET.Core.Telemetry;

namespace Laraue.Telegram.NET.Tests.Telemetry;

public sealed record RecordedMeasurement(string InstrumentName, double Value, IReadOnlyList<KeyValuePair<string, object?>> Tags);

/// <summary>Captures every measurement recorded on the <c>"Laraue.Telegram.NET"</c> meter while subscribed.</summary>
public sealed class MetricSpy : IDisposable
{
    private readonly MeterListener _listener;
    private readonly List<RecordedMeasurement> _measurements = [];

    public IReadOnlyList<RecordedMeasurement> Measurements => _measurements;

    public MetricSpy()
    {
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == LaraueTelegramTelemetry.SourceName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };

        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            _measurements.Add(new RecordedMeasurement(instrument.Name, value, tags.ToArray())));
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            _measurements.Add(new RecordedMeasurement(instrument.Name, value, tags.ToArray())));

        _listener.Start();
    }

    public void Dispose() => _listener.Dispose();
}
