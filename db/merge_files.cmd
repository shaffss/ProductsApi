@echo off
:: For every engine folder (sqlserver, postgres) merges each source folder into Publish:
::   Create\*.sql -> Publish\create.sql (schema), Demo\*.sql -> Publish\demo.sql (demo data),
::   Benchmark\*.sql -> Publish\benchmark.sql (bulk data for benchmarks)
:: Files are merged in name order (NNN_ prefix), each preceded by a header comment.
:: UTF-8 BOMs are stripped - in the middle of a merged file both sqlcmd and psql treat them
:: as a syntax error (VS/SSMS may add a BOM when saving). Output uses LF line endings like the
:: rest of the repository (.editorconfig, .gitattributes).
setlocal
cd /d "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ErrorActionPreference = 'Stop';" ^
  "foreach ($source in Get-ChildItem -Directory | Get-ChildItem -Directory | Where-Object Name -ne 'Publish') {" ^
  "  $files = Get-ChildItem $source.FullName -Filter *.sql | Sort-Object Name;" ^
  "  if (-not $files) { continue }" ^
  "  $lines = foreach ($f in $files) { '-- ===== ' + $f.Name + ' ====='; ((Get-Content $f.FullName -Raw -Encoding UTF8) -replace [char]13, '').TrimEnd(); '' };" ^
  "  $publish = New-Item -ItemType Directory -Force (Join-Path $source.Parent.FullName 'Publish');" ^
  "  $output = Join-Path $publish.FullName ($source.Name.ToLower() + '.sql');" ^
  "  [IO.File]::WriteAllText($output, ($lines -join [char]10) + [char]10);" ^
  "  Write-Host ('{0} <- {1} files' -f $output.Substring($PWD.Path.Length + 1), @($files).Count) }"
set "rc=%errorlevel%"

if %rc% neq 0 (echo Merging failed.) else (echo Merging completed.)

:: pause only when started by double-click, otherwise the window would close immediately
echo %cmdcmdline% | findstr /i /c:"%~nx0" >nul && pause
exit /b %rc%
