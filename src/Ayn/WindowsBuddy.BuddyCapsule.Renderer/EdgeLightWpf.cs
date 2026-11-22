using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace WindowsBuddy.BuddyCapsule.Renderer;

public sealed class EdgeLightWpf : Grid, IEdgeLightSurface, IDisposable
{
	private EdgeLightOutline _outline;

	private EdgeLightLook _look = new EdgeLightLook();

	private EdgeLightMode _mode;

	private readonly Grid _level = new Grid
	{
		IsHitTestVisible = false
	};

	private readonly Grid _pulse = new Grid
	{
		IsHitTestVisible = false
	};

	private readonly Grid _glow = new Grid
	{
		IsHitTestVisible = false,
		Opacity = 0.45
	};

	private readonly Grid _core = new Grid
	{
		IsHitTestVisible = false
	};

	private readonly List<Action> _stop = new List<Action>();

	private readonly int _frameRate;

	private readonly bool _preview;

	private bool _built;

	private bool _rebuilding;

	public int RunningAnimations => _stop.Count;

	private double Th => 2.5;

	public EdgeLightWpf(int frameRate = 60, bool preview = false)
	{
		_frameRate = frameRate;
		_preview = preview;
		base.IsHitTestVisible = false;
		base.Focusable = false;
		base.HorizontalAlignment = HorizontalAlignment.Left;
		base.VerticalAlignment = VerticalAlignment.Top;
		if (!preview)
		{
			_glow.Effect = new BlurEffect
			{
				Radius = 9.600000000000001,
				KernelType = KernelType.Gaussian,
				RenderingBias = RenderingBias.Performance
			};
		}
		else
		{
			_glow.Opacity = 0.225;
		}
		_pulse.Children.Add(_glow);
		_pulse.Children.Add(_core);
		_level.Children.Add(_pulse);
		base.Children.Add(_level);
		base.Unloaded += delegate
		{
			StopAnimations();
		};
		base.Loaded += delegate
		{
			if (!_built || (_mode != 0 && _stop.Count == 0))
			{
				Rebuild();
			}
		};
		base.IsVisibleChanged += delegate
		{
			if (!base.IsVisible)
			{
				StopAnimations();
			}
			else if (_mode != 0 && _stop.Count == 0)
			{
				Rebuild();
			}
		};
	}

	protected override void OnVisualParentChanged(DependencyObject oldParent)
	{
		base.OnVisualParentChanged(oldParent);
		if (_preview)
		{
			if (VisualTreeHelper.GetParent(this) == null)
			{
				StopAnimations();
			}
			else if (!_built || (_mode != 0 && _stop.Count == 0))
			{
				Rebuild();
			}
		}
	}

	public void SetOutline(EdgeLightOutline outline, Point cardOffset, double scale)
	{
		bool flag = outline?.SameShape(_outline) ?? false;
		_outline = outline;
		if (outline == null)
		{
			base.Visibility = Visibility.Collapsed;
			return;
		}
		base.Width = outline.Width;
		base.Height = outline.Height;
		if (!flag)
		{
			Rebuild();
		}
	}

	public void SetLook(EdgeLightLook look)
	{
		if (!(look.Key == _look.Key))
		{
			_look = look;
			Rebuild();
		}
	}

	public void SetMode(EdgeLightMode mode)
	{
		if (mode != _mode || !_built)
		{
			_mode = mode;
			Rebuild();
		}
	}

	public void SyncWindow()
	{
	}

	public void Dispose()
	{
		StopAnimations();
		base.Children.Clear();
	}

	private void StopAnimations()
	{
		foreach (Action item in _stop)
		{
			try
			{
				item();
			}
			catch
			{
			}
		}
		_stop.Clear();
	}

	private void Run(IAnimatable target, DependencyProperty prop, AnimationTimeline t)
	{
		Timeline.SetDesiredFrameRate(t, _frameRate);
		AnimationClock clock = t.CreateClock();
		target.ApplyAnimationClock(prop, clock);
		_stop.Add(delegate
		{
			if (clock.Controller != null)
			{
				clock.Controller.Stop();
			}
			target.ApplyAnimationClock(prop, null);
		});
	}

	private void Rebuild()
	{
		if (_rebuilding)
		{
			return;
		}
		_rebuilding = true;
		try
		{
			RebuildCore();
		}
		finally
		{
			_rebuilding = false;
		}
	}

