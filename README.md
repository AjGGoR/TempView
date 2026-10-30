# TempView

Niewielki panel temperatur CPU i GPU dla Windows 11. C#/.NET 10, WPF i LibreHardwareMonitor; czcionka Segoe UI. Projekt udostępnia rzeczywiste pomiary dostępnych czujników. Brak odczytu jest prezentowany jako `N/A` lub `—`, nigdy jako temperatura demonstracyjna.

## Uruchomienie paczki

1. Pobierz artefakt `TempView-win-x64` z zakładki [Actions](https://github.com/AjGGoR/TempView/actions) zakończonego powodzeniem przebiegu. Rozpakuj pobrany ZIP i znajdującą się w nim paczkę aplikacji.
2. Zachowaj razem wszystkie pliki i uruchom `TempView.exe`. Paczka self-contained zawiera wymagane środowisko .NET; nie trzeba instalować runtime.
3. Ustawienia są dostępne z menu aplikacji przy ikonie w obszarze powiadomień. Windows może schować ikonę pod strzałką rozwijającą ten obszar.
4. Nie przenoś samego pliku EXE: biblioteki i pliki towarzyszące są częścią aplikacji.
5. Jeśli brak temperatury CPU, przeczytaj [wymaganie sterownika PawnIO](docs/DRIVER.md) (w paczce: `DRIVER.md`). Skrypt `drivers/download-driver.ps1` pobiera zweryfikowany instalator z internetu; instalację wykonuje użytkownik osobno. Sterownik nie jest potrzebny do uruchomienia interfejsu. Paczka nie instaluje automatycznie wszystkich zależności sprzętowych.

Lokalna kompilacja trafia do `artifacts/TempView-win-x64/TempView.exe`, a paczka do `artifacts/TempView-win-x64.zip`.

## Jak działa panel przy pasku

TempView tworzy osobne okno nakładane na wolny obszar systemowego paska. Nie jest to natywne osadzenie wewnątrz Explorer ani rezerwacja miejsca przez AppBar API. Nie wymaga modyfikowania Explorer, wyłączania zabezpieczeń ani instalowania zamiennika paska. Systemowy zegar pozostaje dostępny.

Windows 11 nie oferuje publicznego kontraktu dla dowolnego panelu umieszczanego wewnątrz nowego paska. Dlatego zgodność nakładki zależy od układu i wersji powłoki. Przy braku bezpiecznego miejsca aplikacja powinna ukryć panel, pozostawiając dostęp do ustawień przez ikonę powiadomień. Wybranie ekranu bez systemowego paska nie tworzy na nim zastępczego widgetu pulpitu. Nie deklarujemy zgodności ze wszystkimi przyszłymi wersjami Windows.

## Czujniki i uprawnienia

LibreHardwareMonitor obsługuje wiele urządzeń Intel, AMD i NVIDIA; dostępność konkretnej temperatury zależy od modelu, firmware, sterownika i zabezpieczeń systemu. Automatyczny dobór można zastąpić wyborem dostępnego czujnika. Pomiary są odświeżane poza wątkiem interfejsu, domyślnie co sekundę.

Aplikacja startuje z uprawnieniami zwykłego użytkownika. LibreHardwareMonitor 0.9.6 wymaga osobno zainstalowanego PawnIO dla dostępu do wielu czujników CPU; samo uruchomienie jako administrator nie zastąpi brakującego sterownika. [Instrukcja i weryfikacja pobrania](docs/DRIVER.md). Instalacja sterownika wymaga administratora, a niektóre odczyty mogą również wymagać świadomego uruchomienia aplikacji jako administrator. To podwyższa uprawnienia całego procesu tej wersji; nie ma jeszcze oddzielnej usługi czujników. **Nie wyłączaj ochrony systemu ani listy blokowanych sterowników** — jeśli odczyt jest blokowany, pozostaje niedostępny. Niedodatnie wyniki temperatury CPU są odrzucane. Podstawowe działanie nie wymaga płatnego oprogramowania.

## Budowanie na drugim komputerze

Wymagane: Windows 11 x64, Git i .NET 10 SDK. [Oficjalny instalator SDK](https://dotnet.microsoft.com/download/dotnet/10.0) jest alternatywą dla skryptu instalującego SDK w profilu użytkownika.

```powershell
git clone https://github.com/AjGGoR/TempView.git
cd TempView
git switch --track origin/feat/windows-taskbar-widget
./scripts/setup.ps1  # opcjonalne, jeśli SDK 10 jest już zainstalowane
./scripts/test.ps1
./scripts/build.ps1
./artifacts/TempView-win-x64/TempView.exe
```

Skrypty korzystają z `dotnet` w PATH albo z `%LOCALAPPDATA%/TempViewTools/dotnet/dotnet.exe`. Nie wymagają stałej ścieżki repozytorium. Jeżeli lokalna polityka PowerShell blokuje skrypty, zastosuj politykę organizacji lub wykonaj równoważne polecenia SDK ręcznie; projekt nie zmienia polityki wykonywania skryptów:

```powershell
dotnet test tests/TempView.Tests/TempView.Tests.csproj -c Release
dotnet publish src/TempView/TempView.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o artifacts/TempView-win-x64
```

Ustawienia są przechowywane w `%LOCALAPPDATA%/TempView/settings.json`. [Przykład konfiguracji](docs/example-config.json) odpowiada wartościom domyślnym; najwygodniej zmieniać je przez interfejs aplikacji. Plik z przyszłym numerem formatu nie jest nadpisywany. Przy uszkodzeniu konfiguracji aplikacja próbuje zachować kopię i uruchamia wartości domyślne.

## Kontynuowanie pracy przez GitHub

Używaj tej samej gałęzi etapu na obu komputerach. Przed zmianami:

```powershell
git status --short --branch
git fetch origin
git pull --ff-only
```

Pierwszy etap można rozpocząć przez `git switch -c feature/nazwa-etapu`. Na drugim komputerze: `git fetch origin`, następnie `git switch --track origin/feature/nazwa-etapu`. Jeśli gałąź istnieje lokalnie, wystarczy `git switch feature/nazwa-etapu` i `git pull --ff-only` po zachowaniu lokalnej pracy.

Po testach utwórz opisowy commit i wyślij go:

```powershell
git add <zmienione-pliki>
git commit -m "Describe the completed change"
git push -u origin feature/nazwa-etapu
```

Git nie synchronizuje niezapisanych plików w czasie rzeczywistym. W razie rozbieżności sprawdź `git log --oneline --graph --all`, połącz zmiany przez zwykły merge i rozwiąż konflikty z zachowaniem pracy obu komputerów. Nie wykonuj force push. Przy błędzie uwierzytelnienia zaloguj Git Credential Manager do GitHub podczas ponownego `git push`, ewentualnie skonfiguruj własny klucz SSH. Nigdy nie wpisuj tokenu w plik projektu ani w adres `origin`.

## Weryfikacja i licencje

Aktualny zakres sprawdzeń, ograniczenia i kolejny krok: [docs/STATUS.md](docs/STATUS.md). Instalator i automatyczne aktualizacje pozostają poza zakresem tego etapu.

Kod TempView: MIT. LibreHardwareMonitor: MPL 2.0; biblioteka nie jest modyfikowana przez TempView. Źródła zależności i informacje licencyjne: [docs/licenses](docs/licenses). Paczka zawiera te informacje. Integracja paska i monitoring wymagają testów na konkretnym sprzęcie; testy jednostkowe nie potwierdzają kompatybilności urządzenia.
