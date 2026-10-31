// Harness for installer\Running.iss: asks a running TempView to exit, writes the outcome to /OUT=<file>,
// installs nothing (InitializeSetup returns False). Used by scripts/test-installer.ps1.

[Setup]
AppName=TempView close test
AppVersion=1
CreateAppDir=no
Uninstallable=no
PrivilegesRequired=lowest
OutputDir=output
OutputBaseFilename=CloseTests

[Code]
#include "..\Running.iss"

function InitializeSetup(): Boolean;
var
  Lines: TArrayOfString;
begin
  SetArrayLength(Lines, 2);
  if CheckForMutexes(AppMutexName) then Lines[0] := 'RUNNING_BEFORE 1' else Lines[0] := 'RUNNING_BEFORE 0';
  if CloseTempView then Lines[1] := 'CLOSED 1' else Lines[1] := 'CLOSED 0';
  SaveStringsToUTF8File(ExpandConstant('{param:OUT}'), Lines, False);
  Result := False;
end;