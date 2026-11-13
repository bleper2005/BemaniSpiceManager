using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace BemaniSpiceManager.Ui;

/// <summary>Small helpers so module pages can be built in code with a consistent, plain look.</summary>
public static class Ui
{
    public static TextBlock Header(string text) => new()
    {
        Text = text, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 6),
    };

    public static TextBlock Note(string text) => new()
    {
        Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 2, 0, 6),
    };

    public static Button Button(string text, Action onClick, string? tip = null)
    {
        var b = new Button { Content = text, Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 6, 0), ToolTip = tip };
        b.Click += (_, _) => onClick();
        return b;
    }

    public static StackPanel Row(params UIElement[] items)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
        foreach (var i in items) p.Children.Add(i);
        return p;
    }

    /// <summary>A labelled setting row: label on the left, editor on the right, description below.</summary>
    public static FrameworkElement Setting(string label, FrameworkElement editor, string? desc = null)
    {
        var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var l = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, ToolTip = desc };
        g.Children.Add(l);
        var right = new StackPanel();
        right.Children.Add(editor);
        if (!string.IsNullOrWhiteSpace(desc))
            right.Children.Add(new TextBlock { Text = desc, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray, FontSize = 11 });
        Grid.SetColumn(right, 1);
        g.Children.Add(right);
        return g;
    }

    public static CheckBox Check(bool value, Action<bool> changed, string? text = null)
    {
        var c = new CheckBox { IsChecked = value, Content = text, VerticalAlignment = VerticalAlignment.Center };
        c.Click += (_, _) => changed(c.IsChecked == true);
        return c;
    }

    public static TextBox Text(string value, Action<string> changed, double width = 260)
    {
        var t = new TextBox { Text = value, Width = width, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(2) };
        t.TextChanged += (_, _) => changed(t.Text);
        return t;
    }

    /// <summary>Path text box with a Browse… button, so paths never need typing.</summary>
    public static FrameworkElement FolderPicker(string value, Action<string> changed, string title)
    {
        var t = new TextBox { Text = value, Width = 380, Padding = new Thickness(2) };
        t.TextChanged += (_, _) => changed(t.Text);
        var b = Button("Browse…", () =>
        {
            var d = new OpenFolderDialog { Title = title };
            if (System.IO.Directory.Exists(t.Text)) d.InitialDirectory = t.Text;
            if (d.ShowDialog() == true) t.Text = d.FolderName;
        });
        b.Margin = new Thickness(6, 0, 0, 0);
        return Row(t, b);
    }

    public static TextBlock Status(string text, bool good) => new()
    {
        Text = text, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center,
        Foreground = good ? Brushes.ForestGreen : Brushes.Firebrick, Margin = new Thickness(0, 0, 10, 0),
    };

    public static void Error(string message) =>
        MessageBox.Show(message, "Bemani Spice Manager", MessageBoxButton.OK, MessageBoxImage.Warning);

    public static bool Confirm(string message) =>
        MessageBox.Show(message, "Bemani Spice Manager", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
