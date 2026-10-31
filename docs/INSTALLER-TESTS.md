# Testy instalatora w środowisku jednorazowym

Instalatora, sterownika PawnIO, aktualizacji i dezinstalacji nie testuj na komputerze używanym na co dzień. Używaj Piaskownicy systemu Windows albo maszyny wirtualnej, którą można odrzucić. Środowisko wirtualne sprawdza zachowanie instalatora, ale nie dowodzi zgodności z fizycznymi czujnikami.

## Testy bez instalacji (każdy komputer, CI)

```powershell
./scripts/test.ps1
./scripts/build-installer.ps1
./scripts/test-installer.ps1
```

`test-installer.ps1` sprawdza:
- odrzucenie podmienionego pliku PawnIO i obcego wydawcy;
- logikę decyzji PawnIO (instalacja, aktualizacja, ponowne użycie, brak obniżania wersji, blokada, kody wyjścia);
- zamknięcie działającego TempView przez instalator;
- sumę SHA-256 instalatora.

Niczego nie instaluje.

## Piaskownica systemu Windows

Włączenie (jednorazowo, przez użytkownika):
1. Naciśnij Win i wpisz „Włącz lub wyłącz funkcje systemu Windows”.
2. Zaznacz **Piaskownica systemu Windows**, potwierdź UAC i uruchom komputer ponownie.

Wymaga Windows 11 Pro/Enterprise/Education i wirtualizacji włączonej w firmware.

Uruchomienie scenariuszy:

```powershell
./scripts/start-sandbox-test.ps1
```

Skrypt tworzy konfigurację z **wyłączoną siecią** (dowód instalacji offline), mapuje katalog instalatora tylko do odczytu i uruchamia `installer/tests/sandbox/Run-Scenarios.ps1`. Wyniki: `artifacts/sandbox/results/report.txt` i dzienniki instalatora.

Scenariusze automatyczne:
- czysta instalacja z odczytem CPU (PawnIO i usługa);
- wpis w Zainstalowanych aplikacjach i autostart;
- uprawnienia katalogu;
- aktualizacja przy działającym TempView z zachowaniem ustawień;
- dezinstalacja: usunięcie usługi, plików i autostartu przy zachowaniu PawnIO i ustawień;
- instalacja bez CPU do ścieżki ze spacjami i polskimi znakami.

Ograniczenia: Piaskownica może nie wczytać sterownika jądra (zależnie od wersji Windows i zasad). Wynik — także porażka z kodem 101 — jest zapisywany w raporcie i nie oznacza zgodności sprzętowej. Piaskownica nie pozwala sprawdzić restartu.

## Pełna maszyna wirtualna (scenariusze sterownika i restartu)

Hyper-V lub inny hiperwizor, czysty Windows 11 x64 bez .NET i PawnIO, migawka przed testami.

1. Zaloguj się jako **zwykły użytkownik** (bez administratora). Uruchom instalator, sprawdź okno UAC z prośbą o dane administratora. Raz kliknij **Nie**: instalator kończy się bez zmian (kod 1 lub 2, brak wpisu w Zainstalowanych aplikacjach). Za drugim razem zatwierdź.
2. Przejdź kreator: strony zadań i „Temperatura procesora”, podsumowanie, ekran końcowy z opisem stanu odczytu CPU. TempView po zakończeniu działa jako zwykły użytkownik (Menedżer zadań > Szczegóły > kolumna „Podniesione”: Nie).
3. Odłącz sieć przed instalacją (dowód offline).
4. Restart: jeżeli instalator PawnIO zwróci 3010, kreator proponuje restart; po restarcie strona Czujniki pokazuje usługę „Działa”.
5. Istniejący PawnIO: zainstaluj wcześniej PawnIO 2.1.0 (aktualizacja do 2.2.0) oraz osobno 2.2.0 (ponowne użycie bez reinstalacji). Sprawdź w dzienniku instalatora wiersz `decision:`.
6. Blokada sterownika: w VM z zasadami App Control / listą blokowanych sterowników instalacja PawnIO kończy się błędem; TempView instaluje się z kodem 101, ekran końcowy podaje przyczynę, GPU działa.
7. Uruchom `installer/tests/sandbox/Run-Scenarios.ps1` jako administrator (z `C:\TempViewTest\installer` zawierającym instalator), aby powtórzyć scenariusze automatyczne.
8. Przywróć migawkę po testach.

Na fizycznym sprzęcie (Intel, AMD Zen, APU AMD, karty NVIDIA/AMD/Intel) potwierdź rzeczywiste odczyty. Zapisz wyniki w docs/STATUS.md.

## Kody wyjścia instalatora

| Kod | Znaczenie |
|---|---|
| 0 | zainstalowano; wybrane składniki działają (odczyt CPU skonfigurowany albo świadomie pominięty) |
| 101 | TempView zainstalowany, ale wybrany odczyt CPU nie powiódł się (sterownik lub usługa); GPU działa |
| 1–8 | standardowe kody Inno Setup, np. 2 = anulowano przed instalacją, 5 = anulowano w trakcie, 7 = przygotowanie przerwane (np. TempView nie zamknął się) |

Dziennik: `/LOG="ścieżka"` albo domyślnie `%TEMP%\Setup Log *.txt`. Instalacja cicha: `/VERYSILENT /SUPPRESSMSGBOXES`. Bez odczytu CPU: `/CPUSENSORS=0`. Zadania: `/TASKS="autostart,desktopicon"`.
