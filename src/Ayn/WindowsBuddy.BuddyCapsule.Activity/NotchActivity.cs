using System;
using System.Windows;
using System.Windows.Media;

namespace WindowsBuddy.BuddyCapsule.Activity;

public class NotchActivity
{
	public string Id { get; set; }

	public string EventId
	{
		get
		{
			return Id;
		}
		set
		{
			Id = value;
		}
	}

	public ActivityType Type { get; set; }

	public ActivityPriority Priority { get; set; }

	public ActivityMode Mode { get; set; }

	public TimeSpan Duration { get; set; }

	public string Title { get; set; }

	public string Subtitle { get; set; }

	public string ValueText { get; set; }

	public Func<double, Brush, UIElement> IconFactory { get; set; }

	public ImageSource Artwork { get; set; }

	public Brush AccentColor { get; set; }

	public string SourceAppId { get; set; }

	public string Source { get; set; }

	public object Payload { get; set; }

	public string NavigateToToolId { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime MinimumVisibleUntil { get; set; }

	public DateTime ExpiresAt { get; set; }

	public NotchActivity PreviousActivity { get; set; }

	public long Generation { get; set; }

	public CollapsedActivityProfile Profile { get; set; }

	public bool IsPlaying { get; set; }

	public double PlaybackProgress { get; set; }

	public Action PlayPauseAction { get; set; }

	public Action NextAction { get; set; }

	public Action PreviousAction { get; set; }

	public string DiagnosticDetails { get; set; }

	public NotchActivity()
	{
		Id = Guid.NewGuid().ToString("N");
		CreatedAt = DateTime.UtcNow;
		Mode = ActivityMode.Temporary;
		Priority = ActivityPriority.P3_Transient;
		Duration = TimeSpan.FromSeconds(3.0);
		MinimumVisibleUntil = CreatedAt.Add(Duration);
		ExpiresAt = MinimumVisibleUntil;
		PlaybackProgress = -1.0;
		Profile = CollapsedActivityProfile.Resting;
	}

	public NotchActivity Clone()
	{
		NotchActivity notchActivity = new NotchActivity();
		notchActivity.Id = Id;
		notchActivity.Type = Type;
		notchActivity.Priority = Priority;
		notchActivity.Mode = Mode;
		notchActivity.Duration = Duration;
		notchActivity.Title = Title;
		notchActivity.Subtitle = Subtitle;
		notchActivity.ValueText = ValueText;
		notchActivity.IconFactory = IconFactory;
		notchActivity.Artwork = Artwork;
		notchActivity.AccentColor = AccentColor;
		notchActivity.SourceAppId = SourceAppId;
		notchActivity.Source = Source;
		notchActivity.Payload = Payload;
		notchActivity.NavigateToToolId = NavigateToToolId;
		notchActivity.CreatedAt = CreatedAt;
		notchActivity.MinimumVisibleUntil = MinimumVisibleUntil;
		notchActivity.ExpiresAt = ExpiresAt;
		notchActivity.PreviousActivity = PreviousActivity;
		notchActivity.Generation = Generation;
		notchActivity.Profile = Profile;
		notchActivity.IsPlaying = IsPlaying;
		notchActivity.PlaybackProgress = PlaybackProgress;
		notchActivity.PlayPauseAction = PlayPauseAction;
		notchActivity.NextAction = NextAction;
		notchActivity.PreviousAction = PreviousAction;
		notchActivity.DiagnosticDetails = DiagnosticDetails;
		return notchActivity;
	}

	public PillActivitySnapshot ToSnapshot(ActivityTier? overrideTier = null)
	{
		ActivityTier tier = (ActivityTier)(((int?)overrideTier) ?? ((Type != ActivityType.BatteryLow) ? ((Type == ActivityType.Media || Type == ActivityType.TimerRunning) ? 2 : ((Mode == ActivityMode.Temporary) ? 1 : ((Mode == ActivityMode.Persistent) ? 2 : 3))) : 0));
		string sessionId = ((Type == ActivityType.Media) ? "media" : ((Type == ActivityType.TimerRunning) ? "timer" : ((Type == ActivityType.Clock) ? "clock" : (Id ?? Guid.NewGuid().ToString("N")))));
		return new PillActivitySnapshot(Id, sessionId, tier, Type, Title, Subtitle, ValueText, IconFactory, Artwork, AccentColor, (PlaybackProgress >= 0.0) ? new double?(PlaybackProgress) : null, CreatedAt, (Mode == ActivityMode.Temporary) ? new DateTime?(ExpiresAt) : null, Generation, Profile, IsPlaying, PlayPauseAction, NextAction, PreviousAction, NavigateToToolId, Payload);
	}
}
