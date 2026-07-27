using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
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

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
