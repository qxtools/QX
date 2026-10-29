using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Qx.Hosting;
using Qx.Interception.GEarth;

namespace RoomInspector;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        AppBuilder.Configure<InspectorApp>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
}

public sealed class InspectorApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var options = GEarthOptions.Parse(desktop.Args ?? [], new GEarthOptions
            {
                Title = "Room Inspector",
                Description = "Search, inspect and export room furniture.",
                Author = "QX",
                Version = "1.0.0",
                OnClickUsed = true
            });
            desktop.MainWindow = new InspectorWindow(new GameHost(new GameHostOptions
            {
                GEarth = options,
                ReconnectTransport = true
            }));
        }
        base.OnFrameworkInitializationCompleted();
    }
}
