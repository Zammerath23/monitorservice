using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MonitorService.Configuration;
using MonitorService.Detection;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MonitorService.Notifications;

public sealed class TelegramNotifier : INotifier
{
    private readonly ITelegramBotClient _bot;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramNotifier> _log;

    public TelegramNotifier(IOptions<MonitorOptions> options, ILogger<TelegramNotifier> log)
    {
        _options = options.Value.Telegram;
        _log = log;
        _bot = new TelegramBotClient(_options.BotToken);
    }

    public async Task NotifyAsync(DetectedChange change, CancellationToken ct)
    {
        if (!_options.Enabled) return;

        var text = BuildMarkdown(change);
        var chatId = ParseChatId(_options.ChatId);

        var attempt = 0;
        while (true)
        {
            try
            {
                await _bot.SendMessage(
                    chatId: chatId,
                    text: text,
                    parseMode: ParseMode.MarkdownV2,
                    linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                    cancellationToken: ct);
                return;
            }
            catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 429 && attempt < 3)
            {
                var wait = ex.Parameters?.RetryAfter ?? 5;
                _log.LogWarning("Telegram rate limit; retrying in {Seconds}s", wait);
                await Task.Delay(TimeSpan.FromSeconds(wait), ct);
                attempt++;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to send Telegram notification");
                return;
            }
        }
    }

    private static ChatId ParseChatId(string raw) =>
        long.TryParse(raw, out var id) ? new ChatId(id) : new ChatId(raw);

    private static string BuildMarkdown(DetectedChange c)
    {
        var sb = new StringBuilder();
        var kind = c.Kind == ChangeKind.NewItem ? "🆕 *Nuevo*" : "📈 *Cambio detectado*";
        sb.Append(kind).Append(" en _").Append(Escape(c.SourceName)).Append('_').Append('\n');
        if (!string.IsNullOrWhiteSpace(c.Title))
            sb.Append('*').Append(Escape(c.Title!)).Append('*').Append('\n');

        foreach (var d in c.Deltas)
        {
            sb.Append("\n• `").Append(Escape(d.Field)).Append("`: ");
            if (c.Kind == ChangeKind.NewItem)
                sb.Append(Escape(d.Current ?? "—"));
            else
                sb.Append("~").Append(Escape(d.Previous ?? "—")).Append("~ → *").Append(Escape(d.Current ?? "—")).Append('*');
        }

        if (!string.IsNullOrWhiteSpace(c.Url))
            sb.Append("\n\n[Abrir](").Append(EscapeUrl(c.Url!)).Append(')');

        return sb.ToString();
    }

    // https://core.telegram.org/bots/api#markdownv2-style
    private static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if ("_*[]()~`>#+-=|{}.!\\".IndexOf(c) >= 0) sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static string EscapeUrl(string url)
    {
        var sb = new StringBuilder(url.Length);
        foreach (var c in url)
        {
            if (c == ')' || c == '\\') sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }
}
