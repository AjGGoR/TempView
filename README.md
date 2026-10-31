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

- Windows 11 w wersji x64.
- Sprzęt udostępniający obsługiwane czujniki temperatury. Dostępność pomiarów zależy od urządzenia i jego sterowników.
- Dla wielu czujników CPU: sterownik PawnIO oraz uprawnienia administratora.

Paczka aplikacji zawiera środowisko .NET potrzebne do uruchomienia. Nie trzeba instalować go osobno. Do pobrania aplikacji i opcjonalnego sterownika potrzebne jest połączenie z internetem.

## Pobieranie i uruchomienie

1. Otwórz zakładkę [Actions](https://github.com/AjGGoR/TempView/actions) i wybierz najnowszy zakończony powodzeniem przebieg **Windows build and tests** dla gałęzi `main`.
2. W sekcji **Artifacts** pobierz **TempView-win-x64**. GitHub może wymagać zalogowania, aby pobrać plik.
3. Rozpakuj pobrany ZIP. Jeśli zawiera kolejną paczkę ZIP, rozpakuj również ją.
4. Otwórz rozpakowany folder i uruchom **TempView.exe**. Zachowaj wszystkie dołączone pliki w tym samym folderze.

Jeżeli aplikacja już działa, ponowne uruchomienie **TempView.exe** otwiera jej ustawienia. Ikona TempView znajduje się też w obszarze powiadomień obok zegara; może być schowana pod strzałką **Pokaż ukryte ikony**.

## Ustawienia i obsługa

Kliknij panel temperatur lub ikonę TempView w obszarze powiadomień. Prawy przycisk na panelu lub ikonie otwiera menu: **Ustawienia**, **Diagnostyka czujników** i **Zakończ TempView**.

- **Wygląd** — podgląd panelu z bieżącymi odczytami, układ, ikony, szerokość, rozmiar tekstu, margines, motyw, krycie tła i własne kolory.
- **Ekrany** — ekran główny, wszystkie ekrany z paskiem lub wybrane ekrany; strona paska osobno dla każdego ekranu. Przy każdym ekranie widać, czy panel jest widoczny, a jeśli nie, dlaczego.
- **Czujniki** — źródło temperatury CPU i GPU z bieżącym odczytem, częstotliwość odczytu oraz stan sterownika PawnIO, uprawnień i plików diagnostyki.
- **Kolory temperatur** — kolor odczytu zależny od temperatury. Skala pokazuje punkty i bieżący odczyt. Temperatury punktów nie mogą się powtarzać.

Zmiany są od razu widoczne na panelu, ale zostają zachowane dopiero po kliknięciu **Zapisz** (Ctrl+S). Stan zapisu jest zawsze widoczny na dole okna. **Odrzuć zmiany** wraca do ostatnio zapisanych ustawień. **Przywróć domyślne** ustawia wartości początkowe bez zapisywania. Przy zamykaniu okna z niezapisanymi zmianami TempView zapyta, czy je zapisać.

Ustawienia z poprzedniej wersji TempView są wczytywane automatycznie; Twoje kolory, rozmiary i wybór czujników zostają zachowane.

## Gdy temperatura pokazuje N/A

`N/A` oznacza brak wiarygodnego, aktualnego pomiaru. Najedź na panel albo wybierz **Diagnostyka czujników** w menu TempView, aby poznać przyczynę. Strona **Czujniki** opisuje problem i stan sterownika.

1. Sprawdź wybór czujnika w ustawieniach. Po podłączeniu urządzenia otwórz ustawienia ponownie, aby odświeżyć dostępną listę.
2. Jeśli brakuje temperatury CPU, sprawdź [instrukcję instalacji PawnIO](docs/DRIVER.md). Jest również dołączona do paczki jako **DRIVER.md**. Sterownik instaluje się osobno; samo uruchomienie TempView jako administrator nie zastępuje jego instalacji.
3. Po instalacji sterownika zamknij TempView i uruchom ponownie. Jeśli pomiary nadal wymagają dodatkowych uprawnień, kliknij **TempView.exe** prawym przyciskiem i wybierz **Uruchom jako administrator**. Podwyższone uprawnienia obejmują wtedy całą aplikację.

Nie wyłączaj zabezpieczeń Windows, aby uzyskać pomiar. Nie każdy model CPU lub GPU udostępnia obsługiwaną temperaturę; dla niedostępnych czujników pozostanie `N/A`.

## Gdy panel jest niewidoczny

Panel jest nakładką w wolnym obszarze paska zadań. Nie rezerwuje na nim miejsca. Może się ukryć przy braku wolnego obszaru, ukrytym pasku, aplikacji pełnoekranowej lub problemie z odczytaniem układu paska.

Otwórz ustawienia przez ikonę TempView i sprawdź stronę **Ekrany** — przy każdym ekranie widać powód ukrycia panelu. Wybierz inną stronę paska lub zmniejsz panel. Na monitorze bez systemowego paska panel nie jest wyświetlany. Zachowanie może różnić się między wersjami Windows i układami wielu monitorów.
