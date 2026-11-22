using System;
using System.Collections.Generic;

namespace WindowsBuddy.BuddyCapsule.State;

public static class CapsuleStateManager
{
	private static CapsuleLiveActivity _current;

	private static readonly object _lock = new object();

	public static CapsuleLiveActivity Current
	{
		get
		{
			lock (_lock)
			{
				return _current;
			}
		}
	}

	public static bool HasActiveActivity
	{
		get
		{
			lock (_lock)
			{
				return _current != null && _current.IsPinned;
			}
		}
	}

	public static event EventHandler<CapsuleLiveActivity> LiveActivityChanged;

	public static void SetActivity(CapsuleLiveActivity activity)
	{
		lock (_lock)
		{
			_current = activity;
		}
		NotifyChanged();
	}

	public static void UpdatePreviewText(string ownerId, string previewText, List<CapsuleTextSpan> formattedSpans = null)
	{
		lock (_lock)
		{
			if (_current == null || !string.Equals(_current.OwnerId, ownerId, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			_current.PreviewText = previewText;
			if (formattedSpans != null)
			{
				_current.FormattedSpans = formattedSpans;
			}
			_current.UpdatedAt = DateTime.UtcNow;
		}
		NotifyChanged();
	}

	public static void UpdateMessagePresentation(string ownerId, MessagePresentationState state)
	{
		lock (_lock)
		{
			if (_current == null || !string.Equals(_current.OwnerId, ownerId, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			_current.MessageState = state;
			if (state != null)
			{
				_current.PreviewText = state.Text;
			}
			_current.UpdatedAt = DateTime.UtcNow;
		}
		NotifyChanged();
	}

	public static void ClearActivity(string ownerId)
	{
		lock (_lock)
		{
			if (_current == null || !string.Equals(_current.OwnerId, ownerId, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			_current = null;
		}
		NotifyChanged();
	}

	private static void NotifyChanged()
	{
		CapsuleLiveActivity current;
		lock (_lock)
		{
			current = _current;
		}
		EventHandler<CapsuleLiveActivity> liveActivityChanged = CapsuleStateManager.LiveActivityChanged;
		if (liveActivityChanged != null)
		{
			try
			{
				liveActivityChanged(null, current);
			}
			catch
			{
			}
		}
	}
}
