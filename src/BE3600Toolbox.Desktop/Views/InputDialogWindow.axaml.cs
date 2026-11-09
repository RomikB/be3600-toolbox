using Avalonia.Controls;
using Avalonia.Interactivity;

namespace BE3600Toolbox.Desktop.Views;

public partial class InputDialogWindow : Window
{
    public InputDialogWindow()
    {
        InitializeComponent();
    }

    public InputDialogWindow(string message, string defaultValue = "") : this()
    {
        PromptTextBlock.Text = message;
        InputTextBox.Text = defaultValue;
        InputTextBox.CaretIndex = defaultValue.Length;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Close(InputTextBox.Text ?? string.Empty);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}

