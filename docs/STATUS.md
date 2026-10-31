# Stan TempView — 2026-10-31

Przeprojektowany interfejs i instalator all-in-one są scalone do `main` (przewinięcie z `feature/installer`, która zawiera też `feature/fluent-redesign`). Obie gałęzie etapów pozostają na origin. Wydanie publiczne jest wstrzymane — zob. „Blokery publicznego wydania” i „Kontynuacja”.

## Przekazanie — 2026-10-31

- `main` = `1cf6042` (przewinięcie z `feature/installer`; zawiera przeprojektowanie i instalator). Gałęzie `feature/fluent-redesign` i `feature/installer` pozostają na origin.
- CI dla `1cf6042`: **w toku** w chwili zapisu. Testy, budowa instalatora, testy instalatora i smoke test zakończyły się powodzeniem; wysyłanie artefaktów nie było jeszcze zakończone. Wynik końcowy sprawdź w Actions. CI dla `feature/installer` `43681a1` i `47e740a`: sukces.
- Lokalnie przed scaleniem: `test.ps1` 56/56, `build-installer.ps1` bez ostrzeżeń, `test-installer.ps1` 13/13. Instalator (niepodpisany, 0.3.0) zbudowany tylko lokalnie i w CI; nie jest w repozytorium.
- Na komputerze deweloperskim nie instalowano TempView, usługi ani PawnIO. Piaskownica systemu Windows **nie jest włączona** — testy instalacyjne nie zostały wykonane.
- Następna sesja: 1) włączyć Piaskownicę (docs/INSTALLER-TESTS.md) albo przygotować VM i uruchomić `scripts/start-sandbox-test.ps1`; 2) testy UAC/restart/PawnIO w VM; 3) odczyt CPU na fizycznym sprzęcie; 4) certyfikat podpisu; 5) potwierdzenie licencji PawnIO.Setup.
## Etap instalatora — postęp

### Kamień 1: usługa odczytu CPU o najmniejszych uprawnieniach (zrobione)

Audyt dostępu do czujników (źródła: kod LibreHardwareMonitor v0.9.6, PawnIO 2.2.0 — INF i `src/driver.cpp`, sprawdzone 2026-10-31):

| Źródło | Co mierzy | Zależność | Uprawnienia |
|---|---|---|---|
| NVIDIA (NVAPI, sterownik karty) | temperatura GPU | sterownik NVIDIA | zwykły użytkownik — sprawdzone: RTX 5070, 32–50°C |
| AMD dGPU (ADL, sterownik karty) | temperatura GPU | sterownik AMD | bez PawnIO w kodzie LHM; na tym komputerze iGPU Radeon zgłasza czujnik bez PawnIO |
| Intel GPU (IGCL) | temperatura GPU | sterownik Intel | bez PawnIO w kodzie LHM; niesprawdzone sprzętowo |
| Intel CPU (MSR IA32_THERM_STATUS) | rdzenie/pakiet | PawnIO | ACL urządzenia PawnIO: tylko SYSTEM i Administratorzy |
| AMD Zen CPU (SMN, Tctl/Tdie) | Tctl/Tdie, CCD | PawnIO | jw. |
| AMD APU iGPU (SMU „GFX”) | temperatura iGPU | PawnIO | jw. |
| ACPI thermal zone (WMI/licznik) | strefa płyty, nie CPU | brak | odrzucone: na tym komputerze `\_TZ.TZ10` = 290 K (17°C) przy pracującym CPU — nie jest temperaturą procesora |

Wniosek: PawnIO jest potrzebny wyłącznie do temperatury CPU (i iGPU w APU AMD); GPU dedykowane działają bez niego. Ponieważ PawnIO wpuszcza tylko SYSTEM i Administratorów, odczyt CPU został przeniesiony do osobnej usługi `TempViewSensors` (`TempView.Sensors.exe`). Interfejs nadal działa bez uprawnień administratora.

