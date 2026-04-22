using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DjiActionDbMergeTool.Core;

namespace DjiActionDbMergeTool;

public partial class MainWindow : Window
{
    private CancellationTokenSource? _cts;

    public MainWindow()
    {
        InitializeComponent();
        Title = $"DJI Action DB Merge Tool v{Program.AppVersion}";
    }

    private async void BrowseSource_Click(object? sender, RoutedEventArgs e)
    {
        var path = await PickFileAsync(save: false);
        if (path != null)
            SourcePathBox.Text = path;
    }

    private async void BrowseTarget_Click(object? sender, RoutedEventArgs e)
    {
        var path = await PickFileAsync(save: true);
        if (path != null)
            TargetPathBox.Text = path;
    }

    private async Task<string?> PickFileAsync(bool save)
    {
        var topLevel = TopLevel.GetTopLevel(this)!;
        var dbFileType = new FilePickerFileType("SQLite Database") { Patterns = ["*.db"] };
        var allFileType = new FilePickerFileType("All Files") { Patterns = ["*"] };

        if (save)
        {
            var result = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Select Target Database",
                SuggestedFileName = "AC001.db",
                FileTypeChoices = [dbFileType, allFileType],
                DefaultExtension = "db"
            });
            return result?.TryGetLocalPath();
        }
        else
        {
            var results = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Source Database",
                AllowMultiple = false,
                FileTypeFilter = [dbFileType, allFileType]
            });
            return results.Count > 0 ? results[0].TryGetLocalPath() : null;
        }
    }

    private async void Merge_Click(object? sender, RoutedEventArgs e)
    {
        var sourcePath = SourcePathBox.Text?.Trim() ?? string.Empty;
        var targetPath = TargetPathBox.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(targetPath))
        {
            StatusLabel.Text = "Please specify both source and target database paths.";
            return;
        }

        MergeButton.IsEnabled = false;
        ProgressBar.Value = 0;
        LogBox.Text = string.Empty;
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
        var line = $"[{System.DateTime.Now:HH:mm:ss}] {message}{System.Environment.NewLine}";
        LogBox.Text = (LogBox.Text ?? string.Empty) + line;
        // Scroll to end
        Dispatcher.UIThread.Post(() => LogBox.CaretIndex = LogBox.Text?.Length ?? 0);
    }
}
