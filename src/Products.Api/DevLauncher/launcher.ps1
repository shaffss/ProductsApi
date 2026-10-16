# Okno wyboru wariantu uruchomienia Products.Api (profil "Launcher" w launchSettings.json).
# Wynik na stdout: argumenty konfiguracji "--Klucz=wartość", po jednym w linii. Anuluj = kod wyjścia 1.
# Ostatni wybór zapamiętany w %LOCALAPPDATA%\ProductsApi\launcher.json.

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$settingsPath = Join-Path $env:LOCALAPPDATA 'ProductsApi\launcher.json'
$settings = [ordered]@{ Orm = 'EfCore'; Database = 'Postgres'; Cache = $true; Kafka = $true; Schema = 'None'; Docker = $true }
if (Test-Path $settingsPath) {
    $saved = Get-Content $settingsPath -Raw | ConvertFrom-Json
    foreach ($key in @($settings.Keys)) { if ($null -ne $saved.$key) { $settings[$key] = $saved.$key } }
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
# schemat przy starcie: None (już jest), Migrations (migracje EF), Scripts (skrypty db/, bez EF) - ten sam wynik
$schema = New-Combo @('None', 'Migrations', 'Scripts') $settings.Schema
$docker = New-Check 'uruchom bazę i zaznaczone usługi' $settings.Docker
Add-Row 'ORM' $orm
Add-Row 'Baza danych' $database
Add-Row 'Cache' $cache
Add-Row 'Zdarzenia' $kafka
Add-Row 'Schemat przy starcie' $schema
Add-Row 'Kontenery (compose)' $docker

$buttons = New-Object System.Windows.Forms.FlowLayoutPanel -Property @{ AutoSize = $true; Anchor = 'Right'; Margin = '3,12,3,3' }
$ok = New-Object System.Windows.Forms.Button -Property @{ Text = 'Uruchom'; DialogResult = 'OK' }
$cancel = New-Object System.Windows.Forms.Button -Property @{ Text = 'Anuluj'; DialogResult = 'Cancel' }
$buttons.Controls.AddRange(@($ok, $cancel))
$layout.Controls.Add($buttons)
$layout.SetColumnSpan($buttons, 2)
$form.AcceptButton = $ok
$form.CancelButton = $cancel

if ($form.ShowDialog() -ne 'OK') { exit 1 }

$settings = [ordered]@{
    Orm = $orm.SelectedItem; Database = $database.SelectedItem
    Cache = $cache.Checked; Kafka = $kafka.Checked; Schema = $schema.SelectedItem; Docker = $docker.Checked
}
New-Item -ItemType Directory -Force (Split-Path $settingsPath) | Out-Null
$settings | ConvertTo-Json | Set-Content $settingsPath -Encoding UTF8

# Kontenery wariantu przed startem API - tylko baza oraz Redis i Kafka, jeśli zaznaczone (bez kontenerów API).
# SQL Server: jednorazowy sqlserver-init osobno (run), bo --wait traktuje zakończony kontener jak błąd.
if ($settings.Docker) {
    $root = $PSScriptRoot
    while ($root -and -not (Test-Path (Join-Path $root 'docker-compose.yml'))) { $root = Split-Path $root }
    if (-not $root) { [System.Windows.Forms.MessageBox]::Show('Nie znaleziono docker-compose.yml.', 'Docker') | Out-Null; exit 1 }
    # stdout tego skryptu to argumenty dla API - wyjście dockera tylko do zmiennej (pokazywane przy błędzie)
    function Invoke-Compose { & docker compose -f (Join-Path $root 'docker-compose.yml') @args 2>&1 | ForEach-Object { "$_" } | Out-String }

    $services = @($settings.Database.ToLower()) + @(if ($settings.Cache) { 'redis' }) + @(if ($settings.Kafka) { 'kafka' })
    $status = New-Object System.Windows.Forms.Form -Property @{
        Text = 'Products API'; StartPosition = 'CenterScreen'; TopMost = $true; ControlBox = $false; Width = 420; Height = 110
    }
    $status.Controls.Add((New-Object System.Windows.Forms.Label -Property @{
        Text = "Uruchamianie kontenerów: $($services -join ', ')..."; Dock = 'Fill'; TextAlign = 'MiddleCenter'
    }))
    $status.Show(); $status.Refresh()
    $log = Invoke-Compose up -d --wait --wait-timeout 180 @services
    if ($LASTEXITCODE -eq 0 -and $settings.Database -eq 'SqlServer') { $log += Invoke-Compose run --rm sqlserver-init }
    $failed = $LASTEXITCODE -ne 0
    $status.Close()
    if ($failed) { [System.Windows.Forms.MessageBox]::Show($log, 'Docker - błąd uruchamiania kontenerów', 'OK', 'Error') | Out-Null; exit 1 }
}

"--Persistence:Orm=$($settings.Orm)"
"--Persistence:Database=$($settings.Database)"
"--Cache:Enabled=$($settings.Cache)"
"--Kafka:Enabled=$($settings.Kafka)"
"--Database:Schema=$($settings.Schema)"