Granica bezpieczeństwa usługi:
- tylko odczyt czujników CPU i publikacja migawki; brak poleceń, plików, parametrów i ścieżek od klienta;
- potok `\\.\pipe\TempView.Sensors.v1` jednokierunkowy (serwer → klient), ACL: SYSTEM pełny dostęp, zalogowani użytkownicy tylko odczyt, NETWORK zabroniony; komunikat ≤ 64 KiB, walidowany po stronie klienta (wersja, długości, znaki sterujące, zakres temperatur);
- klient łączy się z poziomem `Identification` (brak podszywania się pod użytkownika) i sprawdza, że serwer potoku to plik usługi zarejestrowany w `HKLM\SYSTEM\CurrentControlSet\Services\TempViewSensors` (klucz tylko dla administratorów);
- usługa ładuje natywne DLL wyłącznie z System32 i katalogu instalacji; dziennik w Dzienniku zdarzeń, nie w katalogach zapisywalnych przez użytkowników. Instalator (kamień 2) ograniczy token usługi do `SeChangeNotifyPrivilege`.

Zweryfikowane: 56 testów (16 nowych: protokół i złośliwe komunikaty, wybór czujnika, diagnoza przyczyn braku CPU, potok z weryfikacją pliku serwera i odrzuceniem podszytego serwera). `TempView.Sensors.exe --console` jako zwykły użytkownik: odczyt potoku działa, otwarcie z prawem zapisu odrzucone, czujnik CPU zgłoszony z `value: null` (brak PawnIO). GUI: GPU 50°C, CPU N/A z poprawnym powodem. Paczka self-contained zawiera obie aplikacje, runtime .NET 10.0.14. Usługa nie była rejestrowana na tym komputerze — rejestracja i odczyt CPU z PawnIO wymagają środowiska testowego (Windows Sandbox/VM).
### Kamień 2: instalator offline (zrobione, testy instalacyjne czekają na środowisko jednorazowe)

Inno Setup 6.7.4 (dojrzała linia 6.x, licencja zezwalająca na użycie komercyjne). Jeden plik `TempView-Setup-0.3.0-x64-unsigned.exe` (ok. 57 MB) zawiera:
- self-contained TempView i usługę `TempViewSensors` z runtime .NET 10.0.14;
- niezmieniony, podpisany `PawnIO_setup.exe` 2.2.0;
- licencje i odpowiadające źródła PawnIO/PawnPP.

Działa bez internetu; niczego nie pobiera podczas instalacji.

- **Kreator:** krótki i po polsku — lokalizacja (tylko przy pierwszej instalacji), zadania (autostart, skrót), strona „Temperatura procesora” z wyjaśnieniem sterownika, podsumowanie, ekran końcowy ze stanem odczytu CPU. TempView po instalacji startuje bez podniesionych uprawnień (`runasoriginaluser`).
- **PawnIO warunkowo** (logika w `installer/Decisions.iss`):
  - instalacja, gdy brak sterownika, a CPU to Intel/AMD;
  - ponowne użycie wersji ≥ 2.2.0, bez obniżania wersji;
  - aktualizacja wersji 2.1.x;
  - blokada starszych lub nierozpoznanych wersji z instrukcją;
  - użytkownik może zrezygnować.
- **Kody wyjścia:** 0, 3010 (restart) i 1223/1275/577/1260 dostają czytelny opis; nieudany sterownik daje kod wyjścia instalatora 101 (TempView zainstalowany, CPU ograniczone).
- **Bezpieczeństwo:**
  - Payload jest weryfikowany przy budowaniu (SHA-256 zgodny ze skrótem wydania GitHub + wydawca Authenticode). Przy instalacji sprawdzany jest ponownie SHA-256 w katalogu `Program Files` (tylko administratorzy), bez folderu tymczasowego użytkownika.
  - Usługa ma cytowaną ścieżkę i token ograniczony do `SeChangeNotifyPrivilege`.
  - Brak wyłączania zabezpieczeń, wyjątków antywirusa, wydania unrestricted i zadań harmonogramu.
- **Aktualizacja i odinstalowanie:**
  - Działający TempView (zainstalowany lub przenośny) jest zamykany sygnałem `Local\TempView.Desktop.Exit`; starsze wersje bez sygnału dostają komunikat.
  - Odinstalowanie usuwa pliki, skróty, autostart i usługę; zachowuje ustawienia i PawnIO.
