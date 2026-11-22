using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WindowsBuddy.BuddyCapsule.UI;

public sealed class BuddyVirtualList : Grid
{
	private struct Entry
	{
		public bool IsHeader;

		public string Label;

		public object Item;

		public double Top;

		public double Height;

		public bool LastInSection;
	}

	private const double LabelBox = 13.0;

	public static readonly DependencyProperty OffsetProperty = DependencyProperty.Register("Offset", typeof(double), typeof(BuddyVirtualList), new PropertyMetadata(0.0, delegate(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		((BuddyVirtualList)d).Realize();
	}));

	private readonly Func<BuddyRow> _rowFactory;

	private readonly Func<object, string> _keyOf;

	private readonly double _rowHeight;

	private readonly double _headerHeight;

	private readonly double _firstHeaderHeight;

	private readonly double _inset;

	private readonly Canvas _canvas = new Canvas
	{
		ClipToBounds = true,
		Background = Brushes.Transparent
	};

	private readonly Border _thumb;

	private readonly TranslateTransform _thumbY = new TranslateTransform();

	private readonly DispatcherTimer _thumbHide;

	private readonly List<Entry> _entries = new List<Entry>();

	private readonly Dictionary<int, FrameworkElement> _live = new Dictionary<int, FrameworkElement>();

	private readonly Stack<BuddyRow> _rowPool = new Stack<BuddyRow>();

	private readonly Stack<BuddyLabel> _headerPool = new Stack<BuddyLabel>();

	private readonly List<int> _scratch = new List<int>();

	private double _extent;

	private double _target;

	private string _selectedKey;

	private LinearGradientBrush _fade;

	private double _fadeHeight;

	public Brush HeaderInk = BuddyTheme.Text50;

	public double FadeHeight = 20.0;

	private double Offset => (double)GetValue(OffsetProperty);

	private double MaxOffset => Math.Max(0.0, _extent - base.ActualHeight);

	public event Action<object, int> ItemClick;

	public event Action<object, FrameworkElement> ItemContext;

