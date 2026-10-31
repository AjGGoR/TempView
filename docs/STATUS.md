# Stan TempView — 2026-10-31

Etap: instalator all-in-one (gałąź `feature/installer`, od `feature/fluent-redesign` @ e0facb9). Etap przeprojektowania interfejsu opisano niżej bez zmian.

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
- PawnIO wymaga osobnej instalacji; uprawnienia administratora obejmują cały proces. Rzeczywiste temperatury ≤0°C są odrzucane; niewspierany czujnik nie jest zastępowany 0°C.
- `Application.ThemeMode` jest w .NET 10 oznaczone jako eksperymentalne (WPF0001). Ostrzeżenie wyciszono w jednym miejscu.

## Kontynuacja

Przeczytaj AGENTS.md, pobierz gałąź `feature/fluent-redesign`, uruchom scripts/test.ps1 i scripts/build.ps1. Następne kroki: test przy 125–200% i mieszanym DPI, wysoki kontrast i wyłączona przezroczystość na żywo, Narrator, odczyt CPU z PawnIO; potem scalenie do `main`. Dalej: macierz sprzętu i powłoki, ograniczenie narzutu UIA, broker uprawnień.