- **Podpis:** ścieżka `-SignThumbprint` (signtool + znacznik czasu dla plików TempView, deinstalatora i instalatora). **Brak certyfikatu** — artefakt jest niepodpisany i oznaczony sufiksem `-unsigned`.
- **Inwentarz** `*.inventory.json`: wersje, licencje, SHA-256, commit, status podpisu, 29 pakietów NuGet. Formalnego SBOM (SPDX/CycloneDX) nie generujemy; brak istniejącego narzędzia w projekcie.
- **CI:** buduje instalator jako główny artefakt (`TempView-Setup-win-x64`), uruchamia testy instalatora; ZIP przenośny jest drugorzędny. Usunięto ręczny skrypt `download-driver.ps1`.

Zweryfikowane na tym komputerze (bez instalowania czegokolwiek):
- `scripts/test.ps1`: 56/56.
- `scripts/test-installer.ps1`: 13/13:
  - podmieniony bajt w PawnIO i obcy wydawca są odrzucane;
  - 17 przypadków logiki decyzji;
  - wykrycie i łagodne zamknięcie działającego TempView na zbudowanej paczce;
  - suma SHA-256.
- Podgląd kreatora (`/DPreview`, bez UAC): wszystkie strony obejrzane na zrzutach; przycisk Instaluj odmówił, katalog docelowy nie powstał.
- Paczka: smoke test 0, usługa w trybie konsoli publikuje potok.

Niezweryfikowane (wymaga Piaskownicy/VM, procedura w docs/INSTALLER-TESTS.md):
- rzeczywista instalacja i deinstalacja, praca offline, UAC jako zwykły użytkownik i jego anulowanie;
- instalacja PawnIO 2.2.0, aktualizacja z 2.1.0, ponowne użycie nowszej wersji, blokada sterownika, restart 3010;
- rejestracja i start usługi z ograniczonym tokenem, odczyt CPU przez usługę na sprzęcie Intel/AMD/APU;
- ścieżki z polskimi znakami;
- zgodność PawnIO 2.2.0 z modułami LHM 0.9.6 (notatki wydania deklarują zgodność wsteczną).

Piaskownica jest na tym komputerze wyłączona (Hyper-V włączony). Funkcji nie włączano bez zgody użytkownika.

Blokery publicznego wydania:
1. brak certyfikatu podpisu kodu;
2. brak wyraźnej licencji programu instalacyjnego PawnIO.Setup (sterownik jest GPL) — potrzebne potwierdzenie autora;
3. brak testów instalacyjnych w środowisku jednorazowym i na fizycznym sprzęcie.
### Kamień 3: przegląd ścieżek uprzywilejowanych i CI (zrobione)

Poprawki po przeglądzie:
- usługa zapisuje błędy we własnym źródle Dziennika zdarzeń `TempViewSensors` (wcześniej `"Application"`, które nie jest zarejestrowanym źródłem); deinstalator usuwa to źródło;
- payload PawnIO i katalog `prerequisites` są usuwane z `Program Files` tuż po użyciu.

Sprawdzone w przeglądzie bez zmian:
- cytowana ścieżka usługi (`sc create binPath= "\"…\""`);
- brak danych wejściowych od klientów potoku;
- uruchomienie GUI bez podniesienia;
- uruchamianie programów wyłącznie z pełnych ścieżek `{sys}`/`{app}`;
- brak plików wykonywalnych w katalogach zapisywalnych przez użytkownika.

CI (GitHub Actions, czysty runner `windows-latest`) dla 47e740a: testy, budowa instalatora z weryfikacją PawnIO i Inno Setup oraz testy instalatora zakończone powodzeniem. Lokalnie po poprawkach: 56/56, test-installer 13/13, smoke 0.
## Etap przeprojektowania interfejsu

Gałąź `feature/fluent-redesign`. Panel na pasku i okno ustawień mają nowy wygląd zgodny z Windows 11 / Fluent 2. Odczyt czujników, wykrywanie paska i mechanizm nakładki pozostały bez zmian funkcjonalnych.

## Zmiany w tym etapie

