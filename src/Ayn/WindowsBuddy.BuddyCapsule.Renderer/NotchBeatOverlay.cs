using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Windows.UI;
using Windows.UI.Composition;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.Core;

namespace WindowsBuddy.BuddyCapsule.Renderer;

internal sealed class NotchBeatOverlay : IDisposable
{
	private struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private const uint SWP_NOSIZE = 1u;

	private const uint SWP_NOMOVE = 2u;

	private const uint SWP_NOACTIVATE = 16u;

	private const uint SWP_NOOWNERZORDER = 512u;

	private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

	private readonly IntPtr _notch;

	private readonly IntPtr _hwnd;

	private object _target;

	private readonly Compositor _c;

	private readonly Windows.UI.Composition.ContainerVisual _root;

	private readonly ShapeVisual _line;

	private readonly ShapeVisual _mini;

	private readonly CompositionPropertySet _p;

	private readonly CompositionColorBrush _miniBrush;

	private CompositionLinearGradientBrush _lineBrush;

	private int _lineBars;

	private int _miniBars;

	private Rect _lineRect;

	private Rect _miniRect;

	private float _lineBarW;

	private float _miniBarW;

	private float _lineMaxH;

	private System.Windows.Media.Color _color = System.Windows.Media.Colors.White;

	private bool _shown;

	private bool _visible;

	private RECT _lastRect;

	private int _zTicks;

	private readonly NativeSurfaceRegion _nativeRegion = new NativeSurfaceRegion();

	private readonly List<IDisposable> _lineParts = new List<IDisposable>();

	private readonly List<IDisposable> _miniParts = new List<IDisposable>();

	private static readonly string[] Names = new string[14]
	{
		"v0", "v1", "v2", "v3", "v4", "v5", "v6", "v7", "v8", "v9",
		"v10", "v11", "v12", "v13"
	};

	private readonly Vector4[] _sent = new Vector4[14];

	private Vector4 _sentMini;

	public IntPtr Handle => _hwnd;

	public bool IsShown => _shown;

	public bool BarsVisible => _visible;

	public int LineBars => _lineBars;

	public int MiniBars => _miniBars;

	public Rect LineRect => _lineRect;

	public Rect MiniRect => _miniRect;

