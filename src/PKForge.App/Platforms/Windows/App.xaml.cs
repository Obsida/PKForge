namespace PKForge.App.WinUI;

public partial class App : Microsoft.Maui.MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, args) => global::PKForge.App.Services.AppLog.Error("winui", "Unhandled Windows UI exception", args.Exception);
    }
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
