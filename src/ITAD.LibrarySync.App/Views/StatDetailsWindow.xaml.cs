using System.Windows;
using ITAD.LibrarySync.App.ViewModels;

namespace ITAD.LibrarySync.App.Views;

public partial class StatDetailsWindow : Window
{
    public StatDetailsWindow(StatDetailsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
