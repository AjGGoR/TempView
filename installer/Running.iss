// Closing a running TempView (installed or portable) through its exit event. Shared with tests/CloseTests.iss.

const
  AppMutexName = 'Local\TempView.Desktop';
  ExitEventName = 'Local\TempView.Desktop.Exit';

function OpenEvent(Access: Cardinal; Inherit: Boolean; Name: String): THandle; external 'OpenEventW@kernel32.dll stdcall';
function SetEvent(Handle: THandle): Boolean; external 'SetEvent@kernel32.dll stdcall';
function CloseHandle(Handle: THandle): Boolean; external 'CloseHandle@kernel32.dll stdcall';

// Asks a running TempView to exit (EVENT_MODIFY_STATE on its exit event) and waits up to 10 s.
// Returns False if it is still running, e.g. an older portable build without the exit event.
function CloseTempView(): Boolean;
var
  Handle: THandle;
  I: Integer;
begin
  Result := not CheckForMutexes(AppMutexName);
  if Result then Exit;
  Handle := OpenEvent($0002, False, ExitEventName);
  if Handle <> 0 then
  begin
    SetEvent(Handle);
    CloseHandle(Handle);
  end;
  for I := 1 to 20 do
  begin
    Sleep(500);
    if not CheckForMutexes(AppMutexName) then
    begin
      Result := True;
      Exit;
    end;
  end;
  Log('TempView did not exit after the exit signal.');
end;
