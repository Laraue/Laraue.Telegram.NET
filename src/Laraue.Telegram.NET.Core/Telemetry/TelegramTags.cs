namespace Laraue.Telegram.NET.Core.Telemetry;

/// <summary>
/// Tag names used on <see cref="LaraueTelegramTelemetry"/> activities and metrics.
/// </summary>
public static class TelegramTags
{
    /// <summary>
    /// The kind of the incoming Telegram update, e.g. <c>Message</c>, <c>CallbackQuery</c>.
    /// </summary>
    public const string UpdateType = "telegram.update_type";

    /// <summary>
    /// The route matched for the update, when any.
    /// </summary>
    public const string RouteName = "telegram.route_name";

    /// <summary>
    /// The outcome of processing the update: <c>success</c>, <c>not_found</c> or <c>error</c>.
    /// </summary>
    public const string Status = "telegram.status";
}
