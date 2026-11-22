using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Windows.Foundation.Metadata;
using Windows.Graphics;
using Windows.UI;
using Windows.UI.Composition;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.BuddyCapsule.Renderer;

internal sealed class EdgeLightComposition : IEdgeLightSurface, IDisposable
{
	private struct DispatcherQueueOptions
	{
		public int dwSize;

		public int threadType;

		public int apartmentType;
	}

	[ComImport]
	[System.Runtime.InteropServices.Guid("29E691FA-4567-4DCA-B319-D0F207EB6807")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface ICompositorDesktopInterop
	{
		void CreateDesktopWindowTarget(IntPtr hwnd, bool topmost, out IntPtr target);
	}

	[ComImport]
	[System.Runtime.InteropServices.Guid("0657AF73-53FD-47CF-84FF-C8492D2A80A3")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IGeometrySource2DInterop
	{
		[PreserveSig]
		int GetGeometry(out IntPtr value);

		[PreserveSig]
		int TryGetGeometryUsingFactory(IntPtr factory, out IntPtr value);
	}

	private struct P2
	{
		public float X;

		public float Y;
	}

	private delegate int CreatePathGeometryFn(IntPtr self, out IntPtr geom);

	private delegate int OpenFn(IntPtr self, out IntPtr sink);

	private delegate void SetFillModeFn(IntPtr self, int mode);

	private delegate void BeginFigureFn(IntPtr self, P2 p, int begin);

	private delegate void AddLinesFn(IntPtr self, [In] P2[] pts, int n);

	private delegate void EndFigureFn(IntPtr self, int end);

	private delegate int CloseFn(IntPtr self);

	private delegate IntPtr WndProcFn(IntPtr h, uint m, IntPtr w, IntPtr l);

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct WNDCLASSEX
	{
		public int cbSize;

		public int style;

		public WndProcFn lpfnWndProc;

		public int cbClsExtra;

		public int cbWndExtra;

		public IntPtr hInstance;

		public IntPtr hIcon;

		public IntPtr hCursor;

		public IntPtr hbrBackground;

		public string lpszMenuName;

		public string lpszClassName;

		public IntPtr hIconSm;
	}

	private struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private sealed class GeometrySource : IGeometrySource2D, IGeometrySource2DInterop
	{
		private IntPtr _g;

		public GeometrySource(IntPtr g)
		{
			_g = g;
		}

		public int GetGeometry(out IntPtr value)
		{
			Marshal.AddRef(_g);
			value = _g;
			return 0;
		}

		public int TryGetGeometryUsingFactory(IntPtr factory, out IntPtr value)
		{
			value = IntPtr.Zero;
			return -2147467263;
		}

		~GeometrySource()
		{
			if (_g != IntPtr.Zero)
			{
				Marshal.Release(_g);
				_g = IntPtr.Zero;
			}
		}
	}

	private const uint SWP_NOSIZE = 1u;

	private const uint SWP_NOMOVE = 2u;

	private const uint SWP_NOACTIVATE = 16u;

	private const uint SWP_NOOWNERZORDER = 512u;

	private const string ClassName = "AynEdgeLight";

	public const double BeatGlowDepth = 13.0;

	public const double BeatGlowMax = 0.7;

	private readonly NativeSurfaceRegion _nativeRegion = new NativeSurfaceRegion();

	private static IntPtr _dispatcherQueue;

	private static Compositor _compositor;

	private static IntPtr _d2dFactory;

	private static bool _classRegistered;

	private static readonly WndProcFn Proc = (IntPtr h, uint m, IntPtr w, IntPtr l) => m switch
	{
		33u => new IntPtr(3), 
		132u => new IntPtr(-1), 
		_ => DefWindowProc(h, m, w, l), 
	};

	private readonly IntPtr _notch;

	private IntPtr _hwnd;

	private object _target;

	private Windows.UI.Composition.ContainerVisual _root;

	private Windows.UI.Composition.ContainerVisual _fade;

	private Windows.UI.Composition.ContainerVisual _clipHost;

	private Windows.UI.Composition.ContainerVisual _level;

	private Windows.UI.Composition.ContainerVisual _layer;

	private ShapeVisual _shapes;

	private CompositionVisualSurface _strokeSurface;

	private SpriteVisual _glow;

	private DropShadow _shadow;

	private CompositionPropertySet _props;

	private readonly List<CompositionPathGeometry> _geoms = new List<CompositionPathGeometry>();

	private readonly List<CompositionLinearGradientBrush> _fadeBrushes = new List<CompositionLinearGradientBrush>();

	private readonly List<string> _running = new List<string>();

	private readonly List<Action> _stop = new List<Action>();

	private CompositionPath _path;

	private EdgeLightOutline _outline;

	private Point _offset;

	private double _scale = 1.0;

	private EdgeLightLook _look = new EdgeLightLook();

	private EdgeLightMode _mode;

	private RECT _lastRect;

	private DispatcherTimer _hideTimer;

	private bool _windowShown;

	private EdgeLightOutline _drawnOutline;

	private Point _drawnOffset;

	private double _drawnScale;

	private RECT _drawnRect;

	private bool _pathDirty = true;

	private Windows.UI.Composition.ContainerVisual _beatHost;

	private SpriteVisual _beatSprite;

	private CompositionRadialGradientBrush _beatBrush;

	private bool _beatOn;

	private System.Windows.Media.Color _beatColor = System.Windows.Media.Colors.White;

	private float _lastBeatLevel = -1f;

	public int RunningAnimations => _running.Count;

	public Point[] AppliedPathScreen
	{
		get
		{
			EdgeLightOutline drawnOutline = _drawnOutline;
			if (drawnOutline == null)
			{
				return null;
			}
			Point[] points = drawnOutline.Points;
			Point[] array = new Point[points.Length];
			for (int i = 0; i < points.Length; i++)
			{
				ref Point reference = ref array[i];
				reference = new Point((double)_drawnRect.Left + (points[i].X + _drawnOffset.X) * _drawnScale, (double)_drawnRect.Top + (points[i].Y + _drawnOffset.Y) * _drawnScale);
			}
			return array;
		}
	}

	public int BuildCount { get; private set; }

	public bool WindowShown
	{
		get
		{
			if (_windowShown)
			{
				return IsWindowVisible(_hwnd);
			}
			return false;
		}
	}

	public IntPtr Handle => _hwnd;

	private static bool Animations
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

	public bool BeatOn => _beatOn;

	public float BeatOpacity
	{
		get
		{
			if (_beatHost == null)
			{
				return 0f;
			}
			return _beatHost.Opacity;
		}
	}

	[DllImport("CoreMessaging.dll")]
	private static extern int CreateDispatcherQueueController(DispatcherQueueOptions o, out IntPtr ctrl);

	[DllImport("d2d1.dll")]
	private static extern int D2D1CreateFactory(int type, ref Guid riid, IntPtr opts, out IntPtr factory);

	private static T Slot<T>(IntPtr com, int slot) where T : class
	{
		IntPtr ptr = Marshal.ReadIntPtr(com);
		return (T)(object)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(ptr, slot * IntPtr.Size), typeof(T));
	}

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern ushort RegisterClassEx(ref WNDCLASSEX c);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr CreateWindowEx(int ex, string cls, string name, int style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr p);

	[DllImport("user32.dll")]
	private static extern IntPtr DefWindowProc(IntPtr h, uint m, IntPtr w, IntPtr l);

	[DllImport("user32.dll")]
	private static extern bool DestroyWindow(IntPtr h);

	[DllImport("user32.dll")]
	private static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr h, int cmd);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(IntPtr h);

