using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace WindowsBuddy.BuddyCapsule.State;

public class CapsuleLiveActivity
{
	public string OwnerId { get; set; }

	public bool IsPinned { get; set; }

	public string Title { get; set; }

	public string PreviewText { get; set; }

	public List<CapsuleTextSpan> FormattedSpans { get; set; }

	public string NavigateToToolId { get; set; }

	public Func<double, Brush, UIElement> IconFactory { get; set; }

	public bool IsMovingText { get; set; }

	public DateTime UpdatedAt { get; set; }

	public MessagePresentationState MessageState { get; set; }

	public CapsuleLiveActivity()
	{
		UpdatedAt = DateTime.UtcNow;
		IsPinned = true;
		IsMovingText = false;
		FormattedSpans = new List<CapsuleTextSpan>();
	}
}