	public BuddyVirtualList(Func<BuddyRow> rowFactory, Func<object, string> keyOf, double rowHeight, double headerHeight, double firstHeaderHeight, double inset)
	{
		_rowFactory = rowFactory;
		_keyOf = keyOf;
		_rowHeight = rowHeight;
		_headerHeight = headerHeight;
		_firstHeaderHeight = firstHeaderHeight;
		_inset = inset;
		base.ClipToBounds = true;
		base.Background = Brushes.Transparent;
		base.Children.Add(_canvas);
		_thumb = new Border
		{
			Width = 3.0,
			CornerRadius = new CornerRadius(1.5),
			Background = BuddyTheme.BarPast,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(0.0, 0.0, 2.0, 0.0),
			Opacity = 0.0,
			IsHitTestVisible = false,
			RenderTransform = _thumbY
		};
		base.Children.Add(_thumb);
		_thumbHide = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(900.0)
		};
		DispatcherTimer thumbHide = _thumbHide;
		EventHandler value = delegate
		{
			_thumbHide.Stop();
			BuddyTheme.Animate(_thumb, UIElement.OpacityProperty, 0.0, BuddyTheme.CrossFade);
		};
		thumbHide.Tick += value;
		base.SizeChanged += delegate
		{
			foreach (KeyValuePair<int, FrameworkElement> item in _live)
			{
				if (item.Value is BuddyRow)
				{
					item.Value.Width = base.ActualWidth;
				}
			}
			ClampOffset();
			Realize();
		};
	}

	internal void RaiseItemClick(object item, int clicks)
	{
		this.ItemClick?.Invoke(item, clicks);
	}

	internal void RaiseItemContext(object item, FrameworkElement anchor)
	{
		this.ItemContext?.Invoke(item, anchor);
	}

	public void SetItems<T>(IList<T> items, Func<T, string> sectionOf, object selected)
	{
		_selectedKey = ((selected != null) ? _keyOf(selected) : null);
		_entries.Clear();
		double num = 0.0;
		string text = null;
		for (int i = 0; i < items.Count; i++)
		{
			string text2 = sectionOf(items[i]);
			if (text2 != text)
			{
				if (_entries.Count > 0)
				{
					MarkLast();
				}
				text = text2;
				double num2 = ((_entries.Count == 0) ? _firstHeaderHeight : _headerHeight);
				_entries.Add(new Entry
				{
					IsHeader = true,
					Label = text2,
					Top = num,
					Height = num2
				});
				num += num2;
			}
			_entries.Add(new Entry
			{
				Item = items[i],
				Top = num,
				Height = _rowHeight
			});
			num += _rowHeight;
		}
		if (_entries.Count > 0)
		{
			MarkLast();
		}
		_extent = num + 4.0;
		foreach (KeyValuePair<int, FrameworkElement> item in _live)
		{
			Recycle(item.Value);
		}
		_live.Clear();
		ClampOffset();
		Realize();
	}

	private void MarkLast()
	{
		int index = _entries.Count - 1;
		Entry value = _entries[index];
		if (!value.IsHeader)
		{
			value.LastInSection = true;
			_entries[index] = value;
		}
	}

	public void SetSelected(object item)
	{
		_selectedKey = ((item != null) ? _keyOf(item) : null);
		foreach (KeyValuePair<int, FrameworkElement> item2 in _live)
		{
			if (item2.Value is BuddyRow buddyRow)
			{
				buddyRow.SetSelected(buddyRow.Item != null && _selectedKey != null && _keyOf(buddyRow.Item) == _selectedKey);
			}
		}
	}

	public void RefreshVisible()
	{
		foreach (KeyValuePair<int, FrameworkElement> item in _live)
		{
			if (item.Value is BuddyRow buddyRow)
			{
				buddyRow.Refresh();
			}
		}
	}

	public void RefreshItem(object item)
	{
		if (item == null)
		{
			return;
		}
		string text = _keyOf(item);
		foreach (KeyValuePair<int, FrameworkElement> item2 in _live)
		{
			if (item2.Value is BuddyRow { Item: not null } buddyRow && _keyOf(buddyRow.Item) == text)
			{
				buddyRow.Refresh();
				break;
			}
		}
	}

	public void ScrollToTop()
	{
		_target = 0.0;
		BeginAnimation(OffsetProperty, null);
		SetValue(OffsetProperty, 0.0);
	}

	public void EnsureVisible(object item)
	{
		if (item == null)
		{
			return;
		}
		string text = _keyOf(item);
		for (int i = 0; i < _entries.Count; i++)
		{
			Entry entry = _entries[i];
			if (!entry.IsHeader && !(_keyOf(entry.Item) != text))
			{
				double top = entry.Top;
				if (i > 0 && _entries[i - 1].IsHeader)
				{
					top = _entries[i - 1].Top;
				}
				double num = entry.Top + entry.Height;
				double target = _target;
				if (top < target)
				{
					ScrollTo(top);
				}
				else if (num > target + base.ActualHeight)
				{
					ScrollTo(num - base.ActualHeight);
				}
				break;
			}
		}
	}

	private void ScrollTo(double target)
	{
		_target = Math.Max(0.0, Math.Min(MaxOffset, target));
		ShowThumb();
		BeginAnimation(OffsetProperty, null);
		SetValue(OffsetProperty, _target);
	}

	private void ClampOffset()
	{
		double maxOffset = MaxOffset;
		if (Offset > maxOffset || _target > maxOffset)
		{
			_target = maxOffset;
			BeginAnimation(OffsetProperty, null);
			SetValue(OffsetProperty, maxOffset);
		}
	}

	protected override void OnMouseWheel(MouseWheelEventArgs e)
	{
		base.OnMouseWheel(e);
		if (!(MaxOffset <= 0.0))
		{
			e.Handled = true;
			double num = ((Math.Abs(_target - Offset) > 0.5) ? _target : Offset);
			ScrollTo(num - (double)e.Delta * 0.45);
		}
	}

	private void ShowThumb()
	{
		if (!(MaxOffset <= 0.0))
		{
			BuddyTheme.Animate(_thumb, UIElement.OpacityProperty, 1.0, BuddyTheme.Hover);
			_thumbHide.Stop();
			_thumbHide.Start();
		}
	}

	private void Realize()
	{
		double actualHeight = base.ActualHeight;
		double actualWidth = base.ActualWidth;
		if (actualHeight <= 0.0 || actualWidth <= 0.0)
		{
			return;
		}
		double offset = Offset;
		_scratch.Clear();
		foreach (KeyValuePair<int, FrameworkElement> item in _live)
		{
			if (item.Key >= _entries.Count)
			{
				_scratch.Add(item.Key);
				continue;
			}
			Entry entry = _entries[item.Key];
			if (entry.Top + entry.Height <= offset || entry.Top >= offset + actualHeight)
			{
				_scratch.Add(item.Key);
			}
		}
		foreach (int item2 in _scratch)
		{
			Recycle(_live[item2]);
			_live.Remove(item2);
		}
		for (int i = 0; i < _entries.Count; i++)
		{
			Entry entry2 = _entries[i];
			if (entry2.Top + entry2.Height <= offset)
			{
				continue;
			}
			if (entry2.Top >= offset + actualHeight)
			{
				break;
			}
			if (!_live.TryGetValue(i, out var value))
			{
				if (entry2.IsHeader)
				{
					BuddyLabel buddyLabel = ((_headerPool.Count > 0) ? _headerPool.Pop() : NewHeader());
					buddyLabel.Text = entry2.Label;
					buddyLabel.Margin = new Thickness(_inset, Math.Max(0.0, entry2.Height - 8.0 - 13.0), 0.0, 0.0);
					value = buddyLabel;
				}
				else
				{
					BuddyRow buddyRow = ((_rowPool.Count > 0) ? _rowPool.Pop() : NewRow());
					buddyRow.Bind(entry2.Item, _selectedKey != null && _keyOf(entry2.Item) == _selectedKey, entry2.LastInSection);
					buddyRow.Width = actualWidth;
					buddyRow.Height = entry2.Height;
					value = buddyRow;
				}
				value.Visibility = Visibility.Visible;
				_live[i] = value;
			}
			Canvas.SetTop(value, Math.Round(entry2.Top - offset));
		}
		double maxOffset = MaxOffset;
		if (maxOffset > 0.0)
		{
			double num = Math.Max(24.0, actualHeight * actualHeight / _extent);
			_thumb.Height = num;
			_thumbY.Y = (actualHeight - num) * (offset / maxOffset);
		}
		else
		{
			_thumb.Opacity = 0.0;
		}
		bool flag = offset < maxOffset - 0.5;
		if (flag && (_fade == null || _fadeHeight != actualHeight))
		{
			_fadeHeight = actualHeight;
			_fade = new LinearGradientBrush
			{
				StartPoint = new Point(0.0, 0.0),
				EndPoint = new Point(0.0, 1.0),
				GradientStops = new GradientStopCollection
				{
					new GradientStop(Colors.White, 0.0),
					new GradientStop(Colors.White, Math.Max(0.0, 1.0 - FadeHeight / actualHeight)),
					new GradientStop(Colors.Transparent, 1.0)
				}
			};
			_fade.Freeze();
		}
		base.OpacityMask = (flag ? _fade : null);
	}

	private BuddyLabel NewHeader()
	{
		BuddyLabel buddyLabel = BuddyTheme.LabelText(string.Empty);
		buddyLabel.Foreground = HeaderInk;
		buddyLabel.IsHitTestVisible = false;
		_canvas.Children.Add(buddyLabel);
		return buddyLabel;
	}

	private BuddyRow NewRow()
	{
		BuddyRow buddyRow = _rowFactory();
		buddyRow.Owner = this;
		_canvas.Children.Add(buddyRow);
		return buddyRow;
	}

	private void Recycle(FrameworkElement el)
	{
		el.Visibility = Visibility.Collapsed;
		if (el is BuddyRow buddyRow)
		{
			buddyRow.Unbind();
			_rowPool.Push(buddyRow);
		}
		else if (el is BuddyLabel item)
		{
			_headerPool.Push(item);
		}
	}
}
