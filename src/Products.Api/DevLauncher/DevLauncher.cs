using System.Diagnostics;

namespace Products.Api.DevLauncher;

// Profil "Launcher" (argument --launcher): wybór wariantu przed startem API - tylko deweloperskie.
// Osobny proces, więc debugger IDE zostaje podpięty do API:
// - Windows: okno launcher.ps1 (WinForms); po starcie zostaje otwarte jako panel statusu kontenerów i zamyka się
//   razem z API (dostaje jego PID);
// - Linux: menu launcher.sh w terminalu (whiptail), kończy pracę po starcie.
internal static class DevLauncher
{
    private const string Flag = "--launcher";

    public static async Task<string[]> ApplyAsync(string[] args)
    {
        if (!args.Contains(Flag))
            return args;

        var directory = Path.Combine(AppContext.BaseDirectory, "DevLauncher");
        var start = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -STA -File \"{Path.Combine(directory, "launcher.ps1")}\" -ParentProcessId {Environment.ProcessId}")
            : OperatingSystem.IsLinux()
                ? new ProcessStartInfo("bash", [Path.Combine(directory, "launcher.sh")])
                : throw new PlatformNotSupportedException("Wybór wariantu (--launcher) działa na Windows i Linuksie.");
        start.RedirectStandardOutput = true; // tylko wynik; menu na Linuksie korzysta z terminala (wejście i /dev/tty)
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        using var dialog = Process.Start(start)!;

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
