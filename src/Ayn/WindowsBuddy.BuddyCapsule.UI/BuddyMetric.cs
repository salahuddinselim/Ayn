using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyMetric : StackPanel
{
	private TextBlock _valueBlock;

	private TextBlock _labelBlock;

	public BuddyMetric(string label, string value, string unit = "")
	{
		base.Orientation = Orientation.Vertical;
		base.VerticalAlignment = VerticalAlignment.Center;
		_valueBlock = new TextBlock
		{
			Text = (string.IsNullOrEmpty(unit) ? value : (value + " " + unit)),
			FontFamily = BuddyTypography.FontDisplay,
			FontSize = 28.0,
			FontWeight = AynType.Semibold,
			Foreground = BuddyTokens.TextDisplayBrush,
			LineHeight = 34.0,
			LineStackingStrategy = LineStackingStrategy.BlockLineHeight
		};
		Typography.SetNumeralAlignment(_valueBlock, FontNumeralAlignment.Tabular);
		base.Children.Add(_valueBlock);
		if (!string.IsNullOrEmpty(label))
		{
			_labelBlock = BuddyTypography.CreateMicroLabel(label);
			_labelBlock.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
			base.Children.Add(_labelBlock);
		}
	}

	public void UpdateValue(string value, string unit = "")
	{
		_valueBlock.Text = (string.IsNullOrEmpty(unit) ? value : (value + " " + unit));
	}
}
