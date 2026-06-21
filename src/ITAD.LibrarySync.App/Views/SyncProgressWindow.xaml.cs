using System.Windows;
using System.Windows.Controls;
using ITAD.LibrarySync.App.ViewModels;

namespace ITAD.LibrarySync.App.Views;

public partial class SyncProgressWindow : Window
{
    public SyncProgressWindow(SyncProgressViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public void ScrollToLatest()
    {
        if (LogList.Items.Count == 0)
            return;

        LogList.ScrollIntoView(LogList.Items[^1]);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
