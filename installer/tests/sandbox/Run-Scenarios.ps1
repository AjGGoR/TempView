# Runs INSIDE a disposable Windows Sandbox / VM (never on a daily-use PC): installs, upgrades and removes
# TempView with the offline installer and records observable facts. Results: C:\TempViewTest\results\.
# Network should be disabled (scripts/start-sandbox-test.ps1 does that) to prove offline installation.
$ErrorActionPreference = 'Continue'
$root = 'C:\TempViewTest'
$results = Join-Path $root 'results'
New-Item -ItemType Directory -Path $results -Force | Out-Null
$report = Join-Path $results 'report.txt'
$setup = Get-ChildItem (Join-Path $root 'installer') -Filter 'TempView-Setup-*.exe' | Select-Object -First 1
$settings = Join-Path $env:LOCALAPPDATA 'TempView\settings.json'
$service = 'TempViewSensors'

function Note([string]$Text) { $line = "[$(Get-Date -Format HH:mm:ss)] $Text"; Write-Host $line; Add-Content -LiteralPath $report -Value $line -Encoding utf8 }
function Check([bool]$Condition, [string]$Name) { Note ($(if ($Condition) { 'PASS ' } else { 'FAIL ' }) + $Name) }
function Run-Setup([string]$Name, [string[]]$Arguments) {
    $log = Join-Path $results "$Name.log"
    $p = Start-Process -FilePath $setup.FullName -ArgumentList (@('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=`"$log`"") + $Arguments) -Wait -PassThru
    Note "$Name -> exit code $($p.ExitCode)"
    return $p.ExitCode
}
function Installed([string]$Dir) { Test-Path -LiteralPath (Join-Path $Dir 'TempView.exe') }
function ServiceInfo { $s = Get-Service $service -ErrorAction SilentlyContinue; if ($s) { "$($s.Status)/$($s.StartType)" } else { 'missing' } }
function PawnIO { (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO' -ErrorAction SilentlyContinue).DisplayVersion }
function ReadPipe {
    try {
        $c = New-Object System.IO.Pipes.NamedPipeClientStream('.', 'TempView.Sensors.v1', [System.IO.Pipes.PipeDirection]::In)
        $c.Connect(5000); $json = (New-Object System.IO.StreamReader($c)).ReadToEnd(); $c.Dispose(); return $json
    } catch { return "pipe error: $($_.Exception.Message)" }
}

Note "Installer: $($setup.Name) SHA-256 $((Get-FileHash $setup.FullName -Algorithm SHA256).Hash)"
Note "Windows: $([Environment]::OSVersion.VersionString); CPU: $((Get-CimInstance Win32_Processor).Name)"
Note "Network adapters up: $(@(Get-NetAdapter -ErrorAction SilentlyContinue | Where-Object Status -eq 'Up').Count)"
Note "PawnIO before: '$(PawnIO)'"

# 1. Fresh offline install with CPU sensors (PawnIO + service).
$default = Join-Path $env:ProgramFiles 'TempView'
$code = Run-Setup 'install' @('/CPUSENSORS=1', '/TASKS=autostart')
Check ($code -in 0, 101) 'fresh install finished (0 = full, 101 = CPU component failed)'
Check (Installed $default) 'files in Program Files'
Check ($null -ne (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{21B97CC7-B326-4BBD-9025-B78A80062FC3}_is1' -ErrorAction SilentlyContinue)) 'registered in Installed apps'
Check ((Get-ItemProperty 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Run' -ErrorAction SilentlyContinue).TempView -like '*TempView.exe*') 'autostart entry'
Check (-not (Test-Path (Join-Path $default 'prerequisites'))) 'driver payload removed after install'
Note "PawnIO after install: '$(PawnIO)'; driver service: $((Get-Service PawnIO -ErrorAction SilentlyContinue).Status)"
Note "Sensor service: $(ServiceInfo)"
Note "Service privileges: $((& sc.exe qprivs $service) -join ' ')"
Note "Pipe snapshot: $(ReadPipe)"
$acl = (Get-Acl $default).Access | Where-Object { $_.IdentityReference -match 'Users|Użytkownicy' -and $_.FileSystemRights -match 'Write|Modify|FullControl' }
Check ($null -eq $acl) 'installation directory not writable by users'

# 2. Upgrade/reinstall keeps user settings; running TempView is closed by setup.
New-Item -ItemType Directory -Path (Split-Path $settings) -Force | Out-Null
'{"SchemaVersion":2,"Theme":"light","Width":150,"AutoWidth":false}' | Set-Content -LiteralPath $settings -Encoding utf8
$before = Get-FileHash $settings
$app = Start-Process (Join-Path $default 'TempView.exe') -PassThru; Start-Sleep 5
$code = Run-Setup 'upgrade' @('/CPUSENSORS=1', '/CLOSEAPPLICATIONS')
Check ($code -in 0, 101) 'reinstall over running TempView finished'
Check ($app.HasExited) 'running TempView was closed for the upgrade'
Check ((Get-FileHash $settings).Hash -eq $before.Hash) 'settings preserved across upgrade'
Check (@(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*' | Where-Object DisplayName -eq 'TempView').Count -eq 1) 'single Installed apps entry'

# 3. Uninstall: TempView-owned items removed, PawnIO and settings kept.
$uninstaller = Join-Path $default 'unins000.exe'
$p = Start-Process $uninstaller -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=`"$(Join-Path $results 'uninstall.log')`"" -Wait -PassThru
Note "uninstall -> exit code $($p.ExitCode)"; Start-Sleep 3
Check (-not (Installed $default)) 'program files removed'
Check ((ServiceInfo) -eq 'missing') 'sensor service removed'
Check (-not (Test-Path 'HKLM:\SYSTEM\CurrentControlSet\Services\EventLog\Application\TempViewSensors')) 'service event source removed'
Check ($null -eq (Get-ItemProperty 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Run' -ErrorAction SilentlyContinue).TempView) 'autostart entry removed'
Check (Test-Path $settings) 'user settings preserved after uninstall'
Note "PawnIO after uninstall (expected unchanged): '$(PawnIO)'"

# 4. Install without CPU sensors into a path with spaces and non-ASCII characters.
$odd = 'C:\Program Files\Temp View Żółć'
$code = Run-Setup 'gpu-only-unicode-path' @('/CPUSENSORS=0', "/DIR=`"$odd`"", '/TASKS=')
Check ($code -eq 0) 'GPU-only install finished'
Check (Installed $odd) 'installed into path with spaces and non-ASCII characters'
Check ((ServiceInfo) -eq 'missing') 'no sensor service when CPU reading was declined'
Start-Process (Join-Path $odd 'unins000.exe') -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -Wait
Check (-not (Installed $odd)) 'unicode-path installation removed'
Note 'Done. Manual checks left: UAC prompt/cancel as standard user, wizard pages, restart-required flow (see docs/INSTALLER-TESTS.md).'
