# TempView

TempView wyświetla temperatury procesora (CPU) i karty graficznej (GPU) w niewielkim panelu w obszarze paska zadań Windows 11. Pozwala obserwować temperatury podczas codziennej pracy bez otwierania dodatkowego okna. Systemowy zegar pozostaje dostępny.

## Możliwości

- Temperatury CPU i GPU w stopniach Celsjusza, domyślnie odświeżane co sekundę.
- Kompaktowy panel w stylu Windows 11: dwa wiersze lub jeden, ikony podzespołów i liczby, które nie przesuwają się przy zmianie odczytu.
- Wybór lewej lub prawej strony paska oraz monitorów, na których ma być widoczny panel.
- Motyw systemowy, jasny lub ciemny. Systemowy motyw panelu odpowiada kolorowi paska zadań.
- Własne kolory tekstu i tła oraz opcjonalne kolory zależne od temperatury z własnymi punktami.
- Automatyczny wybór czujników z możliwością ręcznego wskazania innego źródła pomiaru.
- Podgląd zmian na żywo z bieżącymi odczytami i zapamiętywanie ustawień po zamknięciu aplikacji.

## Wymagania

| Platforma | Stan |
|---|---|
| Windows 11 x64 (kompilacja 22000 lub nowsza) | obsługiwany; instalator i aplikacja testowane na Windows 11 Pro 26200 |
| Windows 10 | nieobsługiwany: integracja z paskiem zadań jest przygotowana dla Windows 11 i nie była testowana na Windows 10; instalator go blokuje |
| Windows 11 ARM64 | nieobsługiwany: aplikacja, runtime i sterownik PawnIO są x64; instalator blokuje emulację x64 |

- Temperatura GPU: karty NVIDIA, AMD i Intel ze sterownikiem producenta. Nie wymaga dodatkowych sterowników ani uprawnień administratora.
- Temperatura CPU: procesory Intel i AMD obsługiwane przez LibreHardwareMonitor 0.9.6. Wymaga sterownika PawnIO, który instalator może zainstalować (szczegóły w [DRIVER.md](docs/DRIVER.md)). Nie każdy model udostępnia czujnik; wtedy TempView pokazuje N/A.
- Instalacja wymaga zgody administratora (UAC). TempView działa później bez uprawnień administratora.

Instalator zawiera wszystko: aplikację, środowisko .NET 10 i sterownik PawnIO. Nie trzeba niczego pobierać osobno, a instalacja działa bez internetu.

## Instalacja

