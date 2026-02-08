// ============================================================================
// File: EventHubSettings.cs
// Purpose: Configuration POCO for Azure Event Hub connectivity, used to
//          receive trade signals from an upstream signal-generation service.
// Binding: Bound from the "EventHub" section of appsettings.json via
//          IOptions<EventHubSettings> in the DI container.
// Notes: Azure Event Hub uses a checkpoint-based consumer model. The
//        BlobStorageConnectionString and BlobContainerName properties
//        configure the Azure Blob Storage account that persists checkpoint
//        offsets, ensuring the consumer resumes from where it left off
//        after restarts.
// ============================================================================

namespace testTradingBotFramework.Configuration;

/// <summary>
/// Configures the Azure Event Hub consumer used to receive trade signals
/// from an external signal-generation pipeline. Includes connection details
/// for both the Event Hub itself and the Azure Blob Storage account used
/// for checkpoint persistence.
/// </summary>
public class EventHubSettings
{
    /// <summary>
    /// The configuration section name used to bind this settings class.
    /// Referenced during service registration (e.g.,
    /// <c>builder.Services.Configure&lt;EventHubSettings&gt;(config.GetSection(EventHubSettings.SectionName))</c>).
    /// </summary>
    public const string SectionName = "EventHub";

    /// <summary>
    /// The full Azure Event Hub connection string, including the namespace
    /// endpoint and shared access key. Obtained from the Azure portal under
    /// the Event Hub namespace's "Shared access policies" blade.
    /// Must be kept confidential -- never log or expose this value.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// The name of the specific Event Hub (topic) to consume messages from.
    /// This is the individual hub within the Event Hub namespace.
    /// </summary>
    public string EventHubName { get; set; } = string.Empty;

    /// <summary>
    /// The consumer group to use when reading from the Event Hub.
    /// Defaults to <c>"$Default"</c>, which is the built-in consumer group
    /// created automatically with every Event Hub. Use a dedicated consumer
    /// group in production if multiple independent consumers exist.
    /// </summary>
    public string ConsumerGroup { get; set; } = "$Default";

    /// <summary>
    /// The Azure Blob Storage connection string used by the
    /// <c>EventProcessorClient</c> to persist checkpoint offsets. This
    /// enables the consumer to resume from its last processed position
    /// after application restarts or crashes.
    /// Must be kept confidential -- never log or expose this value.
    /// </summary>
    public string BlobStorageConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// The name of the blob container where Event Hub checkpoint data is
    /// stored. Defaults to <c>"eventhub-checkpoints"</c>. The container
    /// is typically created automatically by the Event Processor Client
    /// if it does not already exist.
    /// </summary>
    public string BlobContainerName { get; set; } = "eventhub-checkpoints";
}
