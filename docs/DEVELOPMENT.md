# Rozwijanie TempView

Instrukcje dla osób pracujących nad kodem. Instrukcja użytkownika znajduje się w README.md; stan implementacji i wyniki weryfikacji w STATUS.md.

## Przygotowanie środowiska

Wymagane: Windows 11 x64, Git i .NET 10 SDK. Projekt używa C#, WPF oraz LibreHardwareMonitor. Skrypt setup.ps1 instaluje SDK w profilu użytkownika, jeśli nie jest dostępne.

```powershell
git clone https://github.com/AjGGoR/TempView.git
cd TempView
./scripts/setup.ps1
./scripts/test.ps1
./scripts/build.ps1
```

Zamknij działający TempView przed budowaniem. Wyniki publikacji trafiają do `artifacts/TempView-win-x64`, a paczka do `artifacts/TempView-win-x64.zip`. Publikacja zawiera runtime .NET, biblioteki i usługę czujników; sterownik PawnIO i rejestrację usługi zapewnia instalator (sekcja „Instalator”). Nie kopiuj wyłącznie EXE.

Skrypty korzystają z SDK w PATH albo z instalacji wykonanej przez setup.ps1. Jeżeli polityka systemu blokuje skrypty, wykonaj równoważne polecenia SDK zgodnie z zasadami swojego środowiska:

```powershell
dotnet test tests/TempView.Tests/TempView.Tests.csproj -c Release
dotnet publish src/TempView/TempView.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o artifacts/TempView-win-x64
```

## Praca na dwóch komputerach

Przestrzegaj AGENTS.md. Oba komputery powinny kontynuować tę samą gałąź etapu. Przed zmianami sprawdź lokalną pracę i aktualizacje:

```powershell
git status --short --branch
git fetch origin
git pull --ff-only
```

Nowy etap rozpocznij przez `git switch -c feature/nazwa-etapu`. Na drugim komputerze wykonaj `git fetch origin` i `git switch --track origin/feature/nazwa-etapu`. Istniejącą lokalną gałąź wybierz przez `git switch feature/nazwa-etapu`.

Po sprawdzeniu zmian utwórz opisowy commit i wykonaj push. Przy rozbieżnościach połącz pracę obu komputerów zwykłym merge; nie wykonuj force push. Git przenosi zatwierdzone zmiany, a nie niezapisane pliki ani ustawienia użytkownika. Nie zapisuj tokenów ani haseł w repozytorium.

## Konfiguracja i weryfikacja

Ustawienia użytkownika są zapisywane lokalnie i wykluczone z Git. Przykład formatu znajduje się w example-config.json. Zapis konfiguracji z nowszym numerem formatu jest blokowany; uszkodzony plik jest zabezpieczany kopią, jeśli to możliwe.

GitHub Actions uruchamia testy, buduje paczkę Windows x64, sprawdza jej uruchomienie i udostępnia artefakt. Testy automatyczne nie potwierdzają zgodności sprzętowej ani poprawności wyświetlania na każdej wersji paska Windows. Aktualizuj STATUS.md zgodnie z rzeczywistymi wynikami.

Kod TempView jest objęty MIT. Informacje o zależnościach i ich źródłach znajdują się w katalogu licenses.

## Interfejs

- WPF z wbudowanym motywem Fluent z .NET 10 (`Application.ThemeMode`), bez dodatkowych bibliotek UI. API jest w .NET 10 oznaczone jako eksperymentalne (WPF0001); ostrzeżenie wyciszono wyłącznie w `SystemTheme.Apply`.
- Wspólne tokeny i style (odstępy, promienie, karty, nawigacja, przełącznik, InfoBar) są w `src/TempView/Themes/TempView.xaml`. Kolory pochodzą wyłącznie z zasobów Fluent, dlatego jasny, ciemny i wysoki kontrast działają bez osobnych palet.
- `WidgetView` jest wspólny dla nakładki na pasku i podglądu w ustawieniach. Kolory panelu wylicza testowalny `WidgetTheme` w TempView.Core.
- Ikony panelu, zasobnika i aplikacji są własnymi kształtami wektorowymi (`Icons`). Plik `Assets/TempView.ico` odtworzysz poleceniem `TempView.exe --export-icon src/TempView/Assets/TempView.ico`.
- Testy ręczne bez naruszania własnej konfiguracji: `TempView.exe --settings-file <plik> --diagnostics <plik> --settings [look|displays|sensors|colors]`.
- Format ustawień ma wersję 2. Plik w wersji 1 jest migrowany przy odczycie (`SettingsMigration`) i zapisywany jako wersja 2 dopiero po kliknięciu Zapisz.

## Instalator

