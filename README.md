# WinOptimizer

Ein Windows-11-Tool, das ich als eigenes Projekt mit C# und WPF entwickle. Es zeigt Systeminfos an, verwaltet den Autostart, schaltet einige Datenschutz-Einstellungen ab und hilft beim Aufräumen von Speicherplatz. Jede Änderung an der Registry lässt sich wieder zurücknehmen.

Das Projekt ist noch nicht fertig.

## Was es kann

- **Übersicht:** Windows-Version, CPU, GPU, Mainboard, BIOS und Laufwerke, dazu CPU- und RAM-Auslastung live.
- **Datenschutz:** Werbe-ID, maßgeschneiderte Erfahrungen, Eingabe-Personalisierung und Aktivitätsverlauf abschalten. Vorher sichert das Tool die betroffenen Registry-Werte.
- **Autostart:** Einträge aus der Registry, den Autostart-Ordnern und von Store-Apps anzeigen und ein- oder ausschalten. Auch hier wird vorher gesichert.
- **Speicher:** Temp-Ordner, Downloads und Papierkorb analysieren und zeigen, welche Ordner und Dateien am meisten Platz belegen. Aufgeräumt werden nur die Temp-Ordner (Dateien, die älter als 24 Stunden sind) und der Papierkorb, jeweils nach einer Rückfrage. Downloads werden nur angezeigt.
- **Leistung:** RAM-Verbrauch nach Anwendungen, Programme im Autostart und Hintergrunddienste anzeigen, dazu Hinweise, was sich lohnen könnte. Die einzige Änderung, die das Tool hier vornimmt: Windows Search von "Automatisch" auf "Manuell" stellen, nur mit Bestätigung und Adminrechten.
- **Wiederherstellen:** Frühere Änderungen an Datenschutz, Autostart und Diensten zurücknehmen. Eine Historie hält fest, was wann geändert wurde.

## Aufbau

- `Views/`: die WPF-Ansichten (XAML)
- `Services/`: die Logik für Registry, Autostart, Hardware und Speicher
- `Models/`: die Datenklassen
- `Config/privacy.json`: beschreibt die Datenschutz-Einstellungen als Daten. Neue Einstellungen brauchen so keinen neuen Code.

Datenschutz-Änderungen laufen über `TweakEngine`, Autostart-Änderungen über `StartupManager`, Dienständerungen über `BackgroundServiceManager`. Alle legen vor jeder Änderung eine Sicherung an. Der Dienst-Manager prüft danach, ob Windows die neue Einstellung wirklich übernommen hat, und setzt sie bei einem Fehler zurück. Einstellungen, die Administratorrechte brauchen, erkennt `AdminService` und startet das Tool bei Bedarf mit erhöhten Rechten neu.

## Bauen und starten

Du brauchst Windows 11 und das .NET 10 SDK.

```
dotnet build
dotnet run
```

Für systemweite Einstellungen muss das Tool als Administrator laufen.

## Was noch fehlt

- **MVVM:** Die Logik der Ansichten steckt noch im Code-Behind. Das baue ich als Nächstes um.
- **Tests:** Bisher gibt es nur `Tests/BackgroundServiceChecks`, ein Prüfprogramm für die Dienständerung (Aufruf siehe dort). Ein richtiges Testprojekt für die übrigen Services fehlt noch.

## Hinweis

Das Tool ändert Windows-Einstellungen und die Registry. Für unterstützte Änderungen gibt es Sicherungen. Gelöschte Dateien lassen sich aber nicht wiederherstellen. Prüf deshalb, was du anwendest.
