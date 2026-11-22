using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyWaveMeter : Grid
{
	private const int MaxSamples = 30;

	private const double TrackHeight = 18.0;

	private readonly List<double> _samples = new List<double>();

	private readonly TextBlock _labelBlock;

	private readonly TextBlock _valueBlock;

	private readonly Border _waveHost;

	private readonly Path _wavePath;

	private double _percent;

	private string _valueFormat = "{0:0}%";

	public double Value
	{
		get
		{
			return _percent;
		}
		set
		{
			SetValue(value);
		}
	}

	public BuddyWaveMeter(string label, double initialPercent = 0.0, string customValueText = null)
	{
		base.Height = 28.0;
		base.VerticalAlignment = VerticalAlignment.Center;
		base.SnapsToDevicePixels = true;
		base.UseLayoutRounding = true;
		base.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(34.0, GridUnitType.Pixel)
		});
		base.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		base.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		_labelBlock = new TextBlock
		{
			Text = (label ?? string.Empty),
			FontFamily = BuddyTokens.FontText,
			FontSize = 11.5,
			FontWeight = FontWeights.Normal,
			Foreground = BuddyTokens.Text60Brush,
			VerticalAlignment = VerticalAlignment.Center
		};
		Grid.SetColumn(_labelBlock, 0);
		base.Children.Add(_labelBlock);
		_waveHost = new Border
		{
			Height = 18.0,
			Background = Brushes.Transparent,
			ClipToBounds = true,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(6.0, 0.0, 6.0, 0.0)
		};
		_wavePath = new Path
		{
			Stroke = BuddyTokens.Text100Brush,
			StrokeThickness = 1.5,
			StrokeStartLineCap = PenLineCap.Round,
			StrokeEndLineCap = PenLineCap.Round,
			StrokeLineJoin = PenLineJoin.Round,
			Fill = null,
			VerticalAlignment = VerticalAlignment.Stretch,
			HorizontalAlignment = HorizontalAlignment.Stretch
		};
		_waveHost.Child = _wavePath;
		_waveHost.SizeChanged += delegate
		{
			RebuildWaveGeometry();
		};
		Grid.SetColumn(_waveHost, 1);
		base.Children.Add(_waveHost);
		_valueBlock = new TextBlock
		{
			FontFamily = BuddyTokens.FontDisplay,
			FontSize = 11.5,
			FontWeight = AynType.Medium,
			Foreground = BuddyTokens.Text100Brush,
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Right,
			MinWidth = 30.0,
			TextAlignment = TextAlignment.Right
		};
		Typography.SetNumeralAlignment(_valueBlock, FontNumeralAlignment.Tabular);
		Grid.SetColumn(_valueBlock, 2);
		base.Children.Add(_valueBlock);
		double num = Math.Max(0.0, Math.Min(100.0, initialPercent));
		Random random = new Random((int)DateTime.UtcNow.Ticks);
		for (int i = 0; i < 30; i++)
		{
			double num2 = (random.NextDouble() - 0.5) * 4.0;
			_samples.Add(Math.Max(0.0, Math.Min(100.0, num + num2)));
		}
		SetValue(initialPercent, customValueText);
	}

	public void SetValue(double percent, string customValueText = null)
	{
		_percent = Math.Max(0.0, Math.Min(100.0, percent));
		_valueBlock.Text = customValueText ?? string.Format(_valueFormat, _percent);
		lock (_samples)
		{
			_samples.Add(_percent);
			while (_samples.Count > 30)
			{
				_samples.RemoveAt(0);
			}
		}
		RebuildWaveGeometry();
	}

	private void RebuildWaveGeometry()
	{
		double actualWidth = _waveHost.ActualWidth;
		double num = 18.0;
		if (actualWidth <= 10.0)
		{
			return;
		}
		List<Point> list = new List<Point>();
		lock (_samples)
		{
			int count = _samples.Count;
			if (count == 0)
			{
				return;
			}
			double num2 = actualWidth / 29.0;
			double num3 = 2.0;
			double num4 = num - num3 * 2.0;
			int num5 = 30 - count;
			for (int i = 0; i < count; i++)
			{
				double x = (double)(num5 + i) * num2;
				double num6 = Math.Max(0.0, Math.Min(1.0, _samples[i] / 100.0));
				double y = num - num3 - num6 * num4;
				list.Add(new Point(x, y));
			}
		}
		if (list.Count >= 2)
		{
			PathGeometry pathGeometry = new PathGeometry();
			PathFigure pathFigure = new PathFigure();
			pathFigure.StartPoint = list[0];
			pathFigure.IsClosed = false;
			PathFigure pathFigure2 = pathFigure;
			for (int j = 0; j < list.Count - 1; j++)
			{
				Point point = ((j == 0) ? list[0] : list[j - 1]);
				Point point2 = list[j];
				Point point3 = list[j + 1];
				Point point4 = ((j + 2 < list.Count) ? list[j + 2] : point3);
				Point point5 = new Point(point2.X + (point3.X - point.X) / 6.0, point2.Y + (point3.Y - point.Y) / 6.0);
				Point point6 = new Point(point3.X - (point4.X - point2.X) / 6.0, point3.Y - (point4.Y - point2.Y) / 6.0);
				point5.Y = Math.Max(1.0, Math.Min(num - 1.0, point5.Y));
				point6.Y = Math.Max(1.0, Math.Min(num - 1.0, point6.Y));
				pathFigure2.Segments.Add(new BezierSegment(point5, point6, point3, isStroked: true));
			}
			pathGeometry.Figures.Add(pathFigure2);
			pathGeometry.Freeze();
			_wavePath.Data = pathGeometry;
		}
	}
}
