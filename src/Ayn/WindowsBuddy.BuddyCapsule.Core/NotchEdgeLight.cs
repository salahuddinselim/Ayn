using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.Services.Pet;

namespace WindowsBuddy.BuddyCapsule.Core;

public sealed class NotchEdgeLight : IDisposable
{
	private struct NativeRect
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private readonly Window _win;

	private readonly Grid _card;

	private readonly UIElement _content;

	private IEdgeLightSurface _surface;

	private bool _enabled;

	private string _style = "Comet";

	private bool _running;

	private bool _shown;

	private EdgeLightLook _look = new EdgeLightLook();

	private EdgeLightOutline _outline;

	private string _outlineKey;

	private string _outlineStyle;

	private double _outlineCornerRadius;

	private EdgeLightOutline _arrangedOutline;

	private string _arrangedOutlineKey;

	private Point _offset;

	private double _scale = 1.0;

	private bool _resting;

	private bool _presenceFull = true;

	private DateTime _runUntil = DateTime.MinValue;

	private DateTime _chargingUntil = DateTime.MinValue;

	private DateTime _lastCharging = DateTime.MinValue;

	private Color? _previewColor;

	private DispatcherTimer _timer;

	private EdgeLightMode _mode;

	public string Backend
	{
		get
		{
			if (!(_surface is EdgeLightComposition))
			{
				if (!(_surface is EdgeLightWpf))
				{
					return "none";
				}
				return "wpf";
			}
			return "compositor";
		}
	}

	public EdgeLightMode Mode => _mode;

	public bool IsAnimating => _running;

	public bool IsShown => _shown;

	public int RunningAnimations
	{
		get
		{
			if (_surface == null)
			{
				return 0;
			}
			return _surface.RunningAnimations;
		}
	}

	public IntPtr LightWindow
	{
		get
		{
			if (!(_surface is EdgeLightComposition edgeLightComposition))
			{
				return IntPtr.Zero;
			}
			return edgeLightComposition.Handle;
		}
	}

	public bool BeatGlowOn
	{
		get
		{
			if (_surface is EdgeLightComposition edgeLightComposition)
			{
				return edgeLightComposition.BeatOn;
			}
			return false;
		}
	}

	public float BeatGlowOpacity
	{
		get
		{
			if (!(_surface is EdgeLightComposition edgeLightComposition))
			{
				return 0f;
			}
			return edgeLightComposition.BeatOpacity;
		}
	}