```powershell
./scripts/build-installer.ps1        # build.ps1 + zweryfikowane zależności + Inno Setup
./scripts/test-installer.ps1         # testy bez instalacji
```

Wynik: `artifacts/installer/TempView-Setup-<wersja>-x64[-unsigned].exe` oraz pliki `.sha256` i `.inventory.json`. Inwentarz zawiera wersje i licencje składników, runtime .NET, PawnIO, pakiety NuGet, commit i status podpisu.

- Toolchain: Inno Setup 6.7.4, instalowany przez `scripts/setup-installer-tools.ps1` w profilu użytkownika (tryb przenośny, bez administratora). Skrypt weryfikuje SHA-256 i wydawcę.
- Zależności i ich przypięte wersje, sumy SHA-256 i wydawców opisuje `scripts/prerequisites.psd1`. Pliki są pobierane wyłącznie podczas budowania, z kanonicznych źródeł, i weryfikowane przed dołączeniem. Przy aktualizacji zmień wszystkie pola razem i ponownie sprawdź sumę z niezależnym źródłem (skrót wydania GitHub) oraz podpis.
- Skrypty instalatora: `installer/TempView.iss` (kreator, usługa, PawnIO), `installer/Decisions.iss` (logika decyzji, testowana przez `installer/tests/DecisionTests.iss`), `installer/Running.iss` (zamykanie działającego TempView, testowane przez `installer/tests/CloseTests.iss`).
- Podgląd kreatora bez instalacji: skompiluj `installer/TempView.iss` z `/DPreview` (bez UAC, przycisk Instaluj jest zablokowany).
- Grafika kreatora `installer/WizardSmall-*.png` jest renderowana z `src/TempView/Assets/TempView.ico`.
- Testy w Piaskownicy/VM: docs/INSTALLER-TESTS.md.

### Podpisywanie

```powershell
./scripts/build-installer.ps1 -SignThumbprint <odcisk SHA-1 certyfikatu> [-TimestampUrl <adres RFC 3161>]
```

- Certyfikat podpisu kodu musi być w magazynie Windows (CurrentUser\My lub LocalMachine\My), np. z tokenu sprzętowego albo usługi podpisywania w chmurze.
- Skrypt podpisuje `TempView*.exe` i `TempView*.dll`, deinstalator i instalator przez `signtool` z Windows SDK, ze znacznikiem czasu.
- Bez certyfikatu powstaje plik z sufiksem `-unsigned`, a inwentarz ma `"signed": false`.
- Nigdy nie zapisuj certyfikatów, kluczy, haseł ani tokenów w repozytorium.
- Podpis nie gwarantuje braku ostrzeżeń SmartScreen; reputacja buduje się z czasem.

### Utrzymanie

Każde wydanie TempView dołącza runtime .NET znany SDK w chwili budowania (obecnie 10.0.14). Po każdej poprawce bezpieczeństwa .NET 10 zaktualizuj SDK (`./scripts/setup.ps1`) i wydaj nowy instalator. Wersje PawnIO, LibreHardwareMonitor i Inno Setup sprawdzaj przy każdym wydaniu.

## Architektura czujników

- `TempView.Core` — logika bez sprzętu: ustawienia, protokół usługi (`BrokerProtocol`), wybór czujnika, diagnoza braku CPU.
- `TempView.Hardware` — LibreHardwareMonitor (`HardwareReader`) oraz potok usługi (`BrokerServer`, `BrokerClient`).
- `TempView.Sensors` — usługa Windows `TempViewSensors` (LocalSystem). Czyta wyłącznie czujniki CPU, bo urządzenie PawnIO dopuszcza tylko SYSTEM i Administratorów. Do testów bez instalacji: `TempView.Sensors.exe --console` (bez uprawnień sprzętowych CPU pozostaje `null`).
- `TempView` (GUI) czyta GPU we własnym procesie; CPU z usługi, jeśli jest zarejestrowana, w przeciwnym razie lokalnie (działa tylko z uprawnieniami administratora).
- build.ps1 publikuje GUI i usługę do jednego katalogu (wspólny runtime).

## Uwagi o środowisku

- Skrypty `.ps1` są zapisane jako UTF-8 z BOM, ponieważ Windows PowerShell 5.1 odczytuje pliki bez BOM jako ANSI i psuje polskie napisy.
- `nuget.config` w repozytorium wskazuje nuget.org, więc kompilacja nie zależy od globalnej konfiguracji NuGet komputera.
- Kompilacja Debug wymaga zainstalowanego runtime .NET. Przy SDK z setup.ps1 ustaw `DOTNET_ROOT=%LOCALAPPDATA%\TempViewTools\dotnet` przed uruchomieniem `bin/Debug/.../TempView.exe`; inaczej Windows zaproponuje pobranie runtime. Paczka z build.ps1 jest self-contained.
