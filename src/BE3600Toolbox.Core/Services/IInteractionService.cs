namespace BE3600Toolbox.Core.Services;

public interface IInteractionService
{
    Task<bool> ConfirmAsync(string title, string message, bool defaultAnswer = false, CancellationToken ct = default);
    Task<string> PromptPasswordAsync(string message, CancellationToken ct = default);
    Task<string> PromptInputAsync(string message, string defaultValue = "", CancellationToken ct = default);
    Task<T> SelectOptionAsync<T>(string title, IReadOnlyList<T> options, CancellationToken ct = default) where T : notnull;
}