	[DllImport("user32.dll")]
	private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr h, out RECT r);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern IntPtr GetModuleHandle(string n);

	private static CompositionPath Path(List<P2[]> figures, List<bool> closed, bool alternate)
	{
		if (_d2dFactory == IntPtr.Zero)
		{
			Guid riid = new Guid("06152247-6f50-465a-9245-118bfd3b6007");
			Marshal.ThrowExceptionForHR(D2D1CreateFactory(1, ref riid, IntPtr.Zero, out _d2dFactory));
		}
		Marshal.ThrowExceptionForHR(Slot<CreatePathGeometryFn>(_d2dFactory, 10)(_d2dFactory, out var geom));
		Marshal.ThrowExceptionForHR(Slot<OpenFn>(geom, 17)(geom, out var sink));
		try
		{
			Slot<SetFillModeFn>(sink, 3)(sink, (!alternate) ? 1 : 0);
			for (int i = 0; i < figures.Count; i++)
			{
				P2[] array = figures[i];
				Slot<BeginFigureFn>(sink, 5)(sink, array[0], (!closed[i]) ? 1 : 0);
				P2[] array2 = new P2[array.Length - 1];
				Array.Copy(array, 1, array2, 0, array2.Length);
				Slot<AddLinesFn>(sink, 6)(sink, array2, array2.Length);
				Slot<EndFigureFn>(sink, 8)(sink, closed[i] ? 1 : 0);
			}
			Marshal.ThrowExceptionForHR(Slot<CloseFn>(sink, 9)(sink));
		}
		finally
		{
			Marshal.Release(sink);
		}
		return new CompositionPath(new GeometrySource(geom));
	}

	public static EdgeLightComposition TryCreate(IntPtr notchHwnd, out string error)
	{
		error = null;
		try
		{
			return new EdgeLightComposition(notchHwnd);
		}
		catch (Exception ex)
		{
			error = ex.GetType().Name + ": " + ex.Message;
			return null;
		}
	}

	internal static Compositor SharedCompositor()
	{
		if (_dispatcherQueue == IntPtr.Zero)
		{
			DispatcherQueueOptions dispatcherQueueOptions = default(DispatcherQueueOptions);
			dispatcherQueueOptions.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
			dispatcherQueueOptions.threadType = 2;
			dispatcherQueueOptions.apartmentType = 2;
			DispatcherQueueOptions o = dispatcherQueueOptions;
			Marshal.ThrowExceptionForHR(CreateDispatcherQueueController(o, out _dispatcherQueue));
		}
		if (_compositor == null)
		{
			_compositor = new Compositor();
		}
		return _compositor;
	}

	internal static IntPtr CreateCompositionWindow(string title, out object target)
	{
		Compositor compositor = SharedCompositor();
		IntPtr moduleHandle = GetModuleHandle(null);
		if (!_classRegistered)
		{
			WNDCLASSEX wNDCLASSEX = default(WNDCLASSEX);
			wNDCLASSEX.cbSize = Marshal.SizeOf(typeof(WNDCLASSEX));
			wNDCLASSEX.lpfnWndProc = Proc;
			wNDCLASSEX.hInstance = moduleHandle;
			wNDCLASSEX.lpszClassName = "AynEdgeLight";
			WNDCLASSEX c = wNDCLASSEX;
			RegisterClassEx(ref c);
			_classRegistered = true;
		}
		IntPtr intPtr = CreateWindowEx(136839336, "AynEdgeLight", title, int.MinValue, 0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, moduleHandle, IntPtr.Zero);
		if (intPtr == IntPtr.Zero)
		{
			throw new InvalidOperationException("CreateWindowEx failed (" + Marshal.GetLastWin32Error() + ")");
		}
		SetLayeredWindowAttributes(intPtr, 0u, byte.MaxValue, 2u);
		((ICompositorDesktopInterop)(object)compositor).CreateDesktopWindowTarget(intPtr, topmost: true, out var target2);
		target = Marshal.GetObjectForIUnknown(target2);
		Marshal.Release(target2);
		return intPtr;
	}

	private EdgeLightComposition(IntPtr notchHwnd)
	{
		_notch = notchHwnd;
		Compositor compositor = SharedCompositor();
		_hwnd = CreateCompositionWindow("Ayn edge light", out _target);
		_root = compositor.CreateContainerVisual();
		_root.RelativeSizeAdjustment = Vector2.One;
		_fade = compositor.CreateContainerVisual();
		_fade.RelativeSizeAdjustment = Vector2.One;
		_fade.Opacity = 0f;
		_clipHost = compositor.CreateContainerVisual();
		_clipHost.RelativeSizeAdjustment = Vector2.One;
		_level = compositor.CreateContainerVisual();
		_level.RelativeSizeAdjustment = Vector2.One;
		_shapes = compositor.CreateShapeVisual();
		_shapes.RelativeSizeAdjustment = Vector2.One;
		_shadow = compositor.CreateDropShadow();
		_shadow.Offset = Vector3.Zero;
		if (ApiInformation.IsMethodPresent("Windows.UI.Composition.Compositor", "CreateVisualSurface"))
		{
			_layer = compositor.CreateContainerVisual();
			_strokeSurface = compositor.CreateVisualSurface();
			_strokeSurface.SourceVisual = _shapes;
			CompositionSurfaceBrush compositionSurfaceBrush = compositor.CreateSurfaceBrush(_strokeSurface);
			compositionSurfaceBrush.Stretch = CompositionStretch.None;
			_shadow.Mask = compositionSurfaceBrush;
			_glow = compositor.CreateSpriteVisual();
			_glow.RelativeSizeAdjustment = Vector2.One;
			_glow.Shadow = _shadow;
			_layer.Children.InsertAtTop(_glow);
		}
		else
		{
			LayerVisual layerVisual = compositor.CreateLayerVisual();
			_shadow.SourcePolicy = CompositionDropShadowSourcePolicy.InheritFromVisualContent;
			layerVisual.Shadow = _shadow;
			_layer = layerVisual;
		}
		_layer.RelativeSizeAdjustment = Vector2.One;
		_layer.Children.InsertAtTop(_shapes);
		_level.Children.InsertAtTop(_layer);
		_clipHost.Children.InsertAtTop(_level);
		_fade.Children.InsertAtTop(_clipHost);
		_root.Children.InsertAtTop(_fade);
		BuildBeatVisuals(compositor);
		((Windows.UI.Composition.CompositionTarget)_target).Root = _root;
		_props = compositor.CreatePropertySet();
		_props.InsertScalar("p", 0f);
		_props.InsertScalar("v", 1f);
		_props.InsertScalar("f", 0f);
	}

	public void SetOutline(EdgeLightOutline outline, Point cardOffset, double scale)
	{
		if (outline == null || (!(Math.Abs(cardOffset.X - _offset.X) > 0.01) && !(Math.Abs(cardOffset.Y - _offset.Y) > 0.01) && !(Math.Abs(scale - _scale) > 0.001) && outline.SameShape(_outline)))
		{
			return;
		}
		_outline = outline;
		_offset = cardOffset;
		_scale = scale;
		if (_mode == EdgeLightMode.Off && !_beatOn)
		{
			_pathDirty = true;
			return;
		}
		SyncWindow();
		ApplyPath();
		if (_geoms.Count == 0 && _mode != 0)
		{
			Build();
		}
	}

	public void SetLook(EdgeLightLook look)
	{
		if (!(look.Key == _look.Key))
		{
			bool flag = look.Style != _look.Style || look.Color != _look.Color || look.Speed != _look.Speed;
			_look = look;
			_level.Opacity = (float)look.Intensity;
			if (flag && _mode != 0)
			{
				Build();
			}
		}
	}

	public void SetMode(EdgeLightMode mode)
	{
		if (mode == _mode)
		{
			return;
		}
		EdgeLightMode mode2 = _mode;
		_mode = mode;
		if (mode == EdgeLightMode.Off)
		{
			FadeTo(0f, 0.09);
			if (_hideTimer == null)
			{
				_hideTimer = new DispatcherTimer(DispatcherPriority.Background)
				{
					Interval = TimeSpan.FromMilliseconds(130.0)
				};
				_hideTimer.Tick += delegate
				{
					_hideTimer.Stop();
					if (_mode == EdgeLightMode.Off)
					{
						StopAll();
						if (!_beatOn)
						{
							Show(on: false);
						}
					}
				};
			}
			_hideTimer.Stop();
			_hideTimer.Start();
		}
		else
		{
			if (_hideTimer != null)
			{
				_hideTimer.Stop();
			}
			if (_pathDirty || _path == null)
			{
				_lastRect = default(RECT);
				SyncWindow();
				ApplyPath();
			}
			Build();
			Show(on: true);
			if (mode2 == EdgeLightMode.Off)
			{
				_fade.Opacity = 0f;
				FadeTo(1f, Animations ? 0.18 : 0.0);
			}
			else
			{
				FadeTo(1f, 0.0);
			}
		}
	}

	public void FadeOutSlow()
	{
		FadeTo(0f, 0.6);
	}

	private void FadeTo(float to, double seconds)
	{
		_fade.StopAnimation("Opacity");
		if (seconds <= 0.0 || !Animations)
		{
			_fade.Opacity = to;
			return;
		}
		ScalarKeyFrameAnimation scalarKeyFrameAnimation = _compositor.CreateScalarKeyFrameAnimation();
		scalarKeyFrameAnimation.InsertExpressionKeyFrame(0f, "this.StartingValue");
		scalarKeyFrameAnimation.InsertKeyFrame(1f, to, _compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0f), new Vector2(0.2f, 1f)));
		scalarKeyFrameAnimation.Duration = TimeSpan.FromSeconds(seconds);
		_fade.StartAnimation("Opacity", scalarKeyFrameAnimation);
	}

	private void Show(bool on)
	{
		if (on == _windowShown)
		{
			if (on)
			{
				SyncWindow();
			}
		}
		else
		{
			_windowShown = on;
			SyncWindow();
		}
	}

	public void SyncWindow()
	{
		if (!_windowShown || !IsWindowVisible(_notch))
		{
			ShowWindow(_hwnd, 0);
			_nativeRegion.Apply(_hwnd, Rect.Empty);
			SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0, 20u);
			_lastRect = default(RECT);
		}
		else
		{
			if (_notch == IntPtr.Zero || !GetWindowRect(_notch, out var r))
			{
				return;
			}
			if (r.Left != _lastRect.Left || r.Top != _lastRect.Top || r.Right != _lastRect.Right || r.Bottom != _lastRect.Bottom)
			{
				_lastRect = r;
				SetWindowPos(_hwnd, _notch, r.Left, r.Top, Math.Max(1, r.Right - r.Left), Math.Max(1, r.Bottom - r.Top), 528u);
				Vector2 value = new Vector2(Math.Max(1, r.Right - r.Left), Math.Max(1, r.Bottom - r.Top));
				Windows.UI.Composition.Visual[] array = new Windows.UI.Composition.Visual[8] { _root, _fade, _clipHost, _level, _layer, _shapes, _glow, _beatHost };
				foreach (Windows.UI.Composition.Visual visual in array)
				{
					if (visual != null)
					{
						visual.RelativeSizeAdjustment = Vector2.Zero;
						visual.Size = value;
					}
				}
				ApplyPath();
			}
			else if (_windowShown)
			{
				KeepBelowNotch();
			}
			if (BoundNativeSurface() && !IsWindowVisible(_hwnd))
			{
				ShowWindow(_hwnd, 4);
			}
		}
	}

	private bool BoundNativeSurface()
	{
		if (!_windowShown || _outline == null)
		{
			return _nativeRegion.Apply(_hwnd, Rect.Empty);
		}
		Rect drawing = new Rect(_offset.X * _scale, _offset.Y * _scale, _outline.Width * _scale, _outline.Height * _scale);
		drawing.Inflate((18.0 + _look.Intensity * 2.0) * _scale, (18.0 + _look.Intensity * 2.0) * _scale);
		return _nativeRegion.Apply(_hwnd, drawing);
	}

	private void KeepBelowNotch()
	{
		SetWindowPos(_hwnd, _notch, 0, 0, 0, 0, 531u);
	}

	private P2[] ToPx(Point[] pts)
	{
		P2[] array = new P2[pts.Length];
		for (int i = 0; i < pts.Length; i++)
		{
			ref P2 reference = ref array[i];
			reference = new P2
			{
				X = (float)((pts[i].X + _offset.X) * _scale),
				Y = (float)((pts[i].Y + _offset.Y) * _scale)
			};
		}
		return array;
	}

	private void ApplyPath()
	{
		if (_outline == null)
		{
			return;
		}
		_pathDirty = false;
		_path = Path(new List<P2[]> { ToPx(_outline.Points) }, new List<bool> { false }, alternate: false);
		foreach (CompositionPathGeometry geom in _geoms)
		{
			geom.Path = _path;
		}
		_drawnOutline = _outline;
		_drawnOffset = _offset;
		_drawnScale = _scale;
		_drawnRect = _lastRect;
		float x = Math.Max(1, _lastRect.Right - _lastRect.Left);
		float y = Math.Max(1, _lastRect.Bottom - _lastRect.Top);
		P2[] array = new P2[4]
		{
			new P2
			{
				X = 0f,
				Y = 0f
			},
			new P2
			{
				X = x,
				Y = 0f
			},
			new P2
			{
				X = x,
				Y = y
			},
			new P2
			{
				X = 0f,
				Y = y
			}
		};
		CompositionPathGeometry geometry = _compositor.CreatePathGeometry(Path(new List<P2[]>
		{
			array,
			ToPx(_outline.Fill)
		}, new List<bool> { true, true }, alternate: true));
		_clipHost.Clip = _compositor.CreateGeometricClip(geometry);
		ApplyBeatPath(array);
		float num = (float)((_outline.Top + _offset.Y) * _scale);
		float val = (float)(_outline.FadeTop * _scale);
		foreach (CompositionLinearGradientBrush fadeBrush in _fadeBrushes)
		{
			fadeBrush.StartPoint = new Vector2(0f, num);
			fadeBrush.EndPoint = new Vector2(0f, num + Math.Max(0.5f, val));
		}
		if (_strokeSurface != null)
		{
			_strokeSurface.SourceSize = new Vector2(x, y);
		}
		_shadow.BlurRadius = (float)(6.0 * _scale);
		BoundNativeSurface();
	}

	private void StopAll()
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
		_running.Clear();
	}

	private void Start(CompositionObject target, string property, CompositionAnimation a, string name)
	{
		target.StartAnimation(property, a);
		_running.Add(name);
		_stop.Add(delegate
		{
			target.StopAnimation(property);
		});
	}

	private static Windows.UI.Color W(System.Windows.Media.Color c, double alpha)
	{
		return Windows.UI.Color.FromArgb((byte)Math.Round(255.0 * Math.Max(0.0, Math.Min(1.0, alpha))), c.R, c.G, c.B);
	}

	private CompositionPathGeometry Stroke(System.Windows.Media.Color color, double alpha)
	{
		Compositor compositor = _compositor;
		CompositionPathGeometry compositionPathGeometry = compositor.CreatePathGeometry(_path);
		CompositionSpriteShape compositionSpriteShape = compositor.CreateSpriteShape(compositionPathGeometry);
		compositionSpriteShape.StrokeBrush = StrokeBrush(color, alpha);
		compositionSpriteShape.StrokeThickness = (float)(2.5 * _scale);
		compositionSpriteShape.StrokeStartCap = CompositionStrokeCap.Round;
		compositionSpriteShape.StrokeEndCap = CompositionStrokeCap.Round;
		compositionSpriteShape.StrokeLineJoin = CompositionStrokeLineJoin.Round;
		((ICollection<CompositionShape>)_shapes.Shapes).Add((CompositionShape)compositionSpriteShape);
		_geoms.Add(compositionPathGeometry);
		return compositionPathGeometry;
	}

	private CompositionLinearGradientBrush StrokeBrush(System.Windows.Media.Color color, double alpha)
	{
		Compositor compositor = _compositor;
		CompositionLinearGradientBrush compositionLinearGradientBrush = compositor.CreateLinearGradientBrush();
		compositionLinearGradientBrush.MappingMode = CompositionMappingMode.Absolute;
		bool flag = _outline != null && !_outline.Closed;
		((ICollection<CompositionColorGradientStop>)compositionLinearGradientBrush.ColorStops).Add(compositor.CreateColorGradientStop(0f, W(color, flag ? 0.0 : alpha)));
		if (flag)
		{
			((ICollection<CompositionColorGradientStop>)compositionLinearGradientBrush.ColorStops).Add(compositor.CreateColorGradientStop(0.55f, W(color, alpha * 0.55)));
		}
		((ICollection<CompositionColorGradientStop>)compositionLinearGradientBrush.ColorStops).Add(compositor.CreateColorGradientStop(1f, W(color, alpha)));
		_fadeBrushes.Add(compositionLinearGradientBrush);
		return compositionLinearGradientBrush;
	}

	private void Trim(CompositionPathGeometry g, string start, string end, float len)
	{
		ExpressionAnimation expressionAnimation = _compositor.CreateExpressionAnimation(start);
		expressionAnimation.SetReferenceParameter("P", _props);
		expressionAnimation.SetScalarParameter("L", len);
		ExpressionAnimation expressionAnimation2 = _compositor.CreateExpressionAnimation(end);
		expressionAnimation2.SetReferenceParameter("P", _props);
		expressionAnimation2.SetScalarParameter("L", len);
		Start(g, "TrimStart", expressionAnimation, "trim");
		Start(g, "TrimEnd", expressionAnimation2, "trim");
	}

	private void Build()
	{
		StopAll();
		((ICollection<CompositionShape>)_shapes.Shapes).Clear();
		_geoms.Clear();
		_fadeBrushes.Clear();
		_layer.Opacity = 1f;
		if (_outline == null || _path == null || _mode == EdgeLightMode.Off)
		{
			return;
		}
		BuildCount++;
		Compositor compositor = _compositor;
		bool flag = Animations && _mode != EdgeLightMode.Static;
		System.Windows.Media.Color color = ((_mode == EdgeLightMode.Charging) ? EdgeLightLook.ChargingColor : _look.Color);
		_level.Opacity = (float)_look.Intensity;
		_shadow.Color = W(color, 1.0);
		_shadow.Opacity = 0.45f;
		_shadow.BlurRadius = (float)(6.0 * _scale);
		if (!flag)
		{
			Stroke(color, 0.6);
			ApplyPath();
			return;
		}
		float num = (float)_look.CycleSeconds;
		if (_mode == EdgeLightMode.Charging)
		{
			CompositionPathGeometry g = Stroke(color, 0.85);
			Trim(g, "0", "Clamp(P.f * 0.5, 0, 1)", 0f);
			CompositionPathGeometry g2 = Stroke(color, 0.85);
			Trim(g2, "Clamp(1 - P.f * 0.5, 0, 1)", "1", 0f);
			CompositionPathGeometry g3 = Stroke(EdgeLightLook.Tint(color, 0.6), 1.0);
			Trim(g3, "Clamp(P.f * 0.5 - L, 0, 1)", "Clamp(P.f * 0.5, 0, 1)", 0.02f);
			CompositionPathGeometry g4 = Stroke(EdgeLightLook.Tint(color, 0.6), 1.0);
			Trim(g4, "Clamp(1 - P.f * 0.5, 0, 1)", "Clamp(1 - P.f * 0.5 + L, 0, 1)", 0.02f);
			_props.InsertScalar("f", 0f);
			ScalarKeyFrameAnimation scalarKeyFrameAnimation = compositor.CreateScalarKeyFrameAnimation();
			scalarKeyFrameAnimation.InsertKeyFrame(0f, 0f);
			scalarKeyFrameAnimation.InsertKeyFrame(1f, 1f, compositor.CreateCubicBezierEasingFunction(new Vector2(0.25f, 0.1f), new Vector2(0.25f, 1f)));
			scalarKeyFrameAnimation.Duration = TimeSpan.FromSeconds(1.1);
			Start(_props, "f", scalarKeyFrameAnimation, "charging");
			ApplyPath();
			return;
		}
		switch (_look.Style)
		{
		case EdgeLightStyle.Breathe:
		{
			Stroke(color, 0.9);
			ScalarKeyFrameAnimation scalarKeyFrameAnimation5 = compositor.CreateScalarKeyFrameAnimation();
			CubicBezierEasingFunction easingFunction4 = compositor.CreateCubicBezierEasingFunction(new Vector2(0.37f, 0f), new Vector2(0.63f, 1f));
			scalarKeyFrameAnimation5.InsertKeyFrame(0f, 0.3f);
			scalarKeyFrameAnimation5.InsertKeyFrame(0.5f, 1f, easingFunction4);
			scalarKeyFrameAnimation5.InsertKeyFrame(1f, 0.3f, easingFunction4);
			scalarKeyFrameAnimation5.Duration = TimeSpan.FromSeconds(num);
			scalarKeyFrameAnimation5.IterationBehavior = AnimationIterationBehavior.Forever;
			Start(_layer, "Opacity", scalarKeyFrameAnimation5, "breathe");
			break;
		}
		case EdgeLightStyle.TwinComets:
		{
			float num3 = 0.15f;
			double[][] cometLayers2 = EdgeLightLook.CometLayers;
			foreach (double[] array2 in cometLayers2)
			{
				float len2 = (float)(array2[0] * (double)num3 / 0.18);
				System.Windows.Media.Color color3 = ((array2 == EdgeLightLook.CometLayers[0]) ? EdgeLightLook.Tint(color, 0.6) : color);
				Trim(Stroke(color3, array2[1]), "Clamp(Min(P.p, P.p - L * P.v), 0, 1)", "Clamp(Max(P.p, P.p - L * P.v), 0, 1)", len2);
				Trim(Stroke(color3, array2[1]), "Clamp(Min(1 - P.p, 1 - P.p + L * P.v), 0, 1)", "Clamp(Max(1 - P.p, 1 - P.p + L * P.v), 0, 1)", len2);
			}
			CubicBezierEasingFunction easingFunction2 = compositor.CreateCubicBezierEasingFunction(new Vector2(0.45f, 0f), new Vector2(0.55f, 1f));
			ScalarKeyFrameAnimation scalarKeyFrameAnimation3 = compositor.CreateScalarKeyFrameAnimation();
			scalarKeyFrameAnimation3.InsertKeyFrame(0f, 0f);
			scalarKeyFrameAnimation3.InsertKeyFrame(0.5f, 0.5f, easingFunction2);
			scalarKeyFrameAnimation3.InsertKeyFrame(1f, 0f, easingFunction2);
			scalarKeyFrameAnimation3.Duration = TimeSpan.FromSeconds(num);
			scalarKeyFrameAnimation3.IterationBehavior = AnimationIterationBehavior.Forever;
			ScalarKeyFrameAnimation scalarKeyFrameAnimation4 = compositor.CreateScalarKeyFrameAnimation();
			CubicBezierEasingFunction easingFunction3 = compositor.CreateCubicBezierEasingFunction(new Vector2(0.37f, 0f), new Vector2(0.63f, 1f));
			scalarKeyFrameAnimation4.InsertKeyFrame(0f, 0f);
			scalarKeyFrameAnimation4.InsertKeyFrame(0.25f, 1f, easingFunction3);
			scalarKeyFrameAnimation4.InsertKeyFrame(0.5f, 0f, easingFunction3);
			scalarKeyFrameAnimation4.InsertKeyFrame(0.75f, -1f, easingFunction3);
			scalarKeyFrameAnimation4.InsertKeyFrame(1f, 0f, easingFunction3);
			scalarKeyFrameAnimation4.Duration = scalarKeyFrameAnimation3.Duration;
			scalarKeyFrameAnimation4.IterationBehavior = AnimationIterationBehavior.Forever;
			Start(_props, "p", scalarKeyFrameAnimation3, "twin.p");
			Start(_props, "v", scalarKeyFrameAnimation4, "twin.v");
			break;
		}
		default:
		{
			float num2 = 0.18f;
			bool closed = _outline.Closed;
			double[][] cometLayers = EdgeLightLook.CometLayers;
			foreach (double[] array in cometLayers)
			{
				float len = (float)array[0];
				System.Windows.Media.Color color2 = ((array == EdgeLightLook.CometLayers[0]) ? EdgeLightLook.Tint(color, 0.6) : color);
				Trim(Stroke(color2, array[1]), "Clamp(P.p - L, 0, 1)", "Clamp(P.p, 0, 1)", len);
				if (closed)
				{
					Trim(Stroke(color2, array[1]), "P.p < L ? 1 + P.p - L : 1", "1", len);
				}
			}
			ScalarKeyFrameAnimation scalarKeyFrameAnimation2 = compositor.CreateScalarKeyFrameAnimation();
			LinearEasingFunction easingFunction = compositor.CreateLinearEasingFunction();
			scalarKeyFrameAnimation2.InsertKeyFrame(0f, 0f);
			if (closed)
			{
				scalarKeyFrameAnimation2.InsertKeyFrame(1f, 1f, easingFunction);
			}
			else
			{
				scalarKeyFrameAnimation2.InsertKeyFrame(0.86f, 1f + num2, easingFunction);
				scalarKeyFrameAnimation2.InsertKeyFrame(1f, 1f + num2, easingFunction);
			}
			scalarKeyFrameAnimation2.Duration = TimeSpan.FromSeconds(num);
			scalarKeyFrameAnimation2.IterationBehavior = AnimationIterationBehavior.Forever;
			_props.InsertScalar("v", 1f);
			Start(_props, "p", scalarKeyFrameAnimation2, "comet.p");
			break;
		}
		}
		ApplyPath();
	}

	private void BuildBeatVisuals(Compositor c)
	{
		_beatHost = c.CreateContainerVisual();
		_beatHost.Opacity = 0f;
		_beatSprite = c.CreateSpriteVisual();
		_beatBrush = c.CreateRadialGradientBrush();
		_beatBrush.MappingMode = CompositionMappingMode.Absolute;
		_beatSprite.Brush = _beatBrush;
		ApplyBeatColor();
		_beatHost.Children.InsertAtTop(_beatSprite);
		_root.Children.InsertAtTop(_beatHost);
	}

	private void ApplyBeatColor()
	{
		Compositor compositor = _compositor;
		((ICollection<CompositionColorGradientStop>)_beatBrush.ColorStops).Clear();
		((ICollection<CompositionColorGradientStop>)_beatBrush.ColorStops).Add(compositor.CreateColorGradientStop(0f, W(_beatColor, 0.95)));
		((ICollection<CompositionColorGradientStop>)_beatBrush.ColorStops).Add(compositor.CreateColorGradientStop(0.35f, W(_beatColor, 0.55)));
		((ICollection<CompositionColorGradientStop>)_beatBrush.ColorStops).Add(compositor.CreateColorGradientStop(0.7f, W(_beatColor, 0.16)));
		((ICollection<CompositionColorGradientStop>)_beatBrush.ColorStops).Add(compositor.CreateColorGradientStop(1f, W(_beatColor, 0.0)));
	}

	private void ApplyBeatPath(P2[] windowRect)
	{
		if (_outline == null)
		{
			return;
		}
		double num = double.MaxValue;
		double num2 = double.MinValue;
		double num3 = 0.0;
		double num4 = _outline.Height * 0.55;
		Point[] fill = _outline.Fill;
		for (int i = 0; i < fill.Length; i++)
		{
			Point point = fill[i];
			num3 = Math.Max(num3, point.Y);
			if (point.Y >= num4)
			{
				num = Math.Min(num, point.X);
				num2 = Math.Max(num2, point.X);
			}
		}
		if (!(num > num2))
		{
			float x = windowRect[2].X;
			float y = windowRect[2].Y;
			_beatSprite.Size = new Vector2(x, y);
			float x2 = (float)(((num + num2) / 2.0 + _offset.X) * _scale);
			float y2 = (float)((num3 + _offset.Y) * _scale);
			_beatBrush.EllipseCenter = new Vector2(x2, y2);
			_beatBrush.EllipseRadius = new Vector2((float)((num2 - num) / 2.0 * 1.08 * _scale), (float)(13.0 * _scale));
			CompositionPathGeometry geometry = _compositor.CreatePathGeometry(Path(new List<P2[]>
			{
				windowRect,
				ToPx(_outline.Fill)
			}, new List<bool> { true, true }, alternate: true));
			_beatHost.Clip = _compositor.CreateGeometricClip(geometry);
		}
	}

	public void SetBeat(bool on, System.Windows.Media.Color color)
	{
		if (color != _beatColor)
		{
			_beatColor = color;
			ApplyBeatColor();
		}
		if (on == _beatOn)
		{
			return;
		}
		_beatOn = on;
		_beatHost.Opacity = 0f;
		_lastBeatLevel = -1f;
		if (on)
		{
			if (_hideTimer != null)
			{
				_hideTimer.Stop();
			}
			if (_pathDirty || _path == null)
			{
				_lastRect = default(RECT);
				SyncWindow();
				ApplyPath();
			}
			Show(on: true);
		}
		else if (_mode == EdgeLightMode.Off)
		{
			StopAll();
			Show(on: false);
		}
	}

	public void SetBeatLevel(float level)
	{
		if (_beatOn)
		{
			float num = (float)Math.Round((double)Math.Max(0f, Math.Min(1f, level)) * 0.7, 2);
			if (!(Math.Abs(num - _lastBeatLevel) < 0.005f))
			{
				_lastBeatLevel = num;
				_beatHost.Opacity = num;
			}
		}
	}

	public void Dispose()
	{
		try
		{
			if (_hideTimer != null)
			{
				_hideTimer.Stop();
			}
			StopAll();
			if (_target != null)
			{
				((Windows.UI.Composition.CompositionTarget)_target).Root = null;
			}
			_target = null;
			if (_strokeSurface != null)
			{
				_strokeSurface.SourceVisual = null;
			}
			_shadow.Mask = null;
			if (_hwnd != IntPtr.Zero)
			{
				DestroyWindow(_hwnd);
			}
			_hwnd = IntPtr.Zero;
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("[EdgeLight] dispose: " + ex.Message);
		}
	}
}
