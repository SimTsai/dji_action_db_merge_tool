using System.Reflection;
using Avalonia;
using DjiActionDbMergeTool.Core;

namespace DjiActionDbMergeTool;

class Program
{
    internal static readonly string AppVersion =
        typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        // --version flag
        if (args.Contains("--version"))
        {
            Console.WriteLine($"DjiActionDbMergeTool v{AppVersion}");
            return 0;
        }

        // CLI mode: detect --source / --target before starting Avalonia
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
            return RunCli(source, target);
        }

        // GUI mode
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static int RunCli(string source, string target)
    {
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
            service.MergeAsync(source, target, progress).GetAwaiter().GetResult();
            Console.WriteLine("Done.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Merge failed: {ex.Message}");
            return 1;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
