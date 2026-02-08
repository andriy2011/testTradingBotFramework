using System.Text;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using testTradingBotFramework.Configuration;

namespace testTradingBotFramework.Services.EventProcessing;

public class EventHubListenerService : BackgroundService
{
    private readonly ISignalParser _signalParser;
    private readonly ISignalDispatcher _signalDispatcher;
    private readonly EventHubSettings _settings;
    private readonly ILogger<EventHubListenerService> _logger;
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