	private void RebuildCore()
	{
		if (_preview && VisualTreeHelper.GetParent(this) == null)
		{
			_built = false;
			return;
		}
		StopAnimations();
		_glow.Children.Clear();
		_core.Children.Clear();
		_pulse.Opacity = 1.0;
		_built = true;
		if (_outline == null || _mode == EdgeLightMode.Off)
		{
			base.Visibility = Visibility.Collapsed;
			return;
		}
		base.Visibility = Visibility.Visible;
		UIElement uIElement = VisualTreeHelper.GetParent(this) as UIElement;
		if (base.IsLoaded && uIElement != null && !uIElement.IsVisible)
		{
			return;
		}
		_level.Opacity = _look.Intensity;
		StreamGeometry streamGeometry = new StreamGeometry();
		using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
		{
			streamGeometryContext.BeginFigure(_outline.Fill[0], isFilled: true, isClosed: true);
			streamGeometryContext.PolyLineTo(_outline.Fill, isStroked: true, isSmoothJoin: true);
		}
		streamGeometry.Freeze();
		base.Clip = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(-40.0, -40.0, _outline.Width + 80.0, _outline.Height + 80.0)), streamGeometry);
		if (!_outline.Closed)
		{
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush();
			linearGradientBrush.MappingMode = BrushMappingMode.Absolute;
			linearGradientBrush.StartPoint = new Point(0.0, _outline.Top);
			linearGradientBrush.EndPoint = new Point(0.0, _outline.Top + _outline.FadeTop);
			LinearGradientBrush linearGradientBrush2 = linearGradientBrush;
			linearGradientBrush2.GradientStops.Add(new GradientStop(Colors.Transparent, 0.0));
			linearGradientBrush2.GradientStops.Add(new GradientStop(Color.FromArgb(150, 0, 0, 0), 0.55));
			linearGradientBrush2.GradientStops.Add(new GradientStop(Colors.Black, 1.0));
			base.OpacityMask = linearGradientBrush2;
		}
		else
		{
			base.OpacityMask = null;
		}
		Geometry geometry = Polyline(_outline.Points, reverse: false);
		Geometry geometry2 = Polyline(_outline.Points, reverse: true);
		double length = _outline.Length;
		bool flag = _mode == EdgeLightMode.Animated || _mode == EdgeLightMode.Charging;
		Color c = ((_mode == EdgeLightMode.Charging) ? EdgeLightLook.ChargingColor : _look.Color);
		if (_mode == EdgeLightMode.Static || !flag)
		{
			AddStroke(geometry, c, 0.6, null, 0.0);
			return;
		}
		double cycleSeconds = _look.CycleSeconds;
		if (_mode == EdgeLightMode.Charging)
		{
			Geometry[] array = new Geometry[2] { geometry, geometry2 };
			foreach (Geometry g in array)
			{
				Path target = AddStroke(g, c, 0.9, new DoubleCollection
				{
					length / 2.0 / Th,
					4.0 * length / Th
				}, length / 2.0 / Th);
				DoubleAnimation doubleAnimation = new DoubleAnimation(length / 2.0 / Th, 0.0, TimeSpan.FromSeconds(1.1));
				doubleAnimation.EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				};
				doubleAnimation.FillBehavior = FillBehavior.HoldEnd;
				DoubleAnimation t = doubleAnimation;
				Run(target, Shape.StrokeDashOffsetProperty, t);
			}
			return;
		}
		switch (_look.Style)
		{
		case EdgeLightStyle.Breathe:
		{
			AddStroke(geometry, c, 0.9, null, 0.0);
			DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames = new DoubleAnimationUsingKeyFrames();
			doubleAnimationUsingKeyFrames.Duration = TimeSpan.FromSeconds(cycleSeconds);
			doubleAnimationUsingKeyFrames.RepeatBehavior = RepeatBehavior.Forever;
			DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames2 = doubleAnimationUsingKeyFrames;
			doubleAnimationUsingKeyFrames2.KeyFrames.Add(new LinearDoubleKeyFrame(0.3, KeyTime.FromPercent(0.0)));
			doubleAnimationUsingKeyFrames2.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0.5), new SineEase
			{
				EasingMode = EasingMode.EaseInOut
			}));
			doubleAnimationUsingKeyFrames2.KeyFrames.Add(new EasingDoubleKeyFrame(0.3, KeyTime.FromPercent(1.0), new SineEase
			{
				EasingMode = EasingMode.EaseInOut
			}));
			Run(_pulse, UIElement.OpacityProperty, doubleAnimationUsingKeyFrames2);
			break;
		}
		case EdgeLightStyle.TwinComets:
			AddComet(geometry, length, cycleSeconds, twin: true, 0.15);
			AddComet(geometry2, length, cycleSeconds, twin: true, 0.15);
			break;
		default:
			AddComet(geometry, length, cycleSeconds, twin: false, 0.18);
			break;
		}
	}

	private void AddComet(Geometry g, double L, double cycle, bool twin, double tail)
	{
		for (int i = 0; i < EdgeLightLook.CometLayers.Length; i++)
		{
			double[] array = EdgeLightLook.CometLayers[i];
			if (!_preview || i <= 0 || i % 2 != 1)
			{
				double len = array[0] * tail / 0.18;
				Color c = ((array == EdgeLightLook.CometLayers[0]) ? EdgeLightLook.Tint(_look.Color, 0.6) : _look.Color);
				bool flag = _preview && !twin;
				Path target = AddStroke(g, c, (_preview && i > 0) ? (array[1] * 1.8) : array[1], new DoubleCollection
				{
					len * L / Th,
					(flag ? (1.0 - len) : 4.0) * L / Th
				}, 0.0);
				Func<double, double> func = (double head) => (0.0 - (head - len)) * L / Th;
				DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames = new DoubleAnimationUsingKeyFrames();
				doubleAnimationUsingKeyFrames.Duration = TimeSpan.FromSeconds(cycle);
				doubleAnimationUsingKeyFrames.RepeatBehavior = RepeatBehavior.Forever;
				DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames2 = doubleAnimationUsingKeyFrames;
				if (twin)
				{
					doubleAnimationUsingKeyFrames2.KeyFrames.Add(new LinearDoubleKeyFrame(func(0.0), KeyTime.FromPercent(0.0)));
					doubleAnimationUsingKeyFrames2.KeyFrames.Add(new EasingDoubleKeyFrame(func(0.5), KeyTime.FromPercent(0.5), new SineEase
					{
						EasingMode = EasingMode.EaseInOut
					}));
					doubleAnimationUsingKeyFrames2.KeyFrames.Add(new EasingDoubleKeyFrame(func(0.0), KeyTime.FromPercent(1.0), new SineEase
					{
						EasingMode = EasingMode.EaseInOut
					}));
				}
				else if (flag)
				{
					doubleAnimationUsingKeyFrames2.KeyFrames.Add(new LinearDoubleKeyFrame(func(0.5), KeyTime.FromPercent(0.0)));
					doubleAnimationUsingKeyFrames2.KeyFrames.Add(new LinearDoubleKeyFrame(func(1.5), KeyTime.FromPercent(1.0)));
				}
				else
				{
					doubleAnimationUsingKeyFrames2.KeyFrames.Add(new LinearDoubleKeyFrame(func(0.0), KeyTime.FromPercent(0.0)));
					doubleAnimationUsingKeyFrames2.KeyFrames.Add(new LinearDoubleKeyFrame(func(1.0 + tail), KeyTime.FromPercent(0.85)));
					doubleAnimationUsingKeyFrames2.KeyFrames.Add(new LinearDoubleKeyFrame(func(1.0 + tail), KeyTime.FromPercent(1.0)));
				}
				Run(target, Shape.StrokeDashOffsetProperty, doubleAnimationUsingKeyFrames2);
			}
		}
	}

	private Path AddStroke(Geometry g, Color c, double alpha, DoubleCollection dash, double dashOffset)
	{
		SolidColorBrush brush = new SolidColorBrush(Color.FromArgb((byte)(255.0 * alpha), c.R, c.G, c.B));
		brush.Freeze();
		Func<Path> func = delegate
		{
			Path path3 = new Path
			{
				Data = g,
				Stroke = brush,
				StrokeThickness = Th,
				IsHitTestVisible = false,
				StrokeStartLineCap = PenLineCap.Round,
				StrokeEndLineCap = PenLineCap.Round,
				StrokeDashCap = PenLineCap.Round,
				StrokeLineJoin = PenLineJoin.Round
			};
			if (dash != null)
			{
				path3.StrokeDashArray = dash;
				path3.StrokeDashOffset = dashOffset;
			}
			return path3;
		};
		Path path = func();
		Path path2 = func();
		path2.StrokeThickness = (_preview ? (Th * 3.2) : (Th * 1.6));
		_core.Children.Add(path);
		_glow.Children.Add(path2);
		if (dash != null)
		{
			path2.SetBinding(Shape.StrokeDashOffsetProperty, new Binding("StrokeDashOffset")
			{
				Source = path
			});
		}
		return path;
	}

	private static Geometry Polyline(Point[] pts, bool reverse)
	{
		List<Point> list = new List<Point>(pts);
		if (reverse)
		{
			list.Reverse();
		}
		StreamGeometry streamGeometry = new StreamGeometry();
		using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
		{
			streamGeometryContext.BeginFigure(list[0], isFilled: false, isClosed: false);
			list.RemoveAt(0);
			streamGeometryContext.PolyLineTo(list, isStroked: true, isSmoothJoin: true);
		}
		streamGeometry.Freeze();
		return streamGeometry;
	}
}
