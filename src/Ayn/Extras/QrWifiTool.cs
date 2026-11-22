using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using QRCoder;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.Tools.Personal;

public sealed class QrWifiTool : AynPersonalToolBase, IDisposable
{
    private System.Windows.Controls.TextBox _input;
    private Image _image;
    private TextBlock _status;
    private ComboBox _profiles;
    private byte[] _qrPng;
    private TcpListener _server;
    private bool _sharing;
    private string _shareToken = "";
    private DateTime _shareDeadline;
    private readonly SemaphoreSlim _clientSlots = new(8);
    private readonly object _pngLock = new();
    private readonly DispatcherTimer _shareTimer;

    public override string Id => "qr";
    public override string DisplayName => "QR & Wi-Fi Share";
    public override bool RequiresNetwork => true;

    public override double DesiredWidth => 620.0;
    public override double DesiredHeight => 380.0;

    public QrWifiTool()
    {
        OrderIndex = 10;
        _shareTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _shareTimer.Tick += (_, _) =>
        {
            if (_sharing && DateTime.Now >= _shareDeadline) StopShare("Share link expired.");
        };
    }

    public override UIElement CreateIcon(double size, Brush stroke) => VectorIcons.CreateGridMenuIcon(size, stroke);

    protected override FrameworkElement BuildView()
    {
        var root = new StackPanel { Margin = new Thickness(18, 12, 18, 12) };
        root.Children.Add(BreaksTool.Heading("QR & Wi-Fi Share"));

        _input = new System.Windows.Controls.TextBox
        {
            Height = 30,
            Margin = new Thickness(0, 12, 0, 0),
            Background = ThemeTokens.Surface2,
            Foreground = ThemeTokens.Text,
            BorderBrush = ThemeTokens.Border
        };
        root.Children.Add(_input);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        row.Children.Add(BreaksTool.ActionButton("Generate QR", Generate));
        row.Children.Add(BreaksTool.ActionButton("Save PNG", SavePng));
        root.Children.Add(row);

        _image = new Image { Width = 180, Height = 180, Margin = new Thickness(0, 10, 0, 0), Stretch = Stretch.Uniform };
        root.Children.Add(_image);

        _status = new TextBlock { Foreground = ThemeTokens.Text2, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        root.Children.Add(_status);

        root.Children.Add(new TextBlock
        {
            Text = "Wi-Fi passwords on this PC",
            Foreground = ThemeTokens.Text,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 14, 0, 6)
        });
        _profiles = new ComboBox { Width = 260, Height = 28, Foreground = ThemeTokens.Text, Background = ThemeTokens.Surface2 };
        root.Children.Add(_profiles);
        var wifiRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        wifiRow.Children.Add(BreaksTool.ActionButton("Make Wi-Fi QR", UseProfile));
        wifiRow.Children.Add(BreaksTool.ActionButton("Share on LAN", ToggleShare));
        root.Children.Add(wifiRow);
        LoadProfiles();
        return root;
    }

