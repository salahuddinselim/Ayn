using System;
using System.Drawing;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.UI.Settings;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyCustomColorPickerWindow : Window
{
	private const double SvWidth = 286.0;

	private const double SvHeight = 116.0;

	private const double HueWidth = 286.0;

	private const double HueHeight = 13.0;

	private readonly Action<System.Windows.Media.Color, string> _onApplied;

	private readonly Action<System.Windows.Media.Color, string> _onLivePreview;

	private readonly Action _onCancelled;

	private double _hue = 18.0;

	private double _sat = 0.76;

	private double _val = 1.0;

	private System.Windows.Media.Color _currentColor;

	private bool _isUpdating;

	private Border _svContainer;

	private System.Windows.Shapes.Rectangle _baseHueRect;

	private Border _svThumb;

	private Canvas _svCanvas;

	private Border _hueContainer;

	private Border _hueThumb;

	private Canvas _hueCanvas;

	private Border _previewBox;

	private System.Windows.Controls.TextBox _hexInput;

	public System.Windows.Media.Color CurrentColor => _currentColor;

	public string CurrentHex => $"#{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}";

	public BuddyCustomColorPickerWindow(System.Windows.Media.Color initialColor, Action<System.Windows.Media.Color, string> onApplied, Action<System.Windows.Media.Color, string> onLivePreview = null, Action onCancelled = null)
	{
		_onApplied = onApplied;
		_onLivePreview = onLivePreview;
		_onCancelled = onCancelled;
		_currentColor = initialColor;
		ColorToHSV(initialColor, out _hue, out _sat, out _val);
		base.Title = "Custom Color";
		base.WindowStyle = WindowStyle.None;
		base.AllowsTransparency = true;
		base.Background = System.Windows.Media.Brushes.Transparent;
		base.ShowInTaskbar = false;
		base.SizeToContent = SizeToContent.Height;
		base.Width = 320.0;
		base.WindowStartupLocation = WindowStartupLocation.Manual;
		base.SnapsToDevicePixels = true;
		base.UseLayoutRounding = true;
		SettingsTheme.PrepareRoot(this);
		Border border = new Border
		{
			Background = ThemeTokens.Surface,
			BorderBrush = ThemeTokens.BorderStrong,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(14.0),
			Padding = new Thickness(16.0),
			Width = 320.0
		};
		StackPanel stackPanel = new StackPanel();
		Grid grid = new Grid
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		TextBlock element = new TextBlock
		{
			Text = "Custom color",
			FontSize = 12.0,
			FontWeight = AynType.Medium,
			Foreground = ThemeTokens.Text2,
			FontFamily = SettingsTheme.Font,
			VerticalAlignment = VerticalAlignment.Center
		};
		Grid.SetColumn(element, 0);
		grid.Children.Add(element);
		_previewBox = new Border
		{
			Width = 30.0,
			Height = 15.0,
			CornerRadius = new CornerRadius(4.0),
			BorderBrush = ThemeTokens.BorderStrong,
			BorderThickness = new Thickness(1.0),
			Background = new SolidColorBrush(_currentColor),
			VerticalAlignment = VerticalAlignment.Center
		};
		Grid.SetColumn(_previewBox, 1);
		grid.Children.Add(_previewBox);
		stackPanel.Children.Add(grid);
		Func<string> read = () => _sat.ToString("R", CultureInfo.InvariantCulture) + "," + _val.ToString("R", CultureInfo.InvariantCulture);
		_svContainer = new SettingsColorPlane(read, delegate(string value)
		{
			string[] array = (value ?? "").Split(',');
			if (array.Length != 2 || !double.TryParse(array[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var result) || !double.TryParse(array[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2) || double.IsNaN(result) || double.IsNaN(result2) || result < 0.0 || result > 1.0 || result2 < 0.0 || result2 > 1.0)
			{
				throw new ArgumentException("Use saturation,brightness with values from 0 to 1.");
			}
			_sat = result;
			_val = result2;
			UpdateColorFromHsv(fireLivePreview: true);
		})
		{
			Width = 286.0,
			Height = 116.0,
			CornerRadius = new CornerRadius(8.0),
			ClipToBounds = true,
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0),
			Cursor = System.Windows.Input.Cursors.Cross,
			Background = System.Windows.Media.Brushes.Transparent
		};
		Grid grid2 = new Grid
		{
			Width = 286.0,
			Height = 116.0
		};
		_baseHueRect = new System.Windows.Shapes.Rectangle
		{
			Width = 286.0,
			Height = 116.0,
			Fill = new SolidColorBrush(ColorFromHSV(_hue, 1.0, 1.0))
		};
		grid2.Children.Add(_baseHueRect);
		LinearGradientBrush linearGradientBrush = new LinearGradientBrush
		{
			StartPoint = new System.Windows.Point(0.0, 0.0),
			EndPoint = new System.Windows.Point(1.0, 0.0)
		};
		linearGradientBrush.GradientStops.Add(new GradientStop(Colors.White, 0.0));
		linearGradientBrush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0, byte.MaxValue, byte.MaxValue, byte.MaxValue), 1.0));
		linearGradientBrush.Freeze();
		grid2.Children.Add(new System.Windows.Shapes.Rectangle
		{
			Width = 286.0,
			Height = 116.0,
			Fill = linearGradientBrush
		});
		LinearGradientBrush linearGradientBrush2 = new LinearGradientBrush
		{
			StartPoint = new System.Windows.Point(0.0, 0.0),
			EndPoint = new System.Windows.Point(0.0, 1.0)
		};
		linearGradientBrush2.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0, 0, 0, 0), 0.0));
		linearGradientBrush2.GradientStops.Add(new GradientStop(Colors.Black, 1.0));
		linearGradientBrush2.Freeze();
		grid2.Children.Add(new System.Windows.Shapes.Rectangle
		{
			Width = 286.0,
			Height = 116.0,
			Fill = linearGradientBrush2
		});
		_svCanvas = new Canvas
		{
			Width = 286.0,
			Height = 116.0,
			IsHitTestVisible = false
		};
		_svThumb = new Border
		{
			Width = 14.0,
			Height = 14.0,
			CornerRadius = new CornerRadius(7.0),
			BorderThickness = new Thickness(2.0),
			BorderBrush = System.Windows.Media.Brushes.White,
			Background = System.Windows.Media.Brushes.Transparent,
			Effect = new DropShadowEffect
			{
				Color = Colors.Black,
				BlurRadius = 4.0,
				ShadowDepth = 1.0,
				Opacity = 0.8
			}
		};
		_svCanvas.Children.Add(_svThumb);
		grid2.Children.Add(_svCanvas);
		_svContainer.Child = grid2;
		((SettingsColorSurface)_svContainer).BindIndicator(_svThumb);
		_svContainer.MouseLeftButtonDown += OnSvMouseDown;
		_svContainer.MouseMove += OnSvMouseMove;
		_svContainer.MouseLeftButtonUp += OnSvMouseUp;
		stackPanel.Children.Add(_svContainer);
		_hueContainer = new SettingsColorAxis(() => _hue, delegate(double value)
		{
			_hue = value % 360.0;
			_baseHueRect.Fill = new SolidColorBrush(ColorFromHSV(_hue, 1.0, 1.0));
			UpdateColorFromHsv(fireLivePreview: true);
		})
		{
			Width = 286.0,
			Height = 32.0,
			CornerRadius = new CornerRadius(6.5),
			ClipToBounds = true,
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0),
			Cursor = System.Windows.Input.Cursors.Hand,
			Background = System.Windows.Media.Brushes.Transparent
		};
		Grid grid3 = new Grid
		{
			Width = 286.0,
			Height = 13.0,
			VerticalAlignment = VerticalAlignment.Center
		};
		LinearGradientBrush linearGradientBrush3 = new LinearGradientBrush
		{
			StartPoint = new System.Windows.Point(0.0, 0.0),
			EndPoint = new System.Windows.Point(1.0, 0.0)
		};
		linearGradientBrush3.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromRgb(byte.MaxValue, 0, 0), 0.0));
		linearGradientBrush3.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromRgb(byte.MaxValue, byte.MaxValue, 0), 0.167));
		linearGradientBrush3.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromRgb(0, byte.MaxValue, 0), 0.333));
		linearGradientBrush3.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromRgb(0, byte.MaxValue, byte.MaxValue), 0.5));
		linearGradientBrush3.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromRgb(0, 0, byte.MaxValue), 0.667));
		linearGradientBrush3.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromRgb(byte.MaxValue, 0, byte.MaxValue), 0.833));
		linearGradientBrush3.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromRgb(byte.MaxValue, 0, 0), 1.0));
		linearGradientBrush3.Freeze();
		grid3.Children.Add(new System.Windows.Shapes.Rectangle
		{
			Width = 286.0,
			Height = 13.0,
			RadiusX = 6.5,
			RadiusY = 6.5,
			Fill = linearGradientBrush3
		});
		_hueCanvas = new Canvas
		{
			Width = 286.0,
			Height = 13.0,
			IsHitTestVisible = false
		};
		_hueThumb = new Border
		{
			Width = 7.0,
			Height = 17.0,
			CornerRadius = new CornerRadius(3.5),
			Background = ThemeTokens.Text,
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 20, 24)),
			BorderThickness = new Thickness(1.0),
			Effect = new DropShadowEffect
			{
				Color = Colors.Black,
				BlurRadius = 3.0,
				ShadowDepth = 1.0,
				Opacity = 0.6
			}
		};
		_hueCanvas.Children.Add(_hueThumb);
		grid3.Children.Add(_hueCanvas);
		_hueContainer.Child = grid3;
		((SettingsColorSurface)_hueContainer).BindIndicator(_hueThumb);
		_hueContainer.MouseLeftButtonDown += OnHueMouseDown;
		_hueContainer.MouseMove += OnHueMouseMove;
		_hueContainer.MouseLeftButtonUp += OnHueMouseUp;
		stackPanel.Children.Add(_hueContainer);
		Grid grid4 = new Grid
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 16.0)
		};
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(8.0, GridUnitType.Pixel)
		});
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		TextBlock element2 = new TextBlock
		{
			Text = "Hex",
			FontSize = 12.0,
			FontWeight = AynType.Medium,
			Foreground = ThemeTokens.Text2,
			FontFamily = SettingsTheme.Font,
			VerticalAlignment = VerticalAlignment.Center
		};
		Grid.SetColumn(element2, 0);
		grid4.Children.Add(element2);
		STextBox sTextBox = new STextBox($"#{initialColor.R:X2}{initialColor.G:X2}{initialColor.B:X2}", "#RRGGBB", double.NaN, "Hex color");
		_hexInput = sTextBox.Box;
		_hexInput.FontFamily = SettingsTheme.Font;
		AynType.Tabular(_hexInput);
		_hexInput.FontWeight = FontWeights.Medium;
		_hexInput.TextChanged += HexInput_TextChanged;
		Grid.SetColumn(sTextBox, 2);
		grid4.Children.Add(sTextBox);
		stackPanel.Children.Add(grid4);
		Grid grid5 = new Grid
		{
			ColumnDefinitions = 
			{
				new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				},
				new ColumnDefinition
				{
					Width = new GridLength(8.0, GridUnitType.Pixel)
				},
				new ColumnDefinition
				{
					Width = new GridLength(1.15, GridUnitType.Star)
				}
			}
		};
		SButton sButton = new SButton("Cancel")
		{
			HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
		};
		sButton.Click += delegate
		{
			if (_onCancelled != null)
			{
				_onCancelled();
			}
			base.DialogResult = false;
			Close();
		};
		Grid.SetColumn(sButton, 0);
		grid5.Children.Add(sButton);
		SButton sButton2 = new SButton("Apply color", SButtonKind.Primary)
		{
			HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
		};
		sButton2.Click += delegate
		{
			string arg = $"#{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}";
			if (_onApplied != null)
			{
				_onApplied(_currentColor, arg);
			}
			base.DialogResult = true;
			Close();
		};
		Grid.SetColumn(sButton2, 2);
		grid5.Children.Add(sButton2);
		stackPanel.Children.Add(grid5);
		border.Child = stackPanel;
		base.Content = border;
		base.PreviewKeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				if (_onCancelled != null)
				{
					_onCancelled();
				}
				base.DialogResult = false;
				Close();
			}
		};
		SettingsTheme.MakeFocusable(_svContainer, 6.0, "Saturation and brightness");
		SettingsTheme.MakeFocusable(_hueContainer, 6.0, "Hue");
		AutomationProperties.SetHelpText(_svContainer, "Left and Right change saturation. Up and Down change brightness. Value is saturation,brightness from 0 to 1.");
		AutomationProperties.SetHelpText(_hueContainer, "Left and Right change hue by one degree.");
		_svContainer.KeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == Key.Left)
			{
				_sat -= 0.01;
			}
			else if (e.Key == Key.Right)
			{
				_sat += 0.01;
			}
			else if (e.Key == Key.Down)
			{
				_val -= 0.01;
			}
			else
			{
				if (e.Key != Key.Up)
				{
					return;
				}
				_val += 0.01;
			}
			_sat = Math.Max(0.0, Math.Min(1.0, _sat));
			_val = Math.Max(0.0, Math.Min(1.0, _val));
			e.Handled = true;
			UpdateColorFromHsv(fireLivePreview: true);
		};
		_hueContainer.KeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == Key.Left)
			{
				_hue -= 1.0;
			}
			else
			{
				if (e.Key != Key.Right)
				{
					return;
				}
				_hue += 1.0;
			}
			_hue = (_hue + 360.0) % 360.0;
			_baseHueRect.Fill = new SolidColorBrush(ColorFromHSV(_hue, 1.0, 1.0));
			e.Handled = true;
			UpdateColorFromHsv(fireLivePreview: true);
		};
		UpdateVisualThumbs();
	}

	public void PositionBelow(UIElement anchor)
	{
		if (anchor == null)
		{
			return;
		}
		try
		{
			Window window = Window.GetWindow(anchor);
			if (window != null)
			{
				base.Owner = window;
			}
			base.WindowStartupLocation = WindowStartupLocation.Manual;
			PresentationSource presentationSource = PresentationSource.FromVisual(anchor) ?? ((window != null) ? PresentationSource.FromVisual(window) : null);
			double num = presentationSource?.CompositionTarget.TransformToDevice.M11 ?? 1.0;
			double num2 = presentationSource?.CompositionTarget.TransformToDevice.M22 ?? 1.0;
			if (num <= 0.0)
			{
				num = 1.0;
			}
			if (num2 <= 0.0)
			{
				num2 = 1.0;
			}
			System.Windows.Point point = anchor.PointToScreen(new System.Windows.Point(0.0, 0.0));
			System.Windows.Point point2 = anchor.PointToScreen(new System.Windows.Point(anchor.RenderSize.Width, anchor.RenderSize.Height));
			double num3 = point.X / num;
			double num4 = point.Y / num2;
			double num5 = point2.X / num;
			double num6 = point2.Y / num2;
			double num7 = (num3 + num5) / 2.0;
			double num8 = ((base.Width > 0.0) ? base.Width : 318.0);
			Measure(new System.Windows.Size(num8, double.PositiveInfinity));
			double num9 = ((base.DesiredSize.Height > 50.0) ? base.DesiredSize.Height : 310.0);
			double num10 = num7 - num8 / 2.0;
			double num11 = num6 + 8.0;
			Screen screen = Screen.FromPoint(new System.Drawing.Point((int)point.X, (int)point2.Y));
			System.Drawing.Rectangle workingArea = screen.WorkingArea;
			double num12 = (double)workingArea.Left / num;
			double num13 = (double)workingArea.Right / num;
			double num14 = (double)workingArea.Top / num2;
			double num15 = (double)workingArea.Bottom / num2;
			if (num10 + num8 > num13 - 12.0)
			{
				num10 = num13 - num8 - 12.0;
			}
			if (num10 < num12 + 12.0)
			{
				num10 = num12 + 12.0;
			}
			if (window != null && window.IsVisible)
			{
				double num16 = window.Left + window.ActualWidth - 12.0;
				double num17 = window.Left + 12.0;
				if (num10 + num8 > num16)
				{
					num10 = num16 - num8;
				}
				if (num10 < num17)
				{
					num10 = num17;
				}
			}
			bool flag = num11 + num9 <= num15 - 12.0;
			if (window != null && window.IsVisible && num11 + num9 > window.Top + window.ActualHeight - 12.0)
			{
				flag = false;
			}
			if (!flag)
			{
				num11 = num4 - num9 - 8.0;
				if (num11 < num14 + 12.0)
				{
					num11 = num14 + 12.0;
				}
				if (window != null && window.IsVisible && num11 < window.Top + 12.0)
				{
					num11 = window.Top + 12.0;
				}
			}
			base.Left = num10;
			base.Top = num11;
		}
		catch
		{
		}
	}

	private void OnSvMouseDown(object sender, MouseButtonEventArgs e)
	{
		_svContainer.Focus();
		_svContainer.CaptureMouse();
		UpdateSvFromMouse(e.GetPosition(_svContainer));
	}

	private void OnSvMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
	{
		if (_svContainer.IsMouseCaptured)
		{
			UpdateSvFromMouse(e.GetPosition(_svContainer));
		}
	}

	private void OnSvMouseUp(object sender, MouseButtonEventArgs e)
	{
		if (_svContainer.IsMouseCaptured)
		{
			_svContainer.ReleaseMouseCapture();
		}
	}

	private void UpdateSvFromMouse(System.Windows.Point p)
	{
		double num = Math.Max(0.0, Math.Min(286.0, p.X));
		double num2 = Math.Max(0.0, Math.Min(116.0, p.Y));
		_sat = num / 286.0;
		_val = 1.0 - num2 / 116.0;
		UpdateColorFromHsv(fireLivePreview: true);
	}

	private void OnHueMouseDown(object sender, MouseButtonEventArgs e)
	{
		_hueContainer.Focus();
		_hueContainer.CaptureMouse();
		UpdateHueFromMouse(e.GetPosition(_hueContainer));
	}

	private void OnHueMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
	{
		if (_hueContainer.IsMouseCaptured)
		{
			UpdateHueFromMouse(e.GetPosition(_hueContainer));
		}
	}

	private void OnHueMouseUp(object sender, MouseButtonEventArgs e)
	{
		if (_hueContainer.IsMouseCaptured)
		{
			_hueContainer.ReleaseMouseCapture();
		}
	}

	private void UpdateHueFromMouse(System.Windows.Point p)
	{
		double num = Math.Max(0.0, Math.Min(286.0, p.X));
		_hue = num / 286.0 * 360.0;
		if (_hue >= 360.0)
		{
			_hue = 359.9;
		}
		_baseHueRect.Fill = new SolidColorBrush(ColorFromHSV(_hue, 1.0, 1.0));
		UpdateColorFromHsv(fireLivePreview: true);
	}

	private void UpdateColorFromHsv(bool fireLivePreview)
	{
		_currentColor = ColorFromHSV(_hue, _sat, _val);
		string text = $"#{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}";
		_isUpdating = true;
		_hexInput.Text = text;
		_previewBox.Background = new SolidColorBrush(_currentColor);
		_isUpdating = false;
		UpdateVisualThumbs();
		if (fireLivePreview && _onLivePreview != null)
		{
			_onLivePreview(_currentColor, text);
		}
	}

	private void UpdateVisualThumbs()
	{
		double length = Math.Max(1.0, Math.Min(271.0, _sat * 286.0 - 7.0));
		double length2 = Math.Max(1.0, Math.Min(101.0, (1.0 - _val) * 116.0 - 7.0));
		Canvas.SetLeft(_svThumb, length);
		Canvas.SetTop(_svThumb, length2);
		double length3 = Math.Max(0.0, Math.Min(279.0, _hue / 360.0 * 286.0 - 3.5));
		Canvas.SetLeft(_hueThumb, length3);
		Canvas.SetTop(_hueThumb, -2.0);
	}

	private void HexInput_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (_isUpdating)
		{
			return;
		}
		string text = _hexInput.Text.Trim();
		if (!text.StartsWith("#"))
		{
			text = "#" + text;
		}
		if (text.Length != 4 && text.Length != 7)
		{
			return;
		}
		try
		{
			System.Windows.Media.Color color = (_currentColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(text));
			ColorToHSV(color, out _hue, out _sat, out _val);
			_baseHueRect.Fill = new SolidColorBrush(ColorFromHSV(_hue, 1.0, 1.0));
			_previewBox.Background = new SolidColorBrush(color);
			UpdateVisualThumbs();
			if (_onLivePreview != null)
			{
				_onLivePreview(color, text);
			}
		}
		catch
		{
		}
	}

	public static System.Windows.Media.Color ColorFromHSV(double hue, double saturation, double value)
	{
		int num = Convert.ToInt32(Math.Floor(hue / 60.0)) % 6;
		double num2 = hue / 60.0 - Math.Floor(hue / 60.0);
		value = Math.Max(0.0, Math.Min(1.0, value));
		saturation = Math.Max(0.0, Math.Min(1.0, saturation));
		byte b = Convert.ToByte(value * 255.0);
		byte b2 = Convert.ToByte(value * (1.0 - saturation) * 255.0);
		byte b3 = Convert.ToByte(value * (1.0 - num2 * saturation) * 255.0);
		byte b4 = Convert.ToByte(value * (1.0 - (1.0 - num2) * saturation) * 255.0);
		return num switch
		{
			0 => System.Windows.Media.Color.FromRgb(b, b4, b2), 
			1 => System.Windows.Media.Color.FromRgb(b3, b, b2), 
			2 => System.Windows.Media.Color.FromRgb(b2, b, b4), 
			3 => System.Windows.Media.Color.FromRgb(b2, b3, b), 
			4 => System.Windows.Media.Color.FromRgb(b4, b, b2), 
			_ => System.Windows.Media.Color.FromRgb(b, b2, b3), 
		};
	}

	public static void ColorToHSV(System.Windows.Media.Color color, out double hue, out double saturation, out double value)
	{
		double num = (double)(int)color.R / 255.0;
		double num2 = (double)(int)color.G / 255.0;
		double num3 = (double)(int)color.B / 255.0;
		double num4 = Math.Max(num, Math.Max(num2, num3));
		double num5 = Math.Min(num, Math.Min(num2, num3));
		double num6 = num4 - num5;
		value = num4;
		saturation = ((num4 <= 0.0001) ? 0.0 : (num6 / num4));
		if (num6 <= 0.0001)
		{
			hue = 0.0;
		}
		else if (Math.Abs(num4 - num) < 0.0001)
		{
			hue = (num2 - num3) / num6 % 6.0;
		}
		else if (Math.Abs(num4 - num2) < 0.0001)
		{
			hue = (num3 - num) / num6 + 2.0;
		}
		else
		{
			hue = (num - num2) / num6 + 4.0;
		}
		hue *= 60.0;
		if (hue < 0.0)
		{
			hue += 360.0;
		}
	}
}
