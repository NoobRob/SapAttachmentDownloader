# SAP Eingangsrechnungen – Anhang-Download (FUCHS Verwaltungs AG)

Lädt Anhänge (PDFs etc.) zu FI-Eingangsrechnungen aus SAP S/4HANA Cloud herunter.

Nutzt:
- **API_SUPPLIERINVOICE_PROCESS_SRV** (Entity `A_SupplierInvoice`, Communication Scenario `SAP_COM_0057`) – Liste der Rechnungen (ein Datensatz pro Rechnung, kein Dedup nötig)
- **API_CV_ATTACHMENT_SRV** (im selben Arrangement `SAP_COM_0057` enthalten) – `GetAllOriginals` + `AttachmentContentSet/$value` zum Download

> **Historie:** Ein früherer Ansatz über `API_OPLACCTGDOCITEMCUBE_SRV` (BKPF-Belegpositionen) +
> `BusinessObjectTypeName='BKPF'` lieferte zwar eine Belegliste, aber **keine Anhänge** – die
> Anhänge hängen tatsächlich an der SupplierInvoice (RBKP/`BUS2081`), nicht am Buchungsbeleg (BKPF).
> Der aktuelle Stand greift daher direkt auf `A_SupplierInvoice` zu; Schlüssel für den Attachment-Aufruf
> ist `SupplierInvoice (10-stellig) + FiscalYear` (ohne Buchungskreis).

## Projektstruktur

```
SapAttachmentDownloader.Core/         Wiederverwendbare Logik (kein UI), .NET 8, keine NuGet-Pakete
SapAttachmentDownloader.WinForms/     GUI: Liste laden, Anhänge prüfen & herunterladen
SapAttachmentDownloader.ConsoleJob/   Für Aufgabenplanung/Dienst: läuft ohne UI, merkt sich bereits geladene Belege
```

## Voraussetzungen

- .NET 8 SDK
- Zugang zu den beiden Communication Arrangements (`SAP_COM_0002`, `SAP_COM_0303`) mit Basic-Auth-Benutzer

## GUI starten (WinForms)

1. `SapAttachmentDownloader.WinForms/appsettings.json` anpassen (Host, Benutzer, Buchungskreis, Geschäftsjahr, Zielordner)
2. Projekt öffnen/starten (`dotnet run --project SapAttachmentDownloader.WinForms`)
3. Kennwort in der GUI eintragen (wird **nicht** gespeichert)
4. **"1) Rechnungen laden"** – zieht und dedupliziert die Belegliste
5. **"2) Anhänge prüfen & herunterladen"** – prüft pro Beleg, ob ein Anhang existiert, lädt ihn passend zur
   konfigurierten Datei-/Ordnerbenennung herunter (siehe nächster Abschnitt)

## Datei- und Ordnerbenennung konfigurieren

Standardmäßig werden Anhänge unter ihrem Original-Dateinamen aus SAP in einem Unterordner
`Rechnungsnummer_Lieferant-Nr.` je Beleg gespeichert. Beides lässt sich in der WinForms-GUI umstellen
(Bereiche **"Dateibenennung"** und **"Zielordner"**) und wird über **"Einstellungen speichern"** in die
`appsettings.json` geschrieben – dadurch übernimmt auch der `ConsoleJob` exakt dieselbe Konfiguration.

**Dateibenennung:**
- *Original-Dateiname aus SAP* (Default) – unverändertes Verhalten.
- *Benutzerdefiniert* – Dateiname wird aus einer geordneten Auswahl von Rechnungs-/Anhang-Feldern
  (Rechnungsnummer, Lieferant, Lieferantenname, Buchungsdatum, Original-Dateiname, …) und frei
  eingegebenem Text zusammengesetzt, verbunden durch ein beliebig langes Trennzeichen (z. B. `" - "`).
  Beispiel: `Eingangsrechnung - 5105600186 - 1000000123 - 20260315.pdf`. Die Dateiendung wird immer
  automatisch angehängt.

**Zielordner:**
- *Alle Dateien in einem gemeinsamen Ordner* – kein Unterordner, alle Anhänge landen direkt im
  Zielordner (`OutputFolder`).
- *Rechnungs-Unterordner* (Default) – Unterordner ebenso frei aus Feldern/Text zusammensetzbar, z. B.
  gruppiert nach Lieferant statt nach Rechnungsnummer (mehrere Rechnungen desselben Lieferanten landen
  dann automatisch im selben Ordner).

Beide Einstellungen liegen als eigene Abschnitte in der `appsettings.json`:

