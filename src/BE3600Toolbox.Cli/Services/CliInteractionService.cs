using BE3600Toolbox.Core.Services;

namespace BE3600Toolbox.Cli.Services;

public class CliInteractionService : IInteractionService
{
    public Task<bool> ConfirmAsync(string title, string message, bool defaultAnswer = false, CancellationToken ct = default)
    {
        Console.WriteLine($"\n[?] {title}");
        Console.Write($"{message} ({(defaultAnswer ? "Y/n" : "y/N")}): ");
        var input = Console.ReadLine()?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(input))
        {
            return Task.FromResult(defaultAnswer);
        }

        return Task.FromResult(input == "y" || input == "yes");
    }

    public Task<string> PromptPasswordAsync(string message, CancellationToken ct = default)
    {
        Console.Write($"{message}: ");
        var pass = string.Empty;
        ConsoleKey key;
        do
        {
            var keyInfo = Console.ReadKey(intercept: true);
            key = keyInfo.Key;

            if (key == ConsoleKey.Backspace && pass.Length > 0)
            {
                pass = pass[0..^1];
                Console.Write("\b \b");
            }
            else if (!char.IsControl(keyInfo.KeyChar))
            {
                pass += keyInfo.KeyChar;
                Console.Write("*");
            }
        } while (key != ConsoleKey.Enter);

        Console.WriteLine();
        return Task.FromResult(pass);
    }

    public Task<string> PromptInputAsync(string message, string defaultValue = "", CancellationToken ct = default)
    {
        var prompt = string.IsNullOrEmpty(defaultValue) ? $"{message}: " : $"{message} [{defaultValue}]: ";
        Console.Write(prompt);
        var input = Console.ReadLine()?.Trim();
        return Task.FromResult(string.IsNullOrEmpty(input) ? defaultValue : input);
    }

    public Task<T> SelectOptionAsync<T>(string title, IReadOnlyList<T> options, CancellationToken ct = default) where T : notnull
    {
        Console.WriteLine($"\n--- {title} ---");
        for (var i = 0; i < options.Count; i++)
        {
            Console.WriteLine($"[{i + 1}] {options[i]}");
        }

        while (true)
        {
            Console.Write($"Select option (1-{options.Count}): ");
            var input = Console.ReadLine();
            if (int.TryParse(input, out var choice) && choice >= 1 && choice <= options.Count)
            {
                return Task.FromResult(options[choice - 1]);
            }
            Console.WriteLine("Invalid selection. Try again.");
        }
    }
}

