using Telegram.Bot;

namespace LarkTG.Source.Utils;

public static class BotUtilities
{
    private static readonly Dictionary<string, string> CommandAliases = new()
    {
        { "/go", "/start_game" },
        { "/stop", "/stop_game" },
        { "/info", "/game_info" },
        { "/config", "/settings" },
        { "/dices", "/dice" },
        { "/dice", "/dices" }
    };

    private static readonly HashSet<string> ValidCommands = new()
    {
        "/start_game", "/stop_game", "/join", "/leave", "/game_info",
        "/settings", "/help", "/dices", "/start"
    };

    /// <summary>
    /// Normalizes a command string, handling aliases and variations
    /// </summary>
    public static string NormalizeCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return string.Empty;

        var normalizedCommand = command.ToLower().Trim();

        // Handle bot mentions in commands like "/start@botname"
        var atIndex = normalizedCommand.IndexOf('@');
        if (atIndex > 0)
        {
            normalizedCommand = normalizedCommand[..atIndex];
        }

        // Apply aliases
        return CommandAliases.TryGetValue(normalizedCommand, out var alias) ? alias : normalizedCommand;
    }

    /// <summary>
    /// Checks if a command is valid
    /// </summary>
    public static bool IsValidCommand(string command)
    {
        var normalized = NormalizeCommand(command);
        return ValidCommands.Contains(normalized);
    }

    /// <summary>
    /// Formats user mention for display
    /// </summary>
    public static string FormatUserMention(TUser user)
    {
        return user.Username is not null
            ? $"[{user.FirstName}](https://t.me/{user.Username})"
            : $"**{user.FirstName}**";
    }

    /// <summary>
    /// Creates a safe message that won't exceed Telegram's limits
    /// </summary>
    public static string TruncateMessage(string message, int maxLength = 4096)
    {
        if (string.IsNullOrEmpty(message) || message.Length <= maxLength)
            return message;

        var truncated = message[..(maxLength - 50)];
        var lastNewline = truncated.LastIndexOf('\n');

        if (lastNewline > maxLength - 200)
        {
            truncated = truncated[..lastNewline];
        }

        return truncated + "\n\n... _(message truncated)_";
    }

    /// <summary>
    /// Safely deletes a message without throwing exceptions
    /// </summary>
    public static async Task SafeDeleteMessageAsync(ITelegramBotClient botClient, long chatId, int messageId, int delayMs = 0)
    {
        try
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs);
            }
            await botClient.DeleteMessage(chatId, messageId);
        }
        catch (Exception)
        {
            // Silently ignore deletion errors (message might already be deleted, bot might not have permissions, etc.)
        }
    }

    /// <summary>
    /// Safely edits a message without throwing exceptions
    /// </summary>
    public static async Task<bool> SafeEditMessageAsync(ITelegramBotClient botClient, long chatId, int messageId,
        string text, ParseMode parseMode = ParseMode.Markdown, InlineKeyboardMarkup? replyMarkup = null)
    {
        try
        {
            await botClient.EditMessageText(chatId, messageId, text, parseMode: parseMode, replyMarkup: replyMarkup);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a temporary message that auto-deletes after a delay
    /// </summary>
    public static async Task<Message> SendTemporaryMessageAsync(ITelegramBotClient botClient, long chatId,
        string text, int deleteAfterMs = 5000, ParseMode parseMode = ParseMode.Markdown, int? replyToMessageId = null)
    {
        var message = await botClient.SendMessage(
            chatId,
            text,
            parseMode: parseMode,
            replyParameters: replyToMessageId.HasValue ? new ReplyParameters { MessageId = replyToMessageId.Value } : null);

        // Schedule deletion
        _ = Task.Run(async () =>
        {
            await Task.Delay(deleteAfterMs);
            await SafeDeleteMessageAsync(botClient, chatId, message.MessageId);
        });

        return message;
    }

    /// <summary>
    /// Formats time remaining in a human-readable way
    /// </summary>
    public static string FormatTimeRemaining(TimeSpan timeRemaining)
    {
        if (timeRemaining.TotalSeconds <= 0)
            return "⏰ Time's up!";

        if (timeRemaining.TotalDays >= 1)
            return $"⏱️ {timeRemaining.Days}d {timeRemaining.Hours}h remaining";

        if (timeRemaining.TotalHours >= 1)
            return $"⏱️ {timeRemaining.Hours}h {timeRemaining.Minutes}m remaining";

        if (timeRemaining.TotalMinutes >= 1)
            return $"⏱️ {timeRemaining.Minutes}m {timeRemaining.Seconds}s remaining";

        return $"⏱️ {timeRemaining.Seconds}s remaining";
    }

    /// <summary>
    /// Formats a duration in a human-readable way
    /// </summary>
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
            return $"{duration.Days}d {duration.Hours}h {duration.Minutes}m";

        if (duration.TotalHours >= 1)
            return $"{duration.Hours}h {duration.Minutes}m";

        if (duration.TotalMinutes >= 1)
            return $"{duration.Minutes}m {duration.Seconds}s";

        return $"{duration.Seconds}s";
    }

    /// <summary>
    /// Creates a progress bar string
    /// </summary>
    public static string CreateProgressBar(int current, int total, int length = 10, char filled = '█', char empty = '░')
    {
        if (total <= 0) return new string(empty, length);

        var progress = Math.Min(1.0, Math.Max(0.0, (double)current / total));
        var filledLength = (int)(progress * length);
        var emptyLength = length - filledLength;

        return new string(filled, filledLength) + new string(empty, emptyLength);
    }

    /// <summary>
    /// Escapes markdown special characters for safe display
    /// </summary>
    public static string EscapeMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var specialChars = new[] { '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!' };

        foreach (var ch in specialChars)
        {
            text = text.Replace(ch.ToString(), $"\\{ch}");
        }

        return text;
    }

    /// <summary>
    /// Gets a user-friendly error message for common exceptions
    /// </summary>
    public static string GetUserFriendlyErrorMessage(Exception ex)
    {
        return ex switch
        {
            TimeoutException => "⏰ The operation timed out. Please try again.",
            UnauthorizedAccessException => "🔐 Bot doesn't have permission to perform this action.",
            InvalidOperationException => "❌ Cannot perform this action right now.",
            ArgumentException => "❌ Invalid input provided.",
            _ => "❌ An unexpected error occurred. Please try again."
        };
    }

    /// <summary>
    /// Validates that a group has the necessary permissions for the bot
    /// </summary>
    public static async Task<bool> ValidateGroupPermissionsAsync(ITelegramBotClient botClient, long chatId)
    {
        try
        {
            var botMember = await botClient.GetChatMember(chatId, (await botClient.GetMe()).Id);

            return botMember.Status == ChatMemberStatus.Administrator ||
                   botMember.Status == ChatMemberStatus.Member;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a button layout that fits well on mobile devices
    /// </summary>
    public static InlineKeyboardMarkup CreateResponsiveKeyboard(List<(string text, string callbackData)> buttons, int maxButtonsPerRow = 2)
    {
        var rows = new List<InlineKeyboardButton[]>();

        for (int i = 0; i < buttons.Count; i += maxButtonsPerRow)
        {
            var rowButtons = buttons
                .Skip(i)
                .Take(maxButtonsPerRow)
                .Select(b => InlineKeyboardButton.WithCallbackData(b.text, b.callbackData))
                .ToArray();

            rows.Add(rowButtons);
        }

        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>
    /// Generates a unique session ID with embedded timestamp for debugging
    /// </summary>
    public static Guid GenerateSessionId()
    {
        // Create a GUID that includes timestamp information for easier debugging
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var guidBytes = Guid.NewGuid().ToByteArray();

        // Embed timestamp in the first 4 bytes
        var timestampBytes = BitConverter.GetBytes((uint)timestamp);
        Array.Copy(timestampBytes, 0, guidBytes, 0, 4);

        return new Guid(guidBytes);
    }

    /// <summary>
    /// Extracts timestamp from a session ID created with GenerateSessionId
    /// </summary>
    public static DateTime? ExtractTimestampFromSessionId(Guid sessionId)
    {
        try
        {
            var guidBytes = sessionId.ToByteArray();
            var timestampBytes = new byte[4];
            Array.Copy(guidBytes, 0, timestampBytes, 0, 4);

            var timestamp = BitConverter.ToUInt32(timestampBytes, 0);
            return DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if a user is likely a bot based on common patterns
    /// </summary>
    public static bool IsLikelyBot(User user)
    {
        if (user.IsBot) return true;

        var username = user.Username?.ToLower() ?? "";
        var firstName = user.FirstName?.ToLower() ?? "";

        var botIndicators = new[] { "bot", "admin", "channel", "group", "service", "automated" };

        return botIndicators.Any(indicator =>
            username.Contains(indicator) || firstName.Contains(indicator));
    }

    /// <summary>
    /// Creates a comprehensive error report for logging
    /// </summary>
    public static string CreateErrorReport(Exception ex, string context, Dictionary<string, object>? additionalData = null)
    {
        var report = new StringBuilder();
        report.AppendLine($"Error Context: {context}");
        report.AppendLine($"Exception Type: {ex.GetType().Name}");
        report.AppendLine($"Message: {ex.Message}");
        report.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

        if (additionalData?.Any() == true)
        {
            report.AppendLine("Additional Data:");
            foreach (var kvp in additionalData)
            {
                report.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
        }

        if (ex.InnerException != null)
        {
            report.AppendLine($"Inner Exception: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
        }

        report.AppendLine($"Stack Trace:\n{ex.StackTrace}");

        return report.ToString();
    }

    /// <summary>
    /// Rate limiting helper to prevent spam
    /// </summary>
    public static class RateLimiter
    {
        private static readonly Dictionary<long, DateTime> _lastCommandTime = new();
        private static readonly object _lock = new();

        public static bool IsRateLimited(long userId, TimeSpan cooldown)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;

                if (_lastCommandTime.TryGetValue(userId, out var lastTime))
                {
                    if (now - lastTime < cooldown)
                    {
                        return true;
                    }
                }

                _lastCommandTime[userId] = now;

                // Clean up old entries (older than 1 hour)
                var cutoff = now.AddHours(-1);
                var keysToRemove = _lastCommandTime
                    .Where(kvp => kvp.Value < cutoff)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _lastCommandTime.Remove(key);
                }

                return false;
            }
        }
    }

    /// <summary>
    /// Text formatting utilities
    /// </summary>
    public static class TextFormatter
    {
        public static string Bold(string text) => $"**{text}**";
        public static string Italic(string text) => $"_{text}_";
        public static string Code(string text) => $"`{text}`";
        public static string CodeBlock(string text, string language = "") => $"```{language}\n{text}\n```";
        public static string Link(string text, string url) => $"[{text}]({url})";
        public static string Strikethrough(string text) => $"~{text}~";

        public static string CreateTable(List<List<string>> rows, bool hasHeader = true)
        {
            if (!rows.Any()) return string.Empty;

            var result = new StringBuilder();

            // Header
            if (hasHeader && rows.Count > 0)
            {
                result.AppendLine("| " + string.Join(" | ", rows[0]) + " |");
                result.AppendLine("|" + string.Join("|", rows[0].Select(_ => "---")) + "|");
                rows = rows.Skip(1).ToList();
            }

            // Data rows
            foreach (var row in rows)
            {
                result.AppendLine("| " + string.Join(" | ", row) + " |");
            }

            return result.ToString();
        }
    }
}