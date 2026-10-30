# Dostęp CPU: PawnIO

LibreHardwareMonitor 0.9.6 korzysta z osobno instalowanego sterownika PawnIO. W środowisku testowym brak PawnIO uniemożliwił odczyt temperatur CPU. Samo uruchomienie TempView jako administrator nie instaluje sterownika i nie rozwiązuje tego przypadku. Brak pomiaru pozostaje `N/A`; zerowy/niedodatni wynik CPU jest odrzucany jako niewiarygodny.

Instalator sterownika nie jest częścią paczki TempView. Nie uruchamia się automatycznie. Jeżeli chcesz umożliwić dostęp do CPU, pobierz PawnIO z [oficjalnej strony autora](https://pawnio.eu) albo wersję 2.1.0 dołączoną przez [LibreHardwareMonitor 0.9.6](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitor/Resources/PawnIO_setup.exe). Instalacja sterownika wymaga zatwierdzenia uprawnień administratora. Przed instalacją sprawdź podpis cyfrowy. Następnie zamknij i ponownie uruchom TempView. Dostępność konkretnego sensora nadal zależy od sprzętu i zabezpieczeń.

Repozytorium udostępnia skrypt pobierający dokładnie tę wersję, sprawdzający SHA256 i poprawny podpis Authenticode z podmiotem `namazso.eu`:

```powershell
./scripts/download-driver.ps1
```

W rozpakowanej paczce aplikacji ten sam skrypt jest dostępny jako `./drivers/download-driver.ps1`. Pobranie wymaga połączenia z internetem. Paczka nie instaluje automatycznie wszystkich zależności sprzętowych; sterownik instaluje użytkownik osobno.

Skrypt tylko pobiera plik do katalogu Downloads. Nie uruchamia go, nie podnosi uprawnień i nie wyłącza ochrony Windows. Oczekiwany SHA256 instalatora:

`A3A46226C5E2824F4CDD42BE0EECBABFC672C86F7889710F5AB1E6AD385B47A0`

Jeżeli podpis lub hash się nie zgadza, nie uruchamiaj pobranego pliku. Jeżeli Windows blokuje sterownik, pozostaw zabezpieczenia włączone i korzystaj z dostępnych odczytów.

## Licencje i źródła

PawnIO 2.1.0 jest objęty GPL 2 lub późniejszą z wyjątkiem opisanym w upstream README, obejmującym niezależne moduły komunikujące się wyłącznie przez IOCTL oraz wskazane połączenia z LGPL. Wyjątek nie obejmuje w ten sam sposób modułów wykonywanych przez interfejs Pawn. Źródła sterownika: [tag 2.1.0](https://github.com/namazso/PawnIO/tree/2.1.0), commit `e1043d7750f501b6b0d3ac0a71bd5b9406cd17b6`; GPL i pełny wyjątek znajdują się również w katalogu licenses. TempView nie redystrybuuje binarnego instalatora sterownika; pobranie odbywa się bezpośrednio z upstream.

LibreHardwareMonitor 0.9.6 osadza moduły PawnIO.Modules 0.1.6 na LGPL 2.1. Ich licencja, informacja upstream i odpowiadające źródła ZIP są dołączane do paczki przez skrypt budowania. Źródła: [PawnIO.Modules 0.1.6](https://github.com/namazso/PawnIO.Modules/tree/0.1.6).