	private static bool AnimationsAllowed
	{
		get
		{
			if (CapsuleMotion.AnimationsEnabled && !PowerSaver.IsBatterySaverOn)
			{
				return !RenderProfile.LightMode;
			}
			return false;
		}
	}

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect bounds);

	public NotchEdgeLight(Window notchWindow, Grid card, UIElement content)
	{
		_win = notchWindow;
		_card = card;
		_content = content;
		_card.LayoutUpdated += OnCardLayoutUpdated;
		PowerSaver.Changed += OnPolicyChanged;
		CapsuleMotion.AnimationsPolicyChanged += OnPolicyChanged;
		SystemParameters.StaticPropertyChanged += OnSystemParameterChanged;
	}

	public void ApplyConfig(NotchConfig cfg)
	{
		_enabled = cfg.EdgeLightEnabled;
		_style = cfg.EdgeLightStyle;
		_previewColor = null;
		_look = EdgeLightLook.FromConfig(cfg);
		if (_surface != null)
		{
			_surface.SetLook(_look);
		}
		Update();
	}

	public void PreviewColor(Color c)
	{
		_previewColor = c;
		EdgeLightLook edgeLightLook = EdgeLightLook.FromConfig(NotchConfig.Instance);
		edgeLightLook.Color = c;
		_look = edgeLightLook;
		if (_surface != null)
		{
			_surface.SetLook(_look);
		}
		Trigger();
	}

	public void SetOutline(string style, double width, double height, double cornerRadius)
	{
		_outlineStyle = style;
		_outlineCornerRadius = cornerRadius;
		string text = style + "|" + width.ToString("0.###") + "|" + height.ToString("0.###") + "|" + cornerRadius.ToString("0.##");
		if (text != _outlineKey)
		{
			_outlineKey = text;
			_outline = EdgeLightOutline.For(style, width, height, cornerRadius);
		}
		PushOutline();
	}

	public void SetResting(bool atRest)
	{
		if (_resting != atRest)
		{
			_resting = atRest;
			Update();
		}
	}

	public void SetPresenceFull(bool full)
	{
		if (_presenceFull != full)
		{
			_presenceFull = full;
			Update();
		}
	}

	public void Trigger()
	{
		_runUntil = DateTime.UtcNow.AddSeconds(_look.EventRunSeconds);
		Update();
	}

	public void PlayCharging()
	{
		if (!((DateTime.UtcNow - _lastCharging).TotalSeconds < 10.0))
		{
			_lastCharging = DateTime.UtcNow;
			_chargingUntil = DateTime.UtcNow.AddSeconds(EdgeLightLook.ChargingTotalSeconds);
			Update();
		}
	}

	public void SyncWindow()
	{
		if (_surface != null)
		{
			_surface.SyncWindow();
		}
		PushOutline();
	}

	public void Refresh()
	{
		Update();
	}

	public void InvalidateSurface()
	{
		EdgeLightWpf edgeLightWpf = _surface as EdgeLightWpf;
		if (_surface != null)
		{
			_surface.Dispose();
		}
		if (edgeLightWpf != null)
		{
			_card.Children.Remove(edgeLightWpf);
		}
		_surface = null;
		_mode = EdgeLightMode.Off;
		_shown = (_running = false);
	}

	public bool SetBeat(bool on, Color color)
	{
		on = on && !PetPreferences.Instance.Enabled;
		try
		{
			if (on && !EnsureSurface())
			{
				return false;
			}
			if (!(_surface is EdgeLightComposition edgeLightComposition))
			{
				return false;
			}
			if (on)
			{
				PushOutline();
			}
			edgeLightComposition.SetBeat(on, color);
			return on;
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("[EdgeLight] beat glow: " + ex.Message);
			return false;
		}
	}

	public void SetBeatLevel(float level)
	{
		if (_surface is EdgeLightComposition edgeLightComposition)
		{
			edgeLightComposition.SetBeatLevel(level);
		}
	}

	private EdgeLightMode Decide(DateTime now)
	{
		if (!_enabled || !(_look.Intensity > 0.005) || !_resting || !_presenceFull || _outline == null || _win.Visibility != 0 || !_win.IsVisible)
		{
			return EdgeLightMode.Off;
		}
		bool animationsAllowed = AnimationsAllowed;
		if (now < _chargingUntil)
		{
			if (!animationsAllowed)
			{
				return EdgeLightMode.Static;
			}
			return EdgeLightMode.Charging;
		}
		if (_look.Always || now < _runUntil)
		{
			if (!animationsAllowed)
			{
				return EdgeLightMode.Static;
			}
			return EdgeLightMode.Animated;
		}
		return EdgeLightMode.Off;
	}

	private void Update()
	{
		try
		{
			DateTime utcNow = DateTime.UtcNow;
			EdgeLightMode edgeLightMode = Decide(utcNow);
			if (edgeLightMode != 0 && !EnsureSurface())
			{
				edgeLightMode = EdgeLightMode.Off;
			}
			bool flag = edgeLightMode == EdgeLightMode.Off && _mode != 0 && _resting && _presenceFull && _enabled && !_look.Always;
			if (edgeLightMode != _mode)
			{
				_mode = edgeLightMode;
				if (edgeLightMode != 0)
				{
					PushOutline();
					_surface.SetLook(_look);
				}
				if (_surface != null)
				{
					EdgeLightComposition edgeLightComposition = _surface as EdgeLightComposition;
					if (flag)
					{
						edgeLightComposition?.FadeOutSlow();
					}
					_surface.SetMode(edgeLightMode);
				}
			}
			_shown = edgeLightMode != EdgeLightMode.Off;
			_running = edgeLightMode == EdgeLightMode.Animated || edgeLightMode == EdgeLightMode.Charging;
			ScheduleNext(utcNow);
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("[EdgeLight] disabled after error: " + ex.GetType().Name + ": " + ex.Message);
			try
			{
				if (_surface != null)
				{
					_surface.SetMode(EdgeLightMode.Off);
				}
			}
			catch
			{
			}
			_mode = EdgeLightMode.Off;
			_shown = false;
			_running = false;
		}
	}

	private void ScheduleNext(DateTime now)
	{
		DateTime dateTime = DateTime.MaxValue;
		if (_chargingUntil > now)
		{
			dateTime = _chargingUntil;
		}
		if (!_look.Always && _runUntil > now && _runUntil < dateTime)
		{
			dateTime = _runUntil;
		}
		if (dateTime == DateTime.MaxValue)
		{
			if (_timer != null)
			{
				_timer.Stop();
			}
			return;
		}
		if (_timer == null)
		{
			_timer = new DispatcherTimer(DispatcherPriority.Normal);
			_timer.Tick += delegate
			{
				_timer.Stop();
				Update();
			};
		}
		_timer.Stop();
		_timer.Interval = TimeSpan.FromMilliseconds(Math.Max(15.0, (dateTime - now).TotalMilliseconds + 5.0));
		_timer.Start();
	}

	private bool EnsureSurface()
	{
		if (_surface != null)
		{
			return true;
		}
		IntPtr handle = new WindowInteropHelper(_win).Handle;
		if (handle == IntPtr.Zero)
		{
			return false;
		}
		_surface = CreateComposition(handle, out var error);
		if (_surface == null)
		{
			TransactionLogger.Warn("[EdgeLight] compositor unavailable, drawing with WPF: " + error);
			EdgeLightWpf edgeLightWpf = new EdgeLightWpf();
			int val = _card.Children.IndexOf(_content);
			_card.Children.Insert(Math.Max(0, val), edgeLightWpf);
			_surface = edgeLightWpf;
		}
		_surface.SetLook(_look);
		return true;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static IEdgeLightSurface CreateComposition(IntPtr hwnd, out string error)
	{
		error = null;
		try
		{
			return EdgeLightComposition.TryCreate(hwnd, out error);
		}
		catch (Exception ex)
		{
			error = ex.GetType().Name + ": " + ex.Message;
			return null;
		}
	}

	private void PushOutline()
	{
		if (_surface == null || _outline == null)
		{
			return;
		}
		try
		{
			PresentationSource presentationSource = PresentationSource.FromVisual(_win);
			if (presentationSource == null || presentationSource.CompositionTarget == null)
			{
				return;
			}
			_scale = presentationSource.CompositionTarget.TransformToDevice.M11;
			EdgeLightOutline edgeLightOutline = _outline;
			if (_card.IsArrangeValid && _card.ActualWidth > 4.0 && _card.ActualHeight > 4.0 && Math.Abs(_card.ActualWidth - _outline.Width) < 1.0 && Math.Abs(_card.ActualHeight - _outline.Height) < 1.0)
			{
				string text = _outlineKey + "|" + _card.ActualWidth.ToString("R") + "|" + _card.ActualHeight.ToString("R");
				if (_arrangedOutlineKey != text)
				{
					_arrangedOutlineKey = text;
					_arrangedOutline = EdgeLightOutline.For(_outlineStyle, _card.ActualWidth, _card.ActualHeight, _outlineCornerRadius);
				}
				edgeLightOutline = _arrangedOutline;
			}
			_offset = (_card.IsDescendantOf(_win) ? _card.TransformToAncestor(_win).Transform(new Point(0.0, 0.0)) : default(Point));
			if (_card.IsDescendantOf(_win) && GetWindowRect(new WindowInteropHelper(_win).Handle, out var bounds))
			{
				Point point = _card.PointToScreen(default(Point));
				_offset = new Point((point.X - (double)bounds.Left) / _scale, (point.Y - (double)bounds.Top) / _scale);
			}
			Transform renderTransform = _card.RenderTransform;
			if (renderTransform != null)
			{
				_offset.X -= renderTransform.Value.OffsetX;
				_offset.Y -= renderTransform.Value.OffsetY;
			}
			if (_card.ActualWidth > 0.0 && Math.Abs(_card.ActualWidth - edgeLightOutline.Width) > 0.5)
			{
				_offset.X += (_card.ActualWidth - edgeLightOutline.Width) / 2.0;
			}
			_surface.SetOutline(edgeLightOutline, _offset, _scale);
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("[EdgeLight] outline: " + ex.Message);
		}
	}

	private void OnCardLayoutUpdated(object sender, EventArgs e)
	{
		if (_shown)
		{
			PushOutline();
		}
	}

	private void OnPolicyChanged(object sender, EventArgs e)
	{
		_win.Dispatcher.BeginInvoke(new Action(Update));
	}

	private void OnSystemParameterChanged(object sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "ClientAreaAnimation")
		{
			_win.Dispatcher.BeginInvoke(new Action(Update));
		}
	}

	public void Dispose()
	{
		_card.LayoutUpdated -= OnCardLayoutUpdated;
		PowerSaver.Changed -= OnPolicyChanged;
		CapsuleMotion.AnimationsPolicyChanged -= OnPolicyChanged;
		SystemParameters.StaticPropertyChanged -= OnSystemParameterChanged;
		if (_timer != null)
		{
			_timer.Stop();
		}
		if (_surface != null)
		{
			try
			{
				_surface.Dispose();
			}
			catch
			{
			}
		}
		if (_surface is EdgeLightWpf element)
		{
			_card.Children.Remove(element);
		}
		_surface = null;
		_mode = EdgeLightMode.Off;
		_shown = false;
		_running = false;
	}
}
