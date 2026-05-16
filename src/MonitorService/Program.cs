using Microsoft.Extensions.Options;
using MonitorService;
using MonitorService.Configuration;
using MonitorService.Detection;
using MonitorService.Diagnostics;
using MonitorService.Monitors;
using MonitorService.Notifications;
using MonitorService.Persistence;

// Console under Windows defaults to the OEM code page (CP-850/CP-1252), so
// accented chars in log lines and titles render as mojibake. Force UTF-8.
Console.OutputEncoding = System.Text.Encoding.UTF8;

// Configs (appsettings.json, sources.json, monitor.db by default) live next to the
// REAL .exe — where the user edits them — not next to the bundled assemblies. When
// publishing single-file with IncludeAllContentForSelfExtract=true, the runtime
// extracts the bundle to a temp folder and AppContext.BaseDirectory points there.
// Environment.ProcessPath always returns the path of the actual launched .exe.
var exeFile = Environment.ProcessPath
              ?? throw new InvalidOperationException("Could not determine Environment.ProcessPath");
var exeDir = Path.GetDirectoryName(exeFile)!;
Directory.SetCurrentDirectory(exeDir);

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .SetBasePath(exeDir)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Resolve DataDirectory (where SQLite + Playwright cache live) once, before DI runs.
var dataDir = PathResolver.ResolveDataDirectory(builder.Configuration["DataDirectory"], exeDir);
Directory.CreateDirectory(dataDir);

// Redirect Playwright browser cache into DataDirectory (default would be %USERPROFILE%\.cache\ms-playwright).
// Must be set BEFORE Playwright is loaded so 'install chromium' lands in our folder.
Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", Path.Combine(dataDir, "playwright-browsers"));

builder.Services.Configure<MonitorOptions>(builder.Configuration);
builder.Services.PostConfigure<MonitorOptions>(opts =>
{
    opts.DataDirectory = dataDir;
    // sources.json sigue siendo configuración del usuario: vive junto al .exe salvo
    // que se especifique ruta absoluta. NO se mete dentro de DataDirectory a propósito.
    opts.SourcesFile = PathResolver.ResolveUnder(exeDir, opts.SourcesFile);
    // monitor.db sí va dentro de DataDirectory por defecto.
    opts.Database.Path = PathResolver.ResolveUnder(dataDir, opts.Database.Path);
});

builder.Services.AddHttpClient("scraper", c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
    c.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/130.0 Safari/537.36");
    c.DefaultRequestHeaders.AcceptLanguage.ParseAdd("es-ES,es;q=0.9,en;q=0.8");
});

builder.Services.AddHttpClient("discord", c => c.Timeout = TimeSpan.FromSeconds(15));

builder.Services.AddSingleton<SourcesLoader>();
builder.Services.AddSingleton<SourceDumper>();
builder.Services.AddSingleton<ISnapshotRepository, SqliteSnapshotRepository>();
builder.Services.AddSingleton<ChangeDetector>();

builder.Services.AddSingleton<IMonitor, RssMonitor>();
builder.Services.AddSingleton<IMonitor, HtmlScraperMonitor>();
builder.Services.AddSingleton<IMonitor, PlaywrightScraperMonitor>();
builder.Services.AddSingleton<PlaywrightBrowserAccessor>();

builder.Services.AddSingleton<ConsoleNotifier>();
builder.Services.AddSingleton<TelegramNotifier>();
builder.Services.AddSingleton<DiscordWebhookNotifier>();
builder.Services.AddSingleton<INotifier>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<MonitorOptions>>().Value;
    var list = new List<INotifier>();
    if (opts.UseConsoleNotifier) list.Add(sp.GetRequiredService<ConsoleNotifier>());
    if (opts.Telegram.Enabled)    list.Add(sp.GetRequiredService<TelegramNotifier>());
    if (opts.Discord.Enabled)     list.Add(sp.GetRequiredService<DiscordWebhookNotifier>());
    return new CompositeNotifier(list);
});

