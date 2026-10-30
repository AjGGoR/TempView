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

Zamknij działający TempView przed budowaniem. Wyniki publikacji trafiają do `artifacts/TempView-win-x64`, a paczka do `artifacts/TempView-win-x64.zip`. Publikacja zawiera runtime .NET i biblioteki; sterownik PawnIO instaluje się osobno zgodnie z DRIVER.md. Nie kopiuj wyłącznie EXE.

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
