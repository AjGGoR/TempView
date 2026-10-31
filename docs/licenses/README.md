# Zależności i źródła

- TempView: MIT, plik LICENSE w katalogu głównym.
- LibreHardwareMonitor: Mozilla Public License 2.0. Tekst w LibreHardwareMonitor-MPL-2.0.txt. Źródła: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor ; pakiety: https://www.nuget.org/packages/LibreHardwareMonitorLib . TempView nie modyfikuje kodu biblioteki. Identyfikator wersji znajduje się w src/TempView/TempView.csproj oraz plikach zależności dołączonych do publikacji.
- LibreHardwareMonitor-THIRD-PARTY-NOTICES.txt pochodzi z tagu v0.9.6 upstream i zawiera informacje o elementach włączonych do biblioteki. Sprawdzaj je ponownie przy aktualizacji zależności.
- Moduły PawnIO.Modules 0.1.6 osadzone w LibreHardwareMonitor: LGPL 2.1, tekst w LibreHardwareMonitor-PawnIO-modules-LGPL-2.1.txt. Skrypt build dołącza odpowiadające źródła jako PawnIO.Modules-0.1.6-source.zip z https://github.com/namazso/PawnIO.Modules/tree/0.1.6 .
- Sterownik PawnIO 2.2.0, dołączony do instalatora TempView jako niezmieniony, podpisany `PawnIO_setup.exe`: GPL 2 lub późniejsza z wyjątkiem IOCTL opisanym w PawnIO-2.2.0-README.md; tekst GPL w PawnIO-GPL-2.0.txt. Odpowiadające źródła są dołączane przez instalator: PawnIO-2.2.0-source.zip (commit 5cdf470831fdfff3f7f1d06363ca6b230f3bf35a) i PawnPP-e64e4c3-source.zip (moduł PawnPP). Sam program instalacyjny PawnIO.Setup nie ma opublikowanego pliku licencji — przed publiczną dystrybucją potrzebne jest potwierdzenie autora (docs/DRIVER.md).
- Inno Setup 6.7.4 (narzędzie budowania instalatora, licencja Inno Setup pozwalająca na użycie komercyjne): https://jrsoftware.org/files/is/license.txt .
- .NET/WPF: dystrybucja Microsoft .NET, licencja MIT oraz zawiadomienia dostarczone w publikacji self-contained. Źródła: https://github.com/dotnet/runtime oraz https://github.com/dotnet/wpf .

Pełną listę bezpośrednich i przechodnich pakietów można sprawdzić przez `dotnet list src/TempView/TempView.csproj package --include-transitive`. Przy zmianie zależności należy przejrzeć i dołączyć wymagane zawiadomienia licencyjne. Plików licencji dostarczonych przez `dotnet publish` nie należy usuwać z paczki.