// CLI sub-commands
if (args.Contains("--help") || args.Contains("-h"))
{
    PrintHelp();
    return;
}

if (args.Contains("--paths"))
{
    var opts = new MonitorOptions();
    builder.Configuration.Bind(opts);
    Console.WriteLine($"Executable     : {exeDir}");
    Console.WriteLine($"DataDirectory  : {dataDir}");
    Console.WriteLine($"  monitor.db   : {PathResolver.ResolveUnder(dataDir, opts.Database.Path)}");
    Console.WriteLine($"  playwright   : {Path.Combine(dataDir, "playwright-browsers")}");
    Console.WriteLine($"sources.json   : {PathResolver.ResolveUnder(exeDir, opts.SourcesFile)}");
    return;
}

if (args.Contains("--discover-chat"))
{
    var opts = new MonitorOptions();
    builder.Configuration.Bind(opts);
    Environment.ExitCode = await ChatDiscovery.RunAsync(opts, TimeSpan.FromSeconds(30));
    return;
}

var dumpIdx = Array.IndexOf(args, "--dump-html");
if (dumpIdx >= 0)
{
    if (dumpIdx + 1 >= args.Length)
    {
        Console.Error.WriteLine("Usage: --dump-html \"<source-name>\"");
        Environment.ExitCode = 2;
        return;
    }
    var targetName = args[dumpIdx + 1];

    using var dumpHost = builder.Build();
    var loader = dumpHost.Services.GetRequiredService<SourcesLoader>();
    var dumper = dumpHost.Services.GetRequiredService<SourceDumper>();
    var optsSnapshot = dumpHost.Services.GetRequiredService<IOptions<MonitorOptions>>().Value;

    var file = loader.Load(optsSnapshot.SourcesFile);
    var source = file.Sources.FirstOrDefault(s =>
        string.Equals(s.Name, targetName, StringComparison.OrdinalIgnoreCase));

    if (source is null)
    {
        Console.Error.WriteLine($"Source '{targetName}' not found in {optsSnapshot.SourcesFile}.");
        Console.Error.WriteLine("Available sources:");
        foreach (var s in file.Sources)
            Console.Error.WriteLine($"  - {s.Name} [{s.Type}]");
        Environment.ExitCode = 1;
        return;
    }

    var dumpsDir = Path.Combine(optsSnapshot.DataDirectory, "dumps");
    Console.WriteLine($"Fetching '{source.Name}' [{source.Type}]...");
    try
    {
        var outPath = await dumper.DumpAsync(source, dumpsDir, CancellationToken.None);
        var bytes = new FileInfo(outPath).Length;
        Console.WriteLine($"Wrote {bytes:N0} bytes to {outPath}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Dump failed: {ex.Message}");
        Environment.ExitCode = 1;
    }
    return;
}

builder.Services.AddHostedService<Worker>();
var host = builder.Build();
await host.RunAsync();

static void PrintHelp()
{
    Console.WriteLine("""
        MonitorService - hybrid RSS + scraping monitor

        Usage:
          MonitorService                       Run the monitor.
          MonitorService --discover-chat       Print Telegram chat ids the bot can see.
          MonitorService --dump-html "<name>"  Fetch one source and save raw HTML/XML to
                                               <DataDirectory>\dumps for selector tuning.
          MonitorService --paths               Show resolved file paths and exit.
          MonitorService --help                Show this help.

        Configuration files (next to the .exe):
          appsettings.json   Notifier credentials and DataDirectory location.
          sources.json       RSS feeds and web pages to monitor.

        Generated files (under DataDirectory; defaults to the .exe folder):
          monitor.db                SQLite store of snapshots.
          monitor.db-wal/-shm       WAL sidecars; safe to ignore.
          playwright-browsers/      Chromium download cache (~150 MB).
        """);
}
