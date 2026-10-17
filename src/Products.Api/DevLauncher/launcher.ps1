# Okno wyboru wariantu uruchomienia Products.Api (profil "Launcher" w launchSettings.json).
# Wynik na stdout (Write-Result): argumenty konfiguracji "--Klucz=wartość" albo adres API w kontenerze, po jednym
# w linii, zakończone pustą linią. Po starcie okno zostaje otwarte jako panel statusu kontenerów; zamknięte bez
# uruchomienia - brak wyniku. Nic innego nie może trafić na stdout - wyjście dockera tylko do zmiennych. Ostatni
# wybór zapamiętany w %LOCALAPPDATA%\ProductsApi\launcher.json.
param([int]$ParentProcessId) # proces API uruchomiony z VS - po jego zakończeniu panel się zamyka

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$settingsPath = Join-Path $env:LOCALAPPDATA 'ProductsApi\launcher.json'
$settings = [ordered]@{ Orm = 'EfCore'; Database = 'Postgres'; Cache = $true; Kafka = $true; Schema = 'None'; Docker = $true; ApiContainer = $false; Dashboard = $false }
if (Test-Path $settingsPath) {
    $saved = Get-Content $settingsPath -Raw | ConvertFrom-Json
    foreach ($key in @($settings.Keys)) { if ($null -ne $saved.$key) { $settings[$key] = $saved.$key } }
}

$root = $PSScriptRoot
while ($root -and -not (Test-Path (Join-Path $root 'docker-compose.yml'))) { $root = Split-Path $root }

# docker z limitem czasu: zawieszony Docker Desktop nie blokuje okna ani startu API bez końca (proces
# z potomkami jest wtedy zabijany). Zwraca stdout dockera; błąd, przekroczenie czasu i przerwanie jako wyjątek.
# Wyjście czytane na bieżąco: ReadLineAsync sprawdzane w pętli, bez wątków (zdarzeń .NET z innych wątków
# PowerShell nie obsłuży). $progress - okno postępu: każda linia trafia do podglądu, licznik sekund,
# okno reaguje na „Przerwij” ($launch.Cancelled).
function Invoke-Docker([string]$arguments, [int]$timeoutSeconds, $progress = $null) {
    $start = New-Object System.Diagnostics.ProcessStartInfo -ArgumentList 'docker', $arguments -Property @{
        RedirectStandardOutput = $true; RedirectStandardError = $true; UseShellExecute = $false; CreateNoWindow = $true
    }
    $process = [System.Diagnostics.Process]::Start($start)
    $streams = @($process.StandardOutput, $process.StandardError) | ForEach-Object {
        @{ Reader = $_; Text = New-Object System.Text.StringBuilder; Pending = $_.ReadLineAsync() }
    }
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    $drainUntil = $null # po zakończeniu procesu: chwila na resztę linii
    while ($streams | Where-Object { $_.Pending }) {
        if ($process.WaitForExit(100) -and -not $drainUntil) { $drainUntil = $timer.Elapsed.TotalSeconds + 2 }
        foreach ($stream in $streams) {
            while ($stream.Pending -and $stream.Pending.IsCompleted) {
                $line = $stream.Pending.Result
                $stream.Pending = if ($null -ne $line) { $stream.Reader.ReadLineAsync() }
                if ($null -eq $line) { break }
                [void]$stream.Text.AppendLine($line)
                if ($progress) { Add-ProgressLine $progress $line }
            }
        }
        if ($drainUntil -and $timer.Elapsed.TotalSeconds -gt $drainUntil) { break }
        if (-not $drainUntil -and ($launch.Cancelled -or $timer.Elapsed.TotalSeconds -gt $timeoutSeconds)) {
            taskkill /PID $process.Id /T /F 2>$null | Out-Null
            if ($launch.Cancelled) { throw 'Przerwano uruchamianie kontenerów.' }
            throw "Docker nie odpowiada: 'docker $arguments' trwa ponad $timeoutSeconds s.`n`nZrestartuj Docker Desktop i spróbuj ponownie."
        }
        if ($progress) {
            $progress.Label.Text = "$($progress.Step)`n$($progress.Current) · $([int]$timer.Elapsed.TotalSeconds) s"
            [System.Windows.Forms.Application]::DoEvents()
        }
    }
    $process.WaitForExit()
    $output, $errors = $streams | ForEach-Object { $_.Text.ToString() }
    if ($process.ExitCode -ne 0) { throw "docker $arguments (kod $($process.ExitCode)):`n$output$errors" }
    $output
}

