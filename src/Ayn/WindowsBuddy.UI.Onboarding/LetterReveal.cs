using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.UI.Onboarding;

internal sealed class LetterReveal : FrameworkElement
{
	public const double Stagger = 40.0;

	public const double LetterMs = 420.0;

	public const double Rise = 6.0;

	public const double Soft = 1.6;

	private readonly Brush _ink;

	private readonly double _stagger;

	private readonly double _rise;

	private readonly double _letterMs;

	public static readonly DependencyProperty ClockProperty = DependencyProperty.Register("Clock", typeof(double), typeof(LetterReveal), new FrameworkPropertyMetadata(double.MaxValue, FrameworkPropertyMetadataOptions.AffectsRender));

	private readonly string _text;

	private readonly double _size;

	private readonly double _tracking;

	private readonly GlyphTypeface _gt;

	private readonly ushort[] _glyphs;

	private readonly double[] _x;

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

	public double Clock
	{
		get
		{
			return (double)GetValue(ClockProperty);
		}
		set
		{
			SetValue(ClockProperty, value);
		}
	}

	public double DurationMs => (double)Math.Max(0, _text.Length - 1) * _stagger + _letterMs;

	public LetterReveal(string text, double size, double trackingEm, Brush ink = null, double stagger = 40.0, double rise = 6.0, double letterMs = 420.0)
	{
		_text = text ?? "";
		_size = size;
		_tracking = trackingEm;
		_ink = ink;
		_stagger = stagger;
		_rise = rise;
		_letterMs = letterMs;
		base.IsHitTestVisible = false;
		AutomationProperties.SetName(this, _text);
		Typeface typeface = new Typeface(BuddyFonts.Geist, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
		if (!typeface.TryGetGlyphTypeface(out var glyphTypeface))
		{
			return;
		}
		_gt = glyphTypeface;
		_glyphs = new ushort[_text.Length];
		_x = new double[_text.Length];
		double num = 0.0;
		for (int i = 0; i < _text.Length; i++)
		{
			if (!glyphTypeface.CharacterToGlyphMap.TryGetValue(_text[i], out var value))
			{
				value = (ushort)(glyphTypeface.CharacterToGlyphMap.ContainsKey(63) ? glyphTypeface.CharacterToGlyphMap[63] : 0);
			}
			_glyphs[i] = value;
			_x[i] = num;
			num += glyphTypeface.AdvanceWidths[value] * size + trackingEm * size;
		}
		_w = Math.Max(0.0, num - trackingEm * size);
		_h = Math.Ceiling(((glyphTypeface.Height > 0.0) ? glyphTypeface.Height : 1.2) * size);
	}

	public void Start(double delayMs)
	{
		double value = delayMs + DurationMs;
		DoubleAnimation doubleAnimation = new DoubleAnimation(0.0 - delayMs, DurationMs, new Duration(TimeSpan.FromMilliseconds(value)));
		doubleAnimation.Completed += delegate
		{
			Settle();
		};
		BeginAnimation(ClockProperty, doubleAnimation);
	}

	public void Settle()
	{
		BeginAnimation(ClockProperty, null);
		Clock = double.MaxValue;
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		return new Size(Math.Ceiling(_w), _h);
	}

	protected override void OnRender(DrawingContext dc)
	{
		if (_gt == null)
		{
			return;
		}
		double num = 1.0;
		try
		{
			num = VisualTreeHelper.GetDpi(this).PixelsPerDip;
		}
		catch
		{
		}
		double clock = Clock;
		double num2 = _gt.Baseline * _size;
		Brush foregroundBrush = _ink ?? ThemeTokens.Text;
		for (int i = 0; i < _glyphs.Length; i++)
		{
			if (_text[i] == ' ')
			{
				continue;
			}
			double num3 = ((clock == double.MaxValue) ? 1.0 : Math.Max(0.0, Math.Min(1.0, (clock - (double)i * _stagger) / _letterMs)));
			if (num3 <= 0.0)
			{
				continue;
			}
			double num4 = 1.0 - Math.Pow(1.0 - num3, 3.0);
			double y = num2 + _rise * (1.0 - num4);
			double num5 = 1.6 * (1.0 - num4);
			GlyphTypeface gt = _gt;
			double size = _size;
			float pixelsPerDip = (float)num;
			ushort[] glyphIndices = new ushort[1] { _glyphs[i] };
			Point baselineOrigin = new Point(_x[i], y);
			double[] advanceWidths = new double[1];
			GlyphRun glyphRun = new GlyphRun(gt, 0, isSideways: false, size, pixelsPerDip, glyphIndices, baselineOrigin, advanceWidths, null, null, null, null, null, null);
			if (num5 > 0.05)
			{
				dc.PushOpacity(0.22 * num4);
				Vector[] array = new Vector[4]
				{
					new Vector(0.0 - num5, 0.0),
					new Vector(num5, 0.0),
					new Vector(0.0, 0.0 - num5),
					new Vector(0.0, num5)
				};
				for (int j = 0; j < array.Length; j++)
				{
					Vector vector = array[j];
					dc.PushTransform(new TranslateTransform(vector.X, vector.Y));
					dc.DrawGlyphRun(foregroundBrush, glyphRun);
					dc.Pop();
				}
				dc.Pop();
			}
			dc.PushOpacity(num4 * (1.0 - 0.35 * (1.0 - num4)));
			dc.DrawGlyphRun(foregroundBrush, glyphRun);
			dc.Pop();
		}
	}
}
