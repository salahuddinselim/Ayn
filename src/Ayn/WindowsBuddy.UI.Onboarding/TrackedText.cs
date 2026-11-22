using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.UI.Onboarding;

internal sealed class TrackedText : FrameworkElement
{
	private readonly string _text;

	private readonly double _size;

	private readonly double _tracking;

	private readonly GlyphTypeface _gt;

	private readonly ushort[] _glyphs;

	private readonly double[] _advances;

	private readonly FormattedText _fallback;

	private readonly double _w;

	private readonly double _h;

	public string Text => _text;

	internal string FamilyName
	{
		get
		{
			if (_gt == null)
			{
				return "(fallback)";
			}
			return _gt.FamilyNames.Values.FirstOrDefault();
		}
	}

	public TrackedText(string text, double size, double trackingEm)
	{
		_text = text;
		_size = size;
		_tracking = trackingEm;
		base.IsHitTestVisible = false;
		Typeface typeface = new Typeface(BuddyFonts.Geist, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
		if (typeface.TryGetGlyphTypeface(out var glyphTypeface))
		{
			_gt = glyphTypeface;
			_glyphs = new ushort[text.Length];
			_advances = new double[text.Length];
			double num = 0.0;
			for (int i = 0; i < text.Length; i++)
			{
				if (!glyphTypeface.CharacterToGlyphMap.TryGetValue(text[i], out var value))
				{
					value = (ushort)(glyphTypeface.CharacterToGlyphMap.ContainsKey(63) ? glyphTypeface.CharacterToGlyphMap[63] : 0);
				}
				_glyphs[i] = value;
				_advances[i] = glyphTypeface.AdvanceWidths[value] * size + trackingEm * size;
				num += _advances[i];
			}
			_w = Math.Max(0.0, num - trackingEm * size);
			_h = Math.Ceiling(((glyphTypeface.Height > 0.0) ? glyphTypeface.Height : 1.2) * size);
		}
		else
		{
			_fallback = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, size, Brushes.White);
			_w = _fallback.Width;
			_h = _fallback.Height;
		}
		base.OpacityMask = new LinearGradientBrush(Color.FromArgb(byte.MaxValue, 0, 0, 0), Color.FromArgb(217, 0, 0, 0), 90.0);
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		return new Size(Math.Ceiling(_w), _h);
	}

	protected override void OnRender(DrawingContext dc)
	{
		double num = 1.0;
		try
		{
			num = VisualTreeHelper.GetDpi(this).PixelsPerDip;
		}
		catch
		{
		}
		if (_gt != null)
		{
			GlyphRun glyphRun = new GlyphRun(_gt, 0, isSideways: false, _size, (float)num, _glyphs, new Point(0.0, _gt.Baseline * _size), _advances, null, null, null, null, null, null);
			dc.DrawGlyphRun(ThemeTokens.Text, glyphRun);
		}
		else if (_fallback != null)
		{
			FormattedText formattedText = new FormattedText(_text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(BuddyFonts.Geist, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), _size, ThemeTokens.Text, num);
			dc.DrawText(formattedText, new Point(0.0, 0.0));
		}
	}

	protected override AutomationPeer OnCreateAutomationPeer()
	{
		return new TrackedTextPeer(this);
	}
}
