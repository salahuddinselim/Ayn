using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WindowsBuddy.Services.Media;

namespace WindowsBuddy.BuddyCapsule.UI;

public static class MediaSeekInteraction
{
	public static void Attach(FrameworkElement bar, Func<MediaViewState> state, Action<double> preview)
	{
		if (bar is MediaSeekSurface mediaSeekSurface)
		{
			mediaSeekSurface.StateProvider = state;
			mediaSeekSurface.SeekFraction = delegate(double f)
			{
				MediaViewState state2 = state();
				if (preview != null)
				{
					preview(f);
				}
				MediaService.Instance.Seek(f, state2);
			};
		}
		BuddyKit.KeyboardFocus(bar, "Playback position; left and right arrows seek five seconds");
		bar.KeyDown += delegate(object s, KeyEventArgs e)
		{
			MediaViewState mediaViewState2 = state();
			if (mediaViewState2 != null && mediaViewState2.CanSeek && !(mediaViewState2.Timeline.DurationSeconds <= 0.0))
			{
				double num = mediaViewState2.Timeline.Progress(DateTime.UtcNow);
				if (e.Key == Key.Left || e.Key == Key.Down)
				{
					num -= 5.0 / mediaViewState2.Timeline.DurationSeconds;
				}
				else if (e.Key == Key.Right || e.Key == Key.Up)
				{
					num += 5.0 / mediaViewState2.Timeline.DurationSeconds;
				}
				else if (e.Key == Key.Home)
				{
					num = 0.0;
				}
				else if (e.Key == Key.End)
				{
					num = 1.0;
				}
				else if (e.Key != Key.Return && e.Key != Key.Space)
				{
					return;
				}
				e.Handled = true;
				num = Math.Max(0.0, Math.Min(1.0, num));
				if (preview != null)
				{
					preview(num);
				}
				MediaService.Instance.Seek(num, mediaViewState2);
			}
		};
		MediaViewState target = null;
		Func<MouseEventArgs, double> fraction = (MouseEventArgs e) => Math.Max(0.0, Math.Min(1.0, e.GetPosition(bar).X / Math.Max(1.0, bar.ActualWidth)));
		bar.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			target = state();
			if (target == null || !target.CanSeek || target.Timeline.DurationSeconds <= 0.0)
			{
				target = null;
			}
			else
			{
				bar.Focus();
				bar.CaptureMouse();
				if (preview != null)
				{
					preview(fraction(e));
				}
			}
		};
		bar.MouseMove += delegate(object s, MouseEventArgs e)
		{
			if (target != null && bar.IsMouseCaptured && preview != null)
			{
				preview(fraction(e));
			}
		};
		bar.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			MediaViewState mediaViewState = target;
			target = null;
			double fraction2 = fraction(e);
			bar.ReleaseMouseCapture();
			if (mediaViewState != null)
			{
				MediaService.Instance.Seek(fraction2, mediaViewState);
			}
		};
		bar.LostMouseCapture += delegate
		{
			target = null;
		};
		ToolTipService.SetShowOnDisabled(bar, value: true);
	}

	public static void Update(FrameworkElement bar, MediaViewState state)
	{
		bar.IsEnabled = state != null && state.Present && state.CanSeek && state.Timeline.DurationSeconds > 0.0;
		bar.Cursor = (bar.IsEnabled ? Cursors.Hand : Cursors.Arrow);
		bar.ToolTip = (bar.IsEnabled ? "Click or drag to seek" : "Seeking is unavailable for this session");
	}
}
