using System.Windows;
using ITAD.LibrarySync.App.ViewModels;

namespace ITAD.LibrarySync.App.Views;

public partial class FixMatchWindow : Window
{
    public FixMatchWindow(FixMatchViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) => DialogResult = true;
    }
}
