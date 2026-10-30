# TempView

TempView wyświetla temperatury procesora (CPU) i karty graficznej (GPU) w niewielkim panelu w obszarze paska zadań Windows 11. Pozwala obserwować temperatury podczas codziennej pracy bez otwierania dodatkowego okna. Systemowy zegar pozostaje dostępny.

## Możliwości

- Temperatury CPU i GPU w stopniach Celsjusza, domyślnie odświeżane co sekundę.
- Wybór lewej lub prawej strony paska oraz monitorów, na których ma być widoczny panel.
- Układ pionowy lub poziomy, opcjonalne ikony i regulacja rozmiaru tekstu oraz odstępów.
- Osobne kolory CPU i GPU, ustawienia tła i jego przezroczystości oraz jasny, ciemny lub systemowy motyw.
- Opcjonalne kolory zależne od temperatury z własnymi progami.
- Automatyczny wybór czujników z możliwością ręcznego wskazania innego źródła pomiaru.
- Podgląd zmian i zapamiętywanie ustawień po zamknięciu aplikacji.

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

Jeżeli aplikacja już działa, dostęp do niej znajdziesz przez ikonę w obszarze powiadomień obok zegara. Ikona może być schowana pod strzałką **Pokaż ukryte ikony**.

## Ustawienia i obsługa

Kliknij panel temperatur lub kliknij prawym przyciskiem ikonę TempView w obszarze powiadomień i wybierz **Ustawienia**.

- **Wygląd** — zmień motyw, kolory, przezroczystość, rozmiar, odstępy, układ i widoczność ikon.
- **Monitory** — wybierz ekran główny, wszystkie ekrany z paskiem lub wybrane monitory. Ustaw stronę paska osobno dla każdego ekranu.
- **Czujniki** — ustaw częstotliwość odczytu i wybierz źródło temperatury CPU lub GPU.
- **Kolory temperatur** — włącz kolory zależne od temperatury i dodaj własne punkty z kolorami. Temperatury punktów nie mogą się powtarzać.

Zmiany mają podgląd na żywo. Kliknij **Zapisz**, aby zachować ustawienia, lub **Anuluj**, aby wrócić do poprzednich. Przycisk **Zapisz domyślne** przywraca i zapisuje ustawienia początkowe.

Aby zamknąć aplikację, kliknij prawym przyciskiem jej ikonę w obszarze powiadomień i wybierz **Zakończ**.

## Gdy temperatura pokazuje N/A

`N/A` oznacza brak wiarygodnego, aktualnego pomiaru. Najedź na wiersz temperatury albo wybierz **Diagnostyka** w menu ikony TempView, aby poznać przyczynę.

1. Sprawdź wybór czujnika w ustawieniach. Po podłączeniu urządzenia otwórz ustawienia ponownie, aby odświeżyć dostępną listę.
2. Jeśli brakuje temperatury CPU, sprawdź [instrukcję instalacji PawnIO](docs/DRIVER.md). Jest również dołączona do paczki jako **DRIVER.md**. Sterownik instaluje się osobno; samo uruchomienie TempView jako administrator nie zastępuje jego instalacji.
3. Po instalacji sterownika zamknij TempView i uruchom ponownie. Jeśli pomiary nadal wymagają dodatkowych uprawnień, kliknij **TempView.exe** prawym przyciskiem i wybierz **Uruchom jako administrator**. Podwyższone uprawnienia obejmują wtedy całą aplikację.

Nie wyłączaj zabezpieczeń Windows, aby uzyskać pomiar. Nie każdy model CPU lub GPU udostępnia obsługiwaną temperaturę; dla niedostępnych czujników pozostanie `N/A`.

## Gdy panel jest niewidoczny

Panel jest nakładką w wolnym obszarze paska zadań. Nie rezerwuje na nim miejsca. Może się ukryć przy braku wolnego obszaru, ukrytym pasku, aplikacji pełnoekranowej lub problemie z odczytaniem układu paska.

Otwórz ustawienia przez ikonę TempView, wybierz inny bok paska lub zmniejsz szerokość panelu. Na monitorze bez systemowego paska panel nie jest wyświetlany. Zachowanie może różnić się między wersjami Windows i układami wielu monitorów.
