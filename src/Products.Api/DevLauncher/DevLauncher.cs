using System.Diagnostics;

namespace Products.Api.DevLauncher;

// Profil "Launcher" (argument --launcher): okno wyboru wariantu przed startem API - tylko deweloperskie, tylko Windows.
// Okno to osobny proces (launcher.ps1), więc debugger Visual Studio zostaje podpięty do API. Po starcie zostaje
// otwarte jako panel statusu kontenerów i zamyka się razem z API (dostaje jego PID).
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
        using var dialog = Process.Start(new ProcessStartInfo(
            "powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -STA -File \"{script}\" -ParentProcessId {Environment.ProcessId}")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;

        // wynik to linie do pustej linii - okno działa dalej; koniec wyjścia bez wyniku: okno zamknięte bez uruchomienia
        List<string> chosen = [];
        while (await dialog.StandardOutput.ReadLineAsync() is { Length: > 0 } line)
            chosen.Add(line.Trim());

        if (chosen.Count == 0)
        {
            Console.WriteLine("Uruchomienie anulowane w oknie wyboru wariantu.");
            Environment.Exit(0);
        }
        // API uruchomione w kontenerze (adres zamiast argumentów, grid otwarty w przeglądarce): ten proces nie jest potrzebny
        if (chosen is [var url] && url.StartsWith("http://", StringComparison.Ordinal))
        {
            Console.WriteLine($"API działa w kontenerze: {url} - proces uruchomiony z Visual Studio kończy pracę.");
            Environment.Exit(0);
        }

        // wybór z okna jako argumenty konfiguracji - mają pierwszeństwo przed appsettings i zmiennymi środowiskowymi
        return [.. args.Where(a => a != Flag), .. chosen];
    }
}
