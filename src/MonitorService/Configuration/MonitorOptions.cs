namespace MonitorService.Configuration;

public sealed class MonitorOptions
{
    /// <summary>
    /// Carpeta donde se crean los ficheros generados (SQLite, cache de Playwright, logs).
    /// Vacío => junto al ejecutable. Si es relativa, se resuelve respecto al ejecutable.
    /// Rellenada en Program.cs con la ruta absoluta efectiva.
    /// </summary>
    public string DataDirectory { get; set; } = "";

    public TelegramOptions Telegram { get; set; } = new();
    public DiscordOptions Discord { get; set; } = new();
    public DatabaseOptions Database { get; set; } = new();
    public string SourcesFile { get; set; } = "sources.json";
    public bool UseConsoleNotifier { get; set; } = true;
}

public sealed class TelegramOptions
{
    public string BotToken { get; set; } = "";
    public string ChatId { get; set; } = "";
    public bool Enabled => !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(ChatId);
}

public sealed class DiscordOptions
{
    public string WebhookUrl { get; set; } = "";
    public string? Username { get; set; }
    public bool Enabled => !string.IsNullOrWhiteSpace(WebhookUrl);
}

public sealed class DatabaseOptions
{
    public string Path { get; set; } = "monitor.db";
}
