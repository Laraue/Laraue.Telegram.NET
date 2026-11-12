using Laraue.Telegram.NET.Core.Utils;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Laraue.Telegram.NET.Core.Extensions;

/// <summary>
/// Extensions for the <see cref="ITelegramBotClient"/>.
/// </summary>
public static class TelegramBotClientExtensions
{
    /// <summary>
    /// Sends telegram message builder content to a user.
    /// </summary>
    public static Task SendTextMessageAsync(
        this ITelegramBotClient botClient,
        ChatId chatId,
        TelegramMessageBuilder messageBuilder,
        ParseMode parseMode = default,
        ReplyParameters? replyParameters = default,
        LinkPreviewOptions? linkPreviewOptions = default,
        int? messageThreadId = default,
        IEnumerable<MessageEntity>? entities = default,
        bool disableNotification = default,
        bool protectContent = default,
        string? messageEffectId = default,
        string? businessConnectionId = default,
        bool allowPaidBroadcast = default,
        CancellationToken cancellationToken = default)
    {
        return botClient.SendMessage(
            chatId: chatId,
            text: messageBuilder.Text,
            messageThreadId: messageThreadId,
            replyParameters: replyParameters,
            linkPreviewOptions: linkPreviewOptions,
            parseMode: parseMode,
            entities: entities,
            disableNotification: disableNotification,
            protectContent: protectContent,
            replyMarkup: messageBuilder.InlineKeyboard,
            messageEffectId: messageEffectId,
            businessConnectionId: businessConnectionId,
            allowPaidBroadcast: allowPaidBroadcast,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Sends telegram message builder content to a user as a rich message. The builder text is
    /// rich message HTML: besides the usual inline tags it can contain blocks such as headings,
    /// tables, footers and collapsible details.
    /// See <a href="https://core.telegram.org/bots/api#rich-messages">rich messages</a>.
    /// </summary>
    public static Task SendRichMessageAsync(
        this ITelegramBotClient botClient,
        ChatId chatId,
        TelegramMessageBuilder messageBuilder,
        ReplyParameters? replyParameters = default,
        int? messageThreadId = default,
        bool disableNotification = default,
        bool protectContent = default,
        string? messageEffectId = default,
        string? businessConnectionId = default,
        bool allowPaidBroadcast = default,
        CancellationToken cancellationToken = default)
    {
        return botClient.SendRichMessage(
            chatId: chatId,
            richMessage: ToRichMessage(messageBuilder),
            replyParameters: replyParameters,
            replyMarkup: messageBuilder.InlineKeyboard,
            messageThreadId: messageThreadId,
            disableNotification: disableNotification,
            protectContent: protectContent,
            messageEffectId: messageEffectId,
            businessConnectionId: businessConnectionId,
            allowPaidBroadcast: allowPaidBroadcast,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Edit telegram message with the content of the passed <see cref="TelegramMessageBuilder"/>.
    /// </summary>
    public static Task EditMessageTextAsync(
        this ITelegramBotClient botClient,
        ChatId chatId,
        int messageId,
        TelegramMessageBuilder messageBuilder,
        ParseMode parseMode = ParseMode.None,
        IEnumerable<MessageEntity>? entities = default,
        bool throwOnMessageNotModified = false,
        string? businessConnectionId = default,
        LinkPreviewOptions? linkPreviewOptions = default,
        CancellationToken cancellationToken = default)
    {
        return IgnoreNotModifiedAsync(
            () => botClient.EditMessageText(
                chatId: chatId,
                messageId: messageId,
                text: messageBuilder.Text,
                parseMode: parseMode,
                entities: entities,
                linkPreviewOptions: linkPreviewOptions,
                replyMarkup: messageBuilder.InlineKeyboard,
                businessConnectionId: businessConnectionId,
                cancellationToken: cancellationToken),
            throwOnMessageNotModified);
    }

    /// <summary>
    /// Edit telegram message, replacing its content with a rich message built from the passed
    /// <see cref="TelegramMessageBuilder"/>. Works on both text and rich messages, so a screen
    /// can switch between plain and rich content within one message.
    /// See <see cref="SendRichMessageAsync"/> for the builder text format.
    /// </summary>
    public static Task EditRichMessageAsync(
        this ITelegramBotClient botClient,
        ChatId chatId,
        int messageId,
        TelegramMessageBuilder messageBuilder,
        bool throwOnMessageNotModified = false,
        string? businessConnectionId = default,
        CancellationToken cancellationToken = default)
    {
        return IgnoreNotModifiedAsync(
            () => botClient.EditMessageText(
                chatId: chatId,
                messageId: messageId,
                text: null!,
                richMessage: ToRichMessage(messageBuilder),
                replyMarkup: messageBuilder.InlineKeyboard,
                businessConnectionId: businessConnectionId,
                cancellationToken: cancellationToken),
            throwOnMessageNotModified);
    }

    /// <summary>
    /// Edit telegram message, replacing its content with a rich message built from the passed
    /// <see cref="TelegramMessageBuilder"/>.
    /// </summary>
    public static Task EditRichMessageAsync(
        this ITelegramBotClient botClient,
        TelegramMessageId messageId,
        TelegramMessageBuilder messageBuilder,
        bool throwOnMessageNotModified = false,
        string? businessConnectionId = default,
        CancellationToken cancellationToken = default)
    {
        return botClient.EditRichMessageAsync(
            chatId: messageId.ChatId,
            messageId: messageId.MessageId,
            messageBuilder: messageBuilder,
            throwOnMessageNotModified: throwOnMessageNotModified,
            businessConnectionId: businessConnectionId,
            cancellationToken: cancellationToken);
    }

    private static InputRichMessage ToRichMessage(TelegramMessageBuilder messageBuilder)
    {
        return new InputRichMessage { Html = messageBuilder.Text };
    }

    private static async Task IgnoreNotModifiedAsync(Func<Task> edit, bool throwOnMessageNotModified)
    {
        try
        {
            await edit();
        }
        catch (ApiRequestException e) when (
            e.ErrorCode == 400 && e.Message.StartsWith("Bad Request: message is not modified"))
        {
            if (throwOnMessageNotModified)
            {
                throw;
            }
        }
    }
    
    /// <summary>
    /// Edit telegram message with the content of the passed <see cref="TelegramMessageBuilder"/>.
    /// </summary>
    public static Task EditMessageTextAsync(
        this ITelegramBotClient botClient,
        TelegramMessageId messageId,
        TelegramMessageBuilder messageBuilder,
        ParseMode parseMode = ParseMode.None,
        IEnumerable<MessageEntity>? entities = default,
        bool throwOnMessageNotModified = false,
        string? businessConnectionId = default,
        LinkPreviewOptions? linkPreviewOptions = default,
        CancellationToken cancellationToken = default)
    {
        return botClient.EditMessageTextAsync(
            chatId: messageId.ChatId,
            messageId: messageId.MessageId,
            messageBuilder: messageBuilder,
            parseMode: parseMode,
            entities: entities,
            linkPreviewOptions: linkPreviewOptions,
            throwOnMessageNotModified: throwOnMessageNotModified,
            businessConnectionId: businessConnectionId,
            cancellationToken: cancellationToken);
    }
}