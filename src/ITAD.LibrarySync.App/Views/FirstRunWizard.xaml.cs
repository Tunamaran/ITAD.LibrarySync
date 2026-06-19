using System.ComponentModel;
using System.Windows;
using ITAD.LibrarySync.App.ViewModels;

namespace ITAD.LibrarySync.App.Views;

public partial class FirstRunWizard : Window
{
    private readonly FirstRunWizardViewModel _viewModel;

    public FirstRunWizard(FirstRunWizardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.WizardCompleted += (_, _) => Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_viewModel.IsCompleted)
        {
            var result = MessageBox.Show(
                "Setup is not complete. Exit ITAD Library Sync?",
                "Cancel Setup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            Application.Current.Shutdown();
        }

        base.OnClosing(e);
    }
}
