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
    /// How many seconds Telegram holds a getUpdates request open waiting for new updates, when long pooling
    /// mode is used. While nobody writes to the bot it makes one request per this period instead of one per
    /// <see cref="LongPoolingInterval"/>; a new update still arrives immediately. 0 means short polling,
    /// 30 seconds when not set.
    /// </summary>
    public int? LongPoolingTimeout { get; init; }

    /// <summary>
    /// The interval between long pooling cycles in milliseconds once the bot got no updates for
    /// <see cref="LongPoolingIdleAfter"/>, when long pooling mode is used. The first update switches back to
    /// <see cref="LongPoolingInterval"/>. When not set <see cref="LongPoolingInterval"/> is used all the time.
    /// </summary>
    public int? LongPoolingIdleInterval { get; init; }

    /// <summary>
    /// Milliseconds without updates after which <see cref="LongPoolingIdleInterval"/> is used, when long pooling
    /// mode is used. 10 minutes when not set.
    /// </summary>
    public int? LongPoolingIdleAfter { get; init; }

    /// <summary>
    /// The longest wait in milliseconds between retries while Telegram is unreachable, when long pooling
    /// mode is used. 1 minute when not set.
    /// </summary>
    public int? LongPoolingMaxRetryInterval { get; init; }
    
    /// <summary>
    /// Max telegram updates to load in memory queue from the storage.
    /// </summary>
    public int TelegramUpdatesInMemoryQueueMaxCount { get; init; }
    
    /// <summary>
    /// The interval between loading updates from storage.
    /// </summary>
    public int TelegramUpdatesPoolInterval { get; init; }
}