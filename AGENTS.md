# Zasady pracy nad TempView

## Rozpoczęcie sesji

1. Przeczytaj README.md i docs/STATUS.md. Sprawdź `git status --short --branch`, `git branch --show-current` oraz `git remote -v`.
2. Uruchom `git fetch origin`. Nie nadpisuj niezacommitowanych zmian; obejrzyj je i zachowaj przed aktualizacją.
3. Na czystej gałęzi wykonaj `git pull --ff-only`. Rozbieżności przejrzyj i połącz z zachowaniem obu zestawów zmian. Nie używaj force push.
4. Kontynuuj istniejącą gałąź etapu. Nowy etap: `git switch -c feature/opis-etapu`. Nie twórz osobnej gałęzi dla każdego komputera.

## Implementacja i weryfikacja

- Integracja jest nakładką w obszarze paska, a nie obsługiwanym rozszerzeniem wnętrza paska Windows 11. Nie obiecuj rezerwacji miejsca przez Explorer.
- Nie wyłączaj zabezpieczeń Windows ani nie obchodź blokady sterowników. Nigdy nie zastępuj brakujących pomiarów zerem lub demonstracyjnymi wartościami.
- Odczyty czujników działają poza wątkiem UI. Testy symulowane nie dowodzą zgodności sprzętu.
- Zachowaj projekt niezależny od ścieżek komputera i sekretów. Ustawienia użytkownika pozostają poza repozytorium.
- Główny agent koordynuje operacje Git. Agentom przydzielaj rozłączne pliki.

## Zakończenie sesji

1. Uruchom `./scripts/test.ps1` i `./scripts/build.ps1`; wykonaj odpowiednie testy ręczne.
2. Uaktualnij docs/STATUS.md: zweryfikowane zachowanie, środowisko, ograniczenia, następny krok.
3. Przejrzyj `git diff --check`, `git diff` i status. Commituj opisowo wyłącznie pliki projektu, bez paczek i sekretów.
4. Wykonaj `git push -u origin <gałąź>`. Zapisz w podsumowaniu gałąź, SHA, wyniki testów oraz lokalizację ZIP/EXE.
5. Jeśli uwierzytelnienie nie działa, opisz konkretny błąd i wymagany krok użytkownika. Nie zapisuj tokenów w plikach.

Git przenosi zatwierdzone zmiany; nie synchronizuje niezapisanych plików ani lokalnej konfiguracji w czasie rzeczywistym.