	public long Frames { get; private set; }

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr h, int cmd);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(IntPtr h);

	[DllImport("user32.dll")]
	private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr h, out RECT r);

	[DllImport("user32.dll")]
	private static extern bool DestroyWindow(IntPtr h);

	[DllImport("user32.dll")]
	private static extern IntPtr GetWindow(IntPtr h, uint cmd);

	public static NotchBeatOverlay TryCreate(IntPtr notch, out string error)
	{
		error = null;
		try
		{
			return new NotchBeatOverlay(notch);
		}
		catch (Exception ex)
		{
			error = ex.GetType().Name + ": " + ex.Message;
			return null;
		}
	}

	private NotchBeatOverlay(IntPtr notch)
	{
		_notch = notch;
		_c = EdgeLightComposition.SharedCompositor();
		_hwnd = EdgeLightComposition.CreateCompositionWindow("Ayn beat", out _target);
		_root = _c.CreateContainerVisual();
		_root.Opacity = 0f;
		_line = _c.CreateShapeVisual();
		_mini = _c.CreateShapeVisual();
		_root.Children.InsertAtTop(_line);
		_root.Children.InsertAtTop(_mini);
		((Windows.UI.Composition.CompositionTarget)_target).Root = _root;
		_p = _c.CreatePropertySet();
		for (int i = 0; i < 14; i++)
		{
			_p.InsertVector4("v" + i, Vector4.Zero);
		}
		_p.InsertVector4("m", Vector4.Zero);
		_miniBrush = _c.CreateColorBrush(W(_color, 1.0));
		ScalarKeyFrameAnimation scalarKeyFrameAnimation = _c.CreateScalarKeyFrameAnimation();
		scalarKeyFrameAnimation.InsertExpressionKeyFrame(1f, "this.FinalValue");
		scalarKeyFrameAnimation.Duration = TimeSpan.FromMilliseconds(140.0);
		scalarKeyFrameAnimation.Target = "Opacity";
		ImplicitAnimationCollection implicitAnimationCollection = _c.CreateImplicitAnimationCollection();
		((IDictionary<string, ICompositionAnimationBase>)implicitAnimationCollection)["Opacity"] = scalarKeyFrameAnimation;
		_root.ImplicitAnimations = implicitAnimationCollection;
	}

	private static Windows.UI.Color W(System.Windows.Media.Color c, double a)
	{
		return Windows.UI.Color.FromArgb((byte)Math.Round(255.0 * Math.Max(0.0, Math.Min(1.0, a))), c.R, c.G, c.B);
	}

	public void SetLayout(Rect line, int lineBars, double lineBarW, Rect mini, int miniBars, double miniBarW)
	{
		lineBars = Math.Max(0, Math.Min(56, lineBars));
		miniBars = Math.Max(0, Math.Min(4, miniBars));
		bool flag = lineBars != _lineBars || Math.Abs(lineBarW - (double)_lineBarW) > 0.01 || Math.Abs(line.Width - _lineRect.Width) > 0.5 || Math.Abs(line.Height - _lineRect.Height) > 0.5;
		bool flag2 = miniBars != _miniBars || Math.Abs(miniBarW - (double)_miniBarW) > 0.01 || Math.Abs(mini.Width - _miniRect.Width) > 0.5 || Math.Abs(mini.Height - _miniRect.Height) > 0.5;
		_lineRect = line;
		_miniRect = mini;
		_line.Offset = new Vector3((float)line.X, (float)line.Y, 0f);
		_mini.Offset = new Vector3((float)mini.X, (float)mini.Y, 0f);
		if (flag)
		{
			_lineBars = lineBars;
			_lineBarW = (float)lineBarW;
			BuildLine();
		}
		if (flag2)
		{
			_miniBars = miniBars;
			_miniBarW = (float)miniBarW;
			BuildMini();
		}
		BoundNativeSurface();
	}

	private static void Release(List<IDisposable> parts)
	{
		foreach (IDisposable part in parts)
		{
			try
			{
				if (part is CompositionLineGeometry compositionLineGeometry)
				{
					compositionLineGeometry.StopAnimation("End");
					compositionLineGeometry.StopAnimation("Start");
				}
				part.Dispose();
			}
			catch
			{
			}
		}
		parts.Clear();
	}

	private void BuildLine()
	{
		((ICollection<CompositionShape>)_line.Shapes).Clear();
		Release(_lineParts);
		if (_lineBrush != null)
		{
			_lineBrush.Dispose();
			_lineBrush = null;
		}
		_line.Size = new Vector2((float)Math.Max(1.0, _lineRect.Width), (float)Math.Max(1.0, _lineRect.Height));
		if (_lineBars != 0)
		{
			float num = (float)_lineRect.Width;
			float num2 = (float)_lineRect.Height;
			float lineBarW = _lineBarW;
			_lineMaxH = num2;
			_lineBrush = _c.CreateLinearGradientBrush();
			_lineBrush.MappingMode = CompositionMappingMode.Absolute;
			_lineBrush.StartPoint = new Vector2(0f, 0f);
			_lineBrush.EndPoint = new Vector2(num, 0f);
			ApplyLineColor();
			float num3 = (num - lineBarW) / (float)Math.Max(1, _lineBars - 1);
			float num4 = num2 - lineBarW / 2f;
			float value = Math.Max(0f, num2 - lineBarW);
			for (int i = 0; i < _lineBars; i++)
			{
				float num5 = lineBarW / 2f + (float)i * num3;
				CompositionLineGeometry compositionLineGeometry = _c.CreateLineGeometry();
				compositionLineGeometry.Start = new Vector2(num5, num4);
				compositionLineGeometry.End = new Vector2(num5, num4);
				CompositionSpriteShape compositionSpriteShape = _c.CreateSpriteShape(compositionLineGeometry);
				compositionSpriteShape.StrokeBrush = _lineBrush;
				compositionSpriteShape.StrokeThickness = lineBarW;
				compositionSpriteShape.StrokeStartCap = CompositionStrokeCap.Round;
				compositionSpriteShape.StrokeEndCap = CompositionStrokeCap.Round;
				((ICollection<CompositionShape>)_line.Shapes).Add((CompositionShape)compositionSpriteShape);
				ExpressionAnimation expressionAnimation = _c.CreateExpressionAnimation("Vector2(x, y - P.v" + i / 4 + "." + "XYZW"[i % 4] + " * r)");
				expressionAnimation.SetScalarParameter("x", num5);
				expressionAnimation.SetScalarParameter("y", num4);
				expressionAnimation.SetScalarParameter("r", value);
				expressionAnimation.SetReferenceParameter("P", _p);
				compositionLineGeometry.StartAnimation("End", expressionAnimation);
				_lineParts.Add(compositionLineGeometry);
				_lineParts.Add(compositionSpriteShape);
				_lineParts.Add(expressionAnimation);
			}
		}
	}

	private void BuildMini()
	{
		((ICollection<CompositionShape>)_mini.Shapes).Clear();
		Release(_miniParts);
		_mini.Size = new Vector2((float)Math.Max(1.0, _miniRect.Width), (float)Math.Max(1.0, _miniRect.Height));
		if (_miniBars == 0)
		{
			return;
		}
		float num = (float)_miniRect.Width;
		float num2 = (float)_miniRect.Height;
		float miniBarW = _miniBarW;
		float num3 = (num - miniBarW) / (float)Math.Max(1, _miniBars - 1);
		float num4 = num2 / 2f;
		float value = Math.Max(0f, num2 - miniBarW);
		for (int i = 0; i < _miniBars; i++)
		{
			float num5 = miniBarW / 2f + (float)i * num3;
			CompositionLineGeometry compositionLineGeometry = _c.CreateLineGeometry();
			compositionLineGeometry.Start = new Vector2(num5, num4);
			compositionLineGeometry.End = new Vector2(num5, num4);
			CompositionSpriteShape compositionSpriteShape = _c.CreateSpriteShape(compositionLineGeometry);
			compositionSpriteShape.StrokeBrush = _miniBrush;
			compositionSpriteShape.StrokeThickness = miniBarW;
			compositionSpriteShape.StrokeStartCap = CompositionStrokeCap.Round;
			compositionSpriteShape.StrokeEndCap = CompositionStrokeCap.Round;
			((ICollection<CompositionShape>)_mini.Shapes).Add((CompositionShape)compositionSpriteShape);
			string text = "P.m." + "XYZW"[i];
			ExpressionAnimation expressionAnimation = _c.CreateExpressionAnimation("Vector2(x, cy - " + text + " * r / 2)");
			ExpressionAnimation expressionAnimation2 = _c.CreateExpressionAnimation("Vector2(x, cy + " + text + " * r / 2)");
			ExpressionAnimation[] array = new ExpressionAnimation[2] { expressionAnimation, expressionAnimation2 };
			foreach (ExpressionAnimation expressionAnimation3 in array)
			{
				expressionAnimation3.SetScalarParameter("x", num5);
				expressionAnimation3.SetScalarParameter("cy", num4);
				expressionAnimation3.SetScalarParameter("r", value);
				expressionAnimation3.SetReferenceParameter("P", _p);
			}
			compositionLineGeometry.StartAnimation("End", expressionAnimation);
			compositionLineGeometry.StartAnimation("Start", expressionAnimation2);
			_miniParts.Add(compositionLineGeometry);
			_miniParts.Add(compositionSpriteShape);
			_miniParts.Add(expressionAnimation);
			_miniParts.Add(expressionAnimation2);
		}
	}

	public void SetColor(System.Windows.Media.Color color)
	{
		if (!(color == _color))
		{
			_color = color;
			_miniBrush.Color = W(color, 1.0);
			ApplyLineColor();
		}
	}

	private void ApplyLineColor()
	{
		if (_lineBrush != null)
		{
			((ICollection<CompositionColorGradientStop>)_lineBrush.ColorStops).Clear();
			((ICollection<CompositionColorGradientStop>)_lineBrush.ColorStops).Add(_c.CreateColorGradientStop(0f, W(_color, 0.45)));
			((ICollection<CompositionColorGradientStop>)_lineBrush.ColorStops).Add(_c.CreateColorGradientStop(0.5f, W(_color, 1.0)));
			((ICollection<CompositionColorGradientStop>)_lineBrush.ColorStops).Add(_c.CreateColorGradientStop(1f, W(_color, 0.45)));
		}
	}

	public void SetLevels(float[] line, float[] mini)
	{
		for (int i = 0; i * 4 < _lineBars; i++)
		{
			int num = i * 4;
			Vector4 vector = new Vector4(Q(line, num), Q(line, num + 1), Q(line, num + 2), Q(line, num + 3));
			if (!(vector == _sent[i]))
			{
				_sent[i] = vector;
				_p.InsertVector4(Names[i], vector);
			}
		}
		if (_miniBars > 0)
		{
			Vector4 vector2 = new Vector4(Q(mini, 0), Q(mini, 1), Q(mini, 2), Q(mini, 3));
			if (vector2 != _sentMini)
			{
				_sentMini = vector2;
				_p.InsertVector4("m", vector2);
			}
		}
		Frames++;
		if (_shown && ++_zTicks >= 30)
		{
			_zTicks = 0;
			KeepAbove();
		}
	}

	private static float Q(float[] a, int i)
	{
		return (float)Math.Round(At(a, i) * 128f) / 128f;
	}

	private static float At(float[] a, int i)
	{
		if (a == null || i >= a.Length)
		{
			return 0f;
		}
		return Math.Max(0f, Math.Min(1f, a[i]));
	}

	public void SetBarsVisible(bool on)
	{
		if (on != _visible)
		{
			_visible = on;
			_root.Opacity = (on ? 1f : 0f);
		}
	}

	public void Show(bool on)
	{
		if (on == _shown)
		{
			if (on)
			{
				SyncWindow();
			}
			return;
		}
		_shown = on;
		if (!on)
		{
			_visible = false;
			_root.Opacity = 0f;
		}
		SyncWindow();
	}

	public void SyncWindow()
	{
		RECT r;
		if (!_shown || !IsWindowVisible(_notch))
		{
			ShowWindow(_hwnd, 0);
			_nativeRegion.Apply(_hwnd, Rect.Empty);
			SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0, 20u);
			_lastRect = default(RECT);
		}
		else if (!(_notch == IntPtr.Zero) && GetWindowRect(_notch, out r))
		{
			if (r.Left != _lastRect.Left || r.Top != _lastRect.Top || r.Right != _lastRect.Right || r.Bottom != _lastRect.Bottom)
			{
				_lastRect = r;
				SetWindowPos(_hwnd, HWND_TOPMOST, r.Left, r.Top, Math.Max(1, r.Right - r.Left), Math.Max(1, r.Bottom - r.Top), 528u);
				_root.Size = new Vector2(Math.Max(1, r.Right - r.Left), Math.Max(1, r.Bottom - r.Top));
			}
			else if (_shown)
			{
				KeepAbove();
			}
			if (BoundNativeSurface() && !IsWindowVisible(_hwnd))
			{
				ShowWindow(_hwnd, 4);
			}
		}
	}

	private bool BoundNativeSurface()
	{
		Rect empty = Rect.Empty;
		if (_shown)
		{
			if (_lineBars > 0 && !_lineRect.IsEmpty)
			{
				empty.Union(_lineRect);
			}
			if (_miniBars > 0 && !_miniRect.IsEmpty)
			{
				empty.Union(_miniRect);
			}
			if (!empty.IsEmpty)
			{
				empty.Inflate(2.0, 2.0);
			}
		}
		return _nativeRegion.Apply(_hwnd, empty);
	}

	private void KeepAbove()
	{
		SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, 531u);
	}

	public void Dispose()
	{
		try
		{
			if (_target != null)
			{
				((Windows.UI.Composition.CompositionTarget)_target).Root = null;
			}
			_target = null;
			DestroyWindow(_hwnd);
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("[Beat] overlay dispose: " + ex.Message);
		}
	}
}
