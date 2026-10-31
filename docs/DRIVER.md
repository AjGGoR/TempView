# Temperatura CPU: sterownik PawnIO i usługa TempView

## Kiedy sterownik jest potrzebny

Windows nie udostępnia programom temperatury procesora. TempView korzysta z LibreHardwareMonitor 0.9.6, który czyta czujniki CPU (Intel: MSR, AMD: SMN/SMU) przez sterownik **PawnIO**. Ten sam sterownik jest potrzebny do temperatury zintegrowanej grafiki w procesorach AMD (APU). Temperatury dedykowanych kart NVIDIA, AMD i Intel pochodzą ze sterowników tych kart i **nie wymagają PawnIO**.

Urządzenie PawnIO dopuszcza wyłącznie konto SYSTEM i administratorów (ACL `D:P(A;;GA;;;SY)(A;;GA;;;BA)` w INF sterownika). Dlatego instalator TempView dodaje małą usługę `TempViewSensors`, która czyta tylko czujniki CPU i udostępnia ich wartości do odczytu. Samo okno TempView działa bez uprawnień administratora.

## Co robi instalator

Strona „Temperatura procesora” w instalatorze wyjaśnia wymaganie i pozwala z niego zrezygnować. Decyzja zależy od komputera:

| Stan komputera | Działanie instalatora |
|---|---|
| Procesor Intel lub AMD, brak PawnIO | instaluje dołączony PawnIO 2.2.0 i usługę (opcja domyślnie zaznaczona) |
| PawnIO 2.2.0 lub nowszy | używa zainstalowanego sterownika, nie instaluje go ponownie i nigdy nie obniża wersji |
| PawnIO 2.1.x | aktualizuje go do 2.2.0 (obsługiwana aktualizacja w miejscu) |
| starszy lub nierozpoznany PawnIO | nie zmienia go; odczyt CPU pozostaje wyłączony, instalator mówi, co zrobić |
| inny procesor | nie oferuje odczytu CPU |
| użytkownik odznaczy opcję | brak sterownika i usługi; GPU działa, CPU = N/A |

Sterownik instaluje oryginalny, niezmieniony i podpisany instalator PawnIO z argumentami `-install -silent`. TempView nigdy nie używa wydania „unrestricted” (niepodpisanego, dla programistów). Instalacja sterownika wymaga uprawnień administratora (UAC instalatora TempView). Jeżeli instalator PawnIO zgłosi potrzebę restartu (kod 3010), TempView poprosi o ponowne uruchomienie komputera.

Jeżeli Windows zablokuje sterownik (np. zasady bezpieczeństwa organizacji) albo instalacja się nie powiedzie, TempView zostaje zainstalowany, temperatura CPU pokazuje N/A, a ekran końcowy i dziennik instalacji podają przyczynę. Instalator kończy się wtedy kodem 101. Nie wyłączaj zabezpieczeń Windows, aby obejść blokadę.

Po odinstalowaniu TempView sterownik PawnIO **zostaje**, bo mogą go używać inne programy (np. LibreHardwareMonitor). Usuniesz go w **Ustawienia > Aplikacje > Zainstalowane aplikacje > PawnIO**.

## Usługa TempViewSensors

- Konto LocalSystem, start automatyczny. Instalator ogranicza jej token do `SeChangeNotifyPrivilege`.
- Czyta wyłącznie czujniki temperatury CPU. Nie przyjmuje poleceń, plików ani parametrów.
- Publikuje migawkę na potoku `\\.\pipe\TempView.Sensors.v1`: tylko wyjście, odczyt dla zalogowanych użytkowników, zakaz dostępu sieciowego.
- TempView sprawdza, że potok obsługuje plik usługi zarejestrowany w systemie, i weryfikuje każdy komunikat.
- Stan usługi i sterownika widać w TempView na stronie **Czujniki > Diagnostyka**.

## Wersja przenośna (ZIP)

Paczka ZIP nie instaluje sterownika ani usługi. Odczyt CPU działa w niej tylko wtedy, gdy PawnIO jest już zainstalowany, a TempView zostanie uruchomiony jako administrator. Zalecany jest instalator.

## Weryfikacja i licencje

| Element | Wartość |
|---|---|
| Wersja | PawnIO 2.2.0 (wydanie 2026-03-15) |
| Źródło | https://github.com/namazso/PawnIO.Setup/releases/tag/2.2.0 (link „Download” na https://pawnio.eu) |
| SHA-256 `PawnIO_setup.exe` | `1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032` (zgodny z sumą opublikowaną przez GitHub) |
| Podpis instalatora | `CN=namazso.eu, O=namazso, L=Debrecen, C=HU` (GLOBALTRUST 2015 CODESIGNING 1), znacznik czasu Microsoft |
| Podpis sterownika | katalog `pawnio.cat` podpisany przez Microsoft Windows Hardware Compatibility Publisher |

Skrypt budowania pobiera instalator z powyższego źródła, sprawdza SHA-256 i wydawcę, a dopiero potem dołącza go do instalatora TempView. Instalator TempView ponownie sprawdza SHA-256 tuż przed uruchomieniem sterownika z katalogu instalacji (dostępnego tylko dla administratorów). Instalacja nie pobiera niczego z internetu.

PawnIO jest objęty licencją GPL 2 lub późniejszą z wyjątkiem dla programów komunikujących się przez IOCTL (`licenses/PawnIO-2.2.0-README.md`, `licenses/PawnIO-GPL-2.0.txt`). Odpowiadające źródła są dołączone do instalacji: `licenses/PawnIO-2.2.0-source.zip` (commit `5cdf470831fdfff3f7f1d06363ca6b230f3bf35a`) i `licenses/PawnPP-e64e4c3-source.zip` (moduł PawnPP, commit `e64e4c37b2d8ba0d8ee57205faf8183aee12c438`).

**Otwarta kwestia przed publiczną dystrybucją:** repozytorium PawnIO.Setup nie zawiera pliku licencji dla samego programu instalacyjnego (sterownik jest na GPL). Instalator jest redystrybuowany przez LibreHardwareMonitor, ale nie znaleziono wyraźnej zgody autora dla innych projektów. Przed publicznym wydaniem uzyskaj pisemne potwierdzenie od autora (kontakt w README PawnIO).
