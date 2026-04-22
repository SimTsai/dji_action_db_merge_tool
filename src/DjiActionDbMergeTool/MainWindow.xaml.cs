using System.Windows;
using Microsoft.Win32;
using DjiActionDbMergeTool.Core;

namespace DjiActionDbMergeTool;

public partial class MainWindow : Window
{
    private CancellationTokenSource? _cts;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void BrowseSource_Click(object sender, RoutedEventArgs e)
    {
        if (BrowseFile() is { } path)
            SourcePathBox.Text = path;
    }

    private void BrowseTarget_Click(object sender, RoutedEventArgs e)
    {
        if (BrowseFile(save: true) is { } path)
            TargetPathBox.Text = path;
    }

    private static string? BrowseFile(bool save = false)
    {
        if (save)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Select Target Database",
                Filter = "SQLite Database (*.db)|*.db|All Files (*.*)|*.*",
                DefaultExt = ".db"
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }
        else
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Source Database",
                Filter = "SQLite Database (*.db)|*.db|All Files (*.*)|*.*"
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }
    }

    private async void Merge_Click(object sender, RoutedEventArgs e)
    {
        var sourcePath = SourcePathBox.Text.Trim();
        var targetPath = TargetPathBox.Text.Trim();

        if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(targetPath))
        {
            MessageBox.Show("Please specify both source and target database paths.",
                "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MergeButton.IsEnabled = false;
        ProgressBar.Value = 0;
        LogBox.Clear();
        StatusLabel.Text = "Merging...";

        _cts = new CancellationTokenSource();
        var service = new MergeService();

        var progress = new Progress<MergeProgress>(p =>
        {
            ProgressBar.Value = p.Percentage;
            StatusLabel.Text = p.Message;
            AppendLog(p.Message);

            if (p.HasError)
                AppendLog($"ERROR: {p.ErrorMessage}");
        });

        try
        {
            await service.MergeAsync(sourcePath, targetPath, progress, _cts.Token);
            StatusLabel.Text = "Merge completed successfully.";
            AppendLog("✔ Done.");
        }
        catch (OperationCanceledException)
        {
            StatusLabel.Text = "Merge cancelled.";
            AppendLog("Cancelled.");
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "Merge failed.";
            AppendLog($"ERROR: {ex.Message}");
            MessageBox.Show(ex.Message, "Merge Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            MergeButton.IsEnabled = true;
            _cts.Dispose();
            _cts = null;
        }
    }

    private void AppendLog(string message)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }
}