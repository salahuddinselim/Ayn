using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.State;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.BuddyCapsule.Core;

public class CapsuleTextScrollController : IDisposable
{
	public const double LineHeightValue = 18.0;

	private readonly Border _clipContainer;

	private readonly TextBlock _previewText;

	private readonly TranslateTransform _textTranslate;

	private DispatcherTimer _stepTimer;

	private DispatcherTimer _wheelResumeTimer;

	private CapsuleLiveActivity _currentActivity;

	private double _safeWidth = 140.0;

	private int _totalLines = 1;

	private int _currentLine;

	private bool _isTransitioning;

	private bool _isHoverPaused;

	private bool _isClickPaused;

	private bool _isWheelPaused;

	private string _lastRawText;

	public int TotalLines => _totalLines;

	public int CurrentLine => _currentLine;

	public bool IsPaused
	{
		get
		{
			if (!_isHoverPaused && !_isClickPaused)
			{
				return _isWheelPaused;
			}
			return true;
		}
	}

	public CapsuleTextScrollController(Border clipContainer, TextBlock previewText, TranslateTransform textTranslate)
	{
		if (clipContainer == null)
		{
			throw new ArgumentNullException("clipContainer");
		}
		if (previewText == null)
		{
			throw new ArgumentNullException("previewText");
		}
		if (textTranslate == null)
		{
			throw new ArgumentNullException("textTranslate");
		}
		_clipContainer = clipContainer;
		_previewText = previewText;
		_textTranslate = textTranslate;
		_previewText.LineHeight = 18.0;
		_previewText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		_previewText.TextWrapping = TextWrapping.Wrap;
		_previewText.TextAlignment = TextAlignment.Center;
		_previewText.TextTrimming = TextTrimming.None;
		_clipContainer.ClipToBounds = true;
		_clipContainer.Height = 18.0;
		_clipContainer.Cursor = Cursors.Hand;
		_stepTimer = new DispatcherTimer(DispatcherPriority.Normal)
		{
			Interval = TimeSpan.FromSeconds(2.0)
		};
		_stepTimer.Tick += delegate
		{
			StepNextLine();
		};
		_wheelResumeTimer = new DispatcherTimer(DispatcherPriority.Normal)
		{
			Interval = TimeSpan.FromSeconds(2.5)
		};
		_wheelResumeTimer.Tick += OnWheelResumeTick;
		_clipContainer.MouseEnter += delegate
		{
			OnMouseEnter();
		};
		_clipContainer.MouseLeave += delegate
		{
			OnMouseLeave();
		};
		_clipContainer.PreviewMouseWheel += delegate(object s, MouseWheelEventArgs e)
		{
			OnMouseWheel(e);
		};
		_clipContainer.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			OnMouseClick(e);
		};
	}

	public void UpdateContent(CapsuleLiveActivity activity, double safeWidth, Brush defaultForeground)
	{
		_currentActivity = activity;
		_safeWidth = Math.Max(80.0, safeWidth);
		_clipContainer.Width = _safeWidth;
		_clipContainer.Height = 18.0;
		_previewText.Width = _safeWidth;
		string text = ((activity != null) ? (activity.PreviewText ?? string.Empty) : string.Empty);
		bool flag = !string.Equals(_lastRawText, text, StringComparison.Ordinal);
		_lastRawText = text;
		_previewText.Inlines.Clear();
		if (activity != null && activity.FormattedSpans != null && activity.FormattedSpans.Count > 0)
		{
			foreach (CapsuleTextSpan formattedSpan in activity.FormattedSpans)
			{
				if (!string.IsNullOrEmpty(formattedSpan.Text))
				{
					Run run = new Run(formattedSpan.Text);
					if (formattedSpan.Foreground != null && formattedSpan.Foreground != Brushes.Transparent)
					{
						run.Foreground = formattedSpan.Foreground;
					}
					else
					{
						run.Foreground = defaultForeground ?? Brushes.White;
					}
					if (formattedSpan.Background != null && formattedSpan.Background != Brushes.Transparent)
					{
						run.Background = formattedSpan.Background;
					}
					if (formattedSpan.IsBold)
					{
						run.FontWeight = AynType.Medium;
					}
					_previewText.Inlines.Add(run);
				}
			}
		}
		else
		{
			string text2 = (string.IsNullOrWhiteSpace(text) ? "Empty note" : text.Trim());
			_previewText.Inlines.Add(new Run(text2)
			{
				Foreground = (defaultForeground ?? Brushes.White)
			});
		}
		_previewText.Measure(new Size(_safeWidth, double.PositiveInfinity));
		double height = _previewText.DesiredSize.Height;
		_totalLines = Math.Max(1, (int)Math.Round(height / 18.0));
		if (flag)
		{
			_currentLine = 0;
			_textTranslate.BeginAnimation(TranslateTransform.YProperty, null);
			_textTranslate.BeginAnimation(TranslateTransform.XProperty, null);
			_textTranslate.X = 0.0;
			_textTranslate.Y = 0.0;
			_previewText.BeginAnimation(UIElement.OpacityProperty, null);
			_previewText.Opacity = 1.0;
		}
		else if (_currentLine >= _totalLines)
		{
			_currentLine = Math.Max(0, _totalLines - 1);
			_textTranslate.Y = (double)(-_currentLine) * 18.0;
		}
		if (_totalLines <= 1)
		{
			_stepTimer.Stop();
			_wheelResumeTimer.Stop();
			_textTranslate.Y = 0.0;
			_currentLine = 0;
			_clipContainer.ToolTip = null;
			return;
		}
		string arg = ((activity != null && !string.IsNullOrEmpty(activity.Title)) ? activity.Title : "Message");
		if (activity != null && activity.IsMovingText)
		{
			_clipContainer.ToolTip = BuddyTooltip.Create($"{arg} ({_totalLines} lines - Click to pause, wheel to scroll)");
			if (!IsPaused)
			{
				_stepTimer.Interval = TimeSpan.FromSeconds(2.0);
				_stepTimer.Start();
			}
		}
		else
		{
			_stepTimer.Stop();
			_wheelResumeTimer.Stop();
			_clipContainer.ToolTip = BuddyTooltip.Create($"{arg} ({_totalLines} lines - Scroll with mouse wheel)");
		}
	}

	public void StepNextLine()
	{
		if (_isTransitioning || IsPaused || _totalLines <= 1 || _currentActivity == null || !_currentActivity.IsMovingText)
		{
			return;
		}
		if (_currentLine < _totalLines - 1)
		{
			int num = _currentLine + 1;
			double targetY = (double)(-num) * 18.0;
			_currentLine = num;
			_isTransitioning = true;
			DoubleAnimation doubleAnimation = new DoubleAnimation();
			doubleAnimation.From = _textTranslate.Y;
			doubleAnimation.To = targetY;
			doubleAnimation.Duration = TimeSpan.FromMilliseconds(500.0);
			doubleAnimation.EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseInOut
			};
			DoubleAnimation doubleAnimation2 = doubleAnimation;
			doubleAnimation2.Completed += delegate
			{
				_isTransitioning = false;
				_textTranslate.BeginAnimation(TranslateTransform.YProperty, null);
				_textTranslate.Y = targetY;
				if (!IsPaused && _currentActivity != null && _currentActivity.IsMovingText)
				{
					_stepTimer.Interval = TimeSpan.FromSeconds(2.0);
					_stepTimer.Start();
				}
			};
			_textTranslate.BeginAnimation(TranslateTransform.YProperty, doubleAnimation2);
			return;
		}
		_isTransitioning = true;
		_stepTimer.Stop();
		DoubleAnimation doubleAnimation3 = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(180.0));
		doubleAnimation3.Completed += delegate
		{
			_currentLine = 0;
			_textTranslate.BeginAnimation(TranslateTransform.YProperty, null);
			_textTranslate.Y = 0.0;
			DoubleAnimation doubleAnimation4 = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(220.0));
			doubleAnimation4.Completed += delegate
			{
				_isTransitioning = false;
				_previewText.BeginAnimation(UIElement.OpacityProperty, null);
				_previewText.Opacity = 1.0;
				if (!IsPaused && _currentActivity != null && _currentActivity.IsMovingText)
				{
					_stepTimer.Interval = TimeSpan.FromSeconds(2.0);
					_stepTimer.Start();
				}
			};
			_previewText.BeginAnimation(UIElement.OpacityProperty, doubleAnimation4);
		};
		_previewText.BeginAnimation(UIElement.OpacityProperty, doubleAnimation3);
	}

	private void OnMouseEnter()
	{
		if (_totalLines > 1 && _currentActivity != null && _currentActivity.IsMovingText)
		{
			_isHoverPaused = true;
			_stepTimer.Stop();
		}
	}

	private void OnMouseLeave()
	{
		_isHoverPaused = false;
		if (!IsPaused && _totalLines > 1 && _currentActivity != null && _currentActivity.IsMovingText)
		{
			_stepTimer.Interval = TimeSpan.FromMilliseconds(900.0);
			_stepTimer.Start();
		}
	}

	private void OnMouseWheel(MouseWheelEventArgs e)
	{
		if (_totalLines > 1)
		{
			e.Handled = true;
			_isWheelPaused = true;
			_stepTimer.Stop();
			_wheelResumeTimer.Stop();
			int num = ((e.Delta <= 0) ? 1 : (-1));
			int currentLine = Math.Max(0, Math.Min(_totalLines - 1, _currentLine + num));
			_currentLine = currentLine;
			double targetY = (double)(-_currentLine) * 18.0;
			DoubleAnimation doubleAnimation = new DoubleAnimation(targetY, TimeSpan.FromMilliseconds(160.0));
			doubleAnimation.EasingFunction = new QuadraticEase
			{
				EasingMode = EasingMode.EaseOut
			};
			DoubleAnimation doubleAnimation2 = doubleAnimation;
			doubleAnimation2.Completed += delegate
			{
				_textTranslate.BeginAnimation(TranslateTransform.YProperty, null);
				_textTranslate.Y = targetY;
			};
			_textTranslate.BeginAnimation(TranslateTransform.YProperty, doubleAnimation2);
			_wheelResumeTimer.Interval = TimeSpan.FromSeconds(2.8);
			_wheelResumeTimer.Start();
		}
	}

	private void OnWheelResumeTick(object sender, EventArgs e)
	{
		_wheelResumeTimer.Stop();
		_isWheelPaused = false;
		if (!IsPaused && _totalLines > 1 && _currentActivity != null && _currentActivity.IsMovingText)
		{
			_stepTimer.Interval = TimeSpan.FromSeconds(1.8);
			_stepTimer.Start();
		}
	}

	private void OnMouseClick(MouseButtonEventArgs e)
	{
		if (_totalLines <= 1 || e.ClickCount >= 2)
		{
			return;
		}
		e.Handled = true;
		_isClickPaused = !_isClickPaused;
		if (_isClickPaused)
		{
			_stepTimer.Stop();
			_clipContainer.ToolTip = BuddyTooltip.Create($"Paused on line {_currentLine + 1} of {_totalLines} (Click to resume)");
			return;
		}
		_clipContainer.ToolTip = BuddyTooltip.Create($"Reading line {_currentLine + 1} of {_totalLines} (Click to pause)");
		if (!IsPaused && _currentActivity != null && _currentActivity.IsMovingText)
		{
			_stepTimer.Interval = TimeSpan.FromSeconds(1.2);
			_stepTimer.Start();
		}
	}

	public void ResetPosition()
	{
		_currentLine = 0;
		_textTranslate.BeginAnimation(TranslateTransform.YProperty, null);
		_textTranslate.Y = 0.0;
		_previewText.BeginAnimation(UIElement.OpacityProperty, null);
		_previewText.Opacity = 1.0;
	}

	public void Stop()
	{
		_stepTimer.Stop();
		_wheelResumeTimer.Stop();
		_textTranslate.BeginAnimation(TranslateTransform.YProperty, null);
		_previewText.BeginAnimation(UIElement.OpacityProperty, null);
	}

	public void Dispose()
	{
		Stop();
		_stepTimer = null;
		_wheelResumeTimer = null;
	}
}
