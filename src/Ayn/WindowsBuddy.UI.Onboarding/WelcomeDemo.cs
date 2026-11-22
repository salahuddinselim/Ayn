using System;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Renderer;

namespace WindowsBuddy.UI.Onboarding;

internal sealed class WelcomeDemo
{
	private readonly CapsuleWindow _notch;

	private DispatcherTimer _timer;

	private double _t;

	private int _stage;

	public WelcomeDemo(CapsuleWindow notch)
	{
		_notch = notch;
	}

	public void Start()
	{
		if (_notch != null && _timer == null)
		{
			_t = 0.0;
			_stage = 0;
			_timer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(250.0)
			};
			_timer.Tick += delegate
			{
				Tick();
			};
			_timer.Start();
		}
	}

	public void Stop()
	{
		if (_timer != null)
		{
			_timer.Stop();
			_timer = null;
			Clear();
		}
	}

	private static void Clear()
	{
		try
		{
			PillActivityArbiter instance = PillActivityArbiter.Instance;
			instance.ClearMedia();
			instance.DismissCurrentTransient();
		}
		catch
		{
		}
	}

	private void Tick()
	{
		_t += 0.25;
		NotchPhase notchPhase = _notch.NotchPhase;
		if (notchPhase == NotchPhase.Peeking || notchPhase == NotchPhase.Opening || notchPhase == NotchPhase.Open || notchPhase == NotchPhase.Closing)
		{
			return;
		}
		PillActivityArbiter instance = PillActivityArbiter.Instance;
		try
		{
			if (_stage == 0 && _t >= 2.6)
			{
				instance.PublishClipboardCopied();
				_stage = 1;
			}
			else if (_stage == 1 && _t >= 5.2)
			{
				instance.DismissCurrentTransient();
				_stage = 2;
			}
			else if (_stage == 2 && _t >= 5.8)
			{
				instance.PublishMedia(new PillActivitySnapshot("media_session", "media", ActivityTier.Tier2_Session, ActivityType.Media, "Starboy", "The Weeknd", "1:20", (double sz, Brush br) => VectorIcons.CreateMusicIcon(sz, br), null, null, 0.3, DateTime.UtcNow, null, 0L, CollapsedActivityProfile.Media, isPlaying: true, null, null, null, "media", null));
				_stage = 3;
			}
			else if (_stage == 3 && _t >= 10.4)
			{
				instance.ClearMedia();
				_stage = 4;
			}
			else if (_stage == 4 && _t >= 11.6)
			{
				_t = 0.0;
				_stage = 0;
			}
		}
		catch
		{
		}
	}
}
