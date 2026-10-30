# Stan TempView — 2026-10-30

Dostarczono działającą wersję testową Windows x64. Kod, testy, publikacja self-contained, ustawienia i integracja nakładki są gotowe do kontynuowania na drugim komputerze. Gałąź etapu: `feat/windows-taskbar-widget`.

## Zweryfikowane lokalnie

- Windows 11 Home 10.0.26200, AMD Ryzen 5 3500U z Radeon Vega Mobile, jeden ekran 1920×1080, skala 125% (120 DPI).
- .NET SDK 10.0.403: kompilacja Release bez błędów i ostrzeżeń; paczka self-contained uruchamia się bez instalacji runtime.
- 27 testów zaliczonych: interpolacja i wygładzanie, walidacja progów/konfiguracji, zapis/odczyt, uszkodzony i przyszły format, niedostępne/przeterminowane dane, wolne przedziały paska (obie strony, ujemne współrzędne, nakładanie i brak miejsca).
- Rzeczywiste odczyty po instalacji podpisanego PawnIO 2.1.0 i uruchomieniu TempView jako administrator: CPU 51°C, GPU 46,24°C w próbce kontrolnej. CPU: Tctl/Tdie; GPU: GFX z SMU AMD APU. Nie są to dane demonstracyjne. Bez uprawnień odczyty były niedostępne i prezentowane jako N/A. Nie zmieniono zabezpieczeń Windows.
- Czujnik AMD `GFX` jest przypisywany do GPU mimo umieszczenia przez LHM wewnątrz grupy CPU. Źródło: [RyzenSMU w LHM 0.9.6](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitorLib/Hardware/RyzenSMU.cs).
- Wykrywanie paska, Start, przypiętych aplikacji i zasobnika przez UI Automation. Dla szerokości 112 DIP wolne pozycje: lewa x=8, prawa x=1437, y=1020, rozmiar 140×60 pikseli. Zegar systemowy pozostaje poza panelem.
- Graficzne ustawienia: otwarcie, podgląd, zmiana strony i zapis przez UI; konfiguracja przetrwała ponowne uruchomienia. Dalsze ustawienia użytkownika (81 DIP, ikony, margines 2, tekst 10, lewa strona) zostały zachowane; po restarcie panel miał 102×60 pikseli.
- Instalator PawnIO pobrany z upstream: SHA256 zgodny z DRIVER.md, podpis Authenticode Valid, namazso.eu; instalacja zatwierdzona przez użytkownika. Usługa PawnIO działa.
- Pomiar po poprawkach: 30,09 s, 4,984 s czasu CPU procesu na 8 procesorach logicznych, czyli średnio 2,07% całego CPU; Working Set na końcu 133,95 MiB. Aplikacja podwyższona, rzeczywiste sensory, odświeżanie 1 s, bez otwartego okna ustawień. To pojedyncza próbka na tym laptopie, nie gwarancja dla innych komputerów. Dalsze ograniczenie narzutu skanowania UIA jest wskazanym usprawnieniem.

## Architektura i ograniczenia

WPF/.NET 10 + LibreHardwareMonitorLib 0.9.6 (MPL 2.0). Osobne okna nad obszarem paska, bez wstrzykiwania kodu do Explorer, bez zastępowania zegara i bez AppBar udającego osadzenie. Windows 11 nie udostępnia obsługiwanego kontraktu dla dowolnego panelu wewnątrz nowego paska; zgodność zależy od wersji powłoki. [Dokumentacja rozszerzeń Microsoft](https://learn.microsoft.com/en-us/windows/win32/shell/taskbar-extensions).

- Nakładka nie rezerwuje miejsca w Explorer. Sprawdza wolny obszar co ok. sekundę; zmiana układu między skanami może chwilowo wyprzedzić reakcję. Brak pełnego drzewa UIA, brak miejsca, ukryty pasek lub pełny ekran powodują ukrycie panelu. Skan bez aktualizacji przez 5 s także ukrywa okna. Nie deklarujemy bezwarunkowej stabilności na każdej kompilacji Windows.
- Osobne zadania odczytują sensory i układ paska. UI odrzuca stare pomiary. Lista sprzętu jest ponownie otwierana co minutę, lista pasków jest wykrywana ponownie w każdej iteracji. Ikona zasobnika zachowuje dostęp do ustawień po odłączeniu ekranu.
- PawnIO wymaga osobnej instalacji; paczka zawiera zweryfikowany skrypt pobierania i instrukcję, a nie binarny instalator. Do pobrania potrzebny jest internet. Nie można obiecać temperatur CPU po samym rozpakowaniu ZIP na nowym komputerze.
- Uprawnienia administratora w tej wersji obejmują cały proces. Oddzielny broker czujników pozostaje usprawnieniem architektury. Bezpieczny tryb bez podwyższenia pokazuje dostępne czujniki albo N/A.
- Rzeczywiste temperatury <=0°C są odrzucane jako niewiarygodne w tym konsumenckim zastosowaniu. Czujnik niewspierany nie jest zastępowany 0°C.
- Pozycja wybranego ekranu jest zapisana przez nazwę urządzenia Windows DISPLAY; numeracja może zmienić się po przebudowie konfiguracji monitorów.
- UIA bywało początkowo niedostępne przy uruchamianiu z narzędzi. Próba z Eksploratora oraz późniejsze uruchomienia zwracały poprawne granice; przy niepełnych danych aplikacja ukrywa panel.

## Niesprawdzone ręcznie

- Inne CPU Intel/AMD i osobne GPU NVIDIA/AMD/Intel, wiele GPU, brakujące/odłączane urządzenia.
- Fizyczny drugi monitor, mieszane DPI, odłączanie/dokowanie, zmiana rozdzielczości.
- Restart explorer.exe, usypianie/wybudzanie, autoukrywanie i aplikacje pełnoekranowe. Odpowiednia logika jest zaimplementowana; nie jest to potwierdzenie jej działania na sprzęcie.
- Wizualne przejścia gradientów przy rzeczywistym pomiarze: logika i animacja są zaimplementowane, interpolacja przetestowana automatycznie, ale pełny test wizualny przerwano po zatrzymaniu Computer Use przez użytkownika klawiszem Esc.

## Kontynuacja

Przeczytaj AGENTS.md, pobierz tę samą gałąź na drugim komputerze, uruchom scripts/test.ps1 i scripts/build.ps1. Pobierz artefakt z udanego przebiegu GitHub Actions. Następny etap: testy macierzy sprzętu i powłoki, ograniczenie narzutu UIA oraz broker uprawnień. Instalator TempView i automatyczne aktualizacje pozostają poza zakresem.

## Dokumentacja użytkownika

README zawiera wyłącznie opis aplikacji i instrukcję użytkownika. Instrukcje budowania oraz pracy z Git przeniesiono do DEVELOPMENT.md. Zmiana dokumentacyjna; sprawdzono odnośniki lokalne i diff, bez zmian kodu aplikacji.
