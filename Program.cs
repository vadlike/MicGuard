using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        ApplicationConfiguration.Initialize();

        var configPath = Path.Combine(AppContext.BaseDirectory, "micguard.json");
        var config = GuardConfig.LoadOrCreate(configPath);

        Application.Run(new TrayApplicationContext(config, configPath));
    }
}
