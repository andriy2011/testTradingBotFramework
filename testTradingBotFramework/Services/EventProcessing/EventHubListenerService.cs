// -----------------------------------------------------------------------
// EventHubListenerService.cs
//
// Azure Event Hub consumer that receives trade signals from an upstream
// signal-generation pipeline. Uses the EventProcessorClient SDK for
// reliable, partitioned consumption with Azure Blob Storage-based
// checkpointing, ensuring at-least-once delivery semantics.
//
// Processing pipeline per event:
//   1. Receive raw event from Event Hub partition.
//   2. UTF-8 decode the event body into a JSON string.
//   3. Parse via ISignalParser.Parse() to produce a TradeSignal (or null).
//   4. If valid, dispatch via ISignalDispatcher.DispatchAsync() which
//      forwards to OrderManager for execution.
//   5. Checkpoint the event so it is not reprocessed on restart.
//
// The service gracefully skips startup when no ConnectionString is
// configured, allowing the application to run without Event Hub in
// development/test environments.
//
// After the processor starts, Task.Delay(Infinite) keeps the hosted
// service alive while the processor handles events via callbacks.
// On cancellation (app shutdown), the processor is stopped cleanly.
// -----------------------------------------------------------------------

using System.Text;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using testTradingBotFramework.Configuration;

namespace testTradingBotFramework.Services.EventProcessing;

/// <summary>
/// Background service that connects to an Azure Event Hub, receives trade signal
/// events, parses them into <c>TradeSignal</c> objects, and dispatches them to
/// the order management layer for execution. Checkpoints each event to Azure Blob
/// Storage to track consumer progress across restarts.
/// </summary>
public class EventHubListenerService : BackgroundService
{
    /// <summary>Parses raw JSON event bodies into strongly-typed <c>TradeSignal</c> objects.</summary>
    private readonly ISignalParser _signalParser;

    /// <summary>Routes parsed trade signals to the order management layer.</summary>
    private readonly ISignalDispatcher _signalDispatcher;

    /// <summary>Event Hub connection and consumer group configuration.</summary>
    private readonly EventHubSettings _settings;

    /// <summary>Logger for lifecycle events, received messages, and processing errors.</summary>
    private readonly ILogger<EventHubListenerService> _logger;

    /// <summary>
    /// The Azure Event Processor Client instance. Nullable because it is only
    /// created if a valid connection string is provided at startup.
    /// </summary>
    private EventProcessorClient? _processor;

    public EventHubListenerService(
        ISignalParser signalParser,
        ISignalDispatcher signalDispatcher,
        IOptions<EventHubSettings> settings,
        ILogger<EventHubListenerService> logger)
    {
        _signalParser = signalParser;
        _signalDispatcher = signalDispatcher;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.ConnectionString))
        {
            _logger.LogWarning("Event Hub connection string is not configured. EventHubListenerService will not start.");
            return;
        }

        _logger.LogInformation("Starting Event Hub listener for hub '{HubName}', consumer group '{ConsumerGroup}'",
            _settings.EventHubName, _settings.ConsumerGroup);

        var blobClient = new BlobContainerClient(
            _settings.BlobStorageConnectionString, _settings.BlobContainerName);

        _processor = new EventProcessorClient(
            blobClient,
            _settings.ConsumerGroup,
            _settings.ConnectionString,
            _settings.EventHubName);

        _processor.ProcessEventAsync += ProcessEventAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);

        _logger.LogInformation("Event Hub processor started");

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Event Hub listener stopping...");
        }

        await _processor.StopProcessingAsync();
    }

    private async Task ProcessEventAsync(ProcessEventArgs args)
    {
        if (args.Data is null) return;

        var body = Encoding.UTF8.GetString(args.Data.EventBody.ToArray());
        _logger.LogDebug("Received event: {Body}", body);

        var signal = _signalParser.Parse(body);
        if (signal is not null)
        {
            await _signalDispatcher.DispatchAsync(signal, args.CancellationToken);
        }

        await args.UpdateCheckpointAsync(args.CancellationToken);
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Event Hub processing error. Partition: {Partition}, Operation: {Operation}",
            args.PartitionId, args.Operation);
        return Task.CompletedTask;
    }
}
