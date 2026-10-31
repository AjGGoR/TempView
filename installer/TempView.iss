; TempView — offline all-in-one installer (Windows 11 x64).
; Built by scripts/build-installer.ps1, which passes the defines below after verifying every payload.
;   AppVersion, PublishDir (self-contained TempView + TempView.Sensors), PrereqDir (verified PawnIO payload
;   and sources), PawnIOVersion, PawnIOSha256, OutputName, optional Sign.

#ifndef AppVersion
  #error Run scripts/build-installer.ps1 instead of compiling this file directly.
#endif

[Setup]
; AppId identifies the installation for upgrades. Never change it.
AppId={{21B97CC7-B326-4BBD-9025-B78A80062FC3}
AppName=TempView
AppVersion={#AppVersion}
AppVerName=TempView {#AppVersion}
AppPublisher=TempView contributors
AppPublisherURL=https://github.com/AjGGoR/TempView
AppSupportURL=https://github.com/AjGGoR/TempView/issues
VersionInfoVersion={#AppVersion}
VersionInfoProductName=TempView
VersionInfoDescription=Instalator TempView
DefaultDirName={autopf}\TempView
DisableProgramGroupPage=yes
DisableDirPage=auto
DisableWelcomePage=yes
; Per-machine install into Program Files (only administrators can modify it).
#ifdef Preview
; Visual check only: runs without UAC and refuses to install (see NextButtonClick).
PrivilegesRequired=lowest
#else
PrivilegesRequired=admin
#endif
; x64 Windows only (not ARM64 emulation): PawnIO and the bundled runtime are x64. Windows 11 = build 22000.
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.22000
WizardStyle=modern
SetupIconFile=..\src\TempView\Assets\TempView.ico
; Rendered from Assets\TempView.ico (55 px at 100% scaling, 110 px at 200%).
WizardSmallImageFile=WizardSmall-55.png,WizardSmall-110.png
UninstallDisplayIcon={app}\TempView.exe
UninstallDisplayName=TempView
OutputDir=..\artifacts\installer
OutputBaseFilename={#OutputName}
Compression=lzma2/max
SolidCompression=yes
SetupLogging=yes
ShowLanguageDialog=no
; A running TempView (installed or portable) is asked to exit through its exit event (see CloseTempView);
; Restart Manager covers any other process holding files in the installation directory.
CloseApplications=yes
RestartApplications=no
#ifdef Sign
SignTool=tvsign
SignedUninstaller=yes
#endif

[Languages]
Name: "pl"; MessagesFile: "compiler:Languages\Polish.isl"

[Tasks]
Name: "autostart"; Description: "Uruchamiaj TempView po zalogowaniu"
Name: "desktopicon"; Description: "Utwórz skrót na pulpicie"; Flags: unchecked

[Files]
; Self-contained application: .NET runtime, LibreHardwareMonitor, sensor service, licenses.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Corresponding source for the bundled GPL driver installer.
Source: "{#PrereqDir}\PawnIO-2.2.0-source.zip"; DestDir: "{app}\licenses"; Flags: ignoreversion
Source: "{#PrereqDir}\PawnPP-e64e4c3-source.zip"; DestDir: "{app}\licenses"; Flags: ignoreversion
; Unmodified, signed PawnIO installer. Extracted only when needed, into the admin-only installation directory
; (not a user-writable temp folder), re-hashed before it runs and deleted afterwards.
Source: "{#PrereqDir}\PawnIO_setup.exe"; DestDir: "{app}\prerequisites"; Flags: ignoreversion deleteafterinstall; Check: PawnIOPayloadNeeded

[Icons]
Name: "{autoprograms}\TempView"; Filename: "{app}\TempView.exe"
Name: "{autodesktop}\TempView"; Filename: "{app}\TempView.exe"; Tasks: desktopicon

[Registry]
; Start at sign-in for every user of this PC. Each user can still turn it off in Task Manager > Startup apps.
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "TempView"; ValueData: """{app}\TempView.exe"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
; Never start the GUI elevated: it runs as the user who started setup.
Filename: "{app}\TempView.exe"; Description: "Uruchom TempView"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallDelete]
Type: filesandordirs; Name: "{app}\prerequisites"

[Code]
#include "Decisions.iss"
#include "Running.iss"

const
  ServiceName = 'TempViewSensors';
  UninstallKey = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO';
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  ExitCpuSetupFailed = 101;

var
  CpuPage: TInputOptionWizardPage;
  CpuVendor, InstalledPawnIO: String;
  PawnIOAction: Integer;
  RestartRequired, CpuFailed: Boolean;
  SensorSummary: String;

function ReadInstalledPawnIO(): String;
begin
  if not RegQueryStringValue(HKLM64, UninstallKey, 'DisplayVersion', Result) then
    Result := '';
end;

function CpuSensorsWanted(): Boolean;
begin
  if (PawnIOAction = PawnIONotApplicable) or (PawnIOAction = PawnIOBlocked) then
    Result := False
  else if WizardSilent then
    Result := ExpandConstant('{param:CPUSENSORS|1}') <> '0'
  else
    Result := CpuPage.Values[0];
end;

function PawnIOPayloadNeeded(): Boolean;
begin
  Result := CpuSensorsWanted and ((PawnIOAction = PawnIOInstall) or (PawnIOAction = PawnIOUpgrade));
end;

function InitializeSetup(): Boolean;
begin
  if not RegQueryStringValue(HKLM, 'HARDWARE\DESCRIPTION\System\CentralProcessor\0', 'VendorIdentifier', CpuVendor) then
    CpuVendor := '';
  InstalledPawnIO := ReadInstalledPawnIO;
  PawnIOAction := DecidePawnIO(CpuVendor, InstalledPawnIO, '{#PawnIOVersion}');
  Log(Format('CPU vendor: %s; PawnIO installed: "%s"; bundled: {#PawnIOVersion}; decision: %d', [CpuVendor, InstalledPawnIO, PawnIOAction]));
  Result := True;
end;

procedure InitializeWizard();
var
  Intro, Choice: String;
begin
  Intro :=
    'Windows nie udostępnia programom temperatury procesora. TempView odczytuje ją przez PawnIO — otwartoźródłowy ' +
    'sterownik sprzętowy (licencja GPL) z podpisem Microsoft, z którego korzysta też LibreHardwareMonitor. Sterownik ' +
    'dopuszcza tylko konto systemowe i administratorów, dlatego TempView instaluje razem z nim małą usługę, która ' +
    'wyłącznie odczytuje czujniki procesora. Okno TempView działa bez uprawnień administratora.' + #13#10#13#10 +
    'Bez tej opcji TempView pokaże temperaturę karty graficznej, a dla procesora „N/A”. Sterownik zostaje w systemie ' +
    'po odinstalowaniu TempView, bo mogą z niego korzystać inne programy.';
  case PawnIOAction of
    PawnIOInstall: Choice := 'Zainstaluj sterownik PawnIO {#PawnIOVersion} i usługę odczytu CPU (zalecane)';
    PawnIOUpgrade: Choice := 'Zaktualizuj sterownik PawnIO ' + InstalledPawnIO + ' do {#PawnIOVersion} i włącz odczyt CPU (zalecane)';
    PawnIOReuse: Choice := 'Włącz odczyt CPU (użyje zainstalowanego sterownika PawnIO ' + InstalledPawnIO + ')';
    PawnIOBlocked: Choice := 'Odczyt CPU niedostępny: zainstalowany sterownik PawnIO (' + InstalledPawnIO + ') trzeba najpierw odinstalować w Ustawieniach > Aplikacje';
  else
    Choice := 'Odczyt CPU niedostępny: ten procesor nie jest obsługiwany (wymagany Intel lub AMD)';
  end;
  CpuPage := CreateInputOptionPage(wpSelectTasks, 'Temperatura procesora', 'Odczyt CPU wymaga sterownika sprzętowego.', Intro, False, False);
  CpuPage.Add(Choice);
  CpuPage.Values[0] := (PawnIOAction <> PawnIONotApplicable) and (PawnIOAction <> PawnIOBlocked);
  CpuPage.CheckListBox.ItemEnabled[0] := CpuPage.Values[0];
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
var
  Cpu: String;
begin
  if not CpuSensorsWanted then Cpu := 'nie (tylko temperatura GPU)'
  else if PawnIOAction = PawnIOReuse then Cpu := 'tak, zainstalowany PawnIO ' + InstalledPawnIO + ' + usługa TempView'
  else Cpu := 'tak, sterownik PawnIO {#PawnIOVersion} + usługa TempView (wymaga zgody administratora)';
  Result := MemoDirInfo + NewLine + NewLine + MemoTasksInfo + NewLine + NewLine + 'Odczyt temperatury CPU:' + NewLine + Space + Cpu;
end;

function Sc(const Params: String): Integer;
begin
  if not Exec(ExpandConstant('{sys}\sc.exe'), Params, '', SW_HIDE, ewWaitUntilTerminated, Result) then
    Result := -1;
  Log('sc ' + Params + ' -> ' + IntToStr(Result));
end;

function ServiceStopped(): Boolean;
var
  Output: TExecOutput;
  Code, I: Integer;
begin
  Result := True;
  if ExecAndCaptureOutput(ExpandConstant('{sys}\sc.exe'), 'query ' + ServiceName, '', SW_HIDE, ewWaitUntilTerminated, Code, Output) then
    if Code = 0 then
      for I := 0 to GetArrayLength(Output.StdOut) - 1 do
        if (Pos('STATE', Output.StdOut[I]) > 0) and (Pos('STOPPED', Output.StdOut[I]) = 0) then
          Result := False;
end;

procedure StopSensorService();
var
  I: Integer;
begin
  if Sc('query ' + ServiceName) <> 0 then Exit;
  Sc('stop ' + ServiceName);
  for I := 1 to 30 do
  begin
    if ServiceStopped then Exit;
    Sleep(500);
  end;
  Log('Sensor service did not stop within 15 s.');
end;

procedure RemoveSensorService();
begin
  StopSensorService;
  if Sc('query ' + ServiceName) = 0 then Sc('delete ' + ServiceName);
end;

function InstallSensorService(): Boolean;
var
  Exe, Params: String;
  Code: Integer;
begin
  // Quoted ImagePath: no unquoted-path ambiguity with spaces or non-ASCII characters.
  Exe := ExpandConstant('{app}\TempView.Sensors.exe');
  Params := 'binPath= "\"' + Exe + '\"" start= auto obj= LocalSystem DisplayName= "TempView – odczyt czujników CPU"';
  Code := Sc('create ' + ServiceName + ' ' + Params);
  if Code = 1073 then Code := Sc('config ' + ServiceName + ' ' + Params);
  Result := Code = 0;
  if not Result then Exit;
  Sc('description ' + ServiceName + ' "Odczytuje temperatury procesora dla TempView przez sterownik PawnIO i udostępnia je tylko do odczytu. Nie przyjmuje poleceń."');
  // Least privilege: drop every token privilege except the one every process needs.
  Result := Sc('privs ' + ServiceName + ' SeChangeNotifyPrivilege') = 0;
  Sc('failure ' + ServiceName + ' reset= 86400 actions= restart/60000/restart/60000//0');
  Code := Sc('start ' + ServiceName);
  if (Code <> 0) and (Code <> 1056) then
    Log('Service start returned ' + IntToStr(Code) + ' (it starts automatically after restart).');
end;

function InstallPawnIO(var Problem: String): Boolean;
var
  Setup: String;
  Code: Integer;
begin
  Result := False;
  Setup := ExpandConstant('{app}\prerequisites\PawnIO_setup.exe');
  // Defense in depth: the payload was verified at build time; check it again right before it runs elevated.
  if CompareText(GetSHA256OfFile(Setup), '{#PawnIOSha256}') <> 0 then
  begin
    Problem := 'plik instalatora sterownika jest uszkodzony lub podmieniony';
    Exit;
  end;
  // Unmodified official installer, signed production edition (never -unrestricted).
  if not Exec(Setup, '-install -silent', ExpandConstant('{app}\prerequisites'), SW_HIDE, ewWaitUntilTerminated, Code) then
  begin
    Problem := 'nie można uruchomić instalatora sterownika (' + SysErrorMessage(Code) + ')';
    Exit;
  end;
  Log('PawnIO setup exit code: ' + IntToStr(Code));
  Problem := PawnIOResultText(Code);
  if not PawnIOSucceeded(Code) then Exit;
  if PawnIORestartNeeded(Code) then RestartRequired := True;
  InstalledPawnIO := ReadInstalledPawnIO;
  if InstalledPawnIO = '' then
  begin
    Problem := 'instalator sterownika zakończył się bez rejestracji PawnIO';
    Exit;
  end;
  Result := True;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not CloseTempView then
  begin
    Result := 'TempView nadal działa. Zamknij go (ikona w obszarze powiadomień > Zakończ TempView) i spróbuj ponownie.';
    Exit;
  end;
  // The service binary is replaced during upgrades.
  StopSensorService;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Problem: String;
begin
  if CurStep <> ssPostInstall then Exit;

  if not WizardIsTaskSelected('autostart') then
    RegDeleteValue(HKLM64, RunKey, 'TempView');

  if not CpuSensorsWanted then
  begin
    RemoveSensorService;
    case PawnIOAction of
      PawnIONotApplicable: SensorSummary := 'Ten procesor nie jest obsługiwany przez odczyt temperatury CPU. TempView pokaże temperaturę karty graficznej.';
      PawnIOBlocked: SensorSummary := 'Odczyt CPU nie został włączony: zainstalowany sterownik PawnIO ' + InstalledPawnIO + ' wymaga ręcznego odinstalowania. Potem uruchom ten instalator ponownie.';
    else
      SensorSummary := 'Odczyt temperatury CPU nie został włączony. TempView pokaże temperaturę karty graficznej. Odczyt CPU włączysz, uruchamiając ten instalator ponownie.';
    end;
    Exit;
  end;

  if PawnIOPayloadNeeded and not InstallPawnIO(Problem) then
  begin
    CpuFailed := True;
    RemoveSensorService;
    SensorSummary := 'Nie udało się zainstalować sterownika PawnIO: ' + Problem + '. TempView działa, ale temperatura CPU będzie niedostępna („N/A”). Szczegóły w dzienniku instalacji.';
    Log('CPU sensor setup failed: ' + Problem);
    Exit;
  end;

  if not InstallSensorService then
  begin
    CpuFailed := True;
    SensorSummary := 'Nie udało się zarejestrować usługi odczytu CPU. TempView działa, ale temperatura CPU będzie niedostępna. Szczegóły w dzienniku instalacji.';
    Exit;
  end;

  if RestartRequired then
    SensorSummary := 'Sterownik PawnIO zainstalowano. Odczyt temperatury CPU zacznie działać po ponownym uruchomieniu komputera.'
  else
    SensorSummary := 'Odczyt temperatury CPU i GPU jest skonfigurowany. Dostępność czujników zależy od modelu procesora i karty graficznej.';
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpFinished) and (SensorSummary <> '') then
    WizardForm.FinishedLabel.Caption := 'TempView został zainstalowany.' + #13#10#13#10 + SensorSummary;
end;

#ifdef Preview
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := CurPageID <> wpReady;
  if not Result then MsgBox('Podgląd kreatora: instalacja jest wyłączona w tej wersji.', mbInformation, MB_OK);
end;
#endif

function NeedRestart(): Boolean;
begin
  Result := RestartRequired;
end;

// 0: success. 101: TempView installed, but the selected CPU sensor component failed (limited functionality).
function GetCustomSetupExitCode(): Integer;
begin
  if CpuFailed then Result := ExitCpuSetupFailed else Result := 0;
end;

function InitializeUninstall(): Boolean;
begin
  Result := CloseTempView;
  if not Result then
    MsgBox('TempView nadal działa. Zamknij go (ikona w obszarze powiadomień > Zakończ TempView) i uruchom dezinstalację ponownie.', mbError, MB_OK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // PawnIO is shared and stays installed; settings in %LOCALAPPDATA%\TempView are kept.
  if CurUninstallStep = usUninstall then
    RemoveSensorService;
end;
