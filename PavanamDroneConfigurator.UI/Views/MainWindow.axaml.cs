using Avalonia.Controls;
using Avalonia.Interactivity;
using PavanamDroneConfigurator.UI.ViewModels;

namespace PavanamDroneConfigurator.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void NavButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.CommandParameter is ViewModelBase page)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.CurrentPage = page;
            }
        }
    }
}
