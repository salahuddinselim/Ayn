using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WindowsBuddy.Tools.Calendar;

internal sealed class CalList : Grid
{
	internal struct Entry
	{
		public int Kind;

		public double Height;

		public object Data;
	}

	private readonly Func<int, FrameworkElement> _create;

	private readonly Action<FrameworkElement, Entry> _bind;

	private readonly Canvas _canvas = new Canvas
	{
		ClipToBounds = true,
		Background = Brushes.Transparent
	};

	private readonly List<Entry> _entries = new List<Entry>();

	private readonly List<double> _tops = new List<double>();

	private readonly Dictionary<int, FrameworkElement> _live = new Dictionary<int, FrameworkElement>();

	private readonly Dictionary<int, Stack<FrameworkElement>> _pool = new Dictionary<int, Stack<FrameworkElement>>();

	private readonly List<int> _drop = new List<int>();

	private double _extent;

	private double _offset;

	private LinearGradientBrush _mask;

	private double _maskFor = -1.0;

	private int _touchId = -1;

	private double _touchY;

	private bool _dragging;

	public int Count => _entries.Count;

	public double Offset => _offset;

	public double Extent => _extent;

	private double MaxOffset => Math.Max(0.0, _extent - base.ActualHeight);

	public IEnumerable<FrameworkElement> Visible => _live.Values;

	public CalList(Func<int, FrameworkElement> create, Action<FrameworkElement, Entry> bind)
	{
		_create = create;
		_bind = bind;
		base.ClipToBounds = true;
		base.Background = Brushes.Transparent;
		base.Children.Add(_canvas);
		SizeChangedEventHandler value = delegate
		{
			Clamp();
			Realize(rebind: true);
		};
		base.SizeChanged += value;
		base.MouseWheel += delegate(object s, MouseWheelEventArgs e)
		{
			double offset = _offset;
			ScrollBy((double)(-e.Delta) * 0.4);
			if (_offset != offset)
			{
				e.Handled = true;
			}
		};
		base.PreviewTouchDown += delegate(object s, TouchEventArgs e)
		{
			if (_touchId < 0)
			{
				_touchId = e.TouchDevice.Id;
				_touchY = e.GetTouchPoint(this).Position.Y;
				_dragging = false;
			}
		};
		base.PreviewTouchMove += delegate(object s, TouchEventArgs e)
		{
			if (e.TouchDevice.Id == _touchId)
			{
				double y = e.GetTouchPoint(this).Position.Y;
				if (!_dragging && Math.Abs(y - _touchY) > 8.0 && MaxOffset > 0.0)
				{
					_dragging = true;
					CaptureTouch(e.TouchDevice);
				}
				if (_dragging)
				{
					ScrollBy(_touchY - y);
					_touchY = y;
					e.Handled = true;
				}
			}
		};
		base.PreviewTouchUp += delegate(object s, TouchEventArgs e)
		{
			if (e.TouchDevice.Id == _touchId)
			{
				_touchId = -1;
				if (_dragging)
				{
					_dragging = false;
					ReleaseTouchCapture(e.TouchDevice);
					e.Handled = true;
				}
			}
		};
	}

	public void SetEntries(IList<Entry> entries, bool resetOffset)
	{
		_entries.Clear();
		_tops.Clear();
		double num = 0.0;
		foreach (Entry entry in entries)
		{
			_entries.Add(entry);
			_tops.Add(num);
			num += entry.Height;
		}
		_extent = num + 6.0;
		if (resetOffset)
		{
			_offset = 0.0;
		}
		foreach (KeyValuePair<int, FrameworkElement> item in _live)
		{
			Recycle(item.Value);
		}
		_live.Clear();
		Clamp();
		Realize(rebind: true);
	}

	public void ScrollBy(double dy)
	{
		double num = Math.Max(0.0, Math.Min(MaxOffset, _offset + dy));
		if (num != _offset)
		{
			_offset = num;
			Realize(rebind: false);
		}
	}

