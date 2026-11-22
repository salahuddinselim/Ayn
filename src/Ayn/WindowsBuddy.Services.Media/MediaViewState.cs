using System;
using System.Windows.Media.Imaging;
using WindowsBuddy.BuddyCapsule.Activity;

namespace WindowsBuddy.Services.Media;

public sealed class MediaViewState
{
	public readonly bool Present;

	public readonly bool Playing;

	public readonly bool CanPlayPause;

	public readonly bool CanNext;

	public readonly bool CanPrevious;

	public readonly bool CanSeek;

	public readonly string Title;

	public readonly string Artist;

	public readonly string Source;

	public readonly BitmapSource Artwork;

	public readonly MediaTrackInfo Timeline;

	public readonly long Generation;

	public readonly long SessionId;

	public readonly DateTime? PausedSinceUtc;

	public MediaViewState(bool present, bool playing, string title, string artist, string source, BitmapSource artwork, MediaTrackInfo timeline, bool playPause, bool next, bool previous, bool seek, long generation, long sessionId = 0L, DateTime? pausedSinceUtc = null)
	{
		Present = present;
		Playing = playing;
		Title = title;
		Artist = artist;
		Source = source;
		Artwork = artwork;
		Timeline = timeline;
		CanPlayPause = playPause;
		CanNext = next;
		CanPrevious = previous;
		CanSeek = seek;
		Generation = generation;
		SessionId = sessionId;
		PausedSinceUtc = pausedSinceUtc;
	}
}