    private void Generate()
    {
        var text = _input.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            _status.Text = "Type something or choose a Wi-Fi profile first.";
            return;
        }
        try
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            var png = new PngByteQRCode(data).GetGraphic(8);
            lock (_pngLock) _qrPng = png;
            var bitmap = new BitmapImage();
            using (var ms = new MemoryStream(png))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
            }
            bitmap.Freeze();
            _image.Source = bitmap;
            _status.Text = $"QR ready for {text.Length} characters.";
        }
        catch (Exception ex) { _status.Text = "QR failed: " + ex.Message; }
    }

    private void SavePng()
    {
        byte[] png;
        lock (_pngLock) png = _qrPng;
        if (png == null) { _status.Text = "Generate a QR first."; return; }
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Ayn-QR.png");
            File.WriteAllBytes(path, png);
            _status.Text = "Saved to " + path + ". Delete this file when you are done — Wi-Fi QRs contain the password.";
        }
        catch (Exception ex) { _status.Text = "Save failed: " + ex.Message; }
    }

    private void LoadProfiles()
    {
        _profiles.Items.Clear();
        try
        {
            foreach (var line in RunNetsh(new[] { "wlan", "show", "profiles" }).Split('\n'))
            {
                var idx = line.IndexOf(':');
                if (idx < 0) continue;
                var name = line.Substring(idx + 1).Trim();
                if (IsSafeProfileName(name)) _profiles.Items.Add(name);
            }
            if (_profiles.Items.Count > 0) _profiles.SelectedIndex = 0;
        }
        catch { }
    }

    private static bool IsSafeProfileName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= 32 && Regex.IsMatch(name, @"^[A-Za-z0-9 _-]+$");

    private static string EscapeWifi(string value) =>
        value.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace(":", "\\:").Replace("\"", "\\\"");

    private void UseProfile()
    {
        var name = _profiles.SelectedItem as string;
        if (!IsSafeProfileName(name)) { _status.Text = "Choose a valid saved Wi-Fi profile."; return; }
        try
        {
            var detail = RunNetsh(new[] { "wlan", "show", "profile", $"name={name}", "key=clear" });
            var password = "";
            foreach (var line in detail.Split('\n'))
            {
                if (line.IndexOf("Key Content", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var idx = line.IndexOf(':');
                    if (idx >= 0) password = line.Substring(idx + 1).Trim();
                }
            }
            if (string.IsNullOrEmpty(password))
            {
                _status.Text = "No saved password found for this network.";
                return;
            }
            _input.Text = $"WIFI:T:WPA;S:{EscapeWifi(name)};P:{EscapeWifi(password)};;";
            Generate();
            _status.Text = $"Wi-Fi QR for '{name}' is ready. Keep it private.";
        }
        catch (Exception ex) { _status.Text = "Could not read profile: " + ex.Message; }
    }

    private static string RunNetsh(string[] args)
    {
        var psi = new ProcessStartInfo("netsh")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var p = Process.Start(psi);
        return p?.StandardOutput.ReadToEnd() ?? "";
    }

    private void ToggleShare()
    {
        if (_sharing) { StopShare("LAN sharing stopped."); return; }
        byte[] png;
        lock (_pngLock) png = _qrPng;
        if (png == null) { _status.Text = "Generate a QR first, then share."; return; }
        try
        {
            _shareToken = Guid.NewGuid().ToString("N");
            _shareDeadline = DateTime.Now.AddMinutes(5);
            _server = new TcpListener(IPAddress.Any, 8787);
            _server.Start();
            _sharing = true;
            _shareTimer.Start();
            _ = Task.Run(ServeLoop);
            _status.Text = $"Share this for 5 minutes: http://{LocalIp()}:8787/{_shareToken}  (one-time link, keep it private)";
        }
        catch (Exception ex) { _status.Text = "Share failed: " + ex.Message; }
    }

    private void StopShare(string message)
    {
        _sharing = false;
        _shareTimer.Stop();
        _server?.Stop();
        _server = null;
        _shareToken = "";
        _status.Text = message;
    }

    private async Task ServeLoop()
    {
        while (_sharing && _server != null)
        {
            try
            {
                var client = await _server.AcceptTcpClientAsync();
                if (!await _clientSlots.WaitAsync(0)) { client.Dispose(); continue; }
                _ = Task.Run(() => { try { HandleClient(client); } finally { _clientSlots.Release(); } });
            }
            catch { break; }
        }
    }

    private void HandleClient(TcpClient client)
    {
        try
        {
            using (client)
            {
                client.ReceiveTimeout = 3000;
                client.SendTimeout = 3000;
                var stream = client.GetStream();
                var buffer = new byte[1024];
                var read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0) return;
                var request = Encoding.ASCII.GetString(buffer, 0, read);
                var parts = request.Split(' ');
                if (parts.Length < 2 || !parts[0].Equals("GET", StringComparison.OrdinalIgnoreCase)) return;
                var path = parts[1];
                var token = _shareToken;
                if (string.IsNullOrEmpty(token) || !path.StartsWith("/" + token, StringComparison.Ordinal)) return;

                byte[] png;
                lock (_pngLock) png = _qrPng;
                var isPng = path.EndsWith("/qr.png", StringComparison.OrdinalIgnoreCase);
                var body = isPng ? (png ?? []) : Encoding.UTF8.GetBytes(
                    "<!doctype html><html><head><meta charset='utf-8'><title>Ayn Wi-Fi</title></head>" +
                    "<body style='background:#0b0b0c;color:#fff;font-family:Arial;padding:40px'>" +
                    "<h2>Ayn Wi-Fi share</h2><p>Scan to join the network. This link expires in 5 minutes.</p>" +
                    $"<img src='/{token}/qr.png' width='280' height='280'/></body></html>");
                var type = isPng ? "image/png" : "text/html; charset=utf-8";
                var header = $"HTTP/1.1 200 OK\r\nContent-Type: {type}\r\nContent-Length: {body.Length}\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n";
                var bytes = Encoding.ASCII.GetBytes(header);
                stream.Write(bytes, 0, bytes.Length);
                stream.Write(body, 0, body.Length);
            }
        }
        catch { }
    }

    private static string LocalIp()
    {
        try
        {
            var address = Dns.GetHostAddresses(Dns.GetHostName())
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
            return address?.ToString() ?? "<this-pc>";
        }
        catch { return "<this-pc>"; }
    }

    public void Dispose()
    {
        _sharing = false;
        _shareTimer?.Stop();
        _server?.Stop();
        _clientSlots.Dispose();
    }
}