	private void Clamp()
	{
		_offset = Math.Max(0.0, Math.Min(MaxOffset, _offset));
	}

	private void Recycle(FrameworkElement el)
	{
		_canvas.Children.Remove(el);
		Entry entry = (Entry)el.Tag;
		if (!_pool.TryGetValue(entry.Kind, out var value))
		{
			value = (_pool[entry.Kind] = new Stack<FrameworkElement>());
		}
		value.Push(el);
	}

	private void Realize(bool rebind)
	{
		double actualHeight = base.ActualHeight;
		double actualWidth = base.ActualWidth;
		if (actualHeight <= 0.0 || actualWidth <= 0.0)
		{
			return;
		}
		int num = FirstVisible();
		_drop.Clear();
		foreach (KeyValuePair<int, FrameworkElement> item in _live)
		{
			int key = item.Key;
			if (key < num || key >= _entries.Count || _tops[key] - _offset > actualHeight)
			{
				_drop.Add(key);
			}
		}
		foreach (int item2 in _drop)
		{
			Recycle(_live[item2]);
			_live.Remove(item2);
		}
		for (int i = num; i < _entries.Count && _tops[i] - _offset < actualHeight; i++)
		{
			Entry entry = _entries[i];
			if (!_live.TryGetValue(i, out var value))
			{
				value = ((_pool.TryGetValue(entry.Kind, out var value2) && value2.Count > 0) ? value2.Pop() : _create(entry.Kind));
				value.Tag = entry;
				_bind(value, entry);
				_canvas.Children.Add(value);
				_live[i] = value;
			}
			else if (rebind)
			{
				value.Tag = entry;
				_bind(value, entry);
			}
			value.Width = actualWidth;
			value.Height = entry.Height;
			Canvas.SetTop(value, Math.Round(_tops[i] - _offset));
		}
		UpdateMask(actualHeight);
	}

	private int FirstVisible()
	{
		int num = 0;
		int num2 = _entries.Count - 1;
		int result = _entries.Count;
		while (num <= num2)
		{
			int num3 = (num + num2) / 2;
			if (_tops[num3] + _entries[num3].Height > _offset)
			{
				result = num3;
				num2 = num3 - 1;
			}
			else
			{
				num = num3 + 1;
			}
		}
		return result;
	}

	private void UpdateMask(double h)
	{
		bool flag = _offset < MaxOffset - 0.5;
		bool flag2 = _offset > 0.5;
		if (!flag && !flag2)
		{
			if (_canvas.OpacityMask != null)
			{
				_canvas.OpacityMask = null;
			}
			return;
		}
		double num = h * 4.0 + (double)(flag ? 1 : 0) + (double)(flag2 ? 2 : 0);
		if (_mask != null && _maskFor == num)
		{
			_canvas.OpacityMask = _mask;
			return;
		}
		double num2 = Math.Min(0.4, 16.0 / h);
		LinearGradientBrush linearGradientBrush = new LinearGradientBrush();
		linearGradientBrush.StartPoint = new Point(0.0, 0.0);
		linearGradientBrush.EndPoint = new Point(0.0, 1.0);
		LinearGradientBrush linearGradientBrush2 = linearGradientBrush;
		linearGradientBrush2.GradientStops.Add(new GradientStop(flag2 ? Colors.Transparent : Colors.Black, 0.0));
		linearGradientBrush2.GradientStops.Add(new GradientStop(Colors.Black, flag2 ? num2 : 0.0));
		linearGradientBrush2.GradientStops.Add(new GradientStop(Colors.Black, flag ? (1.0 - num2) : 1.0));
		linearGradientBrush2.GradientStops.Add(new GradientStop(flag ? Colors.Transparent : Colors.Black, 1.0));
		linearGradientBrush2.Freeze();
		_mask = linearGradientBrush2;
		_maskFor = num;
		_canvas.OpacityMask = linearGradientBrush2;
	}
}
