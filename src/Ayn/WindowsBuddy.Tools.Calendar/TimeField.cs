using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.Tools.Calendar;

internal sealed class TimeField : Border
{
	private readonly CalendarEditor _editor;

	private readonly TextBox _box;

	private readonly TextBlock _ph;

	private TimeSpan? _value;

	private bool _editing;

	public bool IsEnd { get; private set; }

	public bool AllowNone { get; set; }

	public string Error { get; private set; }

	public TextBox Box => _box;

	public TimeSpan? Value
	{
		get
		{
			return _value;
		}
		set
		{
			_value = value;
			_editing = false;
			Error = null;
			_box.Foreground = BuddyTheme.Text92;
			_box.Text = (value.HasValue ? Cal.Time(DateTime.Today + value.Value) : "");
		}
	}

	public event Action<TimeSpan?> Committed;

	public TimeField(CalendarEditor editor, bool isEnd)
	{
		_editor = editor;
		IsEnd = isEnd;
		base.Height = 28.0;
		base.Width = 112.0;
		base.CornerRadius = new CornerRadius(14.0);
		base.Background = CalendarTool.QuietFill;
		base.Padding = new Thickness(12.0, 0.0, 8.0, 0.0);
		base.Cursor = Cursors.IBeam;
		Grid grid = new Grid
		{
			ColumnDefinitions = 
			{
				new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				},
				new ColumnDefinition
				{
					Width = GridLength.Auto
				}
			}
		};
		_ph = BuddyTheme.Text("No time", 13.0, BuddyTheme.Text30, AynType.Medium);
		_ph.VerticalAlignment = VerticalAlignment.Center;
		_ph.IsHitTestVisible = false;
		grid.Children.Add(_ph);
		_box = CalendarTool.CleanBox(13.0);
		_box.FontWeight = AynType.Medium;
		_box.Foreground = BuddyTheme.Text92;
		Typography.SetNumeralAlignment(_box, FontNumeralAlignment.Tabular);
		grid.Children.Add(_box);
		Border chevHit = new Border
		{
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			Width = 18.0,
			Child = FluentIcons.Create("chevron_down", 10.0, BuddyTheme.Text50, filled: false)
		};
		((FrameworkElement)chevHit.Child).HorizontalAlignment = HorizontalAlignment.Center;
		((FrameworkElement)chevHit.Child).VerticalAlignment = VerticalAlignment.Center;
		Border el = chevHit;
		Action click = delegate
		{
			_editor.OpenTimePicker(this);
		};
		CalendarTool.AttachClick(el, click, null);
		Grid.SetColumn(chevHit, 1);
		grid.Children.Add(chevHit);
		Child = grid;
		_box.GotKeyboardFocus += delegate
		{
			_editing = true;
			base.Background = CalendarTool.QuietFillHover;
			_box.SelectAll();
		};
		_box.LostKeyboardFocus += delegate
		{
			base.Background = CalendarTool.QuietFill;
			CommitPending();
		};
		_box.TextChanged += delegate
		{
			_ph.Visibility = ((_box.Text.Length != 0) ? Visibility.Collapsed : Visibility.Visible);
			if (_editing && Error != null)
			{
				Error = null;
				_box.Foreground = BuddyTheme.Text92;
				_editor.FieldChanged();
			}
		};
		_box.PreviewKeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Up)
			{
				e.Handled = true;
				Step(15);
			}
			else if (e.Key == Key.Down)
			{
				e.Handled = true;
				Step(-15);
			}
			else if (e.Key == Key.F4 || (e.Key == Key.Down && (Keyboard.Modifiers & ModifierKeys.Alt) != 0))
			{
				e.Handled = true;
				_editor.OpenTimePicker(this);
			}
		};
		base.PreviewMouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			if ((!(e.OriginalSource is DependencyObject) || !IsInside((DependencyObject)e.OriginalSource, chevHit)) && !_editor.PickerOpen)
			{
				_editor.OpenTimePicker(this);
			}
		};
		base.MouseEnter += delegate
		{
			if (!_box.IsKeyboardFocused)
			{
				base.Background = CalendarTool.QuietFillHover;
			}
		};
		base.MouseLeave += delegate
		{
			if (!_box.IsKeyboardFocused)
			{
				base.Background = CalendarTool.QuietFill;
			}
		};
	}

	private static bool IsInside(DependencyObject d, DependencyObject parent)
	{
		while (d != null)
		{
			if (d == parent)
			{
				return true;
			}
			d = VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d);
		}
		return false;
	}

	public void SetFromPicker(TimeSpan? t)
	{
		Value = t;
		this.Committed?.Invoke(t);
	}

	public bool TypeAndCommit(string text)
	{
		_editing = true;
		_box.Text = text;
		return CommitPending();
	}

	private void Step(int minutes)
	{
		CommitPending();
		double totalMinutes = (_value ?? TimeSpan.FromHours(9.0)).TotalMinutes;
		totalMinutes = ((minutes > 0) ? (Math.Floor(totalMinutes / 15.0) * 15.0 + 15.0) : (Math.Ceiling(totalMinutes / 15.0) * 15.0 - 15.0));
		totalMinutes = (totalMinutes % 1440.0 + 1440.0) % 1440.0;
		SetFromPicker(TimeSpan.FromMinutes(totalMinutes));
	}

	public bool CommitPending()
	{
		if (!_editing)
		{
			return Error == null;
		}
		string text = (_value.HasValue ? Cal.Time(DateTime.Today + _value.Value) : "");
		string text2 = _box.Text.Trim();
		if (text2 == text)
		{
			_editing = false;
			return true;
		}
		if (text2.Length == 0 && AllowNone)
		{
			_editing = false;
			SetFromPicker(null);
			return true;
		}
		if (!Cal.TryParseClock(text2, out var time, out var error))
		{
			Error = error;
			_box.Foreground = ThemeTokens.Danger;
			_editor.FieldChanged();
			return false;
		}
		_editing = false;
		SetFromPicker(time);
		return true;
	}
}
