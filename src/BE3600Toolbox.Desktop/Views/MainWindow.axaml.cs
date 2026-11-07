using Avalonia.Controls;
using BE3600Toolbox.Desktop.ViewModels;

namespace BE3600Toolbox.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Loaded += async (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                await vm.InitializeAsync();
            }
        };
    }
}
