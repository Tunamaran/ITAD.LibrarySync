using System.Collections.Specialized;
using System.Windows;
using ITAD.LibrarySync.App.ViewModels;

namespace ITAD.LibrarySync.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        if (SyncInlineLogList.Items is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged += (_, _) =>
            {
                if (SyncInlineLogList.Items.Count > 0)
                {
                    SyncInlineLogList.ScrollIntoView(SyncInlineLogList.Items[^1]);
                }
            };
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
