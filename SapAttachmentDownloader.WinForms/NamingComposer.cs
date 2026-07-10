using SapAttachmentDownloader.Core.Models;

namespace SapAttachmentDownloader.WinForms;

/// <summary>
/// Wiederverwendbare UI-Einheit fuer die Zusammenstellung eines Datei- oder Ordnernamens aus
/// Feldern und freiem Text: Modus-Umschalter (z.B. Original/Benutzerdefiniert oder
/// Flach/Unterordner), Verfuegbar/Ausgewaehlt-Listboxen mit Reihenfolge
/// (Hinzufuegen/Entfernen/Auf/Ab), freier Text, Trennzeichen, Datumsformat und Live-Vorschau.
/// Wird in MainForm sowohl fuer die Dateibenennung als auch fuer die Ordnerbenennung
/// instanziiert, um die Steuerelement-Verdrahtung nicht doppelt pflegen zu muessen.
/// </summary>
public sealed class NamingComposer
{
    public GroupBox Group { get; }
    public RadioButton Mode1 { get; }
    public RadioButton Mode2 { get; }
    public TextBox Separator { get; } = new() { Width = 80 };
    public TextBox DateFormat { get; } = new() { Width = 80 };
    public Label Preview { get; } = new() { AutoSize = true, Text = "Vorschau:" };

    /// <summary>Feuert bei jeder Aenderung, die das Ergebnis beeinflusst (Modus, Auswahl, Reihenfolge, Trennzeichen, Datumsformat).</summary>
    public event Action? Changed;

    public bool IsMode2 => Mode2.Checked;

    private readonly ListBox _available = new() { Width = 220, Height = 90, SelectionMode = SelectionMode.MultiExtended };
    private readonly ListBox _selected = new() { Width = 220, Height = 90, SelectionMode = SelectionMode.MultiExtended };
    private readonly TextBox _literal = new() { Width = 160 };
    private readonly Button _addField = new() { Text = ">", Width = 30 };
    private readonly Button _removeField = new() { Text = "<", Width = 30 };
    private readonly Button _moveUp = new() { Text = "▲", Width = 30 };
    private readonly Button _moveDown = new() { Text = "▼", Width = 30 };
    private readonly Button _addText = new() { Text = "Text hinzufügen", Width = 130 };
    private readonly IReadOnlyList<(string Key, string DisplayName)> _catalog;

    private sealed record FieldPickerItem(string Key, string DisplayName, bool IsLiteral = false)
    {
        public override string ToString() => IsLiteral ? $"\"{DisplayName}\"" : DisplayName;
    }

    public NamingComposer(
        string title, string mode1Label, string mode2Label,
        IReadOnlyList<(string Key, string DisplayName)> catalog,
        string defaultSeparator, string defaultDateFormat, string tip)
    {
        _catalog = catalog;
        Mode1 = new RadioButton { Text = mode1Label, Checked = true, AutoSize = true };
        Mode2 = new RadioButton { Text = mode2Label, AutoSize = true };
        Separator.Text = defaultSeparator;
        DateFormat.Text = defaultDateFormat;

        Group = Build(title, tip);
        PopulateAvailable();
        UpdateEnabled();
    }

    private GroupBox Build(string title, string tip)
    {
        var group = new GroupBox { Text = title, Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        var layout = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };

        var modeRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        modeRow.Controls.Add(Mode1);
        modeRow.Controls.Add(Mode2);
        layout.Controls.Add(modeRow);

        var fieldsRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };

        var availableCol = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        availableCol.Controls.Add(new Label { Text = "Verfügbare Felder", AutoSize = true });
        availableCol.Controls.Add(_available);
        fieldsRow.Controls.Add(availableCol);

