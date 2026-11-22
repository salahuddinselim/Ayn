using System;
using System.Windows;
using System.Windows.Media;

namespace WindowsBuddy.BuddyCapsule.Activity;

public sealed class PillActivitySnapshot
{
	public string Id { get; private set; }

	public string SessionId { get; private set; }

	public ActivityTier Tier { get; private set; }

	public ActivityType Type { get; private set; }

	public string Title { get; private set; }

	public string SubText { get; private set; }

	public string ValueText { get; private set; }

	public Func<double, Brush, UIElement> IconFactory { get; private set; }

	public ImageSource Artwork { get; private set; }

	public Brush AccentColor { get; private set; }

	public double? ProgressFraction { get; private set; }

	public DateTime CreatedAt { get; private set; }

	public DateTime? ExpiresAt { get; private set; }

	public long SequenceNumber { get; private set; }

	public CollapsedActivityProfile Profile { get; private set; }

	public bool IsPlaying { get; private set; }

	public Action PlayPauseAction { get; private set; }

	public Action NextAction { get; private set; }

	public Action PreviousAction { get; private set; }

	public string NavigateToToolId { get; private set; }

	public object Payload { get; private set; }

	public PillActivitySnapshot(string id, string sessionId, ActivityTier tier, ActivityType type, string title, string subText, string valueText, Func<double, Brush, UIElement> iconFactory, ImageSource artwork, Brush accentColor, double? progressFraction, DateTime createdAt, DateTime? expiresAt, long sequenceNumber, CollapsedActivityProfile profile, bool isPlaying, Action playPauseAction, Action nextAction, Action previousAction, string navigateToToolId, object payload)
	{
		Id = id ?? Guid.NewGuid().ToString("N");
		SessionId = sessionId ?? Id;
		Tier = tier;
		Type = type;
		Title = title ?? string.Empty;
		SubText = subText ?? string.Empty;
		ValueText = valueText ?? string.Empty;
		IconFactory = iconFactory;
		Artwork = artwork;
		AccentColor = accentColor;
		ProgressFraction = progressFraction;
		CreatedAt = createdAt;
		ExpiresAt = expiresAt;
		SequenceNumber = sequenceNumber;
		Profile = ((profile != 0 || type == ActivityType.RestingClock) ? profile : NotchProfileMetrics.FromActivityType(type));
		IsPlaying = isPlaying;
		PlayPauseAction = playPauseAction;
		NextAction = nextAction;
		PreviousAction = previousAction;
		NavigateToToolId = navigateToToolId;
		Payload = payload;
	}

	public PillActivitySnapshot WithSequenceNumber(long seq)
	{
		return new PillActivitySnapshot(Id, SessionId, Tier, Type, Title, SubText, ValueText, IconFactory, Artwork, AccentColor, ProgressFraction, CreatedAt, ExpiresAt, seq, Profile, IsPlaying, PlayPauseAction, NextAction, PreviousAction, NavigateToToolId, Payload);
	}

	public PillActivitySnapshot WithExpiresAt(DateTime? expiresAt)
	{
		return new PillActivitySnapshot(Id, SessionId, Tier, Type, Title, SubText, ValueText, IconFactory, Artwork, AccentColor, ProgressFraction, CreatedAt, expiresAt, SequenceNumber, Profile, IsPlaying, PlayPauseAction, NextAction, PreviousAction, NavigateToToolId, Payload);
	}

	public PillActivitySnapshot WithSessionData(string title, string subText, string valueText, bool isPlaying, double? progress)
	{
		return new PillActivitySnapshot(Id, SessionId, Tier, Type, title ?? Title, subText ?? SubText, valueText ?? ValueText, IconFactory, Artwork, AccentColor, progress ?? ProgressFraction, CreatedAt, ExpiresAt, SequenceNumber, Profile, isPlaying, PlayPauseAction, NextAction, PreviousAction, NavigateToToolId, Payload);
	}

	public PillActivitySnapshot WithClockData(string title, string subText, string valueText)
	{
		return new PillActivitySnapshot(Id, SessionId, Tier, Type, title ?? Title, subText ?? SubText, valueText ?? ValueText, IconFactory, Artwork, AccentColor, ProgressFraction, CreatedAt, ExpiresAt, SequenceNumber, Profile, IsPlaying, PlayPauseAction, NextAction, PreviousAction, NavigateToToolId, Payload);
	}

	public NotchActivity ToLegacyActivity()
	{
		return ToLegacyNotchActivity();
	}

	public NotchActivity ToLegacyNotchActivity()
	{
		NotchActivity notchActivity = new NotchActivity();
		notchActivity.Id = Id;
		notchActivity.Type = Type;
		notchActivity.Title = Title;
		notchActivity.Subtitle = SubText;
		notchActivity.ValueText = ValueText;
		notchActivity.IconFactory = IconFactory;
		notchActivity.Artwork = Artwork;
		notchActivity.AccentColor = AccentColor;
		notchActivity.PlaybackProgress = (ProgressFraction.HasValue ? ProgressFraction.Value : (-1.0));
		notchActivity.CreatedAt = CreatedAt;
		notchActivity.ExpiresAt = (ExpiresAt.HasValue ? ExpiresAt.Value : DateTime.MaxValue);
		notchActivity.Generation = SequenceNumber;
		notchActivity.Profile = Profile;
		notchActivity.IsPlaying = IsPlaying;
		notchActivity.PlayPauseAction = PlayPauseAction;
		notchActivity.NextAction = NextAction;
		notchActivity.PreviousAction = PreviousAction;
		notchActivity.NavigateToToolId = NavigateToToolId;
		notchActivity.Payload = Payload;
		notchActivity.Mode = ((Tier == ActivityTier.Tier1_Transient) ? ActivityMode.Temporary : ActivityMode.Persistent);
		notchActivity.Priority = (ActivityPriority)Tier;
		return notchActivity;
	}
}
