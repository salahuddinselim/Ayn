using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyRow : Grid
{
	private readonly Border _bg;

	protected readonly Grid Leading;

	protected readonly Grid Trailing;

	protected readonly TextBlock TitleText;

	protected readonly TextBlock MetaText;

	private readonly Rectangle _divider;

	private readonly Rectangle _bar;

	private readonly bool _flat;

	private bool _selected;

	private bool _hover;

	internal BuddyVirtualList Owner;

	protected bool TitleMuted;

	public object Item { get; private set; }

	public bool IsSelected => _selected;

	protected virtual FrameworkElement ContextAnchor => this;

	public BuddyRow(double leadingWidth, double leadingGap, double trailingWidth, double inset)
		: this(leadingWidth, leadingGap, trailingWidth, inset, flat: false)
	{
	}

	public BuddyRow(double leadingWidth, double leadingGap, double trailingWidth, double inset, bool flat)
	{
		_flat = flat;
		base.Background = Brushes.Transparent;
		base.Cursor = Cursors.Hand;
		BuddyTheme.Crisp(this);
		_bg = new Border
		{
			CornerRadius = new CornerRadius(10.0),
			Background = Brushes.Transparent
		};
		base.Children.Add(_bg);
		Grid grid = new Grid
		{
			Margin = new Thickness(inset, 0.0, Math.Max(0.0, inset - 2.0), 0.0)
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(leadingWidth)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(trailingWidth)
		});
		base.Children.Add(grid);
		Leading = new Grid
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		grid.Children.Add(Leading);
		StackPanel stackPanel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(leadingGap, 0.0, 8.0, 0.0)
		};
		TitleText = BuddyTheme.BodyText(string.Empty);
		MetaText = BuddyTheme.SecondaryText(string.Empty);
		MetaText.Margin = new Thickness(0.0, 3.0, 0.0, 0.0);
		Typography.SetNumeralAlignment(MetaText, FontNumeralAlignment.Tabular);
		stackPanel.Children.Add(TitleText);
		stackPanel.Children.Add(MetaText);
		Grid.SetColumn(stackPanel, 1);
		grid.Children.Add(stackPanel);
		Trailing = new Grid
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		Grid.SetColumn(Trailing, 2);
		grid.Children.Add(Trailing);
		_divider = new Rectangle
		{
			Height = 1.0,
			Fill = BuddyTheme.Hairline,
			VerticalAlignment = VerticalAlignment.Bottom,
			Margin = new Thickness(inset, 0.0, inset, 0.0),
			IsHitTestVisible = false
		};
		base.Children.Add(_divider);
		if (flat)
		{
			_divider.Visibility = Visibility.Collapsed;
		}
		_bar = new Rectangle
		{
			Width = 2.0,
			RadiusX = 1.0,
			RadiusY = 1.0,
			Fill = BuddyTheme.Text100,
			HorizontalAlignment = HorizontalAlignment.Left,
			Margin = new Thickness(0.0, 8.0, 0.0, 8.0),
			IsHitTestVisible = false,
			Visibility = Visibility.Collapsed
		};
		base.Children.Add(_bar);
		base.MouseEnter += delegate
		{
			_hover = true;
			UpdateVisuals();
		};
		base.MouseLeave += delegate
		{
			_hover = false;
			UpdateVisuals();
		};
		base.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			if (Item != null && Owner != null)
			{
				e.Handled = true;
				Owner.RaiseItemClick(Item, e.ClickCount);
			}
		};
		base.MouseRightButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			if (Item != null && Owner != null)
			{
				e.Handled = true;
				Owner.RaiseItemContext(Item, ContextAnchor);
			}
		};
	}

	internal void Bind(object item, bool selected, bool lastInSection)
	{
		Item = item;
		_selected = selected;
		_hover = base.IsMouseOver;
		_divider.Visibility = ((lastInSection || _flat) ? Visibility.Collapsed : Visibility.Visible);
		OnBind(item);
		UpdateVisuals();
	}

	internal void Unbind()
	{
		OnUnbind();
		Item = null;
	}

	internal void SetSelected(bool selected)
	{
		if (_selected != selected)
		{
			_selected = selected;
			UpdateVisuals();
		}
	}

	protected virtual void OnBind(object item)
	{
	}

	protected virtual void OnUnbind()
	{
	}

	public virtual void Refresh()
	{
		if (Item != null)
		{
			OnBind(Item);
			UpdateVisuals();
		}
	}

	protected virtual void UpdateVisuals()
	{
		if (_flat)
		{
			_bar.Visibility = ((!_selected) ? Visibility.Collapsed : Visibility.Visible);
			_bg.Background = ((_hover || _selected) ? ThemeTokens.Selection : Brushes.Transparent);
			TitleText.Foreground = (TitleMuted ? BuddyTheme.Placeholder : ((_selected || _hover) ? BuddyTheme.Text100 : BuddyTheme.Text92));
		}
		else
		{
			_bg.Background = (_selected ? BuddyTheme.RowHover : (_hover ? BuddyTheme.Row : Brushes.Transparent));
			TitleText.Foreground = (TitleMuted ? BuddyTheme.Text50 : (_selected ? BuddyTheme.Text100 : BuddyTheme.Text92));
			_divider.Opacity = (_selected ? 0.0 : 1.0);
		}
	}
}