1. Pobierz instalator. Na razie dostępny jest jako artefakt GitHub Actions: otwórz [Actions](https://github.com/AjGGoR/TempView/actions), wybierz najnowszy udany przebieg **Windows build and tests** i pobierz **TempView-Setup-win-x64** (GitHub może wymagać zalogowania; artefakt jest spakowany w ZIP przez GitHub — otwórz go i uruchom plik `TempView-Setup-…-x64.exe`).
2. Uruchom instalator i zatwierdź okno kontroli konta użytkownika.
3. Wybierz opcje: uruchamianie po zalogowaniu, skrót na pulpicie i odczyt temperatury CPU (sterownik PawnIO z wyjaśnieniem w instalatorze).
4. Kliknij **Instaluj**, a na końcu **Zakończ** z zaznaczonym **Uruchom TempView**.

Panel pojawi się w obszarze paska zadań. Jeżeli instalacja sterownika wymaga restartu, instalator o tym poinformuje.

Aktualna wersja instalatora jest **niepodpisana** (nie ma jeszcze certyfikatu podpisu kodu). Windows SmartScreen może ostrzec przed uruchomieniem. Porównaj sumę SHA-256 z plikiem `.sha256` dołączonym do artefaktu. Nie wyłączaj zabezpieczeń Windows.

Aktualizacja: uruchom nowszy instalator. Ustawienia zostają zachowane, a działający TempView jest zamykany automatycznie.

Odinstalowanie: **Ustawienia > Aplikacje > Zainstalowane aplikacje > TempView > Odinstaluj**. Usuwane są pliki programu, skróty, uruchamianie po zalogowaniu i usługa TempView. Ustawienia osobiste (`%LOCALAPPDATA%\TempView`) i sterownik PawnIO pozostają; sterownik usuniesz osobno jako aplikację PawnIO, jeżeli nie używa go inny program.

Paczka przenośna ZIP (**TempView-win-x64-portable**) jest przeznaczona dla zaawansowanych użytkowników: nie instaluje sterownika ani usługi, więc temperatura CPU działa w niej tylko po uruchomieniu jako administrator z już zainstalowanym PawnIO. Wcześniejsze ustawienia wersji przenośnej są wykorzystywane przez wersję instalowaną; stary folder możesz usunąć ręcznie.

## Stan wydania

TempView jest w fazie testów. Instalator **nie jest jeszcze gotowy do publicznej dystrybucji**:
- Instalator jest **niepodpisany**. Podpisywanie jest przygotowane, ale nie ma jeszcze certyfikatu.
- Instalacja, aktualizacja, dezinstalacja, instalacja sterownika PawnIO i działanie usługi odczytu CPU nie zostały jeszcze sprawdzone w środowisku testowym. Odczyt CPU przez usługę nie został potwierdzony na fizycznym sprzęcie. Sprawdzone są testy automatyczne, budowanie w CI, weryfikacja zależności i kreator instalatora (bez instalacji).
- Redystrybucja programu instalacyjnego PawnIO czeka na potwierdzenie licencji przez autora; sam sterownik jest na licencji GPL.

Szczegóły i aktualny postęp: [docs/STATUS.md](docs/STATUS.md).

## Ustawienia i obsługa

Kliknij panel temperatur lub ikonę TempView w obszarze powiadomień (może być schowana pod strzałką **Pokaż ukryte ikony**). Ponowne uruchomienie TempView z menu Start także otwiera ustawienia działającej aplikacji. Prawy przycisk na panelu lub ikonie otwiera menu: **Ustawienia**, **Diagnostyka czujników** i **Zakończ TempView**.

- **Wygląd** — podgląd panelu z bieżącymi odczytami, układ, ikony, szerokość, rozmiar tekstu, margines, motyw, krycie tła i własne kolory.
- **Ekrany** — ekran główny, wszystkie ekrany z paskiem lub wybrane ekrany; strona paska osobno dla każdego ekranu. Przy każdym ekranie widać, czy panel jest widoczny, a jeśli nie, dlaczego.
- **Czujniki** — źródło temperatury CPU i GPU z bieżącym odczytem, częstotliwość odczytu oraz stan sterownika PawnIO, uprawnień i plików diagnostyki.
- **Kolory temperatur** — kolor odczytu zależny od temperatury. Skala pokazuje punkty i bieżący odczyt. Temperatury punktów nie mogą się powtarzać.

Zmiany są od razu widoczne na panelu, ale zostają zachowane dopiero po kliknięciu **Zapisz** (Ctrl+S). Stan zapisu jest zawsze widoczny na dole okna. **Odrzuć zmiany** wraca do ostatnio zapisanych ustawień. **Przywróć domyślne** ustawia wartości początkowe bez zapisywania. Przy zamykaniu okna z niezapisanymi zmianami TempView zapyta, czy je zapisać.

Ustawienia z poprzedniej wersji TempView są wczytywane automatycznie; Twoje kolory, rozmiary i wybór czujników zostają zachowane.

## Gdy temperatura pokazuje N/A

`N/A` oznacza brak wiarygodnego, aktualnego pomiaru. TempView nigdy nie podstawia zera ani wartości przykładowych. Najedź na panel albo wybierz **Diagnostyka czujników** w menu TempView, aby poznać przyczynę. Strona **Czujniki** pokazuje też stan sterownika PawnIO i usługi odczytu CPU.

1. Temperatura CPU: uruchom instalator TempView ponownie i zaznacz odczyt temperatury CPU. Jeżeli instalator zgłosił potrzebę restartu, uruchom ponownie komputer.
2. Jeżeli Windows zablokował sterownik, pozostaw zabezpieczenia włączone; TempView będzie pokazywać temperaturę GPU. Szczegóły w [DRIVER.md](docs/DRIVER.md).
3. Sprawdź wybór czujnika w ustawieniach. Po podłączeniu urządzenia kliknij **Odśwież listę czujników**.

Nie każdy model CPU lub GPU udostępnia obsługiwaną temperaturę; dla niedostępnych czujników pozostanie `N/A`.

## Gdy panel jest niewidoczny

Panel jest nakładką w wolnym obszarze paska zadań. Nie rezerwuje na nim miejsca. Może się ukryć przy braku wolnego obszaru, ukrytym pasku, aplikacji pełnoekranowej lub problemie z odczytaniem układu paska.

Otwórz ustawienia przez ikonę TempView i sprawdź stronę **Ekrany** — przy każdym ekranie widać powód ukrycia panelu. Wybierz inną stronę paska lub zmniejsz panel. Na monitorze bez systemowego paska panel nie jest wyświetlany. Zachowanie może różnić się między wersjami Windows i układami wielu monitorów.
