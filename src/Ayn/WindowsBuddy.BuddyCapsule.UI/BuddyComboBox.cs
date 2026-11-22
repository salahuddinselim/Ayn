using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyComboBox : ComboBox
{
	public BuddyComboBox()
	{
		base.Height = 30.0;
		base.FontSize = 13.0;
		base.FontFamily = BuddyTypography.FontText;
		base.Foreground = BuddyTokens.TextPrimaryBrush;
		base.Background = BuddyTokens.ButtonSecondarySurfaceBrush;
		base.BorderThickness = new Thickness(0.0);
		base.Focusable = true;
		base.FocusVisualStyle = null;
		base.Cursor = Cursors.Hand;
		ApplyDarkTemplate();
	}

	public static BuddyComboBox Create(double fontSize = 13.0, double width = double.NaN)
	{
		BuddyComboBox buddyComboBox = new BuddyComboBox();
		if (!double.IsNaN(fontSize) && fontSize > 0.0)
		{
			buddyComboBox.FontSize = fontSize;
		}
		if (!double.IsNaN(width) && width > 0.0)
		{
			buddyComboBox.Width = width;
		}
		return buddyComboBox;
	}

	private void ApplyDarkTemplate()
	{
		Style style = new Style(typeof(ComboBoxItem));
		style.Setters.Add(new Setter(Control.FontFamilyProperty, BuddyTypography.FontText));
		style.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
		style.Setters.Add(new Setter(Control.ForegroundProperty, ThemeTokens.Ref(BuddyTokens.TextPrimaryBrush)));
		style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0.0)));
		style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12.0, 6.0, 12.0, 6.0)));
		style.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
		ControlTemplate controlTemplate = new ControlTemplate(typeof(ComboBoxItem));
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory.Name = "ItemBorder";
		frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(10.0));
		frameworkElementFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
		frameworkElementFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
		frameworkElementFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(2.0, 1.0, 2.0, 1.0));
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory2.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		controlTemplate.VisualTree = frameworkElementFactory;
		Trigger trigger = new Trigger();
		trigger.Property = UIElement.IsMouseOverProperty;
		trigger.Value = true;
		Trigger trigger2 = trigger;
		trigger2.Setters.Add(new Setter(Control.BackgroundProperty, ThemeTokens.Ref(BuddyTokens.HoverSurfaceBrush)));
		controlTemplate.Triggers.Add(trigger2);
		Trigger trigger3 = new Trigger();
		trigger3.Property = ListBoxItem.IsSelectedProperty;
		trigger3.Value = true;
		Trigger trigger4 = trigger3;
		trigger4.Setters.Add(new Setter(Control.BackgroundProperty, ThemeTokens.Ref(BuddyTokens.ElevatedSurfaceBrush)));
		trigger4.Setters.Add(new Setter(Control.ForegroundProperty, ThemeTokens.Ref(ThemeTokens.Text)));
		trigger4.Setters.Add(new Setter(Control.FontWeightProperty, AynType.Medium));
		controlTemplate.Triggers.Add(trigger4);
		style.Setters.Add(new Setter(Control.TemplateProperty, controlTemplate));
		base.ItemContainerStyle = style;
		ControlTemplate controlTemplate2 = new ControlTemplate(typeof(ComboBox));
		FrameworkElementFactory frameworkElementFactory3 = new FrameworkElementFactory(typeof(Grid));
		FrameworkElementFactory frameworkElementFactory4 = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory4.Name = "MainBorder";
		frameworkElementFactory4.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
		frameworkElementFactory4.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
		frameworkElementFactory4.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
		frameworkElementFactory4.SetValue(Border.CornerRadiusProperty, new CornerRadius(12.0));
		frameworkElementFactory4.SetValue(Border.PaddingProperty, new Thickness(10.0, 2.0, 8.0, 2.0));
		FrameworkElementFactory frameworkElementFactory5 = new FrameworkElementFactory(typeof(DockPanel));
		frameworkElementFactory5.SetValue(DockPanel.LastChildFillProperty, true);
		FrameworkElementFactory frameworkElementFactory6 = new FrameworkElementFactory(typeof(Path));
		frameworkElementFactory6.SetValue(Path.DataProperty, Geometry.Parse("M 0 0 L 3.5 3.5 L 7 0"));
		frameworkElementFactory6.SetBinding(Shape.StrokeProperty, ThemeTokens.Ref(BuddyTokens.TextMutedBrush));
		frameworkElementFactory6.SetValue(Shape.StrokeThicknessProperty, 1.25);
		frameworkElementFactory6.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
		frameworkElementFactory6.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory6.SetValue(FrameworkElement.MarginProperty, new Thickness(6.0, 0.0, 2.0, 0.0));
		frameworkElementFactory6.SetValue(DockPanel.DockProperty, Dock.Right);
		frameworkElementFactory5.AppendChild(frameworkElementFactory6);
		FrameworkElementFactory frameworkElementFactory7 = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory7.Name = "PART_ContentPresenter";
		frameworkElementFactory7.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ComboBox.SelectionBoxItemProperty));
		frameworkElementFactory7.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(ComboBox.SelectionBoxItemTemplateProperty));
		frameworkElementFactory7.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory7.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
		frameworkElementFactory5.AppendChild(frameworkElementFactory7);
		frameworkElementFactory4.AppendChild(frameworkElementFactory5);
		frameworkElementFactory3.AppendChild(frameworkElementFactory4);
		FrameworkElementFactory frameworkElementFactory8 = new FrameworkElementFactory(typeof(Popup));
		frameworkElementFactory8.Name = "PART_Popup";
		frameworkElementFactory8.SetValue(Popup.IsOpenProperty, new TemplateBindingExtension(ComboBox.IsDropDownOpenProperty));
		frameworkElementFactory8.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
		frameworkElementFactory8.SetValue(Popup.AllowsTransparencyProperty, true);
		frameworkElementFactory8.SetValue(UIElement.FocusableProperty, false);
		FrameworkElementFactory frameworkElementFactory9 = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory9.SetBinding(Border.BackgroundProperty, ThemeTokens.Ref(BuddyTokens.ToolSurfaceBrush));
		frameworkElementFactory9.SetBinding(Border.BorderBrushProperty, ThemeTokens.Ref(BuddyTokens.BorderSubtleBrush));
		frameworkElementFactory9.SetValue(Border.BorderThicknessProperty, new Thickness(1.0));
		frameworkElementFactory9.SetValue(Border.CornerRadiusProperty, new CornerRadius(16.0));
		frameworkElementFactory9.SetValue(Border.PaddingProperty, new Thickness(4.0));
		frameworkElementFactory9.SetValue(FrameworkElement.MinHeightProperty, 30.0);
		frameworkElementFactory9.SetValue(FrameworkElement.MaxHeightProperty, 180.0);
		frameworkElementFactory9.SetValue(FrameworkElement.MinWidthProperty, 120.0);
		FrameworkElementFactory frameworkElementFactory10 = new FrameworkElementFactory(typeof(BuddyScrollViewer));
		FrameworkElementFactory child = new FrameworkElementFactory(typeof(ItemsPresenter));
		frameworkElementFactory10.AppendChild(child);
		frameworkElementFactory9.AppendChild(frameworkElementFactory10);
		frameworkElementFactory8.AppendChild(frameworkElementFactory9);
		frameworkElementFactory3.AppendChild(frameworkElementFactory8);
		controlTemplate2.VisualTree = frameworkElementFactory3;
		base.Template = controlTemplate2;
		base.MouseEnter += delegate
		{
			if (!base.IsFocused)
			{
				base.Background = new SolidColorBrush(Color.FromRgb(38, 38, 42));
			}
		};
		base.MouseLeave += delegate
		{
			if (!base.IsFocused)
			{
				base.Background = BuddyTokens.ButtonSecondarySurfaceBrush;
			}
		};
		base.GotFocus += delegate
		{
			base.BorderBrush = BuddyTokens.FocusRingBrush;
			base.BorderThickness = new Thickness(1.0);
		};
		base.LostFocus += delegate
		{
			base.BorderThickness = new Thickness(0.0);
			base.Background = BuddyTokens.ButtonSecondarySurfaceBrush;
		};
		base.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			base.IsDropDownOpen = !base.IsDropDownOpen;
			e.Handled = true;
		};
	}
}
