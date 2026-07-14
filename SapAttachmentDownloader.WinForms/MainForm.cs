using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using SapAttachmentDownloader.Core;
using SapAttachmentDownloader.Core.Models;

namespace SapAttachmentDownloader.WinForms;

public class MainForm : Form
{
    // --- Eingabefelder ---
    private readonly TextBox _txtHost = new() { Width = 320 };
    private readonly TextBox _txtUsername = new() { Width = 200 };
    private readonly TextBox _txtPassword = new() { Width = 160, UseSystemPasswordChar = true };
    private readonly TextBox _txtCompanyCode = new() { Width = 60 };
    private readonly TextBox _txtFiscalYear = new() { Width = 60 };
    private readonly TextBox _txtOutputFolder = new() { Width = 160 };
    private readonly Button _btnBrowseFolder = new() { Text = "..." , Width = 30 };

    private readonly Button _btnLoadInvoices = new() { Text = "1) Rechnungen laden", Width = 260, Height = 60 };
    private readonly Button _btnCheckAndDownload = new() { Text = "2) Anhänge prüfen && herunterladen", Width = 260, Height = 60 };

    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        AutoGenerateColumns = false,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
    };

    private readonly ProgressBar _progress = new() { Dock = DockStyle.Bottom, Height = 18 };
    private readonly TextBox _log = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        Font = new Font("Consolas", 10),
    };

    // --- Datei- und Ordnerbenennung ---
    private readonly NamingComposer _fileNaming = new(
        "Dateibenennung",
        "Original-Dateiname aus SAP",
        "Benutzerdefiniert",
        FileNameBuilder.Catalog.Select(f => (f.Key, f.DisplayName)).ToList(),
        " - ", "yyyyMMdd",
        "Tipp: Über \"Text hinzufügen\" können Sie an beliebiger Stelle einen freien Text einfügen " +
        "(z. B. \"Eingangsrechnung\") und mit ▲/▼ einsortieren. Ein Anhang-Feld wie \"Original-Dateiname\" " +
        "einbeziehen, um mehrere Anhänge pro Rechnung unterscheidbar zu machen.");

    private readonly NamingComposer _folderNaming = new(
        "Zielordner",
        "Alle Dateien in einem gemeinsamen Ordner",
        "Rechnungs-Unterordner",
        FolderNameBuilder.Catalog.Select(f => (f.Key, f.DisplayName)).ToList(),
        "_", "yyyyMMdd",
        "Tipp: Bei \"Alle Dateien in einem gemeinsamen Ordner\" entfaellt der Unterordner - " +
        "alle heruntergeladenen Dateien landen direkt im Zielordner.");

    private readonly Button _btnSaveNaming = new() { Text = "Einstellungen speichern", Width = 180 };

    private static readonly InvoiceDocument SampleInvoice = new()
    {
        SupplierInvoice = "5105600186",
        SupplierReference = "RE-2026-001",
        Supplier = "1000000123",
        SupplierName = "Musterlieferant GmbH",
        CompanyCode = "1010",
        FiscalYear = "2026",
        AccountingDocumentType = "RE",
        PostingDate = new DateTime(2026, 3, 15),
        DocumentDate = new DateTime(2026, 3, 10),
        InvoiceGrossAmount = 1234.56m,
        DocumentCurrency = "EUR",
    };

    private static readonly AttachmentOriginal SampleAttachment = new()
    {
        FileName = "Rechnung.pdf",
        ArchiveDocumentID = "0090000123",
        LinkedSAPObjectKey = "51056001862026",
    };

    private BindingList<InvoiceDocument> _invoices = new();
    private SapApiOptions? _options;

    public MainForm()
    {
        Text = "FUCHS – SAP Eingangsrechnungen: Anhang-Download";
        Width = 1800;
        Height = 900;
        StartPosition = FormStartPosition.CenterScreen;

        // Default fuer die Ordnerbenennung reproduziert das bisherige, fest verdrahtete
        // Verhalten (Unterordner "Rechnungsnummer_Lieferant-Nr."), damit ohne appsettings.json
        // oder ohne "FolderNaming"- Sektionnichts anders funktioniert als zuvor.
        var folderDefaults = new FolderNamingOptions();
        _folderNaming.Apply(folderDefaults.Mode == FolderNamingMode.Custom, folderDefaults.Segments, folderDefaults.Separator, folderDefaults.DateFormat);

        _fileNaming.Changed += UpdateFilePreview;
        _folderNaming.Changed += UpdateFolderPreview;
        _txtOutputFolder.TextChanged += (_, _) => UpdateFolderPreview();
        _btnSaveNaming.Click += (_, _) => SaveNamingSettingsToAppSettings();

        BuildLayout();
        LoadSettingsFromAppSettings();
        UpdateFilePreview();
        UpdateFolderPreview();

        _btnBrowseFolder.Click += (_, _) => BrowseFolder();
        _btnLoadInvoices.Click += async (_, _) => await LoadInvoicesAsync();
        _btnCheckAndDownload.Click += async (_, _) => await CheckAndDownloadAsync();
    }

    private void BuildLayout()
    {
        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 6,
            Padding = new Padding(8),
        };

        top.Controls.Add(WrapLabelAndControl("Host:", _txtHost));
        top.Controls.Add(WrapLabelAndControl("Benutzer:", _txtUsername));
        top.Controls.Add(WrapLabelAndControl("Kennwort:", _txtPassword));
        top.SetColumnSpan(top.Controls[^1], 1);
        top.Controls.Add(WrapLabelAndControl("Buchungskreis:", _txtCompanyCode));
        top.Controls.Add(WrapLabelAndControl("Geschäftsjahr:", _txtFiscalYear));

        var folderRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        folderRow.Controls.Add(new Label { Text = "Zielordner:", AutoSize = true, Padding = new Padding(0, 6, 6, 0) });
        folderRow.Controls.Add(_txtOutputFolder);
        folderRow.Controls.Add(_btnBrowseFolder);
        top.Controls.Add(folderRow);

        var buttonRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
        buttonRow.Controls.Add(_btnLoadInvoices);
        buttonRow.Controls.Add(_btnCheckAndDownload);
        top.Controls.Add(buttonRow);
        top.SetColumnSpan(buttonRow, 3);

        var namingRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Padding = new Padding(0) };
        namingRow.Controls.Add(_fileNaming.Group);
        namingRow.Controls.Add(_folderNaming.Group);

        var namingPanel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        namingPanel.Controls.Add(namingRow);
        namingPanel.Controls.Add(new FlowLayoutPanel { AutoSize = true, WrapContents = false, Padding = new Padding(0, 4, 0, 4), Controls = { _btnSaveNaming } });

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 380,
        };
        split.Panel1.Controls.Add(_grid);
        split.Panel2.Controls.Add(_log);

        Controls.Add(split);
        Controls.Add(_progress);
        Controls.Add(namingPanel);
        Controls.Add(top);

        SetupGridColumns();
    }

    private void UpdateFilePreview()
    {
        var invoice = _invoices.Count > 0 ? _invoices[0] : SampleInvoice;
        var example = FileNameBuilder.Build(invoice, SampleAttachment, BuildFileNamingOptions());
        _fileNaming.Preview.Text = $"Vorschau: {example}";
    }

    private void UpdateFolderPreview()
    {
        var invoice = _invoices.Count > 0 ? _invoices[0] : SampleInvoice;
        var baseOutput = string.IsNullOrWhiteSpace(_txtOutputFolder.Text) ? "<Zielordner>" : _txtOutputFolder.Text;
        var example = FolderNameBuilder.Build(baseOutput, invoice, BuildFolderNamingOptions());
        _folderNaming.Preview.Text = $"Vorschau: {example}";
    }

    private FileNamingOptions BuildFileNamingOptions() => new()
    {
        Mode = _fileNaming.IsMode2 ? FileNamingMode.Custom : FileNamingMode.Original,
        Segments = _fileNaming.BuildSegments(),
        Separator = _fileNaming.Separator.Text,
        DateFormat = string.IsNullOrWhiteSpace(_fileNaming.DateFormat.Text) ? "yyyyMMdd" : _fileNaming.DateFormat.Text,
    };

    private FolderNamingOptions BuildFolderNamingOptions() => new()
    {
        Mode = _folderNaming.IsMode2 ? FolderNamingMode.Custom : FolderNamingMode.Flat,
        Segments = _folderNaming.BuildSegments(),
        Separator = _folderNaming.Separator.Text,
        DateFormat = string.IsNullOrWhiteSpace(_folderNaming.DateFormat.Text) ? "yyyyMMdd" : _folderNaming.DateFormat.Text,
    };

    private void SaveNamingSettingsToAppSettings()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            var text = File.Exists(path) ? File.ReadAllText(path) : "{}";
            var root = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip }) as JsonObject
                ?? new JsonObject();

            var fileNaming = BuildFileNamingOptions();
            root["FileNaming"] = SegmentsToJson(fileNaming.Mode.ToString(), fileNaming.Segments, fileNaming.Separator, fileNaming.DateFormat);

            var folderNaming = BuildFolderNamingOptions();
            root["FolderNaming"] = SegmentsToJson(folderNaming.Mode.ToString(), folderNaming.Segments, folderNaming.Separator, folderNaming.DateFormat);

            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tempPath, path, overwrite: true);

            Log("Einstellungen gespeichert.");
        }
        catch (Exception ex)
        {
            Log($"Fehler beim Speichern der Einstellungen: {ex.Message}");
        }
    }

    private static JsonObject SegmentsToJson(string mode, List<NamingSegment> segments, string separator, string dateFormat)
    {
        var segmentsArray = new JsonArray();
        foreach (var segment in segments)
        {
            segmentsArray.Add(new JsonObject
            {
                ["Type"] = segment.Type.ToString(),
                ["Value"] = segment.Value,
            });
        }

        return new JsonObject
        {
            ["Mode"] = mode,
            ["Segments"] = segmentsArray,
            ["Separator"] = separator,
            ["DateFormat"] = dateFormat,
        };
    }

    private static Control WrapLabelAndControl(string label, Control control)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        row.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 6, 0) });
        row.Controls.Add(control);
        return row;
    }

    private void SetupGridColumns()
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SupplierInvoice", HeaderText = "Rechnungsnummer", DataPropertyName = "SupplierInvoice", Width = 120 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SupplierReference", HeaderText = "Lieferanten-Ref.", DataPropertyName = "SupplierReference", Width = 110 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PostingDate", HeaderText = "Buchungsdatum", DataPropertyName = "PostingDate", Width = 110 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "AccountingDocumentType", HeaderText = "Typ", DataPropertyName = "AccountingDocumentType", Width = 50 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Supplier", HeaderText = "Lieferant-Nr.", DataPropertyName = "Supplier", Width = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "InvoiceGrossAmount", HeaderText = "Bruttobetrag", DataPropertyName = "InvoiceGrossAmount", Width = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "DocumentCurrency", HeaderText = "Whrg.", DataPropertyName = "DocumentCurrency", Width = 50 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "AttachmentCount", HeaderText = "Anhänge", DataPropertyName = "AttachmentCount", Width = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", DataPropertyName = "Status", Width = 220, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _grid.DataSource = _invoices;
    }

    private void LoadSettingsFromAppSettings()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path)) return;

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var sap = doc.RootElement.GetProperty("Sap");

            _txtHost.Text = sap.GetProperty("Host").GetString() ?? "";
            _txtUsername.Text = sap.GetProperty("Username").GetString() ?? "";
            _txtCompanyCode.Text = sap.GetProperty("CompanyCode").GetString() ?? "";
            _txtFiscalYear.Text = sap.GetProperty("FiscalYear").GetString() ?? "";
            _txtOutputFolder.Text = sap.GetProperty("OutputFolder").GetString() ?? "";

            var fileNaming = FileNamingOptionsReader.Read(doc.RootElement);
            _fileNaming.Apply(fileNaming.Mode == FileNamingMode.Custom, fileNaming.Segments, fileNaming.Separator, fileNaming.DateFormat);

            var folderNaming = FolderNamingOptionsReader.Read(doc.RootElement);
            _folderNaming.Apply(folderNaming.Mode == FolderNamingMode.Custom, folderNaming.Segments, folderNaming.Separator, folderNaming.DateFormat);
        }
        catch (Exception ex)
        {
            Log($"appsettings.json konnte nicht gelesen werden: {ex.Message}");
        }
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = _txtOutputFolder.Text };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _txtOutputFolder.Text = dialog.SelectedPath;
        }
    }

    private bool TryBuildOptions(out SapApiOptions options)
    {
        options = new SapApiOptions
        {
            Host = _txtHost.Text.Trim(),
            Username = _txtUsername.Text.Trim(),
            Password = _txtPassword.Text,
            CompanyCode = _txtCompanyCode.Text.Trim(),
            FiscalYear = _txtFiscalYear.Text.Trim(),
            OutputFolder = _txtOutputFolder.Text.Trim(),
        };

        if (string.IsNullOrWhiteSpace(options.Host) ||
            string.IsNullOrWhiteSpace(options.Username) ||
            string.IsNullOrWhiteSpace(options.Password) ||
            string.IsNullOrWhiteSpace(options.CompanyCode) ||
            string.IsNullOrWhiteSpace(options.FiscalYear))
        {
            MessageBox.Show(this, "Bitte Host, Benutzer, Kennwort, Buchungskreis und Geschäftsjahr ausfüllen.",
                "Angaben unvollständig", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    private async Task LoadInvoicesAsync()
    {
        if (!TryBuildOptions(out var options)) return;
        _options = options;

        SetBusy(true);
        try
        {
            using var client = new SapODataClient(options);
            var service = new InvoiceListService(client, options);
            var progress = new Progress<string>(Log);

            var result = await service.GetInvoicesAsync(progress);

            _invoices = new BindingList<InvoiceDocument>(result);
            _grid.DataSource = _invoices;

            Log($"Fertig: {result.Count} eindeutige Rechnungen geladen.");
        }
        catch (Exception ex)
        {
            Log($"FEHLER beim Laden: {ex.Message}");
            MessageBox.Show(this, ex.Message, "Fehler beim Laden der Rechnungen", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task CheckAndDownloadAsync()
    {
        if (_options is null)
        {
            MessageBox.Show(this, "Bitte zuerst die Rechnungsliste laden.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_invoices.Count == 0) return;

        if (!TryBuildOptions(out var freshOptions)) return;
        // Kennwort neu übernehmen (falls es zwischenzeitlich geaendert wurde), Rest bleibt.
        _options.Password = freshOptions.Password;
        _options.OutputFolder = freshOptions.OutputFolder;

        SetBusy(true);
        _progress.Minimum = 0;
        _progress.Maximum = _invoices.Count;
        _progress.Value = 0;

        using var client = new SapODataClient(_options);
        var attachmentService = new AttachmentDownloadService(client, _options);
        var namingOptions = BuildFileNamingOptions();
        var folderNamingOptions = BuildFolderNamingOptions();

        var downloaded = 0;
        var withoutAttachment = 0;
        var errors = 0;

        foreach (var invoice in _invoices)
        {
            try
            {
                var originals = await attachmentService.GetOriginalsAsync(invoice.LinkedSAPObjectKey);
                invoice.AttachmentCount = originals.Count;

                if (originals.Count == 0)
                {
                    invoice.Status = "Kein Anhang";
                    withoutAttachment++;
                }
                else
                {
                    var savedFiles = new List<string>();
                    var docFolder = FolderNameBuilder.Build(_options.OutputFolder, invoice, folderNamingOptions);

                    foreach (var original in originals)
                    {
                        var desiredFileName = FileNameBuilder.Build(invoice, original, namingOptions);
                        var savedPath = await attachmentService.DownloadAsync(original, docFolder, desiredFileName);
                        savedFiles.Add(Path.GetFileName(savedPath));
                        downloaded++;
                    }

                    invoice.Status = $"OK: {string.Join(", ", savedFiles)}";
                }
            }
            catch (Exception ex)
            {
                invoice.Status = $"FEHLER: {ex.Message}";
                errors++;
            }

            _grid.InvalidateRow(_invoices.IndexOf(invoice));
            _progress.Value++;
            Application.DoEvents(); // einfache UI-Aktualisierung fuer dieses Skeleton
        }

        Log($"Fertig. {downloaded} Datei(en) heruntergeladen, {withoutAttachment} Beleg(e) ohne Anhang, {errors} Fehler.");
        SetBusy(false);

        MessageBox.Show(this,
            $"{downloaded} Datei(en) heruntergeladen nach:\n{_options.OutputFolder}\n\n" +
            $"{withoutAttachment} Beleg(e) ohne Anhang, {errors} Fehler.",
            "Download abgeschlossen", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SetBusy(bool busy)
    {
        _btnLoadInvoices.Enabled = !busy;
        _btnCheckAndDownload.Enabled = !busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }

    private void Log(string message)
    {
        if (_log.InvokeRequired)
        {
            _log.Invoke(() => Log(message));
            return;
        }
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }
}
