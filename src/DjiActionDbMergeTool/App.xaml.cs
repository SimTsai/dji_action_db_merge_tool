using System.Windows;
using DjiActionDbMergeTool.Core;

namespace DjiActionDbMergeTool;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        var args = e.Args;

        string? source = null;
        string? target = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (i + 1 < args.Length)
            {
                if (args[i] == "--source") source = args[i + 1];
                else if (args[i] == "--target") target = args[i + 1];
            }
        }

        if (source != null && target != null)
        {
            // CLI mode - headless
            var service = new MergeService();
            var progress = new Progress<MergeProgress>(p =>
            {
                if (p.HasError)
                    Console.Error.WriteLine($"ERROR: {p.ErrorMessage}");
                else
                    Console.WriteLine($"[{p.Current}/{p.Total}] {p.Message}");
            });

            try
            {
                await service.MergeAsync(source, target, progress);
                Console.WriteLine("Done.");
                Shutdown(0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Merge failed: {ex.Message}");
                Shutdown(1);
            }

            return;
        }

        // GUI mode
        base.OnStartup(e);
    }
}

