using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyMetricDisplay : StackPanel
{
	private readonly TextBlock _valueBlock;

	private readonly TextBlock _labelBlock;

	public string Value
	{
		get
		{
			return _valueBlock.Text;
		}
		set
		{
			_valueBlock.Text = value ?? string.Empty;
		}
	}

	public string Label
	{
		get
		{
			if (_labelBlock == null)
			{
				return string.Empty;
			}
			return _labelBlock.Text;
		}
		set
		{
			if (_labelBlock != null)
			{
				_labelBlock.Text = value ?? string.Empty;
			}
		}
	}

	public BuddyMetricDisplay(string value, double fontSize = 20.0, string label = null, Brush valueBrush = null, Brush labelBrush = null)
	{
		base.Orientation = Orientation.Vertical;
		bool flag = fontSize >= 18.0;
		_valueBlock = new TextBlock
		{
			Text = value,
			FontFamily = (flag ? BuddyTypography.FontDisplay : BuddyTypography.FontText),
			FontSize = fontSize,
			FontWeight = AynType.Calm(FontWeights.SemiBold, fontSize),
			Foreground = (valueBrush ?? BuddyTokens.TextDisplayBrush),
			LineStackingStrategy = LineStackingStrategy.BlockLineHeight
		};
		BuddyTypography.SetTabularFigures(_valueBlock);
		base.Children.Add(_valueBlock);
		if (!string.IsNullOrEmpty(label))
		{
			_labelBlock = new TextBlock
			{
				Text = label,
				FontFamily = BuddyTypography.FontText,
				FontSize = 10.0,
				FontWeight = FontWeights.Normal,
				Foreground = (labelBrush ?? BuddyTokens.TextMutedBrush),
				Margin = new Thickness(0.0, 2.0, 0.0, 0.0)
			};
			base.Children.Add(_labelBlock);
		}
	}
}
