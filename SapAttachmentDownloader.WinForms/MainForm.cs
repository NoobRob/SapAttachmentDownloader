using System.ComponentModel;
using System.Text.Json;
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

    private BindingList<InvoiceDocument> _invoices = new();
    private SapApiOptions? _options;

    public MainForm()
    {
        Text = "FUCHS – SAP Eingangsrechnungen: Anhang-Download";
        Width = 1800;
        Height = 750;
        StartPosition = FormStartPosition.CenterScreen;

        BuildLayout();
        LoadSettingsFromAppSettings();

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

        void AddRow(string label, Control control, Control? extra = null)
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            row.Controls.Add(new Label { Text = label, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 6, 6, 0), Width = 100 });
            row.Controls.Add(control);
            if (extra != null) row.Controls.Add(extra);
            top.Controls.Add(row);
            top.SetColumnSpan(row, extra != null ? 2 : 1);
        }

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

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 420,
        };
        split.Panel1.Controls.Add(_grid);
        split.Panel2.Controls.Add(_log);

        Controls.Add(split);
        Controls.Add(_progress);
        Controls.Add(top);

        SetupGridColumns();
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
        // Kennwort neu uebernehmen (falls es zwischenzeitlich geaendert wurde), Rest bleibt wie geladen.
        _options.Password = freshOptions.Password;
        _options.OutputFolder = freshOptions.OutputFolder;

        SetBusy(true);
        _progress.Minimum = 0;
        _progress.Maximum = _invoices.Count;
        _progress.Value = 0;

        using var client = new SapODataClient(_options);
        var attachmentService = new AttachmentDownloadService(client, _options);

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
                    var docFolder = Path.Combine(_options.OutputFolder,
                        $"{invoice.SupplierInvoice}_{invoice.Supplier}");

                    foreach (var original in originals)
                    {
                        var savedPath = await attachmentService.DownloadAsync(original, docFolder);
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
