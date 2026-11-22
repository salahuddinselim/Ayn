using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.Services.Media;

namespace WindowsBuddy.BuddyCapsule.UI;

public sealed class MediaTransportControls : StackPanel
{
	public readonly Button Previous;

	public readonly Button PlayPause;

	public readonly Button Next;

	private readonly ContentControl _play;

	private readonly Brush _ink;

	private bool? _playing;

	public MediaViewState State { get; private set; }

	public MediaTransportControls(Brush ink, double size = 22.0)
	{
		_ink = ink;
		base.Orientation = Orientation.Horizontal;
		Canvas canvas = VectorIcons.CreateSkipNextIcon(11.0, ink, ink);
		canvas.LayoutTransform = new ScaleTransform(-1.0, 1.0);
		Action action = delegate
		{
			MediaService.Instance.SkipPrevious(State);
		};
		Previous = Make(canvas, "Previous", size, action);
		_play = new ContentControl
		{
			Width = 12.0,
			Height = 12.0,
			IsHitTestVisible = false
		};
		PlayPause = Make(_play, "Play or pause", size, delegate
		{
			MediaViewState state = State;
			if (state != null)
			{
				Update(new MediaViewState(state.Present, !state.Playing, state.Title, state.Artist, state.Source, state.Artwork, state.Timeline, state.CanPlayPause, state.CanNext, state.CanPrevious, state.CanSeek, state.Generation, state.SessionId));
				MediaService.Instance.TogglePlayPause(state);
			}
		});
		Next = Make(VectorIcons.CreateSkipNextIcon(11.0, ink, ink), "Next", size, delegate
		{
			MediaService.Instance.SkipNext(State);
		});
		base.Children.Add(Previous);
		base.Children.Add(PlayPause);
		base.Children.Add(Next);
		Update(null);
	}

	private static Button Make(UIElement icon, string name, double size, Action action)
	{
		Button button = new Button
		{
			Width = size,
			Height = size,
			Padding = new Thickness(0.0),
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			Cursor = Cursors.Hand,
			Content = icon,
			ToolTip = name
		};
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		frameworkElementFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory2.SetValue(Border.BackgroundProperty, Brushes.Transparent);
		frameworkElementFactory2.AppendChild(frameworkElementFactory);
		button.Template = new ControlTemplate(typeof(Button))
		{
			VisualTree = frameworkElementFactory2
		};
		ToolTipService.SetShowOnDisabled(button, value: true);
		AutomationProperties.SetName(button, name);
		button.IsEnabledChanged += delegate
		{
			button.Opacity = (button.IsEnabled ? 1.0 : 0.3);
		};
		button.Click += delegate(object s, RoutedEventArgs e)
		{
			e.Handled = true;
			action();
		};
		return button;
	}

	public void Update(MediaViewState state)
	{
		State = state;
		bool flag = state?.Present ?? false;
		Previous.IsEnabled = flag && state.CanPrevious;
		PlayPause.IsEnabled = flag && state.CanPlayPause;
		Next.IsEnabled = flag && state.CanNext;
		bool flag2 = flag && state.Playing;
		if (_playing != flag2)
		{
			_playing = flag2;
			_play.Content = FluentIcons.Create(flag2 ? "pause" : "play", 12.0, _ink, filled: false);
		}
		Previous.ToolTip = (Previous.IsEnabled ? "Previous" : "Previous is unavailable for this session");
		Next.ToolTip = (Next.IsEnabled ? "Next" : "Next is unavailable for this session");
		PlayPause.ToolTip = ((!PlayPause.IsEnabled) ? "Play/pause is unavailable for this session" : (flag2 ? "Pause" : "Play"));
	}
}
