// Test harness for installer\Decisions.iss. Compiles to a setup that installs nothing:
// InitializeSetup runs the cases, writes the results to /OUT=<file> and returns False.
// Run: ISCC DecisionTests.iss, then  DecisionTests.exe /VERYSILENT /SUPPRESSMSGBOXES /OUT=<file>

[Setup]
AppName=TempView decision tests
AppVersion=1
CreateAppDir=no
Uninstallable=no
PrivilegesRequired=lowest
OutputDir=output
OutputBaseFilename=DecisionTests

[Code]
#include "..\Decisions.iss"

var
  Results: TArrayOfString;
  Failures: Integer;

procedure Check(const Name: String; Actual, Expected: Integer);
var
  N: Integer;
begin
  N := GetArrayLength(Results);
  SetArrayLength(Results, N + 1);
  if Actual = Expected then
    Results[N] := 'PASS ' + Name
  else
  begin
    Results[N] := 'FAIL ' + Name + ': got ' + IntToStr(Actual) + ', expected ' + IntToStr(Expected);
    Failures := Failures + 1;
  end;
end;

procedure CheckText(const Name: String; Condition: Boolean);
begin
  if Condition then Check(Name, 1, 1) else Check(Name, 0, 1);
end;

function InitializeSetup(): Boolean;
begin
  Check('unsupported vendor', DecidePawnIO('CentaurHauls', '', '2.2.0'), PawnIONotApplicable);
  Check('empty vendor', DecidePawnIO('', '2.2.0', '2.2.0'), PawnIONotApplicable);
  Check('fresh AMD', DecidePawnIO('AuthenticAMD', '', '2.2.0'), PawnIOInstall);
  Check('fresh Intel', DecidePawnIO('GenuineIntel', '', '2.2.0'), PawnIOInstall);
  Check('same version reused', DecidePawnIO('AuthenticAMD', '2.2.0', '2.2.0'), PawnIOReuse);
  Check('same version 4-part reused', DecidePawnIO('AuthenticAMD', '2.2.0.0', '2.2.0'), PawnIOReuse);
  Check('newer never downgraded', DecidePawnIO('GenuineIntel', '2.3.1', '2.2.0'), PawnIOReuse);
  Check('2.1.0 upgraded in place', DecidePawnIO('AuthenticAMD', '2.1.0', '2.2.0'), PawnIOUpgrade);
  Check('2.1.5 upgraded in place', DecidePawnIO('AuthenticAMD', '2.1.5', '2.2.0'), PawnIOUpgrade);
  Check('2.0.1 too old for in-place upgrade', DecidePawnIO('AuthenticAMD', '2.0.1', '2.2.0'), PawnIOBlocked);
  Check('1.0 too old', DecidePawnIO('GenuineIntel', '1.0', '2.2.0'), PawnIOBlocked);
  Check('garbage version blocked', DecidePawnIO('GenuineIntel', 'beta', '2.2.0'), PawnIOBlocked);
  CheckText('exit 0 success', PawnIOSucceeded(0) and not PawnIORestartNeeded(0) and (PawnIOResultText(0) = ''));
  CheckText('exit 3010 success with restart', PawnIOSucceeded(3010) and PawnIORestartNeeded(3010));
  CheckText('exit 1275 blocked driver is failure', not PawnIOSucceeded(1275) and (Pos('zablokował', PawnIOResultText(1275)) > 0));
  CheckText('exit 1223 cancelled is failure', not PawnIOSucceeded(1223) and (Pos('anulowano', PawnIOResultText(1223)) > 0));
  CheckText('unknown exit code reported', not PawnIOSucceeded(87) and (Pos('87', PawnIOResultText(87)) > 0));
  SetArrayLength(Results, GetArrayLength(Results) + 1);
  Results[GetArrayLength(Results) - 1] := 'FAILURES ' + IntToStr(Failures);
  SaveStringsToUTF8File(ExpandConstant('{param:OUT}'), Results, False);
  Result := False;
end;
