using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public static class BuddySlider
{
	private static ControlTemplate _cachedSliderTemplate;

	public static Slider Create(double min, double max, double value, Action<double> onValueChanged)
	{
		Slider slider = new Slider();
		slider.Minimum = min;
		slider.Maximum = max;
		slider.Value = value;
		slider.Height = 22.0;
		slider.Cursor = Cursors.Hand;
		slider.IsSnapToTickEnabled = false;
		slider.Focusable = true;
		slider.FocusVisualStyle = null;
		slider.VerticalAlignment = VerticalAlignment.Center;
		Slider slider2 = slider;
		if (_cachedSliderTemplate == null)
		{
			string name = typeof(ThemeTokens).Assembly.GetName().Name;
			string xamlText = $"\r\n<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'\r\n                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'\r\n                 xmlns:t='clr-namespace:WindowsBuddy.BuddyCapsule.Theme;assembly={name}'\r\n                 TargetType='Slider'>" + "\r\n    <Grid VerticalAlignment='Center' Height='22'>\r\n        <Border Height='3' Background='{Binding Source={x:Static t:ThemeTokens.Track}}' CornerRadius='1.5' VerticalAlignment='Center' />\r\n        <Track x:Name='PART_Track'>\r\n            <Track.DecreaseRepeatButton>\r\n                <RepeatButton Command='Slider.DecreaseLarge' Focusable='False'>\r\n                    <RepeatButton.Template>\r\n                        <ControlTemplate TargetType='RepeatButton'>\r\n                            <Border Background='{Binding Source={x:Static t:ThemeTokens.Accent}}' Height='3' CornerRadius='1.5,0,0,1.5' />\r\n                        </ControlTemplate>\r\n                    </RepeatButton.Template>\r\n                </RepeatButton>\r\n            </Track.DecreaseRepeatButton>\r\n            <Track.Thumb>\r\n                <Thumb Focusable='False' Cursor='Hand'>\r\n                    <Thumb.Template>\r\n                        <ControlTemplate TargetType='Thumb'>\r\n                            <Grid Width='14' Height='14'>\r\n                                <Ellipse Width='12' Height='12' Fill='{Binding Source={x:Static t:ThemeTokens.Text}}' Stroke='{Binding Source={x:Static t:ThemeTokens.BorderStrong}}' StrokeThickness='1' />\r\n                            </Grid>\r\n                        </ControlTemplate>\r\n                    </Thumb.Template>\r\n                </Thumb>\r\n            </Track.Thumb>\r\n            <Track.IncreaseRepeatButton>\r\n                <RepeatButton Command='Slider.IncreaseLarge' Focusable='False'>\r\n                    <RepeatButton.Template>\r\n                        <ControlTemplate TargetType='RepeatButton'>\r\n                            <Border Background='Transparent' Height='3' />\r\n                        </ControlTemplate>\r\n                    </RepeatButton.Template>\r\n                </RepeatButton>\r\n            </Track.IncreaseRepeatButton>\r\n        </Track>\r\n    </Grid>\r\n</ControlTemplate>";
			try
			{
				_cachedSliderTemplate = (ControlTemplate)XamlReader.Parse(xamlText);
			}
			catch
			{
			}
		}
		if (_cachedSliderTemplate != null)
		{
			slider2.Template = _cachedSliderTemplate;
		}
		if (onValueChanged != null)
		{
			slider2.ValueChanged += delegate(object s, RoutedPropertyChangedEventArgs<double> e)
			{
				onValueChanged(e.NewValue);
			};
		}
		return slider2;
	}
}
