using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.Tools.Calendar;

internal sealed class ReminderRow : Grid
{
	private const double CheckLen = 12.0;

	private readonly CalendarTool _tool;

	private readonly Ellipse _ring;

	private readonly Ellipse _fill;

	private readonly Path _check;

	private readonly TextBlock _title;

	private readonly TextBlock _meta;

	private readonly Border _hit;

	private UserReminder _r;

	public UserReminder Reminder => _r;

	public FrameworkElement CheckTarget => _hit;

	public ReminderRow(CalendarTool tool)
	{
		_tool = tool;
		base.Background = Brushes.Transparent;
		base.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(30.0)
		});
		base.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		base.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		Grid grid = new Grid
		{
			Width = 17.0,
			Height = 17.0,
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Center,
			IsHitTestVisible = false
		};
		_ring = new Ellipse
		{
			Stroke = BuddyTheme.Text50,
			StrokeThickness = 1.4
		};
		_fill = new Ellipse
		{
			Fill = ThemeTokens.Accent,
			Opacity = 0.0
		};
		_check = new Path
		{
			Data = Geometry.Parse("M 4.6,8.8 L 7.4,11.4 L 12.4,5.8"),
			Stroke = ThemeTokens.AccentFg,
			StrokeThickness = 1.8,
			StrokeStartLineCap = PenLineCap.Round,
			StrokeEndLineCap = PenLineCap.Round,
			StrokeLineJoin = PenLineJoin.Round,
			StrokeDashArray = new DoubleCollection { 12.0, 12.0 },
			StrokeDashOffset = 12.0,
			Opacity = 0.0
		};
		grid.Children.Add(_ring);
		grid.Children.Add(_fill);
		grid.Children.Add(_check);
		_hit = new Border
		{
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			Child = grid,
			Margin = new Thickness(-4.0, 0.0, 0.0, 0.0),
			Padding = new Thickness(4.0, 0.0, 6.0, 0.0)
		};
		Border hit = _hit;
		Action click = delegate
		{
			if (_r != null)
			{
				_tool.TickReminder(_r, this);
			}
		};
		CalendarTool.AttachClick(hit, click, null);
		_hit.MouseEnter += delegate
		{
			if (_r != null && !_r.Completed)
			{
				_ring.Stroke = BuddyTheme.Text100;
			}
		};
		_hit.MouseLeave += delegate
		{
			_ring.Stroke = BuddyTheme.Text50;
		};
		base.Children.Add(_hit);
		_title = BuddyTheme.Text("", 13.5, BuddyTheme.Text92, FontWeights.Normal);
		_title.VerticalAlignment = VerticalAlignment.Center;
		_title.TextTrimming = TextTrimming.CharacterEllipsis;
		Border border = new Border
		{
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			Child = _title
		};
		CalendarTool.AttachClick(border, delegate
		{
			if (_r != null)
			{
				_tool.OpenEditor(null, _r, _r.DueDate);
			}
		}, null);
		Grid.SetColumn(border, 1);
		base.Children.Add(border);
		_meta = BuddyTheme.Text("", 13.0, BuddyTheme.Text50, FontWeights.Normal);
		_meta.VerticalAlignment = VerticalAlignment.Center;
		_meta.Margin = new Thickness(10.0, 0.0, 0.0, 0.0);
		Grid.SetColumn(_meta, 2);
		base.Children.Add(_meta);
		base.MouseRightButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			if (_r != null)
			{
				_tool.ReminderMenu(_r, this);
			}
		};
		base.StylusSystemGesture += delegate(object s, StylusSystemGestureEventArgs e)
		{
			if ((e.SystemGesture == SystemGesture.HoldEnter || e.SystemGesture == SystemGesture.RightTap) && _r != null)
			{
				_tool.ReminderMenu(_r, this);
			}
		};
	}

	public void Bind(UserReminder r, DateTime now, bool ticking)
	{
		_r = r;
		bool flag = r.Completed || ticking;
		_title.Text = r.Title;
		_title.TextDecorations = (flag ? TextDecorations.Strikethrough : null);
		_title.Foreground = (flag ? BuddyTheme.Text50 : BuddyTheme.Text92);
		_fill.BeginAnimation(UIElement.OpacityProperty, null);
		_check.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
		_check.BeginAnimation(UIElement.OpacityProperty, null);
		_fill.Opacity = (flag ? 1 : 0);
		_check.Opacity = (flag ? 1 : 0);
		_check.StrokeDashOffset = (flag ? 0.0 : 12.0);
		_ring.Opacity = ((!flag) ? 1 : 0);
		base.ToolTip = ((r.Title.Length > 30) ? BuddyTooltip.Create(r.Title) : null);
		if (r.Completed)
		{
			_meta.Text = "";
			return;
		}
		bool flag2 = r.IsOverdue(now);
		_meta.Foreground = (flag2 ? CalendarTool.OverdueInk : BuddyTheme.Text50);
		if (flag2)
		{
			_meta.Text = "Overdue";
		}
		else if (r.Repeat != 0 && r.DueDate <= now.Date)
		{
			_meta.Text = (r.DueTime.HasValue ? Cal.Time(r.DueDate + r.DueTime.Value) : Cal.RepeatLabel(r.Repeat));
		}
		else if (r.Repeat != 0)
		{
			_meta.Text = Cal.RepeatLabel(r.Repeat);
		}
		else if (r.DueDate == now.Date)
		{
			_meta.Text = (r.DueTime.HasValue ? Cal.Time(r.DueDate + r.DueTime.Value) : "Today");
		}
		else if (r.DueDate == now.Date.AddDays(1.0))
		{
			_meta.Text = "Tomorrow";
		}
		else if ((r.DueDate - now.Date).TotalDays < 7.0)
		{
			_meta.Text = r.DueDate.ToString("ddd", Cal.Inv);
		}
		else
		{
			_meta.Text = r.DueDate.ToString((r.DueDate.Year == now.Year) ? "d MMM" : "d MMM yyyy", Cal.Inv);
		}
	}

	public void PlayTick()
	{
		IEasingFunction easeOut = BuddyTheme.EaseOut;
		_fill.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(140.0))
		{
			EasingFunction = easeOut
		});
		_check.Opacity = 1.0;
		_check.BeginAnimation(Shape.StrokeDashOffsetProperty, new DoubleAnimation(12.0, 0.0, TimeSpan.FromMilliseconds(240.0))
		{
			BeginTime = TimeSpan.FromMilliseconds(60.0),
			EasingFunction = easeOut
		});
		_ring.Opacity = 0.0;
		_title.TextDecorations = TextDecorations.Strikethrough;
		_title.Foreground = BuddyTheme.Text50;
	}
}