- **Panel:** kompaktowa powierzchnia 40 DIP na pasku 48 DIP, zaokrąglenie 6, półprzezroczyste tło z delikatną krawędzią, ikony CPU/GPU, wyciszone etykiety i pogrubione wartości. Kolumna wartości ma stałą szerokość i cyfry tabelaryczne, więc panel nie zmienia rozmiaru przy zmianie odczytu. Stany najechania i wciśnięcia; wysoki kontrast używa kolorów systemowych. Automatyczna szerokość jest domyślna. Inspiracja wizualna: FluentFlyout (GPL-3.0) — nie skopiowano kodu ani zasobów; TempView nie osadza się w Explorer jak FluentFlyout.
- **Okno ustawień:** nawigacja boczna (Wygląd, Ekrany, Czujniki, Kolory temperatur), karty ustawień, podgląd z bieżącymi odczytami, stały pasek stanu zapisu. Zapis nie zamyka okna; „Odrzuć zmiany” i „Przywróć domyślne” działają na wersji roboczej; zamknięcie z niezapisanymi zmianami pyta o zapis. Strona Czujniki wyjaśnia brak odczytu i stan PawnIO; strona Ekrany pokazuje, dlaczego panel jest ukryty na danym ekranie. Kompaktowa nawigacja poniżej 760 px.
- **Motyw:** wbudowany motyw Fluent z .NET 10 (Mica, kolor akcentu, wysoki kontrast) bez nowych zależności. Motyw systemowy panelu odpowiada trybowi paska zadań; okno — trybowi aplikacji.
- **Zasobnik:** własna monochromatyczna ikona dopasowana do paska, menu Fluent, lewy klik otwiera ustawienia. „Diagnostyka czujników” otwiera stronę Czujniki zamiast okna komunikatu. Ponowne uruchomienie EXE otwiera ustawienia działającej instancji.
- **Ustawienia:** wersja 2 (`CustomColors`, `AutoWidth`) z migracją wersji 1. Jawne wartości użytkownika są zachowane; tylko nigdy niezmienione domyślne wartości układu przechodzą na nowe domyślne.
- **Przegląd kodu:** niezależny przegląd zmian wykrył 7 problemów, wszystkie poprawione i sprawdzone. Najważniejsze: niepoprawny tekst w polu temperatury (np. „-”) zapisywał NaN do ustawień i zamykał aplikację, a niepełny kolor tła powodował wyjątek na stronie kolorów. Teraz niepoprawne pola nie trafiają do ustawień i blokują zapis. Usunięto też wyciek okna, narastanie odświeżeń, utratę kliknięć w wierszach punktów i resetowanie podglądu co sekundę.
- **Środowisko:** skrypty `.ps1` z BOM (działają w Windows PowerShell 5.1), poprawione wykrywanie sukcesu `setup.ps1`, `nuget.config` w repozytorium.

## Zweryfikowane lokalnie (2026-10-31)

Środowisko: Windows 11 Pro 10.0.26200, AMD Ryzen 7 7800X3D, NVIDIA GeForce RTX 5070 + Radeon iGPU, dwa ekrany: 2560×1440 (główny) i 1920×1080 (x = −1920), oba 100% (96 DPI), tryb ciemny, przezroczystość włączona, pasek wyśrodkowany. .NET SDK 10.0.403 zainstalowane przez setup.ps1.

- `scripts/test.ps1`: 40 testów zaliczonych (27 wcześniejszych + migracja v1→v2, paleta motywów, kontrast WCAG). `scripts/build.ps1`: Release bez ostrzeżeń, paczka self-contained. Paczka uruchomiona z `--smoke` bez runtime w PATH: kod wyjścia 0.
- Rzeczywiste odczyty: GPU (NVIDIA GPU Core) 32–49°C. CPU: N/A z powodem „Brak sterownika PawnIO” — PawnIO nie jest zainstalowany na tym komputerze, aplikacja działała bez administratora. Brak danych nigdy nie był pokazany jako 0.
- Zrzuty ekranu i sterowanie przez UI Automation: wszystkie cztery strony, motyw ciemny i wymuszony jasny, układ pionowy i poziomy, kolory zależne od temperatury (zielony odczyt GPU na pasku, znacznik bieżącej temperatury na skali), zapis do pliku, ponowne uruchomienie z zapisanymi ustawieniami, okno „Zapisać zmiany?” i „Nie zapisuj” (plik bez zmian), migracja pliku v1 (81 px, tekst 10, margines 2, ikony) i niedostępny wybrany czujnik („Niedostępny: …”, N/A na panelu), tryb wszystkich ekranów (panel na drugim ekranie), wąskie okno 680 px.
- Najechanie na panel: podpowiedź z odczytami i powodem braku danych. Prawy klik: menu Fluent. Wszystkie elementy fokusowalne na każdej stronie mają nazwy dostępności (zrzut drzewa UIA).
- Położenie: panel x = 8 na pasku głównym i x = −1912 na drugim, status OK, zegar i obszar powiadomień nie są zasłaniane.

