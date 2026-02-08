namespace testTradingBotFramework.Configuration;

public class EventHubSettings
{
    public const string SectionName = "EventHub";

    public string ConnectionString { get; set; } = string.Empty;
    public string EventHubName { get; set; } = string.Empty;
    public string ConsumerGroup { get; set; } = "$Default";
    public string BlobStorageConnectionString { get; set; } = string.Empty;
    public string BlobContainerName { get; set; } = "eventhub-checkpoints";
}