# Linia docker compose do podglądu; "Container postgres  Starting" ustawia stan tego kontenera
# w podsumowaniu: "postgres: uruchamianie, redis: gotowy"
$composeStates = @{
    Creating = 'tworzenie'; Created = 'utworzony'; Recreate = 'odtwarzanie'; Recreated = 'odtworzony'
    Starting = 'uruchamianie'; Started = 'uruchomiony'; Waiting = 'czeka na gotowość (healthcheck)'
    Healthy = 'gotowy'; Running = 'już działa'; Pulling = 'pobieranie obrazu'; Pulled = 'obraz pobrany'
}
function Add-ProgressLine($progress, [string]$line) {
    $progress.Feed.AppendText($line.TrimEnd() + "`r`n")
    if ($line -match '^\s*(?:Container\s+)?(\S+)\s+(\w+)\s*$' -and $composeStates.ContainsKey($Matches[2])) {
        $progress.States[$Matches[1]] = $composeStates[$Matches[2]]
        $progress.Current = ($progress.States.Keys | ForEach-Object { "${_}: $($progress.States[$_])" }) -join ', '
    }
}

# Czeka na odpowiedź 200 z API (schemat bazy przy starcie trwa chwilę); jak Invoke-Docker - limit czasu i „Przerwij”
function Wait-Http([string]$url, [int]$timeoutSeconds, $progress) {
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    while ($true) {
        try {
            $request = [System.Net.WebRequest]::Create($url)
            $request.Timeout = 2000
            $request.GetResponse().Close() # wyjątek przy braku połączenia i przy statusie innym niż 2xx
            Add-ProgressLine $progress "GET $url -> 200"
            return
        }
        catch {
            if ($launch.Cancelled) { throw 'Przerwano uruchamianie kontenerów.' }
            if ($timer.Elapsed.TotalSeconds -gt $timeoutSeconds) { throw "API nie odpowiada pod $url po $timeoutSeconds s - sprawdź docker logs kontenera API." }
        }
        $progress.Label.Text = "$($progress.Step)`n$($progress.Current) · $([int]$timer.Elapsed.TotalSeconds) s"
        for ($i = 0; $i -lt 5; $i++) { [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 100 }
    }
}

function Invoke-Compose([string]$arguments, [int]$timeoutSeconds, $progress = $null) {
    if (-not $root) { throw 'Nie znaleziono docker-compose.yml.' }
    Invoke-Docker "compose -f `"$(Join-Path $root 'docker-compose.yml')`" $arguments" $timeoutSeconds $progress
}

$form = New-Object System.Windows.Forms.Form -Property @{
    Text = 'Products API - uruchomienie'; StartPosition = 'CenterScreen'; TopMost = $true
    FormBorderStyle = 'FixedDialog'; MaximizeBox = $false; MinimizeBox = $false
    AutoSize = $true; AutoSizeMode = 'GrowAndShrink'; Padding = New-Object System.Windows.Forms.Padding(12)
}
$layout = New-Object System.Windows.Forms.TableLayoutPanel -Property @{ ColumnCount = 2; AutoSize = $true }
$form.Controls.Add($layout)

function Add-Row($label, $control) {
    $layout.Controls.Add((New-Object System.Windows.Forms.Label -Property @{ Text = $label; AutoSize = $true; Anchor = 'Left'; Margin = '3,8,12,3' }))
    $layout.Controls.Add($control)
}
function Add-Wide($control) {
    $layout.Controls.Add($control)
    $layout.SetColumnSpan($control, 2)
}
function New-Combo($items, $selected) {
    $combo = New-Object System.Windows.Forms.ComboBox -Property @{ DropDownStyle = 'DropDownList'; Width = 160 }
    $combo.Items.AddRange($items); $combo.SelectedItem = $selected
    $combo
}
function New-Check($text, $checked) {
    New-Object System.Windows.Forms.CheckBox -Property @{ Text = $text; Checked = [bool]$checked; AutoSize = $true }
}

$orm = New-Combo @('EfCore', 'Dapper') $settings.Orm
$database = New-Combo @('Postgres', 'SqlServer') $settings.Database
$cache = New-Check 'Redis (cache-aside)' $settings.Cache
$kafka = New-Check 'Kafka (outbox + konsument)' $settings.Kafka
# OpenTelemetry do Aspire Dashboard (http://localhost:18888); bez zaznaczenia API nie wysyła telemetrii.
# API w kontenerze: tylko uruchomienie dashboardu - kontener wysyła telemetrię zawsze (docker-compose.yml)
$dashboard = New-Check 'Aspire Dashboard (ślady, metryki, logi)' $settings.Dashboard
# schemat przy starcie: None (już jest), Migrations (migracje EF), Scripts (skrypty db/, bez EF) - ten sam wynik
$schema = New-Combo @('None', 'Migrations', 'Scripts') $settings.Schema
$docker = New-Check 'uruchom bazę i zaznaczone usługi' $settings.Docker
# API w kontenerze wariantu (compose: obraz z bieżącego kodu, ustawienia z docker-compose.yml) zamiast z VS
$apiContainer = New-Check 'w kontenerze (compose, bez debuggera VS)' $settings.ApiContainer
Add-Row 'ORM' $orm
Add-Row 'Baza danych' $database
Add-Row 'API' $apiContainer
Add-Row 'Cache' $cache
Add-Row 'Zdarzenia' $kafka
Add-Row 'Telemetria' $dashboard
Add-Row 'Schemat przy starcie' $schema
Add-Row 'Kontenery (compose)' $docker

# --- status kontenerów (osobna ramka): kropka zielona - działa, pomarańczowa - startuje, czerwona - zatrzymany
# albo utknął, szara - brak kontenera; pogrubione - potrzebne wybranemu wariantowi ---

$group = New-Object System.Windows.Forms.GroupBox -Property @{
    Text = 'Kontenery w Dockerze (pogrubione - potrzebne wariantowi)'; AutoSize = $true; AutoSizeMode = 'GrowAndShrink'
    Anchor = 'Left, Right'; Margin = '3,14,3,3'; Padding = '8,6,8,8'
}
$statusLayout = New-Object System.Windows.Forms.TableLayoutPanel -Property @{ ColumnCount = 2; AutoSize = $true; Dock = 'Fill' }
$group.Controls.Add($statusLayout)
Add-Wide $group

# nagłówek ramki (osobny wiersz na całą szerokość, żeby nie poszerzał kolumny nazw): czas ostatniego
# odświeżenia i przycisk z ikoną
$header = New-Object System.Windows.Forms.TableLayoutPanel -Property @{ ColumnCount = 2; AutoSize = $true; Dock = 'Fill'; Margin = '0,0,0,4' }
[void]$header.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle('Percent', 100)))
$refreshed = New-Object System.Windows.Forms.Label -Property @{ Text = 'Odświeżono: -'; AutoSize = $true; ForeColor = 'Gray'; Anchor = 'Left' }
$refresh = New-Object System.Windows.Forms.Button -Property @{
    Text = '⟳'; Size = '34,30'; Anchor = 'Right'; Margin = '12,0,0,0'; Font = New-Object System.Drawing.Font('Segoe UI Symbol', 13)
}
$tooltip = New-Object System.Windows.Forms.ToolTip
$tooltip.SetToolTip($refresh, 'Odśwież status kontenerów')
$header.Controls.Add($refreshed)
$header.Controls.Add($refresh)
$statusLayout.Controls.Add($header)
$statusLayout.SetColumnSpan($header, 2)

# api - kontener API wybranego wariantu (api-efcore-postgres itd.)
$containers = [ordered]@{
    postgres = 'PostgreSQL'; sqlserver = 'SQL Server'; redis = 'Redis'; kafka = 'Kafka'; 'aspire-dashboard' = 'Aspire Dashboard'; api = 'API'
}
# Nazwy w statusie to linki (Open-Container): API i Aspire Dashboard otwierają UI w przeglądarce, a bazy, Redis i Kafka
# kopiują do schowka adres z appsettings.Development.json (np. do SSMS, DBeavera, redis-cli). PowerShell 5.1
# nie czyta JSON z komentarzami - pomijane są linie z samym komentarzem.
$config = try { (Get-Content (Join-Path $root 'src\Products.Api\appsettings.Development.json') -Encoding UTF8) -notmatch '^\s*//' -join "`n" | ConvertFrom-Json } catch { $null }
$addresses = @{
    postgres = $config.ConnectionStrings.Postgres; sqlserver = $config.ConnectionStrings.SqlServer
    redis = $config.ConnectionStrings.Redis; kafka = $config.Kafka.BootstrapServers
}
# kolumna nazw o stałej szerokości - najdłuższej nazwy pogrubionej (także API każdego wariantu), żeby pogrubienie
# potrzebnych kontenerów nie przesuwało statusów
$probe = New-Object System.Windows.Forms.LinkLabel -Property @{ AutoSize = $true; Font = New-Object System.Drawing.Font($form.Font, [System.Drawing.FontStyle]::Bold) }
$names = @($containers.Values) + @('efcore-postgres', 'efcore-sqlserver', 'dapper-postgres', 'dapper-sqlserver' | ForEach-Object { "API ($_)" })
$nameWidth = ($names | ForEach-Object { $probe.Text = $_; $probe.PreferredSize.Width } | Measure-Object -Maximum).Maximum
$rows = @{}
foreach ($service in $containers.Keys) {
    $name = New-Object System.Windows.Forms.LinkLabel -Property @{
        Text = $containers[$service]; AutoSize = $true; MinimumSize = New-Object System.Drawing.Size($nameWidth, 0)
        Anchor = 'Left'; Margin = '3,4,12,0'; LinkBehavior = 'HoverUnderline'; Tag = $service
    }
    $name.add_LinkClicked({ Open-Container $this })
    $tooltip.SetToolTip($name, $(switch ($service) {
        'api' { 'Otwórz grid API wariantu (kontener) w przeglądarce' }
        'aspire-dashboard' { 'Otwórz http://localhost:18888 w przeglądarce' }
        default { "Kopiuj do schowka: $($addresses[$service])" }
    }))
    $dot = New-Object System.Windows.Forms.Label -Property @{
        Text = '●'; AutoSize = $true; ForeColor = 'Gray'; Margin = '0,0,2,0'; Font = New-Object System.Drawing.Font('Segoe UI', 12)
    }
    $text = New-Object System.Windows.Forms.Label -Property @{ Text = 'sprawdzanie...'; AutoSize = $true; Margin = '0,4,0,0' }
    $cell = New-Object System.Windows.Forms.FlowLayoutPanel -Property @{ AutoSize = $true; WrapContents = $false; Margin = '0,0,0,0' }
    $cell.Controls.AddRange(@($dot, $text))
    $statusLayout.Controls.Add($name)
    $statusLayout.Controls.Add($cell)
    $rows[$service] = @{ Name = $name; Dot = $dot; Text = $text; Running = $false }
}
$summary = New-Object System.Windows.Forms.Label -Property @{ AutoSize = $true; MaximumSize = '330,0'; Margin = '3,8,3,0' }
$statusLayout.Controls.Add($summary)
$statusLayout.SetColumnSpan($summary, 2)

# profil i sufiks usługi compose wariantu: efcore-postgres, dapper-sqlserver...
function Get-Variant { "$($orm.SelectedItem)-$($database.SelectedItem)".ToLower() }

# adres API w kontenerze wariantu (port z compose); błąd, gdy kontener nie działa
function Get-ApiUrl {
    $variant = Get-Variant
    "http://localhost:$(((Invoke-Compose "--profile $variant port api-$variant 8080" 10) -split "`r?`n")[0].Split(':')[-1])/"
}

function Open-Container($link) {
    $service = $link.Tag
    if ($service -eq 'aspire-dashboard') { Start-Process 'http://localhost:18888'; return }
    if ($service -eq 'api') {
        try { Start-Process (Get-ApiUrl) }
        catch { [System.Windows.Forms.MessageBox]::Show($form, "Kontener API wariantu $(Get-Variant) nie działa.", 'API', 'OK', 'Information') | Out-Null }
        return
    }
    if (-not $addresses[$service]) { return }
    [System.Windows.Forms.Clipboard]::SetText($addresses[$service])
    $tooltip.Show('Skopiowano do schowka', $link, 0, $link.Height, 1500)
}

# Kontenery potrzebne wariantowi. API w kontenerze: zawsze Redis i Kafka (zależności w compose) oraz kontener API.
# Aspire Dashboard w obu trybach, jeśli zaznaczony.
function Get-Needed {
    $telemetry = @(if ($dashboard.Checked) { 'aspire-dashboard' })
    if ($apiContainer.Checked) { return @($database.SelectedItem.ToLower(), 'redis', 'kafka') + $telemetry + 'api' }
    @($database.SelectedItem.ToLower()) + @(if ($cache.Checked) { 'redis' }) + @(if ($kafka.Checked) { 'kafka' }) + $telemetry
}

# Wiersze statusu i podsumowanie ze stanu z ostatniego odświeżenia - bez wywołań dockera (przy zmianie opcji
# w oknie). Opcje bez znaczenia dla API w kontenerze (ustawienia z docker-compose.yml) i wszystkie po starcie
# (API działa już z tym wyborem) są wyszarzone.
function Show-Status {
    foreach ($control in @($cache, $kafka, $schema, $docker)) { $control.Enabled = -not ($apiContainer.Checked -or $script:started) }
    $rows['api'].Name.Text = "API ($(Get-Variant))"
    foreach ($service in $rows.Keys) {
        $row = $rows[$service]
        $container = $script:states[$(if ($service -eq 'api') { "api-$(Get-Variant)" } else { $service })]
        $row.Running = $container.State -eq 'running' -and $container.Status -notmatch 'health: starting'
        $row.Dot.ForeColor, $row.Text.Text =
            if (-not $script:available) { 'Gray', 'Docker nie odpowiada' }
            elseif (-not $container) { 'Gray', 'brak kontenera' }
            elseif ($container.State -eq 'created') { 'Firebrick', 'utknął przy starcie (zrestartuj Docker Desktop)' }
            elseif ($container.State -ne 'running') { 'Firebrick', "zatrzymany ($($container.State))" }
            elseif (-not $row.Running) { 'DarkOrange', 'startuje' }
            else { 'ForestGreen', 'działa' }
    }

    $needed = Get-Needed
    foreach ($service in $rows.Keys) {
        $style = if ($service -in $needed) { 'Bold' } else { 'Regular' }
        $rows[$service].Name.Font = New-Object System.Drawing.Font($form.Font, [System.Drawing.FontStyle]$style)
    }
    $missing = @($needed | Where-Object { -not $rows[$_].Running } | ForEach-Object { $rows[$_].Name.Text })
    $summary.ForeColor = if ($missing -and $script:stuck) { 'Firebrick' } else { $form.ForeColor }
    $containersText = if (-not $missing) { "Wszystkie potrzebne kontenery działają$(if (-not $script:started) { ' - start API od razu' })." }
        elseif ($script:stuck) { "Nie działa: $($missing -join ', '). Docker nie uruchamia nowych kontenerów (któryś utknął przy starcie) - najpierw zrestartuj Docker Desktop." }
        elseif ($script:started -and -not $apiContainer.Checked) { "Nie działa: $($missing -join ', ')." }
        elseif ($apiContainer.Checked -or $docker.Checked) { "Do uruchomienia: $($missing -join ', ')." }
        else { "Nie działa: $($missing -join ', '). Zaznacz 'Kontenery', żeby uruchomić je przed startem API." }
    $summary.Text = if ($script:started -and -not $apiContainer.Checked) { "API działa z VS - okno zamknie się razem z nim. $containersText" } else { $containersText }
}

# Stan kontenerów projektu z docker ps (szybkie, nie tworzy kontenerów). Kontener w stanie "created" to objaw
# zawieszonego Docker Desktop: start nowych kontenerów nie kończy się, dopóki Docker nie zostanie zrestartowany.
$states = @{}
$available = $true
$stuck = $false
$started = $false # po starcie API okno jest panelem statusu (Set-Started)
function Update-ContainerStatus {
    $script:states = @{}
    $script:available = $true
    $script:stuck = $false
    $refresh.Enabled = $false
    $refreshed.Text = 'Odświeżanie...'
    $refreshed.Refresh()
    try {
        $lines = Invoke-Docker 'ps -a --filter label=com.docker.compose.project=rekrutacja --format "{{.Label \"com.docker.compose.service\"}}|{{.State}}|{{.Status}}"' 10
        foreach ($line in $lines -split "`n") {
            $service, $state, $status = $line.Trim() -split '\|', 3
            if ($state -eq 'created') { $script:stuck = $true }
            if ($service) { $script:states[$service] = @{ State = $state; Status = $status } }
        }
    }
    catch {
        $script:available = $false
    }
    $refreshed.Text = "Odświeżono: $((Get-Date).ToString('dd.MM.yyyy HH:mm:ss'))"
    $refresh.Enabled = $true
    Show-Status
}

foreach ($control in @($orm, $database)) { $control.add_SelectedIndexChanged({ Show-Status }) }
foreach ($control in @($cache, $kafka, $dashboard, $docker, $apiContainer)) { $control.add_CheckedChanged({ Show-Status }) }
$form.add_Shown({ Update-ContainerStatus })
$refresh.add_Click({ Update-ContainerStatus })

$buttons = New-Object System.Windows.Forms.FlowLayoutPanel -Property @{ AutoSize = $true; Anchor = 'Right'; Margin = '3,12,3,3' }
$ok = New-Object System.Windows.Forms.Button -Property @{ Text = 'Uruchom' }
$cancel = New-Object System.Windows.Forms.Button -Property @{ Text = 'Anuluj'; DialogResult = 'Cancel' }
$buttons.Controls.AddRange(@($ok, $cancel))
Add-Wide $buttons
$form.AcceptButton = $ok
$form.CancelButton = $cancel

# Kontenery przed startem:
# - API z VS: tylko baza oraz Redis, Kafka i Aspire Dashboard, jeśli zaznaczone. SQL Server: baza Products tym samym poleceniem co
#   sqlserver-init, ale przez exec w działającym kontenerze sqlserver - bez nowego kontenera (szybciej; działa też,
#   gdy Docker Desktop nie uruchamia nowych kontenerów);
# - API w kontenerze: kontenery API innych wariantów usuwane (działa jeden wariant), compose up --build kontenera
#   API wariantu z zależnościami (i dashboardem, jeśli zaznaczony), czekanie na /health/live i grid w przeglądarce
#   (adres w $script:apiUrl).
# Okno postępu na czas startu; błąd, przekroczenie czasu i przerwanie jako wyjątek.
function Start-Containers($settings) {
    $services = (Get-Needed | Where-Object { $_ -ne 'api' }) -join ' '
    $script:launch = @{ Cancelled = $false } # „Przerwij” - obiekt, więc zmiana z obsługi zdarzenia jest widoczna w Invoke-Docker
    $status = New-Object System.Windows.Forms.Form -Property @{
        Text = 'Products API - kontenery'; StartPosition = 'CenterScreen'; TopMost = $true; ControlBox = $false
        AutoSize = $true; AutoSizeMode = 'GrowAndShrink'; Padding = New-Object System.Windows.Forms.Padding(12)
    }
    $panel = New-Object System.Windows.Forms.FlowLayoutPanel -Property @{ AutoSize = $true; FlowDirection = 'TopDown' }
    $label = New-Object System.Windows.Forms.Label -Property @{ AutoSize = $true; MinimumSize = '480,0'; MaximumSize = '480,0'; Font = New-Object System.Drawing.Font($status.Font, [System.Drawing.FontStyle]::Bold) }
    # podgląd wyjścia docker compose (tworzenie, start, healthcheck, pobieranie i budowanie obrazów)
    $feed = New-Object System.Windows.Forms.TextBox -Property @{
        Multiline = $true; ReadOnly = $true; ScrollBars = 'Vertical'; Size = '480,160'; WordWrap = $false
        Font = New-Object System.Drawing.Font('Consolas', 9); BackColor = 'White'; Margin = '3,6,3,3'
    }
    $hint = New-Object System.Windows.Forms.Label -Property @{
        AutoSize = $true; MaximumSize = '480,0'; ForeColor = 'Gray'; Margin = '3,6,3,6'
        Text = 'Zwykle kilka-kilkanaście sekund (budowanie obrazu API dłużej). Gdy licznik rośnie dłużej niż minutę, a w podglądzie nic się nie zmienia, Docker Desktop mógł się zawiesić - przerwij i zrestartuj go.'
    }
    $abort = New-Object System.Windows.Forms.Button -Property @{ Text = 'Przerwij'; AutoSize = $true; Anchor = 'Right' }
    $abort.add_Click({ $launch.Cancelled = $true })
    $panel.Controls.AddRange(@($label, $feed, $hint, $abort))
    $status.Controls.Add($panel)
    $progress = @{ Label = $label; Feed = $feed; Step = ''; Current = ''; States = [ordered]@{} }
    function Set-Step($step, $current) {
        $progress.Step = $step; $progress.Current = $current; $progress.States.Clear()
        $label.Text = "$step`n$current"
        $feed.AppendText("> $step`r`n")
        $status.Refresh()
    }
    $status.Show()
    try {
        if ($settings.ApiContainer) {
            $variant = Get-Variant
            $others = @('efcore-postgres', 'efcore-sqlserver', 'dapper-postgres', 'dapper-sqlserver') | Where-Object { $_ -ne $variant }
            Set-Step 'Inne warianty API' 'usuwanie kontenerów API innych wariantów - działa jeden wariant'
            Invoke-Compose "--profile `"*`" rm -s -f $(($others | ForEach-Object { "api-$_" }) -join ' ')" 120 $progress | Out-Null
            Set-Step "API w kontenerze: $variant" "docker compose up --build: obraz z bieżącego kodu, $($services -replace ' ', ', ')"
            Invoke-Compose "--profile $variant up -d --build api-$variant $services" 900 $progress | Out-Null
            $script:apiUrl = Get-ApiUrl
            Set-Step "API w kontenerze: $variant" "czekanie na $($apiUrl)health/live (schemat bazy przy starcie)"
            Wait-Http "$($apiUrl)health/live" 180 $progress
            Start-Process $apiUrl
        }
        else {
            # obrazy już pobrane: start trwa sekundy, więc krótki limit szybko wykrywa zawieszony Docker;
            # brakujące obrazy (pierwsze uruchomienie) - zapas na pobieranie
            Set-Step "Sprawdzanie obrazów: $($services -replace ' ', ', ')" 'docker image inspect'
            $images = (Invoke-Compose "config --images $services" 30) -split "`r?`n" | Where-Object { $_ }
            $timeout = 90
            try { Invoke-Docker "image inspect $($images -join ' ')" 30 | Out-Null } catch { $timeout = 600 }
            Set-Step "Kontenery: $($services -replace ' ', ', ')" $(if ($timeout -gt 90) { 'pobieranie brakujących obrazów' } else { 'docker compose up' })
            Invoke-Compose "up -d --wait --wait-timeout $($timeout - 10) $services" $timeout $progress | Out-Null
            if ($settings.Database -eq 'SqlServer') {
                Set-Step 'Baza Products w SQL Server' 'sqlserver - sqlcmd jak w sqlserver-init (exec, bez nowego kontenera)'
                $init = (Invoke-Compose '--profile sqlserver config --format json' 60 | ConvertFrom-Json).services.'sqlserver-init'.entrypoint
                $command = ($init | ForEach-Object { '"' + $_ + '"' }) -join ' '
                Invoke-Compose "exec -T sqlserver $command" 120 $progress | Out-Null
            }
        }
    }
    finally {
        $status.Close()
    }
}

# Wynik dla DevLauncher (koniec - pusta linia); [Console]::Out, bo wyjście obsługi zdarzenia nie trafia na stdout
function Write-Result([string[]]$lines) {
    foreach ($line in $lines + '') { [Console]::Out.WriteLine($line) }
    [Console]::Out.Flush()
}

# Po starcie okno jest panelem statusu kontenerów (linki, odświeżanie), nie na wierzchu.
# - API z VS: opcje zablokowane (API działa już z tym wyborem), panel zamyka się razem z procesem API;
# - API w kontenerze: proces z VS kończy pracę, panel zostaje. Wariant i dashboard można zmienić, a „Uruchom”
#   przebudowuje obraz z bieżącego kodu i uruchamia wybrany wariant ponownie - tylko w kontenerze, bo nie ma już
#   procesu z VS, który przyjąłby argumenty.
$watch = New-Object System.Windows.Forms.Timer -Property @{ Interval = 2000 }
$watch.add_Tick({ if (-not (Get-Process -Id $ParentProcessId -ErrorAction SilentlyContinue)) { $form.Close() } })
function Set-Started {
    $script:started = $true
    $locked = if ($apiContainer.Checked) { @($apiContainer) } else { @($orm, $database, $apiContainer, $dashboard, $ok) }
    foreach ($control in $locked) { $control.Enabled = $false }
    if ($apiContainer.Checked) { $tooltip.SetToolTip($ok, 'Przebuduj obraz API z bieżącego kodu i uruchom wybrany wariant') }
    $cancel.Text = 'Zamknij'
    $form.TopMost = $false
    $form.MinimizeBox = $true
    $form.Text = "Products API - $(Get-Variant)$(if ($apiContainer.Checked) { ' (kontener)' })"
    if ($ParentProcessId -and -not $apiContainer.Checked) { $watch.Start() }
    Update-ContainerStatus
}

# Uruchom: zapis wyboru, kontenery, wynik dla DevLauncher (tylko przy pierwszym starcie - potem proces z VS już nie
# czyta). Błąd Dockera nie zamyka okna - można np. zrestartować Docker Desktop i uruchomić ponownie (API z VS czeka).
$ok.add_Click({
    $settings = [ordered]@{
        Orm = $orm.SelectedItem; Database = $database.SelectedItem
        Cache = $cache.Checked; Kafka = $kafka.Checked; Schema = $schema.SelectedItem; Docker = $docker.Checked
        ApiContainer = $apiContainer.Checked; Dashboard = $dashboard.Checked
    }
    New-Item -ItemType Directory -Force (Split-Path $settingsPath) | Out-Null
    $settings | ConvertTo-Json | Set-Content $settingsPath -Encoding UTF8

    $form.Enabled = $false
    try {
        if ($settings.ApiContainer -or $settings.Docker) { Start-Containers $settings }
    }
    catch {
        [System.Windows.Forms.MessageBox]::Show($form, $_.Exception.Message, 'Docker - błąd uruchamiania kontenerów', 'OK', 'Error') | Out-Null
        return
    }
    finally {
        $form.Enabled = $true
    }

    if (-not $started) {
        Write-Result $(if ($settings.ApiContainer) { $apiUrl } else {
            "--Persistence:Orm=$($settings.Orm)"
            "--Persistence:Database=$($settings.Database)"
            "--Cache:Enabled=$($settings.Cache)"
            "--Kafka:Enabled=$($settings.Kafka)"
            "--Database:Schema=$($settings.Schema)"
            if (-not $settings.Dashboard) { '--OTEL_EXPORTER_OTLP_ENDPOINT=' } # pusty adres wyłącza telemetrię (Observability.cs)
        })
    }
    Set-Started
})

[void]$form.ShowDialog()
