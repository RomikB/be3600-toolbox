using Avalonia.Controls;
using Avalonia.Interactivity;

namespace BE3600Toolbox.Desktop.Views;

public partial class PasswordDialogWindow : Window
{
    public PasswordDialogWindow() : this("Enter router admin password:")
    {
    }

    public PasswordDialogWindow(string prompt)
    {
        InitializeComponent();
        PromptTextBlock.Text = prompt;
        PasswordBox.Focus();
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Close(PasswordBox.Text ?? string.Empty);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnShowPasswordClick(object? sender, RoutedEventArgs e)
    {
        PasswordBox.PasswordChar = ShowPasswordCheckBox.IsChecked == true ? '\0' : '*';
    }
}

