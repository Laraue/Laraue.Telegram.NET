namespace Laraue.Telegram.NET.Core;

/// <summary>
/// Options for Telegram.NET client.
/// </summary>
public class TelegramNetOptions
{
    /// <summary>
    /// Bot token.
    /// </summary>
    public required string Token { get; init; }
    
    /// <summary>
    /// Webhook url for the bot. When the field is filled webhook mode could be run,
    /// otherwise long pooling mode only supported.
    /// </summary>
    public string? WebhookUrl { get; init; }
    
    /// <summary>
    /// The count of messages to request when long pooling mode is used.
    /// </summary>
    public int? LongPoolingBatchSize { get; init; }
    
    /// <summary>
    /// The interval between long pooling cycles in milliseconds, used while the bot gets updates.
    /// </summary>
    public int? LongPoolingInterval { get; init; }

    /// <summary>
    /// How many seconds Telegram holds a getUpdates request open waiting for new updates.
    /// While nobody writes to the bot it makes one request per this period instead of one per
    /// <see cref="LongPoolingInterval"/>; a new update still arrives immediately. 0 means short polling.
    /// </summary>
    public int LongPoolingTimeout { get; init; } = 30;

    /// <summary>
    /// The interval between long pooling cycles in milliseconds once the bot got no updates for
    /// <see cref="LongPoolingIdleAfter"/>. The first update switches back to <see cref="LongPoolingInterval"/>.
    /// Null keeps <see cref="LongPoolingInterval"/> all the time.
    /// </summary>
    public int? LongPoolingIdleInterval { get; init; }

    /// <summary>
    /// Milliseconds without updates after which <see cref="LongPoolingIdleInterval"/> is used. 10 minutes by default.
    /// </summary>
    public int LongPoolingIdleAfter { get; init; } = 600_000;

    /// <summary>
    /// The longest wait in milliseconds between retries while Telegram is unreachable. 1 minute by default.
    /// </summary>
    public int LongPoolingMaxRetryInterval { get; init; } = 60_000;
    
    /// <summary>
    /// Max telegram updates to load in memory queue from the storage.
    /// </summary>
    public int TelegramUpdatesInMemoryQueueMaxCount { get; init; }
    
    /// <summary>
    /// The interval between loading updates from storage.
    /// </summary>
    public int TelegramUpdatesPoolInterval { get; init; }
}