using BE3600Toolbox.Cli.Logging;
using BE3600Toolbox.Cli.Services;
using BE3600Toolbox.Core.Models;
using BE3600Toolbox.Core.Services;
using BE3600Toolbox.Core.Utilities;
using BE3600Toolbox.Engine.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

if (args.Any(a => a.Equals("--test-lmo", StringComparison.OrdinalIgnoreCase)))
{
    foreach (var lang in new[] { "en", "ru" })
    {
        Console.WriteLine($"--- Testing base.{lang}.po -> base.{lang}.lmo ---");
        var converted = BE3600Toolbox.Engine.Localization.LmoConverter.ConvertPoResourceToLmo($"BE3600Toolbox.Engine.Assets.base.{lang}.po");
        var lmoPath = $"base.{lang}.lmo";
        if (File.Exists(lmoPath))
        {
            var refBytes = File.ReadAllBytes(lmoPath);
            Console.WriteLine($"Converted Length: {converted.Length}, Ref Length: {refBytes.Length}");
            using var sha = System.Security.Cryptography.SHA256.Create();
            var cHash = Convert.ToHexString(sha.ComputeHash(converted));
            var rHash = Convert.ToHexString(sha.ComputeHash(refBytes));
            Console.WriteLine($"Converted SHA256: {cHash}");
            Console.WriteLine($"Ref SHA256:       {rHash}");
            Console.WriteLine(cHash == rHash ? "SUCCESS: 100% BIT-FOR-BIT IDENTICAL!" : "FAILURE: MISMATCH");
        }
        else
        {
            Console.WriteLine($"Converted Length: {converted.Length}");
            using var sha = System.Security.Cryptography.SHA256.Create();
            Console.WriteLine($"Converted SHA256: {Convert.ToHexString(sha.ComputeHash(converted))}");
        }
    }
    return;
}

if (args.Any(a => a.Equals("--help", StringComparison.OrdinalIgnoreCase) || a.Equals("-h", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine("=======================================");
    Console.WriteLine("  Xiaomi BE3600 Toolbox (CLI Mode)");
    Console.WriteLine("=======================================");
    Console.WriteLine();
    Console.WriteLine("Usage: BE3600Toolbox.Cli [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  -v, --verbose [level]    Enable verbose console logging (default: Information)");
    Console.WriteLine("  -h, --help               Show help information");
    return;
}

var minLogLevel = LogLevel.Warning;

for (var i = 0; i < args.Length; i++)
{
    var arg = args[i];
    if (arg.Equals("--verbose", StringComparison.OrdinalIgnoreCase) ||
        arg.Equals("-v", StringComparison.OrdinalIgnoreCase))
    {
        if (i + 1 < args.Length && Enum.TryParse<LogLevel>(args[i + 1], ignoreCase: true, out var parsedLevel))
        {
            minLogLevel = parsedLevel;
            i++;
        }
        else
        {
            minLogLevel = LogLevel.Information;
        }
    }
    else if (arg.StartsWith("--verbose=", StringComparison.OrdinalIgnoreCase))
    {
        var val = arg["--verbose=".Length..];
        if (Enum.TryParse<LogLevel>(val, ignoreCase: true, out var parsedLevel))
        {
            minLogLevel = parsedLevel;
        }
        else
        {
            minLogLevel = LogLevel.Information;
        }
    }
    else if (arg.Equals("--debug", StringComparison.OrdinalIgnoreCase) ||
             arg.Equals("-vv", StringComparison.OrdinalIgnoreCase))
    {
        minLogLevel = LogLevel.Debug;
    }
}

Console.WriteLine("=======================================");
Console.WriteLine("  Xiaomi BE3600 Toolbox (CLI Mode)");
Console.WriteLine("=======================================");

if (minLogLevel < LogLevel.Warning)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"[*] Verbose logging enabled (level: {minLogLevel})");
    Console.ResetColor();
}

var services = new ServiceCollection();
services.AddLogging(b =>
{
    b.ClearProviders();
    b.AddConsole(opts => opts.FormatterName = CleanConsoleFormatter.FormatterName)
     .AddConsoleFormatter<CleanConsoleFormatter, ConsoleFormatterOptions>();
    b.SetMinimumLevel(minLogLevel);
    b.AddFilter("System.Net.Http", LogLevel.Warning);
});
services.AddSingleton<IInteractionService, CliInteractionService>();
services.AddBe3600Engine();

var provider = services.BuildServiceProvider();
var settingsService = provider.GetRequiredService<ISettingsService>();
var router = provider.GetRequiredService<IRouterService>();
var sshManager = provider.GetRequiredService<ISshManager>();
var sshConn = provider.GetRequiredService<ISshConnectionService>();
var interaction = provider.GetRequiredService<IInteractionService>();
var logger = provider.GetRequiredService<ILogger<Program>>();

