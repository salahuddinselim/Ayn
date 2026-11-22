using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using Microsoft.Win32;

namespace WindowsBuddy.Tools.Personal;

public sealed class DocConvertTool : AynPersonalToolBase
{
    private System.Windows.Controls.ComboBox _format;
    private TextBlock _status;
    private string _source = "";

    private static readonly string[] Formats = ["pdf", "docx", "xlsx", "pptx", "rtf", "txt", "html", "odt", "csv"];

    public DocConvertTool() => OrderIndex = 11;

    public override string Id => "docs";
    public override string DisplayName => "Doc Converter";

    public override double DesiredWidth => 560.0;
    public override double DesiredHeight => 300.0;

    public override UIElement CreateIcon(double size, Brush stroke) => VectorIcons.CreateFilesIcon(size, stroke);

    protected override FrameworkElement BuildView()
    {
        var root = new StackPanel { Margin = new Thickness(18, 12, 18, 12) };
        root.Children.Add(BreaksTool.Heading("Doc Converter"));

        _status = new TextBlock
        {
            Foreground = ThemeTokens.Text2,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 0)
        };
        root.Children.Add(_status);

        _format = new System.Windows.Controls.ComboBox { Width = 140, Height = 28, Foreground = ThemeTokens.Text, Background = ThemeTokens.Surface2 };
        foreach (var f in Formats) _format.Items.Add(f.ToUpperInvariant());
        _format.SelectedIndex = 0;

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        row.Children.Add(BreaksTool.ActionButton("Choose file", Choose));
        row.Children.Add(_format);
        row.Children.Add(BreaksTool.ActionButton("Convert", Convert));
        root.Children.Add(row);

        UpdateStatus();
        return root;
    }

    private void Choose()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a document",
            Filter = "Documents|*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.rtf;*.odt;*.txt;*.csv;*.html;*.htm|All files|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            _source = dialog.FileName;
            UpdateStatus();
        }
    }

    private static string? FindSoffice()
    {
        var candidates = new[]
        {
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
        };
        foreach (var c in candidates) if (File.Exists(c)) return c;
        return null;
    }

    private void Convert()
    {
        var soffice = FindSoffice();
        if (soffice == null)
        {
            _status.Text = "LibreOffice is not installed. Install it to convert documents: https://www.libreoffice.org";
            return;
        }
        if (string.IsNullOrEmpty(_source))
        {
            _status.Text = "Choose a document first.";
            return;
        }
        try
        {
            var target = Formats[_format.SelectedIndex];
            var outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Ayn Converted");
            Directory.CreateDirectory(outDir);
            var psi = new ProcessStartInfo(soffice)
            {
                ArgumentList =
                {
                    "--headless", "--norestore", "--convert-to", target,
                    "--outdir", outDir, _source
                },
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(psi);
            if (process != null && !process.WaitForExit(60000))
            {
                try { process.Kill(true); } catch { }
                _status.Text = "Conversion timed out and was stopped.";
                return;
            }
            var expected = Path.Combine(outDir, Path.GetFileNameWithoutExtension(_source) + "." + target);
            _status.Text = File.Exists(expected)
                ? "Saved: " + expected
                : "Conversion finished. Check " + outDir + " for the output.";
        }
        catch (Exception ex) { _status.Text = "Conversion failed: " + ex.Message; }
    }

    private void UpdateStatus()
    {
        var available = FindSoffice() != null;
        _status.Text = (string.IsNullOrEmpty(_source) ? "No document chosen." : "Source: " + Path.GetFileName(_source)) +
                       (available ? "\nLibreOffice found — 9 output formats available." : "\nInstall LibreOffice to enable document conversion.");
    }
}