## Niesprawdzone

- Skalowanie 125/150/200% i mieszane DPI: oba ekrany tego komputera mają 100%, a zmiana skalowania systemu nie była częścią testu. Rozmiary są w DIP, a pozycja liczona z DPI paska — jak wcześniej.
- Wysoki kontrast, wyłączona przezroczystość i wyłączone animacje w systemie: ścieżki kodu istnieją (kolory systemowe, nieprzezroczyste tło, brak animacji), przezroczystość jest pokryta testem jednostkowym; nie sprawdzono ich na żywo, bo wymagałoby to zmiany ustawień systemu użytkownika.
- Systemowy jasny tryb paska (testowano jasny motyw wymuszony w TempView), autoukrywanie paska, pełny ekran, restart explorer.exe, uśpienie.
- Odczyt CPU po instalacji PawnIO na tym komputerze; Mica przy innym tle pulpitu; czytniki ekranu (sprawdzono nazwy UIA, nie Narratora).
- Panel nie przyjmuje fokusu klawiatury (WS_EX_NOACTIVATE, aby nie odbierać fokusu aplikacjom); dostęp z klawiatury zapewnia ikona w zasobniku i okno ustawień.

## Architektura i ograniczenia

WPF/.NET 10 + LibreHardwareMonitorLib 0.9.6 (MPL 2.0). Osobne okna nad obszarem paska, bez wstrzykiwania kodu do Explorer, bez zastępowania zegara i bez AppBar udającego osadzenie. Windows 11 nie udostępnia obsługiwanego kontraktu dla dowolnego panelu wewnątrz nowego paska. [Dokumentacja rozszerzeń Microsoft](https://learn.microsoft.com/en-us/windows/win32/shell/taskbar-extensions).

- Nakładka nie rezerwuje miejsca w Explorer. Wolny obszar jest sprawdzany co ok. sekundę; brak pełnego drzewa UIA, brak miejsca, ukryty pasek lub pełny ekran ukrywają panel. Powód widać na stronie Ekrany.
- Osobne zadania odczytują sensory i układ paska; UI odrzuca stare pomiary. Szerokość panelu jest mierzona w wątku UI i przekazywana do skanera paska.
- PawnIO instaluje (warunkowo) instalator TempView; odczyt CPU wykonuje usługa `TempViewSensors`, a GUI działa bez uprawnień administratora. Rzeczywiste temperatury ≤0°C są odrzucane; niewspierany czujnik nie jest zastępowany 0°C.
- `Application.ThemeMode` jest w .NET 10 oznaczone jako eksperymentalne (WPF0001). Ostrzeżenie wyciszono w jednym miejscu.

## Kontynuacja

Przeczytaj AGENTS.md, pobierz `main`, uruchom `scripts/test.ps1`, `scripts/build-installer.ps1` i `scripts/test-installer.ps1`. Następne kroki, w kolejności:
1. Testy instalatora w Piaskownicy lub VM według docs/INSTALLER-TESTS.md: instalacja offline, UAC, PawnIO, usługa, aktualizacja, dezinstalacja, restart.
2. Odczyt CPU przez usługę na fizycznym sprzęcie Intel, AMD i APU.
3. Certyfikat podpisu kodu i podpisane wydanie.
4. Potwierdzenie licencji programu instalacyjnego PawnIO.Setup.
5. Wcześniejsze punkty interfejsu: DPI 125–200% i mieszane, wysoki kontrast, wyłączona przezroczystość, Narrator.
6. Dalej: macierz sprzętu i powłoki, ograniczenie narzutu UIA.
