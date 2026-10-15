using System.Diagnostics;

namespace Products.Api.DevLauncher;

// Profil "Launcher" (argument --launcher): okno wyboru wariantu przed startem API - tylko deweloperskie, tylko Windows.
// Okno to osobny proces (launcher.ps1), więc debugger Visual Studio zostaje podpięty do API.
internal static class DevLauncher
{
    private const string Flag = "--launcher";

    public static async Task<string[]> ApplyAsync(string[] args)
    {
        if (!args.Contains(Flag))
            return args;
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Okno wyboru wariantu (--launcher) działa tylko na Windows.");

        var script = Path.Combine(AppContext.BaseDirectory, "DevLauncher", "launcher.ps1");
        using var dialog = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -STA -File \"{script}\"")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        var output = await dialog.StandardOutput.ReadToEndAsync();
        await dialog.WaitForExitAsync();

        if (dialog.ExitCode != 0)
        {
            Console.WriteLine("Uruchomienie anulowane w oknie wyboru wariantu.");
            Environment.Exit(0);
        }

        // wybór z okna jako argumenty konfiguracji - mają pierwszeństwo przed appsettings i zmiennymi środowiskowymi
        var chosen = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return [.. args.Where(a => a != Flag), .. chosen];
    }
}
