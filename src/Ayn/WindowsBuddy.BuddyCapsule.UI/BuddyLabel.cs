using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyLabel : FrameworkElement
{
	private string _text = string.Empty;

	private Brush _foreground = BuddyTokens.Text38Brush;

	private double _fontSize = 11.0;

	private FontWeight _fontWeight = AynType.Medium;

	private FontFamily _fontFamily = BuddyTokens.FontDisplay;

	private double _tracking;

	private string _raw = string.Empty;

	private bool _uppercase;

	public string Text
	{
		get
		{
			return _text;
		}
		set
		{
			_raw = value ?? string.Empty;
			string text = (_uppercase ? _raw.ToUpperInvariant() : _raw);
			if (_text != text)
			{
				_text = text;
				InvalidateMeasure();
				InvalidateVisual();
			}
		}
	}

	public bool Uppercase
	{
		get
		{
			return _uppercase;
		}
		set
		{
			if (_uppercase != value)
			{
				_uppercase = value;
				Text = _raw;
			}
		}
	}

	public bool TabularDigits { get; set; }

	public Brush Foreground
	{
		get
		{
			return _foreground;
		}
		set
		{
			if (_foreground != value)
			{
				_foreground = value;
				InvalidateVisual();
			}
		}
	}

	public double FontSize
	{
		get
		{
			return _fontSize;
		}
		set
		{
			if (_fontSize != value)
			{
				_fontSize = value;
				InvalidateMeasure();
				InvalidateVisual();
			}
		}
	}

	public FontWeight FontWeight
	{
		get
		{
			return _fontWeight;
		}
		set
		{
			if (_fontWeight != value)
			{
				_fontWeight = value;
				InvalidateMeasure();
				InvalidateVisual();
			}
		}
	}

	public FontFamily FontFamily
	{
		get
		{
			return _fontFamily;
		}
		set
		{
			if (_fontFamily != value)
			{
				_fontFamily = value;
				InvalidateMeasure();
				InvalidateVisual();
			}
		}
	}

	public double Tracking
	{
		get
		{
			return _tracking;
		}
		set
		{
			if (_tracking != value)
			{
				_tracking = value;
				InvalidateMeasure();
				InvalidateVisual();
			}
		}
	}

	private double DigitAdvance(GlyphTypeface gt)
	{
		List<double> list = new List<double>(10);
		for (char c = '0'; c <= '9'; c = (char)(c + 1))
		{
			if (gt.CharacterToGlyphMap.TryGetValue(c, out var value))
			{
				list.Add(gt.AdvanceWidths[value]);
			}
		}
		if (list.Count == 0)
		{
			return 0.6 * _fontSize;
		}
		list.Sort();
		double num = ((list.Count % 2 == 1) ? list[list.Count / 2] : ((list[list.Count / 2 - 1] + list[list.Count / 2]) / 2.0));
		return num * _fontSize;
	}

	public BuddyLabel()
		: this(string.Empty)
	{
	}

	public BuddyLabel(string text, Brush foreground = null)
	{
		base.SnapsToDevicePixels = true;
		base.UseLayoutRounding = true;
		if (foreground != null)
		{
			_foreground = foreground;
		}
		Text = text;
	}

	public BuddyLabel(string text, Brush foreground, double fontSize, FontWeight fontWeight)
	{
		base.SnapsToDevicePixels = true;
		base.UseLayoutRounding = true;
		if (foreground != null)
		{
			_foreground = foreground;
		}
		_fontSize = fontSize;
		_fontWeight = fontWeight;
		Text = text;
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		if (string.IsNullOrEmpty(_text))
		{
			return new Size(0.0, Math.Ceiling(_fontSize * 1.3));
		}
		Typeface typeface = new Typeface(_fontFamily, FontStyles.Normal, AynType.Calm(_fontWeight, _fontSize), FontStretches.Normal);
		if (typeface.TryGetGlyphTypeface(out var glyphTypeface))
		{
			double num = 0.0;
			double num2 = (TabularDigits ? DigitAdvance(glyphTypeface) : 0.0);
			for (int i = 0; i < _text.Length; i++)
			{
				if (glyphTypeface.CharacterToGlyphMap.TryGetValue(_text[i], out var value))
				{
					double num3 = ((TabularDigits && char.IsDigit(_text[i])) ? num2 : (glyphTypeface.AdvanceWidths[value] * _fontSize));
					num += num3 + ((i < _text.Length - 1) ? _tracking : 0.0);
				}
				else
				{
					num += _fontSize * 0.6 + ((i < _text.Length - 1) ? _tracking : 0.0);
				}
			}
			double a = glyphTypeface.Height * _fontSize;
			return new Size(Math.Ceiling(num), Math.Ceiling(a));
		}
		FormattedText formattedText = new FormattedText(_text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, _fontSize, _foreground);
		double num4 = ((_text.Length > 1) ? ((double)(_text.Length - 1) * _tracking) : 0.0);
		return new Size(Math.Ceiling(formattedText.Width + num4), Math.Ceiling(formattedText.Height));
	}

	protected override void OnRender(DrawingContext dc)
	{
		if (string.IsNullOrEmpty(_text))
		{
			return;
		}
		Typeface typeface = new Typeface(_fontFamily, FontStyles.Normal, AynType.Calm(_fontWeight, _fontSize), FontStretches.Normal);
		if (typeface.TryGetGlyphTypeface(out var glyphTypeface))
		{
			try
			{
				ushort[] array = new ushort[_text.Length];
				double[] array2 = new double[_text.Length];
				Point[] array3 = (TabularDigits ? new Point[_text.Length] : null);
				double num = (TabularDigits ? DigitAdvance(glyphTypeface) : 0.0);
				double y = Math.Round(glyphTypeface.Baseline * _fontSize);
				for (int i = 0; i < _text.Length; i++)
				{
					if (!glyphTypeface.CharacterToGlyphMap.TryGetValue(_text[i], out var value))
					{
						value = 0;
					}
					array[i] = value;
					double num2 = (glyphTypeface.AdvanceWidths.ContainsKey(value) ? glyphTypeface.AdvanceWidths[value] : 0.6) * _fontSize;
					if (TabularDigits && char.IsDigit(_text[i]))
					{
						ref Point reference = ref array3[i];
						reference = new Point((num - num2) / 2.0, 0.0);
						num2 = num;
					}
					array2[i] = num2 + _tracking;
				}
				GlyphRun glyphRun = new GlyphRun(glyphTypeface, 0, isSideways: false, _fontSize, array, new Point(0.0, y), array2, array3, null, null, null, null, null);
				dc.DrawGlyphRun(_foreground, glyphRun);
				return;
			}
			catch
			{
			}
		}
		double num3 = 0.0;
		for (int j = 0; j < _text.Length; j++)
		{
			FormattedText formattedText = new FormattedText(_text[j].ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, _fontSize, _foreground);
			dc.DrawText(formattedText, new Point(num3, 0.0));
			num3 += formattedText.WidthIncludingTrailingWhitespace + _tracking;
		}
	}
}
