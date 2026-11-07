using BE3600Toolbox.Cli.Logging;
using BE3600Toolbox.Cli.Services;
using BE3600Toolbox.Core.Models;
using BE3600Toolbox.Core.Services;
using BE3600Toolbox.Engine.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

Console.WriteLine("=======================================");
Console.WriteLine("  Xiaomi BE3600 Toolbox (CLI Mode)");
Console.WriteLine("=======================================");

var services = new ServiceCollection();
services.AddLogging(b =>
{
    b.ClearProviders();
    b.AddConsole(opts => opts.FormatterName = CleanConsoleFormatter.FormatterName)
     .AddConsoleFormatter<CleanConsoleFormatter, ConsoleFormatterOptions>();
    b.SetMinimumLevel(LogLevel.Information);
    b.AddFilter("System.Net.Http", LogLevel.Warning);
});
services.AddSingleton<IInteractionService, CliInteractionService>();
services.AddBe3600Engine();

var provider = services.BuildServiceProvider();
var settingsService = provider.GetRequiredService<ISettingsService>();
var router = provider.GetRequiredService<IRouterService>();
var interaction = provider.GetRequiredService<IInteractionService>();

await settingsService.LoadAsync();

var ip = settingsService.Settings.RouterIp;

// 1. Auto-detect if IP is empty
if (string.IsNullOrWhiteSpace(ip))
{
    Console.WriteLine("[*] Auto-detecting router on 192.168.31.1 (stock MiWiFi)...");
    var detected = await router.DetectRouterAsync();
    if (detected != null)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[+] Found {detected.Description} at {detected.IpAddress}");
        Console.ResetColor();

        ip = detected.IpAddress;
        await settingsService.UpdateAsync(s => s.RouterIp = ip);
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("[-] No stock router responded on 192.168.31.1.");
        Console.ResetColor();

        ip = await interaction.PromptInputAsync("Please enter Router IP manually");
        if (!string.IsNullOrWhiteSpace(ip))
        {
            await settingsService.UpdateAsync(s => s.RouterIp = ip);
        }
    }
}
else
{
    Console.WriteLine($"[*] Using saved router IP: {ip}");
}

var progress = new Progress<StepProgress>(p =>
{
    var pct = p.Percentage.HasValue ? $" [{p.Percentage:F0}%]" : "";
    Console.WriteLine($"-> {p.StepName}{pct}");
});

// 2. Main interactive menu loop
while (true)
{
    var targetLabel = string.IsNullOrWhiteSpace(ip) ? "Not set" : ip;
    Console.WriteLine($"\n[Target Router IP: {targetLabel}]");

    var action = await interaction.SelectOptionAsync("Select Action", new[]
    {
        "1. Change Router IP (manual entry)",
        "2. Read Router Info",
        "3. Unlock SSH",
        "4. Install Russian Language",
        "5. Install English Language",
        "6. Flash OpenWrt (Safe Mode / Dry Run)",
        "7. Exit"
    });

    if (action.StartsWith("1"))
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

    if (action.StartsWith("7"))
    {
        Console.WriteLine("Exiting. Goodbye!");
        break;
    }

    // Guard for operations requiring IP
    if (string.IsNullOrWhiteSpace(ip))
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("[-] Router IP is not set. Please select option 1 first.");
        Console.ResetColor();
        continue;
    }

    try
    {
        if (action.StartsWith("2"))
        {
            var info = await router.GetDeviceInfoAsync(ip);
            Console.WriteLine($"\nModel: {info.Model}\nFirmware: {info.FirmwareVersion}\nChannel: {info.Channel ?? "N/A"}");
        }
        else if (action.StartsWith("3"))
        {
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
        }
        else if (action.StartsWith("4"))
        {
            await router.InstallLocalizationAsync(ip, "ru", progress);
            await settingsService.UpdateAsync(s => s.Language = "ru");
            Console.WriteLine("[+] Russian localization installed.");
        }
        else if (action.StartsWith("5"))
        {
            await router.InstallLocalizationAsync(ip, "en", progress);
            await settingsService.UpdateAsync(s => s.Language = "en");
            Console.WriteLine("[+] English localization installed.");
        }
        else if (action.StartsWith("6"))
        {
            var flash = provider.GetRequiredService<IFlashService>();
            await flash.FlashOpenWrtAsync("dummy_firmware.bin", dryRun: true, progress);
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[-] Error: {ex.Message}");
        Console.ResetColor();
    }
}
