// Pure decision logic for the PawnIO prerequisite. No side effects, so tests/DecisionTests.iss can exercise it.

const
  PawnIONotApplicable = 0;  // CPU not supported by the CPU sensor path
  PawnIOInstall = 1;        // not installed: install the bundled version
  PawnIOUpgrade = 2;        // older supported version: upgrade in place
  PawnIOReuse = 3;          // same or newer: keep it, never downgrade
  PawnIOBlocked = 4;        // unknown or too old to upgrade automatically
  // Oldest version the 2.2.0 setup upgrades in place (PawnIO.Setup 2.2.0 release notes).
  PawnIOMinUpgradable = '2.1.0';

function CpuVendorSupported(const Vendor: String): Boolean;
begin
  Result := (Vendor = 'GenuineIntel') or (Vendor = 'AuthenticAMD');
end;

// Returns <0, 0, >0 like a comparer, or -2 when either version cannot be parsed.
function CompareVersions(const A, B: String; var Parsed: Boolean): Integer;
var
  VA, VB: Int64;
begin
  Result := 0;
  Parsed := StrToVersion(A, VA) and StrToVersion(B, VB);
  if Parsed then
    Result := ComparePackedVersion(VA, VB);
end;

function DecidePawnIO(const Vendor, Installed, Bundled: String): Integer;
var
  Parsed: Boolean;
  Cmp: Integer;
begin
  if not CpuVendorSupported(Vendor) then
    Result := PawnIONotApplicable
  else if Installed = '' then
    Result := PawnIOInstall
  else
  begin
    Cmp := CompareVersions(Installed, Bundled, Parsed);
    if not Parsed then
      Result := PawnIOBlocked
    else if Cmp >= 0 then
      Result := PawnIOReuse
    else
    begin
      Cmp := CompareVersions(Installed, PawnIOMinUpgradable, Parsed);
      if Parsed and (Cmp >= 0) then
        Result := PawnIOUpgrade
      else
        Result := PawnIOBlocked;
    end;
  end;
end;

// PawnIO 2.2.0 returns Windows (DOS) error codes in CLI mode.
function PawnIOResultText(Code: Integer): String;
begin
  case Code of
    0: Result := '';
    3010: Result := '';
    1223: Result := 'instalację sterownika anulowano';
    5: Result := 'brak uprawnień do instalacji sterownika';
    577, 1260, 1275: Result := 'Windows zablokował sterownik zgodnie z zasadami bezpieczeństwa; zabezpieczenia pozostają włączone';
  else
    Result := 'instalator sterownika zakończył się kodem ' + IntToStr(Code);
  end;
end;

function PawnIOSucceeded(Code: Integer): Boolean;
begin
  Result := (Code = 0) or (Code = 3010);
end;

function PawnIORestartNeeded(Code: Integer): Boolean;
begin
  Result := Code = 3010;
end;