        var moveCol = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(4, 24, 4, 0) };
        moveCol.Controls.Add(_addField);
        moveCol.Controls.Add(_removeField);
        fieldsRow.Controls.Add(moveCol);

        var selectedCol = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        selectedCol.Controls.Add(new Label { Text = "Ausgewählt (Reihenfolge = Ergebnis)", AutoSize = true });
        selectedCol.Controls.Add(_selected);
        fieldsRow.Controls.Add(selectedCol);

        var orderCol = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(4, 24, 4, 0) };
        orderCol.Controls.Add(_moveUp);
        orderCol.Controls.Add(_moveDown);
        fieldsRow.Controls.Add(orderCol);

        layout.Controls.Add(fieldsRow);

        var textRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        textRow.Controls.Add(new Label { Text = "Freier Text:", AutoSize = true, Padding = new Padding(0, 6, 6, 0) });
        textRow.Controls.Add(_literal);
        textRow.Controls.Add(_addText);
        layout.Controls.Add(textRow);

        var separatorRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        separatorRow.Controls.Add(new Label { Text = "Trennzeichen:", AutoSize = true, Padding = new Padding(0, 6, 6, 0) });
        separatorRow.Controls.Add(Separator);
        separatorRow.Controls.Add(new Label { Text = "Datumsformat:", AutoSize = true, Padding = new Padding(12, 6, 6, 0) });
        separatorRow.Controls.Add(DateFormat);
        layout.Controls.Add(separatorRow);

        layout.Controls.Add(Preview);
        layout.Controls.Add(new Label { Text = tip, AutoSize = true, MaximumSize = new Size(900, 0), ForeColor = Color.Gray });

        group.Controls.Add(layout);

        Mode1.CheckedChanged += (_, _) => { UpdateEnabled(); Changed?.Invoke(); };
        Mode2.CheckedChanged += (_, _) => { UpdateEnabled(); Changed?.Invoke(); };
        _addField.Click += (_, _) => MoveToSelected();
        _removeField.Click += (_, _) => MoveToAvailable();
        _moveUp.Click += (_, _) => MoveOrder(-1);
        _moveDown.Click += (_, _) => MoveOrder(1);
        _addText.Click += (_, _) => AddLiteral();
        _literal.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            AddLiteral();
        };
        Separator.TextChanged += (_, _) => Changed?.Invoke();
        DateFormat.TextChanged += (_, _) => Changed?.Invoke();

        return group;
    }

    private void AddLiteral()
    {
        var text = _literal.Text.Trim();
        if (text.Length == 0) return;
        _selected.Items.Add(new FieldPickerItem(text, text, IsLiteral: true));
        _literal.Clear();
        _literal.Focus();
        Changed?.Invoke();
    }

    private void PopulateAvailable()
    {
        _available.Items.Clear();
        _selected.Items.Clear();
        foreach (var field in _catalog)
            _available.Items.Add(new FieldPickerItem(field.Key, field.DisplayName));
    }

    private void MoveToSelected()
    {
        foreach (var item in _available.SelectedItems.Cast<FieldPickerItem>().ToList())
        {
            _available.Items.Remove(item);
            _selected.Items.Add(item);
        }
        Changed?.Invoke();
    }

    private void MoveToAvailable()
    {
        // Literal-Text-Elemente gehoeren nicht in die Katalog-Liste - "<" loescht sie einfach.
        foreach (var item in _selected.SelectedItems.Cast<FieldPickerItem>().ToList())
        {
            _selected.Items.Remove(item);
            if (!item.IsLiteral)
                _available.Items.Add(item);
        }
        SortAvailableByCatalogOrder();
        Changed?.Invoke();
    }

    private void SortAvailableByCatalogOrder()
    {
        var catalogKeys = _catalog.Select(f => f.Key).ToList();
        var sorted = _available.Items.Cast<FieldPickerItem>()
            .OrderBy(item => catalogKeys.IndexOf(item.Key))
            .ToList();
        _available.Items.Clear();
        foreach (var item in sorted)
            _available.Items.Add(item);
    }

    private void MoveOrder(int direction)
    {
        var index = _selected.SelectedIndex;
        if (index < 0) return;
        var newIndex = index + direction;
        if (newIndex < 0 || newIndex >= _selected.Items.Count) return;

        var item = _selected.Items[index];
        _selected.Items.RemoveAt(index);
        _selected.Items.Insert(newIndex, item);
        _selected.SelectedIndex = newIndex;
        Changed?.Invoke();
    }

    private void UpdateEnabled()
    {
        var enabled = IsMode2;
        _available.Enabled = enabled;
        _selected.Enabled = enabled;
        _addField.Enabled = enabled;
        _removeField.Enabled = enabled;
        _moveUp.Enabled = enabled;
        _moveDown.Enabled = enabled;
        Separator.Enabled = enabled;
        DateFormat.Enabled = enabled;
        _literal.Enabled = enabled;
        _addText.Enabled = enabled;
    }

    public List<NamingSegment> BuildSegments() =>
        _selected.Items.Cast<FieldPickerItem>()
            .Select(i => new NamingSegment
            {
                Type = i.IsLiteral ? NamingSegmentType.Text : NamingSegmentType.Field,
                Value = i.Key,
            })
            .ToList();

    /// <summary>Setzt Modus, Auswahl, Trennzeichen und Datumsformat - z.B. beim Laden aus appsettings.json oder als Startdefault.</summary>
    public void Apply(bool isMode2, List<NamingSegment> segments, string separator, string dateFormat)
    {
        Mode1.Checked = !isMode2;
        Mode2.Checked = isMode2;
        Separator.Text = separator;
        DateFormat.Text = dateFormat;

        PopulateAvailable();
        foreach (var segment in segments)
        {
            if (segment.Type == NamingSegmentType.Text)
            {
                _selected.Items.Add(new FieldPickerItem(segment.Value, segment.Value, IsLiteral: true));
                continue;
            }

            var item = _available.Items.Cast<FieldPickerItem>().FirstOrDefault(i => i.Key == segment.Value);
            if (item is null) continue;
            _available.Items.Remove(item);
            _selected.Items.Add(item);
        }

        UpdateEnabled();
        Changed?.Invoke();
    }
}
