using System.Text;
using Telegram.Bot.Requests;
using Telegram.Bot.Types.ReplyMarkups;

namespace Laraue.Telegram.NET.Testing;

public static class AssertExtensions
{
    extension(EditMessageTextRequest request)
    {
        /// <summary>
        /// Asserts that client telegram message is equal to the passed. 
        /// </summary>
        public void CheckMessage(string expected)
        {
            AssertEqual(expected, request.Text);
        }

        /// <summary>
        /// Asserts that the message was edited into a rich message with exactly the passed HTML.
        /// </summary>
        public void CheckRichMessage(string expectedHtml)
        {
            if (request.RichMessage is null)
                throw new TelegramNetAssertException(
                    $"Expected a rich message edit, but the message was edited to text '{request.Text}'");

            AssertEqual(expectedHtml, request.RichMessage.Html);
        }

        /// <summary>
        /// Asserts that client telegram message has exactly the passed rows count.
        /// </summary>
        public void HasButtonsRowsCount(int exceptedRowsCount)
        {
            AssertEqual(exceptedRowsCount, GetButtons(request).Length);
        }

        /// <summary>
        /// Asserts the specific buttons of telegram message.
        /// </summary>
        /// <param name="buildAssert"></param>
        public void CheckButtons(Action<CheckButtonsAssert> buildAssert)
        {
            var checkButtonAssert = new CheckButtonsAssert();
            buildAssert(checkButtonAssert);

            var realButtons = GetButtons(request);
            foreach (var assert in checkButtonAssert.Asserts)
            {
                HasButtonRow(request, assert.Row, realButtons, assert.Asserts);
            }
        }
        
        /// <summary>
        /// Asserts all buttons rows of telegram message sequentially.
        /// Throw if any of rows have not been checked.
        /// </summary>
        public void CheckButtonsSequentially(Action<CheckSequentiallyButtonsAssert> buildAssert)
        {
            var checkButtonAssert = new CheckSequentiallyButtonsAssert();
            buildAssert(checkButtonAssert);

            var realButtons = GetButtons(request);
            for (var index = 0; index < checkButtonAssert.Asserts.Count; index++)
            {
                var assert = checkButtonAssert.Asserts[index];
                HasButtonRow(request, index, realButtons, assert);
            }

            var assertsCount = checkButtonAssert.Asserts.Count;
            if (realButtons.Length != assertsCount)
            {
                var sb = new StringBuilder();
                foreach (var nonCheckedRow in realButtons
                    .Skip(checkButtonAssert.Asserts.Count))
                {
                    sb
                        .Append('[')
                        .AppendJoin(", ", nonCheckedRow
                        .Select(nonCheckedButton =>
                            $"Text = {nonCheckedButton.Text}, CallbackData = {nonCheckedButton.CallbackData}"))
                        .Append(']');
                }

                throw new TelegramNetAssertException(
                    $"Not all items of collection checked. Investigate unchecked items below.{Environment.NewLine}{sb}");
            }
        }

        private void HasButtonRow(
            int row,
            IEnumerable<InlineKeyboardButton>[] realButtons,
            params ButtonAssert[] asserts)
        {
            AssertTrue(realButtons.Length >= row);
            
            var realRow = realButtons[row];
            var assertItems = realRow
                .Select(r => new ButtonAssert(
                    r.Text,
                    r.CallbackData))
                .ToArray();

            
            if (!asserts.SequenceEqual(assertItems))
            {
                var assertsString = string.Join(",", asserts);
                var actualString = string.Join(",", assertItems);
                
                throw new TelegramNetAssertException(
                    $"Assert for button row #{row + 1} failed.{Environment.NewLine} Excepted:[{assertsString}]{Environment.NewLine}Actual:[{actualString}]");
            }
        }

        private IEnumerable<InlineKeyboardButton>[] GetButtons()
        {
            return request
                .ReplyMarkup
                ?.InlineKeyboard.ToArray() ?? [];
        }
    }

    private static void AssertEqual<T>(T? excepted, T? actual)
    {
        if (excepted is null && actual is null)
            return;
        
        if (excepted is null && actual is not null || excepted is not null && actual is null)
            throw new TelegramNetAssertException($"Excepted value {excepted} is not equal to {actual}");
        
        var isEqual = excepted!.Equals(actual);
        if (!isEqual)
            throw new TelegramNetAssertException($"Excepted value {excepted} is not equal to {actual}");
    }
    
    private static void AssertTrue(bool excepted)
    {
        if (!excepted)
            throw new TelegramNetAssertException($"Assertion failed");
    }
}

/// <summary>
/// Assert data of a button.
/// </summary>
public record ButtonAssert(string Text, string? CallbackData);

/// <summary>
/// Assert data of a buttons row.
/// </summary>
public record ButtonRowAssert(int Row, params ButtonAssert[] Asserts);

public class CheckButtonsAssert
{
    internal readonly List<ButtonRowAssert> Asserts = new ();
    public CheckButtonsAssert HasButtonsRow(int row, params ButtonAssert[] asserts)
    {
        Asserts.Add(new(row, asserts));

        return this;
    }
}

public class CheckSequentiallyButtonsAssert
{
    internal readonly List<ButtonAssert[]> Asserts = new ();
    public CheckSequentiallyButtonsAssert HasButtonsRow(params ButtonAssert[] asserts)
    {
        Asserts.Add(asserts);

        return this;
    }
}