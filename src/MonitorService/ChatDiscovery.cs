using Microsoft.Extensions.Options;
using MonitorService.Configuration;
using Telegram.Bot;

namespace MonitorService;

public static class ChatDiscovery
{
    public static async Task<int> RunAsync(MonitorOptions options, TimeSpan window)
    {
        if (string.IsNullOrWhiteSpace(options.Telegram.BotToken))
        {
            Console.Error.WriteLine("Telegram:BotToken is not configured (set it in appsettings.json or the TELEGRAM__BOTTOKEN env var).");
            return 2;
        }

        var bot = new TelegramBotClient(options.Telegram.BotToken);
        var me = await bot.GetMe();
        Console.WriteLine($"Bot connected: @{me.Username} ({me.Id})");
        Console.WriteLine();
        Console.WriteLine("Discovering chats. In Telegram:");
        Console.WriteLine("  1. Add this bot to your group (or open a private chat with it).");
        Console.WriteLine("  2. Send any message that mentions @" + me.Username + " (or any message in a private chat).");
        Console.WriteLine($"Listening for {window.TotalSeconds:0}s...");
        Console.WriteLine();

        var deadline = DateTime.UtcNow.Add(window);
        var seen = new Dictionary<long, string>();
        var offset = 0;

        while (DateTime.UtcNow < deadline)
        {
            var updates = await bot.GetUpdates(offset, timeout: 5);
            foreach (var u in updates)
            {
                offset = u.Id + 1;
                var chat = u.Message?.Chat ?? u.ChannelPost?.Chat ?? u.EditedMessage?.Chat;
                if (chat is null) continue;
                if (seen.ContainsKey(chat.Id)) continue;

                var label = chat.Title ?? chat.Username ?? chat.FirstName ?? "(unnamed)";
                seen[chat.Id] = $"{label} [{chat.Type}]";
                Console.WriteLine($"  chat_id = {chat.Id,-20}  {label}  ({chat.Type})");
            }
        }

        Console.WriteLine();
        if (seen.Count == 0)
        {
            Console.WriteLine("No chats found. Make sure you sent a message to the bot or in a group it belongs to.");
            return 1;
        }
        Console.WriteLine($"Copy the chat_id you want into appsettings.json -> Telegram:ChatId.");
        return 0;
    }
}
