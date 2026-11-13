using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BemaniSpiceManager.Core;
using BemaniSpiceManager.Modules.Tools;
using static BemaniSpiceManager.Ui.Ui;

namespace BemaniSpiceManager.Pages;

/// <summary>All spice2x options for one game, grouped by spice2x's own categories.</summary>
public sealed class SpiceOptionsWindow : Window
{
    const string Changed = "★ Changed";
    const string All = "All options";

    readonly GameEntry _g;
    readonly List<SpiceOption> _options;
    readonly ListBox _cats = new() { Width = 200, BorderThickness = new Thickness(0, 0, 1, 0) };
    readonly StackPanel _list = new() { Margin = new Thickness(12, 6, 12, 12) };
    readonly TextBox _search = new() { Width = 220, Padding = new Thickness(2) };

    public SpiceOptionsWindow(GameEntry g, string? spiceGameName)
    {
        _g = g;
        _options = Spice2x.OptionsFor(spiceGameName).ToList();
        Title = $"spice2x options: {g.Name}";
        Width = 900; Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA));

        var cats = new List<string> { Changed, All };
        if (spiceGameName != null && _options.Any(o => o.Games.Count > 0)) cats.Add(spiceGameName);
        cats.AddRange(_options.Select(o => string.IsNullOrEmpty(o.Category) ? "Other" : o.Category).Distinct());
        foreach (var c in cats) _cats.Items.Add(c);
        _cats.SelectionChanged += (_, _) => Fill();

        _search.TextChanged += (_, _) => { if (_search.Text.Length > 0 && (string?)_cats.SelectedItem != All) _cats.SelectedItem = All; else Fill(); };

        var top = Row(new TextBlock { Text = "Search:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }, _search,
            Button("Reset all to default", () =>
            {
                if (_g.SpiceOptions.Count > 0 && Confirm("Clear every spice2x option set for this game?")) { _g.SpiceOptions.Clear(); Fill(); }
            }),
            Button("Close", Close));
        top.Margin = new Thickness(12, 8, 12, 4);

        var dock = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        dock.Children.Add(top);
        DockPanel.SetDock(_cats, Dock.Left);
        dock.Children.Add(_cats);
        dock.Children.Add(new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = dock;

        _cats.SelectedIndex = _g.SpiceOptions.Count > 0 ? 0 : (spiceGameName != null ? 2 : 1);
    }

    void Fill()
    {
        _list.Children.Clear();
        var cat = _cats.SelectedItem as string ?? All;
        var q = _search.Text.Trim();
        IEnumerable<SpiceOption> items = cat switch
        {
            Changed => _options.Where(o => _g.SpiceOptions.ContainsKey(o.Name)),
            All => _options,
            _ when _options.Any(o => o.Games.Contains(cat)) && !_options.Any(o => o.Category == cat) => _options.Where(o => o.Games.Contains(cat)),
            _ => _options.Where(o => (string.IsNullOrEmpty(o.Category) ? "Other" : o.Category) == cat),
        };
        if (q.Length > 0)
            items = items.Where(o => o.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                                  || o.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                                  || o.Desc.Contains(q, StringComparison.OrdinalIgnoreCase));
        var list = items.ToList();
        _list.Children.Add(Header(cat == Changed && list.Count == 0 ? "Nothing changed yet. Pick a category on the left." : cat));
        foreach (var o in list) _list.Children.Add(Editor(o));
    }

    FrameworkElement Editor(SpiceOption o)
    {
        _g.SpiceOptions.TryGetValue(o.Name, out var cur);
        void Set(string? v)
        {
            if (v == null) _g.SpiceOptions.Remove(o.Name); else _g.SpiceOptions[o.Name] = v;
        }
        FrameworkElement ed;
        switch (o.Type)
        {
            case "Bool":
                ed = Check(cur != null, v => Set(v ? "1" : null), "-" + o.Name);
                break;
            case "Enum":
                var cb = new ComboBox { Width = 300, HorizontalAlignment = HorizontalAlignment.Left };
                cb.Items.Add(new ComboBoxItem { Content = "(default)", Tag = null });
                foreach (var el in o.Elements)
                {
                    var label = el.Count > 1 && el[1].Length > 0 && el[1] != el[0] ? $"{el[0]}  –  {el[1]}" : el[0];
                    cb.Items.Add(new ComboBoxItem { Content = label, Tag = el[0] });
                }
                cb.SelectedIndex = 0;
                if (cur != null)
                {
                    var match = cb.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string?)i.Tag == cur);
                    if (match == null) { match = new ComboBoxItem { Content = cur, Tag = cur }; cb.Items.Add(match); }
                    cb.SelectedItem = match;
                }
                cb.SelectionChanged += (_, _) => Set((string?)((ComboBoxItem)cb.SelectedItem).Tag);
                ed = cb;
                break;
            default:
                var t = Text(cur ?? "", v => Set(string.IsNullOrWhiteSpace(v) ? null : v.Trim()), 300);
                ed = Row(t, Button("Clear", () => t.Text = ""));
                break;
        }
        var desc = o.Desc + (o.Type != "Bool" && o.Example.Length > 0 ? $"\nExample: {o.Example}" : "");
        return Setting(o.Title, ed, desc);
    }
}
