using System;
using System.Windows;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Services.Pet;
using WindowsBuddy.Tools;

namespace WindowsBuddy.BuddyCapsule.Behavior;

public sealed class NotchSettings
{
	public const double FastSeconds = 0.28;

	private static NotchSettings _instance;

	public static NotchSettings Instance => _instance ?? (_instance = new NotchSettings());

	private static NotchConfig Look => NotchConfig.Instance;

	private static NotchBehaviorConfig Beh => NotchBehaviorConfig.Instance;

	private static ToolRegistry Reg => ToolRegistry.Instance;

	public NotchVisibilityMode VisibilityMode
	{
		get
		{
			NotchVisibilityMode visibilityMode = Beh.VisibilityMode;
			if (visibilityMode != 0)
			{
				return visibilityMode;
			}
			return NotchVisibilityMode.Smart;
		}
		set
		{
			Beh.VisibilityMode = ((value == NotchVisibilityMode.Desktop) ? NotchVisibilityMode.Smart : value);
		}
	}

	public bool HideInFullscreen => Beh.HideInFullscreen;

	public bool ShowEventsOverApps
	{
		get
		{
			return Beh.ShowBriefEventsWhenHidden;
		}
		set
		{
			Beh.ShowBriefEventsWhenHidden = value;
		}
	}

	public bool NotchEnabled => Reg.CapsuleEnabled;

	public bool OpensOnClick => Reg.OpensOnClick;

	public string OpenOn
	{
		get
		{
			return Reg.OpenOn;
		}
		set
		{
			Reg.OpenOn = value;
		}
	}

	public int HoverDelayMs
	{
		get
		{
			return Reg.HoverDelayMs;
		}
		set
		{
			Reg.HoverDelayMs = value;
		}
	}

	public string Display
	{
		get
		{
			return Reg.PinnedDisplay ?? "primary";
		}
		set
		{
			Reg.PinnedDisplay = value;
		}
	}

	public bool AnimationsEnabled => Reg.AnimationsEnabled;

	public string Style
	{
		get
		{
			return Look.Style;
		}
		set
		{
			Look.Style = value;
		}
	}

	public string StyleFamily => Look.StyleFamily;

	public double SizeScale
	{
		get
		{
			return Look.SizeScale;
		}
		set
		{
			Look.SizeScale = value;
		}
	}

	public string AnimationSpeed => Look.AnimationSpeed;

	public Size RestingSize => new Size(Look.GetCollapsedWidth(), Look.GetCollapsedHeight());

	public OverMaximizedLook OverMaximizedLook
	{
		get
		{
			return Beh.OverMaximizedLook;
		}
		set
		{
			Beh.OverMaximizedLook = value;
		}
	}

	public double MiniNotchWidth => Math.Round(Math.Max(84.0, Look.GetCollapsedWidth() * 0.45));

	public double OpenSeconds
	{
		get
		{
			if (!(MorphSeconds > 0.0))
			{
				return 0.0;
			}
			return 0.2;
		}
	}

	public double CloseSeconds => MorphSeconds;

	private static double MorphSeconds
	{
		get
		{
			if (!Reg.AnimationsEnabled || !CapsuleMotion.AnimationsEnabled)
			{
				return 0.0;
			}
			return 0.28;
		}
	}

	public event EventHandler<NotchSettingsChangedEventArgs> Changed;

	private NotchSettings()
	{
		NotchConfig instance = NotchConfig.Instance;
		EventHandler value = delegate
		{
			Raise(NotchSettingsArea.Appearance);
		};
		instance.ConfigChanged += value;
		PetPreferences.Instance.Changed += delegate
		{
			Raise(NotchSettingsArea.Appearance);
		};
		NotchBehaviorConfig.Instance.ConfigChanged += delegate
		{
			Raise(NotchSettingsArea.Behavior);
		};
		ToolRegistry.Instance.CapsuleSettingsChanged += delegate
		{
			Raise(NotchSettingsArea.Placement);
		};
	}

	public double ScaledProfileWidth(double profileWidth)
	{
		return Math.Round(profileWidth * Look.GetCollapsedWidth() / 228.0);
	}

	private void Raise(NotchSettingsArea area)
	{
		Dispatcher dispatcher = Application.Current?.Dispatcher;
		if (dispatcher != null && !dispatcher.CheckAccess())
		{
			dispatcher.BeginInvoke((Action)delegate
			{
				Raise(area);
			});
			return;
		}
		EventHandler<NotchSettingsChangedEventArgs> changed = this.Changed;
		if (changed == null)
		{
			return;
		}
		Delegate[] invocationList = changed.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			EventHandler<NotchSettingsChangedEventArgs> eventHandler = (EventHandler<NotchSettingsChangedEventArgs>)invocationList[i];
			try
			{
				eventHandler(this, new NotchSettingsChangedEventArgs(area));
			}
			catch (Exception ex)
			{
				NotchLog.Error("Settings change handler", ex);
			}
		}
	}
}
