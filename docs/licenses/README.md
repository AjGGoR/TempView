# Zależności i źródła

- TempView: MIT, plik LICENSE w katalogu głównym.
- LibreHardwareMonitor: Mozilla Public License 2.0. Tekst w LibreHardwareMonitor-MPL-2.0.txt. Źródła: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor ; pakiety: https://www.nuget.org/packages/LibreHardwareMonitorLib . TempView nie modyfikuje kodu biblioteki. Identyfikator wersji znajduje się w src/TempView/TempView.csproj oraz plikach zależności dołączonych do publikacji.
- LibreHardwareMonitor-THIRD-PARTY-NOTICES.txt pochodzi z tagu v0.9.6 upstream i zawiera informacje o elementach włączonych do biblioteki. Sprawdzaj je ponownie przy aktualizacji zależności.
- Moduły PawnIO.Modules 0.1.6 osadzone w LibreHardwareMonitor: LGPL 2.1, tekst w LibreHardwareMonitor-PawnIO-modules-LGPL-2.1.txt. Skrypt build dołącza odpowiadające źródła jako PawnIO.Modules-0.1.6-source.zip z https://github.com/namazso/PawnIO.Modules/tree/0.1.6 .
- Oddzielny sterownik PawnIO 2.1.0: GPL 2 lub późniejsza z wyjątkiem IOCTL/LGPL opisanym w PawnIO-2.1.0-README.md; tekst GPL w PawnIO-GPL-2.0.txt. Jego instalatora nie redystrybuujemy. Źródła: https://github.com/namazso/PawnIO/tree/2.1.0 . Szczegóły w docs/DRIVER.md.
- .NET/WPF: dystrybucja Microsoft .NET, licencja MIT oraz zawiadomienia dostarczone w publikacji self-contained. Źródła: https://github.com/dotnet/runtime oraz https://github.com/dotnet/wpf .

Pełną listę bezpośrednich i przechodnich pakietów można sprawdzić przez `dotnet list src/TempView/TempView.csproj package --include-transitive`. Przy zmianie zależności należy przejrzeć i dołączyć wymagane zawiadomienia licencyjne. Plików licencji dostarczonych przez `dotnet publish` nie należy usuwać z paczki.
