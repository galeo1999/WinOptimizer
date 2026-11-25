# WinOptimizer

Ein Windows-11-Tool, das ich als eigenes Projekt mit C# und WPF entwickle. Es zeigt Systeminfos an, verwaltet den Autostart, schaltet einige Datenschutz-Einstellungen ab und hilft beim Aufräumen von Speicherplatz. Jede Änderung an der Registry lässt sich wieder zurücknehmen.

Das Projekt ist noch nicht fertig.

## Was es kann

- **Übersicht:** Windows-Version, CPU, GPU, Mainboard, BIOS und Laufwerke, dazu CPU- und RAM-Auslastung live.
- **Datenschutz:** Werbe-ID, maßgeschneiderte Erfahrungen, Eingabe-Personalisierung und Aktivitätsverlauf abschalten. Vorher sichert das Tool die betroffenen Registry-Werte.
- **Autostart:** Einträge aus der Registry, den Autostart-Ordnern und von Store-Apps anzeigen und ein- oder ausschalten. Auch hier wird vorher gesichert.
- **Speicher:** Temp-Ordner, Downloads und Papierkorb analysieren. Was aufgeräumt werden darf und was nur angezeigt wird, ist getrennt.
- **Wiederherstellen:** Frühere Änderungen an Datenschutz und Autostart zurücknehmen. Eine lokale Historie hält fest, was wann geändert wurde.

## Aufbau

- `Views/`: die WPF-Ansichten (XAML)
- `Services/`: die Logik für Registry, Autostart, Hardware und Speicher
- `Models/`: die Datenklassen
- `Config/privacy.json`: beschreibt die Datenschutz-Einstellungen als Daten. Neue Einstellungen brauchen so keinen neuen Code.

Datenschutz-Änderungen laufen über `TweakEngine`, Autostart-Änderungen über `StartupManager`. Beide legen vor jeder Änderung eine Sicherung an. Einstellungen, die Administratorrechte brauchen, erkennt `AdminService` und startet das Tool bei Bedarf mit erhöhten Rechten neu.

## Bauen und starten

Du brauchst Windows 11 und das .NET 10 SDK.

```
dotnet build
dotnet run
```

Für systemweite Einstellungen muss das Tool als Administrator laufen.

## Was noch fehlt

- **MVVM:** Die Logik der Ansichten steckt noch im Code-Behind. Das baue ich als Nächstes um.
- **Tests:** Auf `main` gibt es noch keine automatisierten Tests.
- **Weitere Funktionen:** Speicherbereinigung, Speicher- und Hintergrundanalyse liegen auf dem Branch `feature/storage-cleaner` und sind noch nicht in `main`.

## Hinweis

Das Tool ändert Windows-Einstellungen und die Registry. Für unterstützte Änderungen gibt es Sicherungen. Gelöschte Dateien lassen sich aber nicht wiederherstellen. Prüf deshalb, was du anwendest.
