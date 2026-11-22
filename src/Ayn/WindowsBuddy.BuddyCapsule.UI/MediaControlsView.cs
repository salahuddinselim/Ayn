using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Services.Media;

namespace WindowsBuddy.BuddyCapsule.UI;

public sealed class MediaControlsView : Grid
{
	private readonly Border _art;

	private readonly Border _fill;

	private readonly TextBlock _title;

	private readonly TextBlock _artist;

	private readonly TextBlock _elapsed;

	private readonly TextBlock _remaining;

	private readonly MediaTransportControls _controls;

	private readonly Grid _progress;

	private object _artShown;

	public MediaControlsView()
	{
		base.Height = 66.0;
		base.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(32.0)
		});
		base.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(8.0)
		});
		base.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(26.0)
		});
		Grid grid = new Grid
		{
			ColumnDefinitions = 
			{
				new ColumnDefinition
				{
					Width = new GridLength(28.0)
				},
				new ColumnDefinition
				{
					Width = new GridLength(8.0)
				},
				new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				}
			}
		};
		_art = new Border
		{
			Width = 28.0,
			Height = 28.0,
			VerticalAlignment = VerticalAlignment.Center,
			CornerRadius = new CornerRadius(6.0),
			Background = BuddyTheme.Control
		};
		grid.Children.Add(_art);
		Grid grid2 = new Grid
		{
			RowDefinitions = 
			{
				new RowDefinition
				{
					Height = new GridLength(18.0)
				},
				new RowDefinition
				{
					Height = new GridLength(14.0)
				}
			}
		};
		_title = BuddyTheme.Text("", 15.5, BuddyTheme.Text100, FontWeights.Bold);
		_title.TextTrimming = TextTrimming.WordEllipsis;
		_artist = BuddyTheme.Text("", 11.5, BuddyTheme.Text50, FontWeights.Normal);
		_artist.TextTrimming = TextTrimming.WordEllipsis;
		grid2.Children.Add(_title);
		Grid.SetRow(_artist, 1);
		grid2.Children.Add(_artist);
		Grid.SetColumn(grid2, 2);
		grid.Children.Add(grid2);
		base.Children.Add(grid);
		_progress = new MediaSeekSurface
		{
			Background = Brushes.Transparent,
			Cursor = Cursors.Arrow
		};
		_progress.Children.Add(new Border
		{
			Height = 2.0,
			Background = ThemeTokens.Track,
			VerticalAlignment = VerticalAlignment.Center
		});
		_fill = new Border
		{
			Height = 2.0,
			HorizontalAlignment = HorizontalAlignment.Left,
			Background = ThemeTokens.Accent,
			VerticalAlignment = VerticalAlignment.Center
		};
		_progress.Children.Add(_fill);
		Grid.SetRow(_progress, 1);
		base.Children.Add(_progress);
		Grid progress = _progress;
		Func<MediaViewState> state = () => _controls.State;
		MediaSeekInteraction.Attach(progress, state, delegate(double f)
		{
			_fill.Width = _progress.ActualWidth * f;
		});
		Grid grid3 = new Grid();
		_elapsed = BuddyTheme.Text("", 11.0, BuddyTheme.Text50, FontWeights.Normal);
		_remaining = BuddyTheme.Text("", 11.0, BuddyTheme.Text50, FontWeights.Normal);
		_elapsed.VerticalAlignment = (_remaining.VerticalAlignment = VerticalAlignment.Center);
		_remaining.HorizontalAlignment = HorizontalAlignment.Right;
		AynType.Tabular(_elapsed);
		AynType.Tabular(_remaining);
		_controls = new MediaTransportControls(BuddyTheme.Text92)
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		grid3.Children.Add(_elapsed);
		grid3.Children.Add(_remaining);
		grid3.Children.Add(_controls);
		Grid.SetRow(grid3, 2);
		base.Children.Add(grid3);
		base.SizeChanged += delegate
		{
			Refresh(MediaService.Instance.View);
		};
	}

	private static string Time(double seconds)
	{
		TimeSpan timeSpan = TimeSpan.FromSeconds(Math.Max(0.0, seconds));
		if (!(timeSpan.TotalHours >= 1.0))
		{
			return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", (int)timeSpan.TotalMinutes, timeSpan.Seconds);
		}
		return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", (int)timeSpan.TotalHours, timeSpan.Minutes, timeSpan.Seconds);
	}

	public void Refresh(MediaViewState state)
	{
		_controls.Update(state);
		if (state != null && state.Present)
		{
			if (_title.Text != (state.Title ?? "Now playing"))
			{
				_title.Text = state.Title ?? "Now playing";
			}
			if (_artist.Text != (state.Artist ?? ""))
			{
				_artist.Text = state.Artist ?? "";
			}
			if (!object.ReferenceEquals(_artShown, state.Artwork) || (_art.Child == null && state.Artwork == null))
			{
				_artShown = state.Artwork;
				_art.Background = ((state.Artwork != null) ? new ImageBrush(state.Artwork)
				{
					Stretch = Stretch.UniformToFill
				} : BuddyTheme.Control);
				_art.Child = ((state.Artwork != null) ? null : FluentIcons.Create("play", 12.0, BuddyTheme.Text50, filled: false));
			}
			double num = state.Timeline.RemainingSeconds(DateTime.UtcNow);
			double num2 = state.Timeline.Progress(DateTime.UtcNow);
			_progress.Visibility = ((!(num2 >= 0.0)) ? Visibility.Hidden : Visibility.Visible);
			MediaSeekInteraction.Update(_progress, state);
			_fill.Width = Math.Max(0.0, _progress.ActualWidth * Math.Max(0.0, num2));
			_elapsed.Text = ((num >= 0.0) ? Time(state.Timeline.DurationSeconds - num) : "");
			_remaining.Text = ((num >= 0.0) ? ("−" + Time(num)) : "");
		}
	}
}