```json
"FileNaming": {
  "Mode": "Custom",
  "Segments": [
    { "Type": "Text", "Value": "Eingangsrechnung" },
    { "Type": "Field", "Value": "SupplierInvoice" },
    { "Type": "Field", "Value": "Supplier" }
  ],
  "Separator": " - ",
  "DateFormat": "yyyyMMdd"
},
"FolderNaming": {
  "Mode": "Custom",
  "Segments": [ { "Type": "Field", "Value": "SupplierName" } ],
  "Separator": "_",
  "DateFormat": "yyyyMMdd"
}
```

> Fehlt einer der beiden Abschnitte (z. B. in einer älteren `appsettings.json`), greift automatisch das
> bisherige Verhalten (Original-Dateiname bzw. `Rechnungsnummer_Lieferant-Nr.`-Unterordner) – kein
> manuelles Nachziehen nötig.

> **Hinweis für den Konsolen-Job:** Da WinForms und ConsoleJob jeweils eine eigene `appsettings.json`
> besitzen, muss eine in der GUI gespeicherte Konfiguration einmalig auch in die
> `SapAttachmentDownloader.ConsoleJob/appsettings.json` übertragen werden.

## Belegarten-Filter

Aktuell konfiguriert auf `RE`, `KR`, `KN` (siehe gemeinsame Analyse: das sind die tatsächlichen Rechnungsbuchungen;
`WE`/`WL` sind Warenbewegungen, `ZP`/`KZ` sind Zahlungen, `KA` ist uneindeutig und wird bewusst nicht automatisch mitgenommen –
bei Bedarf in `appsettings.json` → `InvoiceDocumentTypes` ergänzen, nachdem ihr stichprobenartig geprüft habt, ob dort echte Rechnungen dranhängen).

## Weg zur zyklischen Automatisierung

Das `ConsoleJob`-Projekt ist genau dafür vorbereitet:

- Es liest dieselbe `appsettings.json`-Struktur, aber **ohne Kennwort** darin – das kommt über die
  Umgebungsvariable `SAP_API_PASSWORD` (einmalig: `setx SAP_API_PASSWORD "..."` im Kontext des
  Dienstkontos/Task-Scheduler-Users, danach neu anmelden).
- Es merkt sich in `downloaded-documents.json` neben der .exe, welche Belege bereits erfolgreich
  heruntergeladen wurden, und überspringt sie beim nächsten Lauf – dadurch bleibt ein täglicher
  Lauf schnell und schont die API, auch wenn die Gesamtliste mit der Zeit auf mehrere Tausend Belege wächst.
- Exit-Code `0` bei Erfolg, `1` bei mind. einem Fehler – damit lässt sich der Lauf in der
  Aufgabenplanung sauber überwachen (z. B. "Bei Fehler E-Mail senden" via zusätzlichem Wrapper-Skript).

**Für den produktiven Betrieb zwei Ausbaustufen, in dieser Reihenfolge:**

1. **Windows-Aufgabenplanung** (schnellster Weg): `SapAttachmentDownloader.ConsoleJob.exe` als
   geplante Aufgabe, z. B. nachts, unter einem Dienstkonto mit gesetzter `SAP_API_PASSWORD`-Variable.
   Kein zusätzlicher Code nötig, das Projekt läuft so wie es ist.
2. **Windows-Dienst** (falls mehrmals täglich oder ereignisgesteuert laufen soll): Der
   Core ist UI-frei und lässt sich 1:1 in ein `Microsoft.Extensions.Hosting`-`BackgroundService`-Projekt
   einbetten (`dotnet new worker`) – die drei Services (`InvoiceListService`, `AttachmentDownloadService`,
   `SapODataClient`) bleiben unverändert, nur die Aufruf-Schleife wandert von `Program.cs` in `ExecuteAsync`.


## Bekannte Vereinfachungen in diesem Skeleton (bewusst, für einen schnellen Start)

- Kennwort wird in der GUI nicht gegen den Windows Credential Manager abgesichert – für Testbetrieb ok,
  für den produktiven Dienst sollte das nachgezogen werden (z. B. `CredentialManagement`-NuGet oder DPAPI).
- Keine Retry-/Backoff-Logik bei HTTP-Fehlern (z. B. bei kurzzeitigen 503ern des Gateways).
- `AccountingDocumentType eq 'KA'` ist standardmäßig nicht im Filter – siehe oben.
- `SupplierName` (Klartext-Lieferantenname) wird aktuell **nicht** befüllt, da `A_SupplierInvoice`
  nur die Business-Partner-Nummer (`InvoicingParty`) liefert, keinen Namen. Ausbauschritt: zusätzlicher
  Aufruf gegen die Business-Partner-API (`API_BUSINESS_PARTNER`) und Anreicherung pro Zeile.
