using Laraue.Telegram.NET.Core.Extensions;
using Laraue.Telegram.NET.Core.Utils;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types.ReplyMarkups;
using Xunit;

namespace Laraue.Telegram.NET.Tests.Extensions;

public class RichMessageTests
{
    private readonly Mock<ITelegramBotClient> _client = new();

    private static TelegramMessageBuilder Builder() => new TelegramMessageBuilder()
        .AppendRow("<h3>Title</h3>")
        .AppendRow("<table><tr><td>1</td></tr></table>")
        .AddInlineKeyboardButtons([InlineKeyboardButton.WithCallbackData("Menu", "menu")]);

    [Fact]
    public async Task SendRichMessage_SendsBuilderTextAsRichHtml_WithKeyboard()
    {
        await _client.Object.SendRichMessageAsync(42, Builder(), cancellationToken: TestContext.Current.CancellationToken);

        var request = Assert.IsType<SendRichMessageRequest>(Assert.Single(_client.Invocations).Arguments[0]);
        Assert.Equal(42, request.ChatId.Identifier);
        Assert.Equal(Builder().Text, request.RichMessage.Html);
        Assert.Null(request.RichMessage.Markdown);
        Assert.Equal("menu", Assert.IsType<InlineKeyboardMarkup>(request.ReplyMarkup).InlineKeyboard.Single().Single().CallbackData);
    }

    [Fact]
    public async Task EditRichMessage_ReplacesContentWithRichHtml_AndNoText()
    {
        await _client.Object.EditRichMessageAsync(new TelegramMessageId(42, 7), Builder(), cancellationToken: TestContext.Current.CancellationToken);

        var request = Assert.IsType<EditMessageTextRequest>(Assert.Single(_client.Invocations).Arguments[0]);
        Assert.Equal(42, request.ChatId.Identifier);
        Assert.Equal(7, request.MessageId);
        Assert.Null(request.Text);
        Assert.Equal(Builder().Text, request.RichMessage!.Html);
        Assert.Equal("menu", request.ReplyMarkup!.InlineKeyboard.Single().Single().CallbackData);
    }
}