await settingsService.LoadAsync();

var ip = settingsService.Settings.RouterIp;

// 1. Auto-detect if IP is empty
if (string.IsNullOrWhiteSpace(ip))
{
    logger.LogInformation("[*] Auto-detecting router on 192.168.31.1 (stock MiWiFi)...");
    var detected = await router.DetectRouterAsync();
    if (detected != null)
    {
        logger.LogInformation($"[+] Found {detected.Description} at {detected.IpAddress}");

        ip = detected.IpAddress;
        await settingsService.UpdateAsync(s => s.RouterIp = ip);
    }
    else
    {
        logger.LogInformation("[-] No stock router responded on 192.168.31.1.");

        ip = await interaction.PromptInputAsync("Please enter router ip");
        if (!string.IsNullOrWhiteSpace(ip))
        {
            await settingsService.UpdateAsync(s => s.RouterIp = ip);
        }
    }
}
else
{
    logger.LogInformation($"[*] Using saved router IP: {ip}");
}

var progress = new Progress<StepProgress>(p =>
{
    var pct = p.Percentage.HasValue ? $" [{p.Percentage:F0}%]" : "";
    Console.WriteLine($"-> {p.StepName}{pct}");
});

// 2. Main interactive menu loop
const string OptConnectDevice = "Connect to device (unlock ssh)";
const string OptInstallLanguages = "Install EN/RU languages";
const string OptPermanentSsh = "Install permanent SSH";
const string OptFlashOpenWrt = "Flash OpenWrt (Safe Mode / Dry Run)";
const string OptUninstallLanguages = "Uninstall EN/RU languages";
const string OptExit = "Exit";

while (true)
{
    var currentIpLabel = string.IsNullOrWhiteSpace(ip) ? "not set" : ip;
    var optSetIp = $"Set IP-address (current value: {currentIpLabel})";

    var action = await interaction.SelectOptionAsync("Select Action", new[]
    {
        optSetIp,
        OptConnectDevice,
        OptInstallLanguages,
        OptPermanentSsh,
        OptFlashOpenWrt,
        OptUninstallLanguages,
        OptExit
    });

    if (action == optSetIp)
    {
        var newIp = await interaction.PromptInputAsync("Enter new Router IP", ip);
        if (!string.IsNullOrWhiteSpace(newIp))
        {
            ip = newIp;
            await settingsService.UpdateAsync(s => s.RouterIp = ip);
            Console.WriteLine($"[+] Router IP updated to: {ip}");
        }
        continue;
    }

    if (action == OptExit)
    {
        Console.WriteLine("Exiting. Goodbye!");
        break;
    }

    // Guard for operations requiring IP
    if (string.IsNullOrWhiteSpace(ip))
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("[-] Router IP is not set. Please configure it first.");
        Console.ResetColor();
        continue;
    }

    try
    {
        switch (action)
        {
            case OptConnectDevice:
                var password = await interaction.PromptInputAsync("Enter router admin password");
                if (string.IsNullOrWhiteSpace(password))
                {
                    Console.WriteLine("Authentication cancelled.");
                    continue;
                }

                Console.WriteLine("[*] Authenticating with router to acquire stok...");
                var stok = await router.AcquireStokAsync(ip, password);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[+] Authentication successful! Acquired stok={stok}");
                Console.ResetColor();

                Console.WriteLine("[*] Sending commands to unlock ssh...");
                var success = await router.UnlockSshAsync(ip, stok, progress);
                if (success)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[+] SSH unlock successful!");
                    Console.ResetColor();
                }
                break;

            case OptInstallLanguages:
                if (!await sshConn.EnsureConnectedAsync(ip, progress))
                {
                    continue;
                }

                await router.InstallLanguagesAsync(ip, progress);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[+] EN/RU languages installed successfully.");
                Console.ResetColor();
                break;

            case OptPermanentSsh:
                if (!await sshConn.EnsureConnectedAsync(ip, progress))
                {
                    continue;
                }

                await router.InstallPermanentSshAsync(ip, progress);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[+] Permanent SSH installed successfully.");
                Console.ResetColor();
                break;

            case OptFlashOpenWrt:
                var flash = provider.GetRequiredService<IFlashService>();
                await flash.FlashOpenWrtAsync("dummy_firmware.bin", dryRun: true, progress);
                break;

            case OptUninstallLanguages:
                if (!await sshConn.EnsureConnectedAsync(ip, progress))
                {
                    continue;
                }

                await router.UninstallLanguagesAsync(ip, progress);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[+] EN/RU languages uninstalled successfully.");
                Console.ResetColor();
                break;
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[-] Error: {ex.Message}");
        Console.ResetColor();
    }
}
