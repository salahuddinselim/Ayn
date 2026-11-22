using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Behavior;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.State;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.Services.Bluetooth;
using WindowsBuddy.Services.CoreAudio;
using WindowsBuddy.Services.Media;
using WindowsBuddy.Services.Messages;
using WindowsBuddy.Services.Pet;
using WindowsBuddy.Tools;
using WindowsBuddy.Tools.AICoding;
using WindowsBuddy.Tools.Personal;
using WindowsBuddy.Tools.Analytics;
using WindowsBuddy.Tools.Calendar;
using WindowsBuddy.Tools.Clipboard;
using WindowsBuddy.Tools.Clock;
using WindowsBuddy.Tools.Drop;
using WindowsBuddy.Tools.FileShelf;
using WindowsBuddy.Tools.Files;
using WindowsBuddy.Tools.Links;
using WindowsBuddy.Tools.Message;
using WindowsBuddy.Tools.Notes;
using WindowsBuddy.Tools.Revenue;
using WindowsBuddy.Tools.ScreenTime;
using WindowsBuddy.Tools.Sounds;
using WindowsBuddy.Tools.Timer;
using WindowsBuddy.Tools.Units;
using WindowsBuddy.Tools.Weather;
using WindowsBuddy.UI;
using WindowsBuddy.UI.Onboarding;
using WindowsBuddy.UI.Settings;

namespace WindowsBuddy.BuddyCapsule.Core;

public class CapsuleWindow : Window, INotchSurface
{
	private struct BLUETOOTH_FIND_RADIO_PARAMS
	{
		public uint dwSize;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct BLUETOOTH_DEVICE_SEARCH_PARAMS
	{
		public uint dwSize;

		[MarshalAs(UnmanagedType.Bool)]
		public bool fReturnAuthenticated;

		[MarshalAs(UnmanagedType.Bool)]
		public bool fReturnRemembered;

		[MarshalAs(UnmanagedType.Bool)]
		public bool fReturnUnknown;

		[MarshalAs(UnmanagedType.Bool)]
		public bool fReturnConnected;

		[MarshalAs(UnmanagedType.Bool)]
		public bool fIssueInquiry;

		public byte cTimeoutMultiplier;

		public IntPtr hRadio;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct BLUETOOTH_DEVICE_INFO
	{
		public uint dwSize;

		public ulong Address;

		public uint ulClassofDevice;

		[MarshalAs(UnmanagedType.Bool)]
		public bool fConnected;

		[MarshalAs(UnmanagedType.Bool)]
		public bool fRemembered;

		[MarshalAs(UnmanagedType.Bool)]
		public bool fAuthenticated;

		public ushort wYear;

		public ushort wMonth;

		public ushort wDayOfWeek;

		public ushort wDay;

		public ushort wHour;

		public ushort wMinute;

		public ushort ushort2;

		public ushort ushort3;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)]
		public string szName;
	}

	public struct MONITORINFO
	{
		public int cbSize;

		public RECT rcMonitor;

		public RECT rcWork;

		public uint dwFlags;
	}

	public struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	public struct POINT
	{
		public int X;

		public int Y;
	}

	private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

	internal enum CollapsedVisualType
	{
		None,
		Clock,
		MinimalDot,
		Clipboard,
		Timer,
		Media,
		Charging,
		Volume,
		Bluetooth,
		Network,
		SystemAlert,
		Message,
		Calendar,
		Reminder,
		Custom
	}

	private class CollapsedVisualOwner
	{
		public CollapsedVisualType VisualType;

		public UIElement VisualInstance;

		public string SnapshotId;

		public long SnapshotSequence;

		public long RenderSequence;

		public string SlotName = "A";
	}

	internal struct CollapsedLook
	{
		public double W;

		public double H;

		public double Scale;

		public double Radius;

		public double EyesHeight;

		public bool Mini;

		public bool Eyes;
	}

	private sealed class RestingPlan
	{
		public string Key;

		public double Width;

		public int TimeForm;

		public bool HideBatteryText;

		public bool DropBluetooth;

		public bool DropNowPlaying;

		public bool Fits;
	}

	internal sealed class MessageAction
	{
		public string Label;

		public bool Primary;

		public Action Run;

		public Action<Border> Decorate;

		public MessageAction(string label, bool primary, Action run)
		{
			Label = label;
			Primary = primary;
			Run = run;
		}
	}

	private struct MARGINS
	{
		public int L;

		public int R;

		public int T;

		public int B;
	}

	private delegate bool WindowVisitor(IntPtr hwnd, IntPtr state);

	private struct PowerState
	{
		public byte Ac;

		public byte Battery;

		public byte Percent;

		public byte Saver;

		public int Life;

		public int FullLife;
	}

	private struct PetQaMouse
	{
		public int X;

		public int Y;

		public uint Data;

		public uint Flags;

		public uint Time;

		public IntPtr Extra;
	}

	private struct PetQaInput
	{
		public uint Type;

		public PetQaMouse Mouse;
	}

	private const int WS_EX_TOOLWINDOW = 128;

	private const int WS_EX_NOACTIVATE = 134217728;

	private const int WM_MOUSEACTIVATE = 33;

	private const int MA_ACTIVATE = 1;

	private const int MA_NOACTIVATE = 3;

	private const int GWL_EXSTYLE = -20;

	private const int GWL_STYLE = -16;

	private const uint WS_CAPTION = 12582912u;

	private const uint MONITOR_DEFAULTTOPRIMARY = 1u;

	private const uint MONITOR_DEFAULTTONEAREST = 2u;

	private const int WM_DPICHANGED = 736;

	private const int WM_WINDOWPOSCHANGED = 71;

	private const int WM_POWERBROADCAST = 536;

	private const int PBT_APMPOWERSTATUSCHANGE = 10;

	private const int PBT_POWERSETTINGCHANGE = 32787;

	private const uint EVENT_SYSTEM_FOREGROUND = 3u;

	private const uint WINEVENT_OUTOFCONTEXT = 0u;

	private const double CollapsedWidth = 228.0;

	private const double CollapsedHeight = 35.0;

	private const double ExpandedWidth = 752.0;

	private const double ExpandedHeight = 352.0;

	private const double ShadowMarginX = 24.0;

	private const double ShadowMarginY = 36.0;

	public const double StableEnvelopeWidth = 800.0;

	public const double StableEnvelopeHeight = 388.0;

	private const int WM_NCHITTEST = 132;

	private const int HTTRANSPARENT = -1;

	private const int WM_DISPLAYCHANGE = 126;

	private const int WM_SETTINGCHANGE = 26;

	private const int SPI_SETWORKAREA = 47;

	private const int PBT_APMRESUMESUSPEND = 7;

	private const int PBT_APMRESUMEAUTOMATIC = 18;

	private const int PBT_APMSUSPEND = 4;

	private const uint SWP_NOMOVE = 2u;

	private const int WS_EX_TOPMOST = 8;

	private const double DockSlotGapMin = 4.0;

	private const double DockSlotGapMax = 10.0;

	private const double DockDividerMargin = 8.0;

	private const double DockDividerTotal = 17.0;

	private const double RestingHitPad = 4.0;

	private const double OpenHitPad = 8.0;

	private const double LineTabHitWidth = 100.0;

	private const double LineTabHitHeight = 22.0;

	private const uint GA_ROOTOWNER = 3u;

	private const double RestingResizeGraceSeconds = 0.5;

	private const double EarUnitW = 12.0;

	private const double EarUnitH = 10.0;

	private const double PresenceSeconds = 0.18;

	private const double CollapseSeconds = 0.07;

	internal const double ArrowLookH = 14.0;

	internal const double ArrowLookBodyW = 30.0;

	internal const double MiniLookH = 20.0;

	internal const double MiniLookBodyW = 50.0;

	internal const double ArrowEyesLookH = 18.0;

	internal const double ArrowEyesH = 12.0;

	internal const double MiniEyesLookH = 22.0;

	internal const double MiniEyesH = 15.0;

	internal const double LookEyesPad = 0.55;

	private const int BadgeRefreshMinIntervalMs = 250;

	private const uint SWP_NOSIZE = 1u;

	private const uint SWP_NOZORDER = 4u;

	private const uint SWP_NOACTIVATE = 16u;

	private const int CompactBars = 40;

	private const int WideBars = 56;

	private const int MiniBarCount = 4;

	internal const double HeaderRobotSize = 28.0;

	internal const double MessageRobotSize = 22.0;

	private const double EyesMinScale = 0.3;

	private const double MiniEyesHeight = 16.0;

	private const double EyesFill = 0.96;

	private const double EyesGroupUnits = 72.0;

	private const double EyesPadHeights = 0.7;

	private const int VK_LBUTTON = 1;

	private const int VK_RBUTTON = 2;

	private const uint GA_ROOT = 2u;

	private static volatile bool _btConnectedCached;

	private static DateTime _lastBtProbeUtc;

	private static int _btProbeRunning;

	private static readonly Guid GUID_BATTERY_PERCENTAGE_REMAINING;

	private static readonly Guid GUID_ACDC_POWER_SOURCE;

	private static readonly Guid GUID_POWER_SAVING_STATUS;

	private static readonly Guid GUID_CONSOLE_DISPLAY_STATE;

	private IntPtr _displayPowerNotification;

	private CollapsedActivityProfile _currentWidthProfile;

	private double _lastCapsuleTargetWidth = 228.0;

	public static readonly DependencyProperty MorphProgressProperty;

	private NotchStateMachine _machine;

	private DispatcherNotchScheduler _scheduler;

	private DispatcherTimer _telemetryTimer;

	private IntPtr _foregroundWinEventHook = IntPtr.Zero;

	private uint _taskbarCreatedMsg;

	private BuddyHeader _capsuleHeader;

	private EventHandler<SystemTelemetryData> _powerChangedHandler;

	private EventHandler<SystemTelemetryData> _networkChangedHandler;

	private EventHandler<AudioVolumeChangedEventArgs> _audioChangedHandler;

	private EventHandler<NotchSettingsChangedEventArgs> _notchSettingsHandler;

	private EventHandler<ToolNavigationEventArgs> _toolNavigatedHandler;

	private PropertyChangedEventHandler _tabSystemMotionHandler;

	private EventHandler _toolsUpdatedHandler;

	private EventHandler _primaryCollapsedToolChangedHandler;

	private EventHandler _clockSettingsChangedHandler;

	private bool _clockUiSubscribed;

	private bool _arbiterUiSubscribed;

	private bool _timerUiSubscribed;

	private bool _emojiUiSubscribed;

	private EventHandler _messageEmojiRendered;

	private DispatcherTimer _messageEmojiRefresh;

	private int _messageEmojiQueued;

	private EventHandler<DateTime> _clockSecondTickHandler;

	private EventHandler _mediaStateHandler;

	private Action<bool, int, string> _revealHandler;

	private EventHandler _hotkeyHandler;

	private EventHandler _licenseHandler;

	private DispatcherTimer _entitlementTimer;

	private bool _activationPromptedForExpiry;

	private EventHandler<string> _headerSubtitleHandler;

	private Grid _capsuleCard;

	private System.Windows.Shapes.Path _notchBackdropPath;

	private BlurEffect _backdropBlurEffect;

	private System.Windows.Shapes.Path _notchSilhouettePath;

	private System.Windows.Shapes.Path _notchOutlinePath;

	private NotchEdgeLight _edgeLight;

	private Grid _contentContainer;

	private Grid _rootGrid;

	private double _panelFitScale = 1.0;

	private Grid _collapsedContent;

	private Grid _expandedContent;

	private Grid _viewportHost;

	private UIElement _currentToolView;

	private UIElement _outgoingToolView;

	private Border _dockIndicator;

	private TranslateTransform _dockIndicatorTransform;

	private System.Windows.Controls.Button _moreBtn;

	private readonly List<string> _primaryToolIds = new List<string>();

	private readonly Dictionary<string, System.Windows.Controls.Button> _dockButtons = new Dictionary<string, System.Windows.Controls.Button>(StringComparer.OrdinalIgnoreCase);

	private StackPanel _navTabsPanel;

	private Border _navDockBorder;

	private BuddyToolLauncher _toolLauncher;

	private BuddyUndoToast _editUndo;

	private Border _launcherDismisser;

	private RotateTransform _moreIconRotateTransform;

	private DropShadowEffect _sharedNotchShadow;

	private DateTime? _lastCollapsedUtc = null;

	private PillActivitySnapshot _pendingSnapshotWhileSuppressed;

	private System.Windows.Media.Brush _outlineBrush;

	private double _outlineThickness = 1.0;

	private double _shadowOpacity = 0.4;

	private Geometry _cachedRestingFillGeometry;

	private Geometry _cachedRestingOutlineGeometry;

	private string _cachedMorphStyle;

	private double _cachedMorphCollapsedW = -1.0;

	private double _cachedMorphCollapsedH = -1.0;

	private double _cachedMorphCornerRadius = -1.0;

	private bool _isShuttingDown;

	private readonly object _shutdownLock = new object();

	private EventHandler<CapsuleLiveActivity> _liveActivityChangedHandler;

	private Grid _defaultCollapsedStack;

	private StackPanel _restingLeft;

	private StackPanel _restingRight;

	private Border _restingSeparator;

	private ContentControl _collapsedBatteryBoltHolder;

	private int _restingBatteryPercent = -1;

	private bool _restingBatteryCharging;

	private double _restingMetricsKey = -1.0;

	private StackPanel _pinnedActivityStack;

	private StackPanel _welcomeActivityStack;

	private TextBlock _welcomeText;

	private DispatcherTimer _firstRunHoldTimer;

	private ContentControl _pinnedIconHolder;

	private TextBlock _pinnedPreviewText;

	private Border _pinnedTextClipContainer;

	private TranslateTransform _pinnedTextTranslate;

	private CapsuleTextScrollController _textScrollController;

	private Canvas _collapsedMiniAnalogCanvas;

	private RotateTransform _miniAnalogHourRotate;

	private RotateTransform _miniAnalogMinuteRotate;

	private RotateTransform _miniAnalogSecondRotate;

	private TextBlock _collapsedPrimaryValueText;

	private ContentControl _collapsedBluetoothIconHolder;

	private ContentControl _collapsedNowPlayingIconHolder;

	private ContentControl _collapsedBatteryIconHolder;

	private TextBlock _collapsedBatteryText;

	private string _lastRenderedBluetoothKey = string.Empty;

	private string _lastRenderedNowPlayingKey = string.Empty;

	private string _lastRenderedBatteryKey = string.Empty;

	private Grid _activityViewport;

	private ContentControl _slotA;

	private ContentControl _slotB;

	private TranslateTransform _slotATranslate;

	private TranslateTransform _slotBTranslate;

	private readonly CollapsedVisualOwner _activeOwner = new CollapsedVisualOwner();

	private bool _isSlotAActive = true;

	private bool _isSlotTransitionInProgress;

	private long _currentRenderSequence;

	private EventHandler<PillActivityChangedEventArgs> _arbiterActivityChangedHandler;

	private EventHandler<TimerUpdateEventArgs> _timerLiveUpdateHandler;

	private EventHandler<VisibilityChangeEventArgs> _notchVisibilityHandler;

	private TranslateTransform _capsuleTranslateY;

	private TextBlock _timerLiveText;

	private ContentControl _timerPlayPauseIconHost;

	private Border _timerPlayPauseButton;

	private bool _timerIsPausedVisual;

	private TextBlock _volumeLivePercentText;

	private Border _volumeLiveSliderFill;

	private ContentControl _volumeLiveIconHost;

	private bool _isCollapsedHovered;

	private bool _lastBatteryCharging;

	private bool _lastBatteryFull;

	private bool _lastNetworkConnected = true;

	private SessionEndingEventHandler _sessionEndingHandler;

	private RotateTransform _clockNotchMiniHourRotate;

	private RotateTransform _clockNotchMiniMinuteRotate;

	private TextBlock _clockNotchLiveTimeText;

	private DispatcherTimer _clockNotchAnalogTimer;

	private DateTime _startupGraceUntil = DateTime.UtcNow.AddSeconds(6.0);

	private static readonly System.Windows.Media.Brush TabActiveBrush;

	private static readonly System.Windows.Media.Brush TabActiveBorderBrush;

	private static readonly System.Windows.Media.Brush TabInactiveBrush;

	private static readonly System.Windows.Media.Brush TabTextActiveBrush;

	private static readonly System.Windows.Media.Brush TabTextInactiveBrush;

	private NotifyIcon _notifyIcon;

	private System.Windows.Forms.ContextMenu? TrayMenu;

	private System.Windows.Forms.ContextMenu _normalTrayMenu;

	private System.Windows.Forms.ContextMenu _expiredTrayMenu;

	private string _normalTrayText;

	private IntPtr _hwnd = IntPtr.Zero;

	private DispatcherOperation _pendingEnvironmentRepair;

	private string _pendingEnvironmentReason;

	private bool _consoleDisplayWasOff;

	private static readonly IntPtr HWND_TOPMOST;

	private readonly ShutdownSession _shutdownSession = new ShutdownSession();

	internal static bool SimulateHangAfterSaveForQa;

	private static bool _clearTypeHandlerRegistered;

	private bool _clearTypeOpaque;

	private int _presentCount;

	private long _openT0;

	private long _openAnimT0;

	private bool _openByHover;

	private DispatcherTimer _restRenderTimer;

	private bool _hardwareRender;

	private static volatile string _lastInteractionSnapshot;

	private readonly Dictionary<UIElement, BuddyScrollViewer> _scrollWrappers = new Dictionary<UIElement, BuddyScrollViewer>();

	private readonly Dictionary<string, UIElement> _attachedViews = new Dictionary<string, UIElement>(StringComparer.OrdinalIgnoreCase);

	private long _viewTransitionGeneration;

	private UIElement _lockView;

	private int _focusToken;

	private DispatcherTimer _focusTimer;

	private double DockSlotGap = 4.0;

	private DispatcherTimer _dockSettle;

	private bool _restingAppearanceDirty;

	private int _restingResizeGeneration;

	private int _cardWidthGeneration;

	private int _restingResizeOwner;

	private long _restingResizeDeadline;

	private RectangleGeometry _morphRect;

	private System.Windows.Shapes.Path _earLeft;

	private System.Windows.Shapes.Path _earRight;

	private ScaleTransform _earLeftScale;

	private ScaleTransform _earRightScale;

	private TranslateTransform _earLeftShift;

	private TranslateTransform _earRightShift;

	private SolidColorBrush _panelFill;

	private byte _panelAlpha = byte.MaxValue;

	private double _closedNotchOpacity = 1.0;

	private Grid _notchBackgroundLayer;

	private SolidColorBrush _miniOpacityFill;

	private bool _backdropBlurWanted;

	private long _edgeLightLastSequence = -1L;

	private string _edgeLightLastSession;

	private bool _toolActivated;

	private long _activationQueuedGeneration = -1L;

	private TranslateTransform _expandedContentTranslate;

	private TranslateTransform _dockTranslate;

	private bool _typedSinceOpen;

	private NotchPresence _shownPresence;

	private int _presenceGen;

	private DateTime _presenceAnimStartedUtc = DateTime.MinValue;

	private Grid _lineLayer;

	private Border _lineTab;

	private ScaleTransform _lineTabScale;

	private System.Windows.Shapes.Path _lineChevron;

	private System.Windows.Shapes.Path _lineShape;

	private Grid _lineSlot;

	private Border _miniNotch;

	private ScaleTransform _miniNotchScale;

	private System.Windows.Shapes.Path _miniShape;

	private Grid _miniSlot;

	private System.Windows.Shapes.Path _peekRing;

	private bool _collapsedIsMini;

	private CollapsedLook _collapsedLook;

	private bool _morphCardPinned;

	private bool _morphRestLine;

	private Canvas _diagCanvas;

	private System.Windows.Shapes.Rectangle _diagHitRect;

	private int _badgeRefreshQueued;

	private long _lastBadgeRefreshTimestamp;

	private DispatcherTimer _badgeThrottle;

	private static readonly Dictionary<string, double> CapsOffsets;

	private RestingPlan _restingPlan;

	private double _restingFitWidth;

	private bool _restingFitImmediate;

	private TextBlock _restingProbe;

	internal static Func<double> MonitorWidthOverride;

	private TextBlock _chargingTimeText;

	private Grid _volumeLiveBar;

	private string _volumeLiveGlyph;

	private Border _expandedReminderBanner;

	private string _expandedReminderId;

	private TextBlock _reminderMoreText;

	private TextBlock _expandedReminderMoreText;

	private static readonly DropShadowEffect BannerShadow;

	private static readonly System.Windows.Media.Brush Detail60;

	private NotchActivity _presentationOffer;

	private Border _presentationOfferView;

	private static readonly ConditionalWeakTable<ImageSource, object> ArtColors;

	private FrameworkElement _mediaRoot;

	private bool _mediaWideShown;

	private object _mediaArtShown;

	private System.Windows.Media.Color _mediaColorShown;

	private MediaTrackInfo _mediaTimeline;

	private TextBlock _mediaRemainingText;

	private Grid _mediaProgressBar;

	private Run _mediaTitleRun;

	private Run _mediaArtistRun;

	private TextBlock _mediaCompactText;

	private string _mediaCompactArtist;

	private string _mediaCompactTitle;

	private string _mediaFitTitle;

	private string _mediaFitArtist;

	private double _mediaFitWidth = -1.0;

	private MediaTransportControls _mediaTransport;

	private TextBlock _mediaWideTitle;

	private TextBlock _mediaWideArtist;

	private StaticBeatBars _beatLineStatic;

	private StaticBeatBars _beatMiniStatic;

	private FrameworkElement _beatLineSlot;

	private FrameworkElement _beatMiniSlot;

	private int _beatLineBars;

	private double _beatLineBarW;

	private double _beatMiniBarW;

	private bool _mediaPlaying;

	private NotchBeatOverlay _beatOverlay;

	private bool _beatOverlayFailed;

	private bool _beatActive;

	private bool _beatPresenceFull = true;

	private int _beatFramePending;

	private Rect _beatLastLine = Rect.Empty;

	private Rect _beatLastMini = Rect.Empty;

	private int _beatStable;

	private readonly float[] _beatBands = new float[28];

	private readonly float[] _beatLine = new float[56];

	private readonly float[] _beatMini = new float[4];

	private Action _beatFrameHandler;

	private long _beatUiTicks;

	private long _beatUiFrames;

	private long _beatLastAcceptedTicks;

	private GardenView _petOutside;

	private PetStageWindow _petStageWindow;

	private Geometry _petStageShapeSource;

	private bool _petPlacementDirty = true;

	private bool _arrangingPet;

	private bool _gardenHandoff;

	private bool _gardenOfferPending;

	private bool _petPromptResize;

	private bool _petRowSuppressed;

	private double _petRowOpacity;

	private bool _petRowHitTest;

	private AynRobotMark _petHeader;

	private bool _petInitialized;

	private bool _petAtomicOwner;

	private bool _lastPetEnabled;

	private bool _lastPetEnvironmentVisible;

	private string _petLayoutKey;

	private bool _petPausedForMorph;

	private DispatcherTimer _petResume;

	private EyesView _eyesResting;

	private CompanionPetCanvas _companionPet;

	private EyesView _eyesMini;

	private DispatcherTimer _eyesSentinel;

	private bool _eyesInitialized;

	private bool _eyesModeSeen;

	internal string EyesState = "off";

	private bool _eyesRowHidden;

	private readonly HashSet<IntPtr> _hiddenSurfaceRegions = new HashSet<IntPtr>();

	internal static readonly bool DwmComposed;

	private Int32Rect _hitRegion = new Int32Rect(-1, -1, -1, -1);

	private DispatcherTimer _surfaceWatchdog;

	private bool _compositionFrameFailed;

	private bool _fileDragOver;

	private DispatcherTimer _desktopClickPoll;

	private bool _buttonDown;

	private bool _pressOnDesktop;

	private POINT _pressAt;

	public double MorphProgress
	{
		get
		{
			return (double)GetValue(MorphProgressProperty);
		}
		set
		{
			SetValue(MorphProgressProperty, value);
		}
	}

	public NotchStateMachine Machine => _machine;

	private CapsuleDisplayState _state
	{
		get
		{
			if (_machine == null)
			{
				return CapsuleDisplayState.Collapsed;
			}
			return _machine.Phase switch
			{
				NotchPhase.Hidden => CapsuleDisplayState.Suppressed, 
				NotchPhase.Peeking => CapsuleDisplayState.Hovering, 
				NotchPhase.Opening => CapsuleDisplayState.Expanding, 
				NotchPhase.Open => CapsuleDisplayState.Expanded, 
				NotchPhase.Closing => CapsuleDisplayState.Collapsing, 
				_ => CapsuleDisplayState.Collapsed, 
			};
		}
	}

	private CapsuleState _capsuleLifecycleState
	{
		get
		{
			if (_machine == null)
			{
				return CapsuleState.Booting;
			}
			switch (_machine.Phase)
			{
			case NotchPhase.Hidden:
				return CapsuleState.HiddenByForegroundApp;
			case NotchPhase.Peeking:
				return CapsuleState.HoverIntent;
			case NotchPhase.Opening:
				return CapsuleState.Expanding;
			case NotchPhase.Open:
				return CapsuleState.Expanded;
			case NotchPhase.Closing:
				return CapsuleState.Collapsing;
			default:
				if (_activeOwner.VisualType != CollapsedVisualType.Clock)
				{
					return CapsuleState.LiveActivity;
				}
				return CapsuleState.Idle;
			}
		}
	}

	public CapsuleState CurrentCapsuleState => _capsuleLifecycleState;

	public CapsuleState CapsuleState => _capsuleLifecycleState;

	public CapsuleDisplayState CapsuleDisplayState => _state;

	private bool IsRestingSurface
	{
		get
		{
			if (_state != 0)
			{
				return _state == CapsuleDisplayState.Hovering;
			}
			return true;
		}
	}

	internal CollapsedVisualType ActiveVisualType => _activeOwner.VisualType;

	internal string ActiveSlotName => _activeOwner.SlotName;

	public static CapsuleWindow Instance { get; private set; }

	internal FrameworkElement NotchCard => _capsuleCard;

	internal NotchPhase NotchPhase
	{
		get
		{
			if (_machine == null)
			{
				return NotchPhase.Hidden;
			}
			return _machine.Phase;
		}
	}

	internal string TrayTooltipForQa
	{
		get
		{
			if (_notifyIcon == null)
			{
				return null;
			}
			return _notifyIcon.Text;
		}
	}

	internal string[] TrayMenuForQa
	{
		get
		{
			if (_notifyIcon == null || TrayMenu == null)
			{
				return new string[0];
			}
			List<string> list = new List<string>();
			foreach (System.Windows.Forms.MenuItem menuItem in TrayMenu.MenuItems)
			{
				list.Add(menuItem.Text);
			}
			return list.ToArray();
		}
	}

	public int EnvironmentRepairs { get; private set; }

	public bool IsShutdownCommitted => _shutdownSession.Committed;

	public bool IsShutdownRecoveryRequired => _shutdownSession.RecoveryRequired;

	internal int RestingPresentCount => _presentCount;

	internal static string LastInteractionSnapshot => _lastInteractionSnapshot;

	internal Func<System.Windows.Point?> CursorOverride { get; set; }

	private bool _restingResizeActive
	{
		get
		{
			if (_restingResizeOwner != 0 && _restingResizeOwner == _cardWidthGeneration)
			{
				return Stopwatch.GetTimestamp() < _restingResizeDeadline;
			}
			return false;
		}
	}

	int INotchSurface.AnimationBudgetMs => (int)(Math.Max(NotchSettings.Instance.OpenSeconds, NotchSettings.Instance.CloseSeconds) * 1000.0) + 150;

	public NotchPresence ShownPresence => _shownPresence;

	internal CollapsedLook CollapsedLookNow => _collapsedLook;

	internal FrameworkElement CollapsedVisual
	{
		get
		{
			if (!_collapsedIsMini)
			{
				return _lineTab;
			}
			return _miniNotch;
		}
	}

	private static double BodyHeight
	{
		get
		{
			NotchConfig instance = NotchConfig.Instance;
			double collapsedHeight = instance.GetCollapsedHeight();
			if (!(instance.StyleFamily == "Island"))
			{
				return collapsedHeight;
			}
			return collapsedHeight - 5.0;
		}
	}

	internal static double ActivityScale => Math.Max(0.8, Math.Min(1.25, BodyHeight / 35.0));

	internal static double PrimaryFontSize => NotchSize(12.5);

	internal static double NumberFontSize => NotchSize(12.5);

	internal static double SecondaryFontSize => NotchSize(11.5);

	internal static double RestingGlyphSize => Math.Round(16.0 * ActivityScale);

	internal static double BatteryHeight => Half(12.0 * ActivityScale);

	private static System.Windows.Media.Brush PrimaryBrush => NotchConfig.Instance.GetForegroundBrush();

	internal string RestingWantsOverride { get; set; }

	internal double RestingFitWidth => _restingFitWidth;

	internal int RestingTimeForm
	{
		get
		{
			if (_restingPlan == null)
			{
				return 0;
			}
			return _restingPlan.TimeForm;
		}
	}

	internal static double MessageScale => Math.Max(0.9, Math.Min(1.2, NotchConfig.Instance.SizeScale));

	internal static bool CompactMessages => NotchConfig.Instance.Style == "Compact";

	internal static double StatusMessageHeight
	{
		get
		{
			NotchConfig instance = NotchConfig.Instance;
			double num = ((instance.StyleFamily == "Island") ? 5.0 : 0.0);
			return instance.GetCollapsedHeight() - num;
		}
	}

	internal static double CardMessageHeight => StatusMessageHeight + Math.Round(Math.Min(8.0, 8.0 * MessageScale));

	internal static double MessageButtonHeight => Math.Floor(Math.Min(24.0 * MessageScale, CardMessageHeight - 10.0 * MessageScale));

	internal bool HasPresentationOffer => _presentationOfferView != null;

	private bool MediaWideWanted
	{
		get
		{
			if (!PillActivityArbiter.Instance.IsMediaWide)
			{
				return _isCollapsedHovered;
			}
			return true;
		}
	}

	private static bool BeatAnimationsAllowed
	{
		get
		{
			if (CapsuleMotion.AnimationsEnabled)
			{
				return !PowerSaver.IsBatterySaverOn;
			}
			return false;
		}
	}

	internal bool BeatActive => _beatActive;

	internal NotchBeatOverlay BeatOverlay => _beatOverlay;

	internal NotchEdgeLight EdgeLightForQa => _edgeLight;

	internal TimeSpan BeatUiTime => TimeSpan.FromSeconds((double)_beatUiTicks / (double)Stopwatch.Frequency);

	internal long BeatUiFrames => _beatUiFrames;

	private bool GardenOwnsStage
	{
		get
		{
			if (!_gardenOfferPending && GardenResting(PillActivityArbiter.Instance.CurrentActivity))
			{
				return !OfferCanDisplay(PillActivityArbiter.Instance.CurrentActivity);
			}
			return false;
		}
	}

	private bool GardenBlocksHover
	{
		get
		{
			if (!IsRestingSurface || !GardenOwnsStage || _petOutside == null || !_petOutside.IsVisible)
			{
				return false;
			}
			if (_petOutside.HasActionPrompt)
			{
				return true;
			}
			if (!GetCursorPos(out var lpPoint))
			{
				return false;
			}
			try
			{
				return _petOutside.BlocksHover(_petOutside.PointFromScreen(new System.Windows.Point(lpPoint.X, lpPoint.Y)));
			}
			catch (InvalidOperationException)
			{
				return false;
			}
		}
	}

	internal bool PetPausedForPanel => _petPausedForMorph;

	internal EyesView EyesRestingView => _eyesResting;

	internal EyesView EyesMiniView => _eyesMini;

	internal bool EyesSentinelActive
	{
		get
		{
			if (_eyesSentinel != null)
			{
				return _eyesSentinel.IsEnabled;
			}
			return false;
		}
	}

	internal static bool EyesModeOn
	{
		get
		{
			if (EyesPreferences.Instance.Enabled)
			{
				return !PetPreferences.Instance.Enabled;
			}
			return false;
		}
	}

	public event EventHandler<CapsuleState> CapsuleLifecycleStateChanged;

	[DllImport("user32.dll")]
	private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

	[DllImport("user32.dll")]
	private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(IntPtr hWnd);

	private void SetNonActivating(bool noActivate)
	{
		try
		{
			IntPtr handle = new WindowInteropHelper(this).Handle;
			if (!(handle == IntPtr.Zero))
			{
				int windowLong = GetWindowLong(handle, -20);
				if (noActivate)
				{
					SetWindowLong(handle, -20, windowLong | 0x8000000);
				}
				else
				{
					SetWindowLong(handle, -20, windowLong & -134217729);
				}
			}
		}
		catch
		{
		}
	}

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

	[DllImport("user32.dll")]
	private static extern bool GetCursorPos(out POINT lpPoint);

	[DllImport("user32.dll")]
	private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

	[DllImport("user32.dll")]
	private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern uint RegisterWindowMessage(string lpString);

	[DllImport("bthprops.cpl", SetLastError = true)]
	private static extern IntPtr BluetoothFindFirstRadio(ref BLUETOOTH_FIND_RADIO_PARAMS pbtfrp, out IntPtr phRadio);

	[DllImport("bthprops.cpl", SetLastError = true)]
	private static extern bool BluetoothFindRadioClose(IntPtr hFind);

	[DllImport("bthprops.cpl", SetLastError = true)]
	private static extern IntPtr BluetoothFindFirstDevice(ref BLUETOOTH_DEVICE_SEARCH_PARAMS pbtsp, ref BLUETOOTH_DEVICE_INFO pbtdi);

	[DllImport("bthprops.cpl", SetLastError = true)]
	private static extern bool BluetoothFindDeviceClose(IntPtr hFind);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool CloseHandle(IntPtr handle);

	[DllImport("bthprops.cpl", SetLastError = true)]
	private static extern bool BluetoothFindNextDevice(IntPtr hFind, ref BLUETOOTH_DEVICE_INFO pbtdi);

	private static bool IsBluetoothDeviceConnectedCached()
	{
		if ((DateTime.UtcNow - _lastBtProbeUtc).TotalSeconds >= 5.0 && Interlocked.CompareExchange(ref _btProbeRunning, 1, 0) == 0)
		{
			_lastBtProbeUtc = DateTime.UtcNow;
			ThreadPool.QueueUserWorkItem(delegate
			{
				try
				{
					bool flag = ProbeBluetoothDeviceConnected();
					if (flag != _btConnectedCached)
					{
						_btConnectedCached = flag;
						CapsuleWindow instance = Instance;
						if (instance != null)
						{
							try
							{
								instance.RequestBadgeRefresh();
								return;
							}
							catch
							{
								return;
							}
						}
					}
				}
				finally
				{
					Interlocked.Exchange(ref _btProbeRunning, 0);
				}
			});
		}
		return _btConnectedCached;
	}

	private static bool ProbeBluetoothDeviceConnected()
	{
		try
		{
			BLUETOOTH_FIND_RADIO_PARAMS pbtfrp = default(BLUETOOTH_FIND_RADIO_PARAMS);
			pbtfrp.dwSize = (uint)Marshal.SizeOf(typeof(BLUETOOTH_FIND_RADIO_PARAMS));
			IntPtr phRadio;
			IntPtr intPtr = BluetoothFindFirstRadio(ref pbtfrp, out phRadio);
			if (intPtr == IntPtr.Zero)
			{
				return false;
			}
			BluetoothFindRadioClose(intPtr);
			if (phRadio == IntPtr.Zero)
			{
				return false;
			}
			try
			{
				BLUETOOTH_DEVICE_SEARCH_PARAMS bLUETOOTH_DEVICE_SEARCH_PARAMS = default(BLUETOOTH_DEVICE_SEARCH_PARAMS);
				bLUETOOTH_DEVICE_SEARCH_PARAMS.dwSize = (uint)Marshal.SizeOf(typeof(BLUETOOTH_DEVICE_SEARCH_PARAMS));
				bLUETOOTH_DEVICE_SEARCH_PARAMS.fReturnConnected = true;
				bLUETOOTH_DEVICE_SEARCH_PARAMS.hRadio = phRadio;
				BLUETOOTH_DEVICE_SEARCH_PARAMS pbtsp = bLUETOOTH_DEVICE_SEARCH_PARAMS;
				BLUETOOTH_DEVICE_INFO bLUETOOTH_DEVICE_INFO = default(BLUETOOTH_DEVICE_INFO);
				bLUETOOTH_DEVICE_INFO.dwSize = (uint)Marshal.SizeOf(typeof(BLUETOOTH_DEVICE_INFO));
				BLUETOOTH_DEVICE_INFO pbtdi = bLUETOOTH_DEVICE_INFO;
				IntPtr intPtr2 = BluetoothFindFirstDevice(ref pbtsp, ref pbtdi);
				if (intPtr2 != IntPtr.Zero)
				{
					try
					{
						int num = 0;
						do
						{
							if (pbtdi.fConnected)
							{
								return true;
							}
							BLUETOOTH_DEVICE_INFO bLUETOOTH_DEVICE_INFO2 = default(BLUETOOTH_DEVICE_INFO);
							bLUETOOTH_DEVICE_INFO2.dwSize = (uint)Marshal.SizeOf(typeof(BLUETOOTH_DEVICE_INFO));
							pbtdi = bLUETOOTH_DEVICE_INFO2;
						}
						while (++num < 64 && BluetoothFindNextDevice(intPtr2, ref pbtdi));
					}
					finally
					{
						BluetoothFindDeviceClose(intPtr2);
					}
				}
			}
			finally
			{
				CloseHandle(phRadio);
			}
			return BluetoothService.Instance != null && BluetoothService.Instance.IsExternalAudioConnected;
		}
		catch
		{
			return false;
		}
	}

	[DllImport("user32.dll")]
	private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

	[DllImport("user32.dll")]
	private static extern uint GetDpiForWindow(IntPtr hwnd);

	[DllImport("user32.dll")]
	private static extern bool UnregisterPowerSettingNotification(IntPtr handle);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid powerSettingGuid, int flags);

	[DllImport("shcore.dll")]
	private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

	[DllImport("user32.dll")]
	private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

	[DllImport("user32.dll")]
	private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

	private static void OnMorphProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is CapsuleWindow capsuleWindow)
		{
			capsuleWindow.UpdateMorphState((double)e.NewValue);
		}
	}

	public void TransitionToState(CapsuleState newState, string reason = null)
	{
	}

	public bool RequestExpand(string reason = null, bool isFirstRun = false)
	{
		if (_machine == null || _machine.IsOpenOrOpening)
		{
			return false;
		}
		_machine.RequestOpen(reason ?? "RequestExpand");
		return _machine.IsOpenOrOpening;
	}

	public bool RequestCollapse(string reason = null)
	{
		if (_machine == null || !_machine.IsOpenOrOpening)
		{
			return false;
		}
		_machine.RequestClose(reason ?? "RequestCollapse");
		return true;
	}

	private static void RegisterEntitlementProducers()
	{
		EntitlementGate.Register("timer", () => TimerService.Instance.PauseForShutdown());
		EntitlementGate.Register("screen-time", () => ScreenTimeService.Instance.PauseForShutdown());
		EntitlementGate.Register("weather", () => WeatherService.Instance.PauseForShutdown());
		EntitlementGate.Register("calendar", () => CalendarService.Instance.PauseForShutdown());
		EntitlementGate.Register("ai-coding", AICodingService.PauseIfCreated);
		EntitlementGate.Register("converter", ConvertEngine.PauseOutputs);
		EntitlementGate.Register("clipboard", () => ClipboardService.Instance.PauseForShutdown());
		EntitlementGate.Register("file-shelf", () => FileShelfService.Instance.PauseForShutdown());
		EntitlementGate.Register("analytics-network", AnalyticsService.PauseNetwork);
		EntitlementGate.Register("revenue-network", RevenueService.PauseNetwork);
		EntitlementGate.Register("live-activities", delegate
		{
			PillActivityArbiter.Instance.SetSuppressed(suppressed: true);
			return delegate
			{
				PillActivityArbiter.Instance.SetSuppressed(suppressed: false);
			};
		});
	}

	private void ApplyEntitlementUi(bool entitled)
	{
		ApplyTrayEntitlementState(entitled);
		if (!entitled)
		{
			ToolRouter.Instance.SuspendActiveTool();
			if (_machine != null)
			{
				_machine.SetEnabled(enabled: false, "free build");
			}
			base.Visibility = Visibility.Hidden;
			if (_edgeLight != null)
			{
				_edgeLight.SyncWindow();
			}
			return;
		}
		_activationPromptedForExpiry = false;
		if (_machine != null)
		{
			_machine.SetEnabled(ToolRegistry.Instance.CapsuleEnabled, "entitled");
		}
		if (_navDockBorder != null)
		{
			_navDockBorder.Visibility = Visibility.Visible;
		}
		if (_viewportHost != null && _lockView != null && _viewportHost.Children.Contains(_lockView))
		{
			_viewportHost.Children.Remove(_lockView);
		}
		if (_currentToolView == _lockView)
		{
			_currentToolView = null;
		}
		EnsureBackgroundTools();
		if (!ToolRouter.Instance.ResumeSuspendedTool())
		{
			IBuddyTool activeTool = ToolRouter.Instance.ActiveTool;
			if (activeTool != null)
			{
				ToolRouter.Instance.RouteTo(activeTool.Id, forceRecreate: true);
			}
		}
		ToolRouter.Instance.EnsureUsableTab();
	}

	private void MaybeShowExpiredActivation()
	{
		if (!EntitlementGate.IsEntitled && !LicenseStub.AwaitingFirstServerContact && !_activationPromptedForExpiry && !_isShuttingDown && !_shutdownSession.Running && !_shutdownSession.Committed && LicenseService.Instance.State != LicenseState.Revoked)
		{
			_activationPromptedForExpiry = true;
			SettingsLauncher.OpenForNotEntitled();
		}
	}

	private void RefreshEditing()
	{
		if (_capsuleHeader != null)
		{
			_capsuleHeader.SetEditing(NotchEditing.IsEditing);
		}
		if (!NotchEditing.IsEditing && _toolLauncher != null && _toolLauncher.WidgetMode)
		{
			_toolLauncher.BeginClose();
		}
		RebuildNavTabs();
	}

	private void RemoveDock(string id, FrameworkElement view)
	{
		if (id == "home")
		{
			return;
		}
		EditGesture.Remove(view, delegate
		{
			ToolRegistry.Instance.SetToolEnabled(id, enabled: false);
			_editUndo.Show("Removed " + BuddyToolLauncher.Name(id), delegate
			{
				if (!ToolRegistry.Instance.TryAddToDock(id))
				{
					_editUndo.ShowNotice("Your dock is full. Remove a tool to add another.");
				}
			}, delegate
			{
			});
		});
	}

	static CapsuleWindow()
	{
		_lastBtProbeUtc = DateTime.MinValue;
		GUID_BATTERY_PERCENTAGE_REMAINING = new Guid("A7AD8041-B45A-4CAE-87A3-EECBB468A9E1");
		GUID_ACDC_POWER_SOURCE = new Guid("5D3E9A59-E9D5-4B00-A6BD-FF34FF516548");
		GUID_POWER_SAVING_STATUS = new Guid("E00958C0-C213-4ACE-AC77-FECCED2EEEA5");
		GUID_CONSOLE_DISPLAY_STATE = new Guid("6FE69556-704A-47A0-8F24-C28D936FDA47");
		MorphProgressProperty = DependencyProperty.Register("MorphProgress", typeof(double), typeof(CapsuleWindow), new PropertyMetadata(0.0, OnMorphProgressChanged));
		HWND_TOPMOST = new IntPtr(-1);
		_lastInteractionSnapshot = "not sampled";
		CapsOffsets = new Dictionary<string, double>(StringComparer.Ordinal);
		BannerShadow = CreateBannerShadow();
		Detail60 = Ink(Colors.White, 179);
		ArtColors = new ConditionalWeakTable<ImageSource, object>();
		DwmComposed = Environment.GetEnvironmentVariable("VAYLI_LAYERED") != "1" && CompositionSupported();
		SolidColorBrush solidColorBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(24, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		solidColorBrush.Freeze();
		TabActiveBrush = solidColorBrush;
		SolidColorBrush solidColorBrush2 = new SolidColorBrush(System.Windows.Media.Color.FromArgb(36, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		solidColorBrush2.Freeze();
		TabActiveBorderBrush = solidColorBrush2;
		TabInactiveBrush = System.Windows.Media.Brushes.Transparent;
		TabTextActiveBrush = System.Windows.Media.Brushes.White;
		TabTextInactiveBrush = BuddyTokens.TextMutedBrush;
	}

	internal static void StartupMark(string phase)
	{
		try
		{
			using Process process = Process.GetCurrentProcess();
			TransactionLogger.Info("Startup: " + phase + " +" + (int)(DateTime.Now - process.StartTime).TotalMilliseconds + " ms");
		}
		catch
		{
		}
	}

	public CapsuleWindow()
	{
		StartupMark("window constructor begins");
		ThemeTokens.Initialize();
		Instance = this;
		InitializeWindow();
		InitializeNotifyIcon();
		BuildUI();
		InitializeTimers();
		StartupMark("UI built");
		if (LicenseStub.Status != LicenseStatus.Active)
		{
			LicenseStub.StartAyn();
		}
		RegisterEntitlementProducers();
		EntitlementGate.Changed += delegate(bool entitled)
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				ApplyEntitlementUi(entitled);
				if (!entitled)
				{
					MaybeShowExpiredActivation();
				}
			});
		};
		_licenseHandler = delegate
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				EntitlementGate.Evaluate();
				ApplyTrayEntitlementState(EntitlementGate.IsEntitled);
				MaybeShowExpiredActivation();
			});
		};
		LicenseStub.Changed += _licenseHandler;
		EntitlementGate.Evaluate();
		StartupMark("entitlement evaluated");
		RegisterCoreTools();
		HookSystemEvents();
		StartupMark("system hooks ready");
		ApplyNotchConfig();
		RefreshLiveBadges();
		UpdateCollapsedLiveActivity(CapsuleStateManager.Current);
		_hotkeyHandler = delegate
		{
			ToggleFromHotkey();
		};
		HotkeyService.Instance.Pressed += _hotkeyHandler;
		HotkeyService.Instance.Initialize();
		NotchDiagnostics.Initialize(this);
		SettingsLauncher.Listen(base.Dispatcher);
		if (!EntitlementGate.IsEntitled)
		{
			ApplyEntitlementUi(entitled: false);
		}
		_entitlementTimer = new DispatcherTimer(DispatcherPriority.Background, base.Dispatcher)
		{
			Interval = TimeSpan.FromSeconds(30.0)
		};
		_entitlementTimer.Tick += delegate
		{
			EntitlementGate.Evaluate();
		};
		_entitlementTimer.Start();
		StartupMark("window constructor done");
		base.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, (Action)delegate
		{
			StartupMark("interactive (first idle)");
		});
	}

	public void ShowToastNotification(string title, string message, Action onClick = null)
	{
		try
		{
			if (_notifyIcon == null)
			{
				return;
			}
			_notifyIcon.ShowBalloonTip(4000, title, message, ToolTipIcon.Info);
			if (onClick == null)
			{
				return;
			}
			EventHandler handler = null;
			handler = delegate
			{
				try
				{
					_notifyIcon.BalloonTipClicked -= handler;
				}
				catch
				{
				}
				try
				{
					onClick();
				}
				catch
				{
				}
			};
			_notifyIcon.BalloonTipClicked += handler;
		}
		catch
		{
		}
	}

	public static void ShowToast(string title, string message, Action onClick = null)
	{
		CapsuleWindow inst = Instance;
		if (inst != null)
		{
			inst.Dispatcher.BeginInvoke((Action)delegate
			{
				inst.ShowToastNotification(title, message, onClick);
			});
		}
	}

	public void SetTemporaryEdgeLightColor(System.Windows.Media.Color color)
	{
		if (_edgeLight != null)
		{
			_edgeLight.PreviewColor(color);
		}
	}

	private void InitializeWindow()
	{
		base.Title = "Ayn";
		base.WindowStyle = WindowStyle.None;
		base.AllowsTransparency = !DwmComposed;
		base.Background = (DwmComposed ? System.Windows.Media.Brushes.Transparent : null);
		base.ResizeMode = ResizeMode.NoResize;
		base.Topmost = true;
		base.ShowInTaskbar = false;
		base.ShowActivated = false;
		base.Width = 800.0;
		base.Height = 388.0;
		base.SnapsToDevicePixels = true;
		base.UseLayoutRounding = true;
		AynType.PrepareRoot(this, TypeSurface.Notch);
		LiveMessageVisuals.Crisp(this);
		_clearTypeOpaque = NotchConfig.Instance.Opacity >= 0.999;
		if (!_clearTypeHandlerRegistered)
		{
			_clearTypeHandlerRegistered = true;
			EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.SizeChangedEvent, new SizeChangedEventHandler(OnAnyElementSized));
		}
		RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
		FocusManager.SetIsFocusScope(this, value: true);
		KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);
		KeyboardNavigation.SetControlTabNavigation(this, KeyboardNavigationMode.Cycle);
		KeyboardNavigation.SetDirectionalNavigation(this, KeyboardNavigationMode.Cycle);
		base.PreviewGotKeyboardFocus += delegate
		{
			if (_machine != null)
			{
				_machine.NotifyTextInputChanged();
			}
		};
		base.PreviewKeyDown += delegate(object s, System.Windows.Input.KeyEventArgs ev)
		{
			CancelPendingNavigationFocus();
			NoteTyping(ev.Key);
		};
		base.PreviewTextInput += delegate
		{
			NoteTyping(Key.A);
		};
		base.PreviewLostKeyboardFocus += delegate
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				if (_machine != null)
				{
					_machine.NotifyTextInputChanged();
				}
			});
		};
		base.Deactivated += delegate
		{
			if (_machine != null)
			{
				_machine.FocusLost("notch deactivated");
			}
		};
		base.PreviewMouseDown += delegate
		{
			CancelPendingNavigationFocus();
			if (_state == CapsuleDisplayState.Expanded || _state == CapsuleDisplayState.Expanding)
			{
				IntPtr handle = new WindowInteropHelper(this).Handle;
				if (handle != IntPtr.Zero && GetForegroundWindow() != handle)
				{
					SetForegroundWindow(handle);
				}
			}
		};
		base.PreviewKeyDown += OnCapsulePreviewKeyDown;
		base.Loaded += delegate
		{
			if (_edgeLight != null)
			{
				_edgeLight.Refresh();
			}
		};
		base.ContentRendered += delegate
		{
			if (_edgeLight != null)
			{
				_edgeLight.Refresh();
			}
		};
		base.IsVisibleChanged += delegate
		{
			if (_edgeLight != null)
			{
				_edgeLight.Refresh();
			}
			UpdateBeat();
		};
	}

	private void OnTrayThemeMaybeChanged(object sender, UserPreferenceChangedEventArgs e)
	{
		if (e.Category != UserPreferenceCategory.General)
		{
			return;
		}
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			if (!_isShuttingDown)
			{
				OnEnvironmentChanged("Windows theme changed");
			}
			if (_notifyIcon != null)
			{
				Icon icon = _notifyIcon.Icon;
				_notifyIcon.Icon = BrandAssets.LoadTrayIcon();
				if (icon != null && !object.ReferenceEquals(icon, SystemIcons.Application))
				{
					icon.Dispose();
				}
			}
		});
	}

	private void InitializeNotifyIcon()
	{
		try
		{
			_notifyIcon = new NotifyIcon();
			_notifyIcon.Text = "Ayn";
			_notifyIcon.Icon = BrandAssets.LoadTrayIcon();
			_notifyIcon.Visible = true;
			SystemEvents.UserPreferenceChanged += OnTrayThemeMaybeChanged;
			System.Windows.Forms.ContextMenu contextMenu = new System.Windows.Forms.ContextMenu();
			System.Windows.Forms.MenuItem toggleItem = new System.Windows.Forms.MenuItem();
			toggleItem.Text = (ToolRegistry.Instance.CapsuleEnabled ? "Hide notch" : "Show notch");
			toggleItem.Click += delegate
			{
				base.Dispatcher.Invoke(delegate
				{
					bool flag = !ToolRegistry.Instance.CapsuleEnabled;
					ToolRegistry.Instance.CapsuleEnabled = flag;
					toggleItem.Text = (flag ? "Hide notch" : "Show notch");
				});
			};
			System.Windows.Forms.MenuItem menuItem = new System.Windows.Forms.MenuItem("Open notch");
			menuItem.Click += delegate
			{
				base.Dispatcher.BeginInvoke((Action)delegate
				{
					OpenFromOutside("tray menu");
				});
			};
			System.Windows.Forms.MenuItem menuItem2 = new System.Windows.Forms.MenuItem("Settings…");
			menuItem2.Click += delegate
			{
				base.Dispatcher.BeginInvoke((Action)delegate
				{
					OpenSettings(null);
				});
			};
			contextMenu.Popup += delegate
			{
				toggleItem.Text = (ToolRegistry.Instance.CapsuleEnabled ? "Hide notch" : "Show notch");
			};
			System.Windows.Forms.MenuItem menuItem3 = new System.Windows.Forms.MenuItem("Exit Ayn");
			menuItem3.Click += delegate
			{
				base.Dispatcher.Invoke(delegate
				{
					RequestQuit("tray Exit");
				});
			};
			contextMenu.MenuItems.Add(menuItem);
			contextMenu.MenuItems.Add(toggleItem);
			contextMenu.MenuItems.Add(menuItem2);
			contextMenu.MenuItems.Add("Reset notch", delegate
			{
				base.Dispatcher.BeginInvoke(new Action(ResetNotchWindows));
			});
			contextMenu.MenuItems.Add("-");
			contextMenu.MenuItems.Add(ActivityPreview.CreateTrayMenu(base.Dispatcher));
			contextMenu.MenuItems.Add("-");
			contextMenu.MenuItems.Add(menuItem3);
			TrayMenu = contextMenu;
			_normalTrayMenu = contextMenu;
			_normalTrayText = _notifyIcon.Text;
			_expiredTrayMenu = BuildExpiredTrayMenu();
			if (!EntitlementGate.IsEntitled)
			{
				ApplyTrayEntitlementState(entitled: false);
			}
			_notifyIcon.DoubleClick += delegate
			{
				base.Dispatcher.BeginInvoke((Action)delegate
				{
					if (!EntitlementGate.IsEntitled)
					{
						SettingsLauncher.OpenForNotEntitled();
					}
					else if (_machine != null && _machine.IsOpenOrOpening)
					{
						_machine.RequestClose("tray double-click");
					}
					else
					{
						OpenFromOutside("tray double-click");
					}
				});
			};
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Tray icon initialization: " + ex.Message);
		}
	}

	private System.Windows.Forms.ContextMenu BuildExpiredTrayMenu()
	{
		System.Windows.Forms.ContextMenu contextMenu = new System.Windows.Forms.ContextMenu();
		System.Windows.Forms.MenuItem menuItem = new System.Windows.Forms.MenuItem("Open Ayn");
		menuItem.Click += delegate
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				SettingsLauncher.OpenForNotEntitled();
			});
		};
		System.Windows.Forms.MenuItem menuItem2 = new System.Windows.Forms.MenuItem("Ayn");
		menuItem2.Click += delegate
		{
			AppLinks.Open("https://github.com/salahuddinselim/Ayn");
		};
		System.Windows.Forms.MenuItem menuItem3 = new System.Windows.Forms.MenuItem("Enter license key");
		menuItem3.Click += delegate
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				AynConnectWindow.ShowOrActivate(keyEntry: true);
			});
		};
		System.Windows.Forms.MenuItem menuItem4 = new System.Windows.Forms.MenuItem("Quit");
		menuItem4.Click += delegate
		{
			base.Dispatcher.Invoke(delegate
			{
				RequestQuit("tray Quit");
			});
		};
		contextMenu.MenuItems.Add(menuItem);
		contextMenu.MenuItems.Add(menuItem2);
		contextMenu.MenuItems.Add(menuItem3);
		contextMenu.MenuItems.Add("Reset notch", delegate
		{
			base.Dispatcher.BeginInvoke(new Action(ResetNotchWindows));
		});
		contextMenu.MenuItems.Add("-");
		contextMenu.MenuItems.Add(menuItem4);
		return contextMenu;
	}

	private void ApplyTrayEntitlementState(bool entitled)
	{
		if (_notifyIcon == null || _normalTrayMenu == null)
		{
			return;
		}
		try
		{
			if (entitled)
			{
				TrayMenu = _normalTrayMenu;
				_notifyIcon.Text = _normalTrayText;
			}
			else
			{
				TrayMenu = _expiredTrayMenu;
				_notifyIcon.Text = (LicenseStub.HasStoredKey ? "Ayn - free build" : ((LicenseStub.Access == AccessState.Offline) ? "Ayn - free build" : ((LicenseStub.Access == AccessState.Checking) ? "Ayn - free build" : (LicenseStub.AynStarted ? "Ayn - free build" : "Ayn - free build"))));
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Tray state update: " + ex.Message);
		}
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		IntPtr handle = new WindowInteropHelper(this).Handle;
		int windowLong = GetWindowLong(handle, -20);
		SetWindowLong(handle, -20, windowLong | 0x80 | 0x8000000);
		_taskbarCreatedMsg = RegisterWindowMessage("TaskbarCreated");
		HwndSource hwndSource = HwndSource.FromHwnd(handle);
		if (hwndSource != null)
		{
			hwndSource.AddHook(WndProc);
			InitializeComposition(handle, hwndSource);
			Guid powerSettingGuid = GUID_BATTERY_PERCENTAGE_REMAINING;
			Guid powerSettingGuid2 = GUID_ACDC_POWER_SOURCE;
			bool flag = RegisterPowerSettingNotification(handle, ref powerSettingGuid, 0) != IntPtr.Zero;
			bool flag2 = RegisterPowerSettingNotification(handle, ref powerSettingGuid2, 0) != IntPtr.Zero;
			Guid powerSettingGuid3 = GUID_POWER_SAVING_STATUS;
			RegisterPowerSettingNotification(handle, ref powerSettingGuid3, 0);
			Guid powerSettingGuid4 = GUID_CONSOLE_DISPLAY_STATE;
			_displayPowerNotification = RegisterPowerSettingNotification(handle, ref powerSettingGuid4, 0);
			if (flag && flag2)
			{
				SystemMonitorService.OnPowerNotificationsRegistered();
			}
		}
		try
		{
			ClipboardService.Instance.AttachWindow(handle);
		}
		catch
		{
		}
		_hwnd = handle;
		UpdatePosition(initial: true);
		AssertTopmost();
		NotchVisibilityEngine instance = NotchVisibilityEngine.Instance;
		instance.NotchWindowHandle = handle;
		instance.NotchMonitorProvider = () => MonitorFromWindow(handle, 2u);
		UpdateMorphState(0.0);
		if (_machine != null)
		{
			_machine.SetEnabled(ToolRegistry.Instance.CapsuleEnabled && EntitlementGate.IsEntitled, "startup");
		}
		instance.ForceReevaluate("startup");
		if (_edgeLight != null)
		{
			_edgeLight.SyncWindow();
		}
		if (FirstRunManager.NeedsFirstRunArrival)
		{
			base.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(StartFirstRunArrival));
		}
	}

	internal void OnEnvironmentChanged(string reason)
	{
		NotchLog.Info("Environment changed: " + reason);
		_pendingEnvironmentReason = ((_pendingEnvironmentReason == null) ? reason : (_pendingEnvironmentReason + ", " + reason));
		if (_pendingEnvironmentRepair != null && _pendingEnvironmentRepair.Status == DispatcherOperationStatus.Pending)
		{
			return;
		}
		_pendingEnvironmentRepair = base.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
		{
			string pendingEnvironmentReason = _pendingEnvironmentReason;
			_pendingEnvironmentReason = null;
			Stopwatch stopwatch = Stopwatch.StartNew();
			NotchLog.Info("Visual recovery started: " + pendingEnvironmentReason);
			HotkeyService.Instance.Recover();
			try
			{
				UpdatePosition(initial: false);
				AssertTopmost();
				SetAnimationCache(on: false);
				UpdateTabSelectionStyles(anim: false);
				if (ToolRouter.Instance.ActiveTool is MessageBannerTool messageBannerTool)
				{
					messageBannerTool.RefreshPresentation();
				}
				if (_currentToolView != null)
				{
					_currentToolView.InvalidateMeasure();
					_currentToolView.InvalidateVisual();
				}
				if (_expandedContent != null)
				{
					_expandedContent.InvalidateVisual();
				}
				RecordInteractionState("environment repaired");
				NotchVisibilityEngine.Instance.ForceReevaluate("environment: " + pendingEnvironmentReason);
				EyesEnvironmentChanged();
				PresentRestingNotch("environment: " + pendingEnvironmentReason, animate: false, rebuildEffects: true);
				EnvironmentRepairs++;
				NotchLog.Info("Visual recovery completed: " + pendingEnvironmentReason + "; elapsedMs=" + stopwatch.ElapsedMilliseconds + "; count=" + EnvironmentRepairs);
			}
			catch (Exception ex)
			{
				NotchLog.Error("Environment repair failed (" + pendingEnvironmentReason + ")", ex);
			}
		});
	}

	private void AssertTopmost()
	{
		if (!(_hwnd == IntPtr.Zero) && base.Visibility == Visibility.Visible)
		{
			if (!base.Topmost)
			{
				base.Topmost = true;
			}
			SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, 19u);
		}
	}

	private bool IsTopmostNow()
	{
		if (_hwnd != IntPtr.Zero)
		{
			return (GetWindowLong(_hwnd, -20) & 8) != 0;
		}
		return false;
	}

	private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
	{
		if ((msg == 512 || msg == 514) && IsRestingSurface && GardenOwnsStage && _petOutside != null && _petOutside.IsVisible && GetCursorPos(out var lpPoint))
		{
			System.Windows.Point point = _petOutside.PointFromScreen(new System.Windows.Point(lpPoint.X, lpPoint.Y));
			if (msg == 512)
			{
				_petOutside.ObserveStagePointer(point);
				if (_machine != null)
				{
					_machine.PointerMoved(!GardenBlocksHover && IsPointerInside() && !HoldsActionCard(), "pet-native-hover");
				}
			}
			else if (_petOutside.HandleStageClick(point))
			{
				handled = true;
				return IntPtr.Zero;
			}
		}
		switch (msg)
		{
		case 132:
		{
			int num3 = (short)(long)lParam;
			int num4 = (short)((long)lParam >> 16);
			if (HitTestScreenPoint(num3, num4, includeOwnPopups: false))
			{
				if (IsRestingSurface && GardenOwnsStage && _petOutside != null && _petOutside.IsVisible)
				{
					handled = true;
					return new IntPtr(1);
				}
				handled = false;
				return IntPtr.Zero;
			}
			if (_state == CapsuleDisplayState.Expanded || _state == CapsuleDisplayState.Expanding)
			{
				try
				{
					System.Windows.Point point2 = PointFromScreen(new System.Windows.Point(num3, num4));
					if (point2.X >= 0.0 && point2.X <= base.ActualWidth && point2.Y >= 0.0 && point2.Y <= base.ActualHeight)
					{
						HitTestResult hitTestResult = VisualTreeHelper.HitTest(this, point2);
						if (hitTestResult != null && hitTestResult.VisualHit != null && hitTestResult.VisualHit != _rootGrid && !IsDiagnosticsVisual(hitTestResult.VisualHit))
						{
							handled = false;
							return IntPtr.Zero;
						}
					}
				}
				catch
				{
				}
			}
			handled = true;
			return (IntPtr)(-1);
		}
		case 33:
			if (_state == CapsuleDisplayState.Expanded || _state == CapsuleDisplayState.Expanding)
			{
				handled = true;
				return (IntPtr)1;
			}
			handled = true;
			return (IntPtr)3;
		case 536:
		{
			int num = wParam.ToInt32();
			NotchLog.Info("Power event: code=0x" + num.ToString("X") + "; visualRecovery=" + ((num == 18 || num == 7) ? "requested" : "not requested"));
			if (num == 32787 && lParam != IntPtr.Zero)
			{
				Guid guid = (Guid)Marshal.PtrToStructure(lParam, typeof(Guid));
				if (guid == GUID_CONSOLE_DISPLAY_STATE && Marshal.ReadInt32(lParam, 16) >= 4)
				{
					int num2 = Marshal.ReadInt32(lParam, 20);
					bool flag = num2 != 0;
					NotchLog.Info("Console display state=" + num2 + "; visualRecovery=" + ((flag && _consoleDisplayWasOff) ? "requested" : "not requested"));
					ScreenTimeService.Instance.SetDisplayOn(flag);
					if (!flag)
					{
						_consoleDisplayWasOff = true;
					}
					else if (_consoleDisplayWasOff)
					{
						_consoleDisplayWasOff = false;
						OnEnvironmentChanged("console display resumed (Modern Standby)");
					}
				}
			}
			switch (num)
			{
			case 10:
			case 32787:
				PowerSaver.Refresh();
				SystemMonitorService.RefreshAsync();
				break;
			case 4:
				ScreenTimeService.Instance.SetDisplayOn(on: false);
				if (_machine != null && !_machine.IsResting && _machine.Phase != 0)
				{
					_machine.Reset("suspend", _machine.Presence, countAsRecovery: false);
				}
				break;
			case 7:
			case 18:
				if (_displayPowerNotification == IntPtr.Zero)
				{
					ScreenTimeService.Instance.SetDisplayOn(on: true);
				}
				OnEnvironmentChanged("resumed from sleep");
				break;
			}
			return IntPtr.Zero;
		}
		case 71:
			if (_edgeLight != null)
			{
				_edgeLight.SyncWindow();
			}
			if (_beatOverlay != null)
			{
				_beatOverlay.SyncWindow();
			}
			return IntPtr.Zero;
		case 736:
			OnEnvironmentChanged("DPI changed");
			return IntPtr.Zero;
		case 794:
		case 798:
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				RefreshCompositionFrame(_hwnd, HwndSource.FromHwnd(_hwnd));
				UpdateHitRegion(verifyNativeRegion: true);
				if (_edgeLight != null)
				{
					_edgeLight.SyncWindow();
				}
				if (_beatOverlay != null)
				{
					_beatOverlay.SyncWindow();
				}
			});
			return IntPtr.Zero;
		case 126:
			OnEnvironmentChanged("display mode changed");
			return IntPtr.Zero;
		case 26:
			if (wParam.ToInt32() == 47)
			{
				OnEnvironmentChanged("work area / taskbar changed");
				return IntPtr.Zero;
			}
			break;
		}
		if (_taskbarCreatedMsg != 0 && msg == (int)_taskbarCreatedMsg)
		{
			OnEnvironmentChanged("Explorer restarted");
			handled = true;
		}
		return IntPtr.Zero;
	}

	private void HookSystemEvents()
	{
		SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
		SystemEvents.SessionSwitch += OnSessionSwitch;
		SystemTelemetryData liveTelemetry = SystemMonitorService.GetLiveTelemetry();
		if (liveTelemetry != null)
		{
			_lastBatteryCharging = liveTelemetry.IsCharging;
			_lastBatteryFull = liveTelemetry.IsFullyCharged;
		}
		_powerChangedHandler = delegate(object s, SystemTelemetryData data)
		{
			try
			{
				base.Dispatcher.BeginInvoke((Action)delegate
				{
					RequestBadgeRefresh();
					if (data.HasBattery && !data.IsCharging && data.BatteryPercent >= 0 && data.BatteryPercent <= 5)
					{
						PillActivityArbiter.Instance.SetBatteryLowAlert(data.BatteryPercent);
					}
					else
					{
						PillActivityArbiter.Instance.ClearBatteryLowAlert();
					}
					if (data.IsCharging && !_lastBatteryCharging)
					{
						_lastBatteryCharging = true;
						_lastBatteryFull = false;
						DynamicActivityService.Instance.PublishActivity(new NotchActivity
						{
							Id = "bat_charging",
							Type = ActivityType.BatteryCharging,
							Priority = ActivityPriority.P3_Transient,
							Mode = ActivityMode.Temporary,
							Duration = TimeSpan.FromSeconds(3.0),
							Title = "Charging",
							Subtitle = ((data.BatteryPercent >= 0) ? (data.BatteryPercent + "%") : "—"),
							ValueText = ((data.BatteryPercent >= 0) ? (data.BatteryPercent + "%") : "—"),
							AccentColor = NotchInk.Success,
							NavigateToToolId = "system"
						});
					}
					else if (!data.IsCharging && _lastBatteryCharging)
					{
						_lastBatteryCharging = false;
					}
					if (data.IsFullyCharged && !_lastBatteryFull && data.IsACConnected)
					{
						_lastBatteryFull = true;
						DynamicActivityService.Instance.PublishActivity(new NotchActivity
						{
							Id = "bat_full",
							Type = ActivityType.BatteryFull,
							Priority = ActivityPriority.P3_Transient,
							Mode = ActivityMode.Temporary,
							Duration = TimeSpan.FromSeconds(3.0),
							Title = "Fully charged",
							Subtitle = "100%",
							ValueText = "100%",
							AccentColor = NotchInk.Success,
							NavigateToToolId = "system"
						});
					}
				});
			}
			catch
			{
			}
		};
		SystemMonitorService.PowerChanged += _powerChangedHandler;
		_networkChangedHandler = delegate(object s, SystemTelemetryData data)
		{
			try
			{
				base.Dispatcher.BeginInvoke((Action)delegate
				{
					RequestBadgeRefresh();
					if (!data.IsConnected && _lastNetworkConnected)
					{
						_lastNetworkConnected = false;
						DynamicActivityService.Instance.PublishActivity(new NotchActivity
						{
							Id = "net_status",
							Type = ActivityType.NetworkDisconnected,
							Priority = ActivityPriority.P3_Transient,
							Mode = ActivityMode.Temporary,
							Duration = TimeSpan.FromSeconds(3.0),
							Title = "Offline",
							Subtitle = "No network connection",
							ValueText = "Offline",
							IconFactory = (double sz, System.Windows.Media.Brush br) => VectorIcons.CreateWiFiIcon(sz, br),
							AccentColor = NotchInk.Danger,
							NavigateToToolId = "system"
						});
					}
					else if (data.IsConnected && !_lastNetworkConnected)
					{
						_lastNetworkConnected = true;
						DynamicActivityService.Instance.PublishActivity(new NotchActivity
						{
							Id = "net_status",
							Type = ActivityType.NetworkConnected,
							Priority = ActivityPriority.P3_Transient,
							Mode = ActivityMode.Temporary,
							Duration = TimeSpan.FromSeconds(3.0),
							Title = (data.IsWiFi ? "Wi-Fi connected" : ((data.ConnectionType == "Ethernet") ? "Ethernet connected" : "Connected")),
							Subtitle = ((!string.IsNullOrEmpty(data.NetworkName)) ? data.NetworkName : "Online"),
							ValueText = "Connected",
							IconFactory = (double sz, System.Windows.Media.Brush br) => VectorIcons.CreateWiFiIcon(sz, br),
							AccentColor = NotchInk.Success,
							NavigateToToolId = "system"
						});
					}
				});
			}
			catch
			{
			}
		};
		SystemMonitorService.NetworkChanged += _networkChangedHandler;
		_audioChangedHandler = delegate(object s, AudioVolumeChangedEventArgs e)
		{
			try
			{
				base.Dispatcher.BeginInvoke((Action)delegate
				{
					RequestBadgeRefresh();
					DynamicActivityService.Instance.PublishActivity(new NotchActivity
					{
						Id = "volume_feedback",
						Type = ActivityType.Volume,
						Priority = ActivityPriority.P3_Transient,
						Mode = ActivityMode.Temporary,
						Duration = TimeSpan.FromSeconds(3.0),
						Title = (e.IsMuted ? "Muted" : (e.VolumePercent + "%")),
						Subtitle = "Volume",
						ValueText = e.VolumePercent + "%",
						IconFactory = (double sz, System.Windows.Media.Brush br) => VectorIcons.CreateVolumeIcon(sz, br, e.IsMuted, e.VolumePercent),
						AccentColor = (e.IsMuted ? NotchInk.Danger : null),
						NavigateToToolId = "system"
					});
				});
			}
			catch
			{
			}
		};
		CoreAudioService.Instance.VolumeChanged += _audioChangedHandler;
		_timerLiveUpdateHandler = delegate(object s, TimerUpdateEventArgs args)
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				if (_activeOwner.VisualType == CollapsedVisualType.Timer)
				{
					if (_timerLiveText == null && _activeOwner.VisualInstance is System.Windows.Controls.Panel panel)
					{
						foreach (UIElement child in panel.Children)
						{
							if (child is TextBlock timerLiveText)
							{
								_timerLiveText = timerLiveText;
								break;
							}
						}
					}
					if (_timerLiveText != null)
					{
						_timerLiveText.Text = args.DisplayText;
						if (_timerPlayPauseIconHost != null && _timerIsPausedVisual != args.IsPaused)
						{
							_timerIsPausedVisual = args.IsPaused;
							_timerPlayPauseIconHost.Content = (args.IsPaused ? VectorIcons.CreatePlayMediaIcon(9.0, System.Windows.Media.Brushes.White) : VectorIcons.CreatePauseMediaIcon(9.0, System.Windows.Media.Brushes.White));
							if (_timerPlayPauseButton != null)
							{
								_timerPlayPauseButton.ToolTip = (args.IsPaused ? "Resume timer" : "Pause timer");
							}
						}
						NotchStabilityCounters.VisualInstancesReused++;
					}
				}
			});
		};
		_notchSettingsHandler = delegate(object s, NotchSettingsChangedEventArgs e)
		{
			ApplySettings(e.Area);
		};
		NotchSettings.Instance.Changed += _notchSettingsHandler;
		_notchVisibilityHandler = delegate(object s, VisibilityChangeEventArgs args)
		{
			if (_machine != null)
			{
				_machine.SetPresence(args.Presence, args.Reason);
			}
		};
		NotchVisibilityEngine.Instance.VisibilityChanged += _notchVisibilityHandler;
		_liveActivityChangedHandler = delegate(object s, CapsuleLiveActivity act)
		{
			try
			{
				base.Dispatcher.BeginInvoke((Action)delegate
				{
					UpdateCollapsedLiveActivity(act);
				});
			}
			catch
			{
			}
		};
		CapsuleStateManager.LiveActivityChanged += _liveActivityChangedHandler;
		_clockSettingsChangedHandler = delegate
		{
			try
			{
				base.Dispatcher.BeginInvoke(new Action(ApplyNotchConfig));
			}
			catch
			{
			}
		};
		ClockSettings.Instance.SettingsChanged += _clockSettingsChangedHandler;
		_clockSecondTickHandler = delegate(object s, DateTime now)
		{
			if (_clockUiSubscribed)
			{
				if (!base.Dispatcher.CheckAccess())
				{
					try
					{
						base.Dispatcher.BeginInvoke((Action)delegate
						{
							if (_clockUiSubscribed)
							{
								UpdateCollapsedClock(DateTime.Now);
							}
						});
						return;
					}
					catch
					{
						return;
					}
				}
				UpdateCollapsedClock(now);
			}
		};
		_arbiterActivityChangedHandler = delegate(object s, PillActivityChangedEventArgs e)
		{
			try
			{
				base.Dispatcher.BeginInvoke((Action)delegate
				{
					RenderResolvedSnapshot(e.OldActivity, e.NewActivity, e.IsUpdateOnly, e.TransitionType);
				});
			}
			catch
			{
			}
		};
		_messageEmojiRendered = delegate
		{
			if (Interlocked.Exchange(ref _messageEmojiQueued, 1) == 1)
			{
				return;
			}
			try
			{
				base.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
				{
					if (_isShuttingDown || !_emojiUiSubscribed || PillActivityArbiter.Instance.CurrentSnapshot.Type != ActivityType.CompanionMessage)
					{
						Interlocked.Exchange(ref _messageEmojiQueued, 0);
					}
					else
					{
						if (_messageEmojiRefresh == null)
						{
							_messageEmojiRefresh = new DispatcherTimer(DispatcherPriority.Background, base.Dispatcher)
							{
								Interval = TimeSpan.FromMilliseconds(150.0)
							};
							_messageEmojiRefresh.Tick += delegate
							{
								_messageEmojiRefresh.Stop();
								Interlocked.Exchange(ref _messageEmojiQueued, 0);
								if (!_isShuttingDown && _emojiUiSubscribed && PillActivityArbiter.Instance.CurrentSnapshot.Type == ActivityType.CompanionMessage)
								{
									RenderCurrentResolvedPillActivity(immediate: true);
								}
							};
						}
						_messageEmojiRefresh.Start();
					}
				});
			}
			catch
			{
				Interlocked.Exchange(ref _messageEmojiQueued, 0);
			}
		};
		UpdateLiveUiSubscriptions(_machine == null || _machine.Phase != NotchPhase.Hidden);
		_sessionEndingHandler = delegate
		{
			try
			{
				BuddyMessageService.Instance.ShowShutdownGreeting();
			}
			catch
			{
			}
		};
		SystemEvents.SessionEnding += _sessionEndingHandler;
		BluetoothService.Instance.Initialize();
		_mediaStateHandler = delegate
		{
			RefreshMediaArtIfStale();
			RequestBadgeRefresh();
		};
		MediaService.Instance.MediaStateChanged += _mediaStateHandler;
		base.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
		{
			if (!_isShuttingDown)
			{
				InitializePet();
				InitializeEyes();
				BuddyMessageService.Instance.TryShowStartupGreeting();
			}
		});
	}

	private void UpdateLiveUiSubscriptions(bool visible)
	{
		if (!visible)
		{
			StopClockNotchAnalogTimer();
			if (_badgeThrottle != null)
			{
				_badgeThrottle.Stop();
			}
			Interlocked.Exchange(ref _badgeRefreshQueued, 0);
		}
		if (_messageEmojiRendered != null && _emojiUiSubscribed != visible)
		{
			if (visible)
			{
				ColorEmoji.Rendered += _messageEmojiRendered;
			}
			else
			{
				ColorEmoji.Rendered -= _messageEmojiRendered;
				if (_messageEmojiRefresh != null)
				{
					_messageEmojiRefresh.Stop();
				}
				Interlocked.Exchange(ref _messageEmojiQueued, 0);
			}
			_emojiUiSubscribed = visible;
		}
		if (_clockSecondTickHandler != null && _clockUiSubscribed != visible)
		{
			if (visible)
			{
				ClockService.Instance.SecondTick += _clockSecondTickHandler;
			}
			else
			{
				ClockService.Instance.SecondTick -= _clockSecondTickHandler;
			}
			_clockUiSubscribed = visible;
		}
		if (_arbiterActivityChangedHandler != null && _arbiterUiSubscribed != visible)
		{
			if (visible)
			{
				PillActivityArbiter.Instance.ActivityChanged += _arbiterActivityChangedHandler;
			}
			else
			{
				PillActivityArbiter.Instance.ActivityChanged -= _arbiterActivityChangedHandler;
			}
			_arbiterUiSubscribed = visible;
		}
		if (_timerLiveUpdateHandler != null && _timerUiSubscribed != visible)
		{
			if (visible)
			{
				DynamicActivityService.Instance.TimerUpdated += _timerLiveUpdateHandler;
			}
			else
			{
				DynamicActivityService.Instance.TimerUpdated -= _timerLiveUpdateHandler;
			}
			_timerUiSubscribed = visible;
		}
	}

	private void UnhookSystemEvents()
	{
		try
		{
			UpdateLiveUiSubscriptions(visible: false);
			SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
			SystemEvents.SessionSwitch -= OnSessionSwitch;
			if (_sessionEndingHandler != null)
			{
				SystemEvents.SessionEnding -= _sessionEndingHandler;
			}
			if (_powerChangedHandler != null)
			{
				SystemMonitorService.PowerChanged -= _powerChangedHandler;
			}
			if (_networkChangedHandler != null)
			{
				SystemMonitorService.NetworkChanged -= _networkChangedHandler;
			}
			if (_audioChangedHandler != null)
			{
				CoreAudioService.Instance.VolumeChanged -= _audioChangedHandler;
			}
			if (_timerLiveUpdateHandler != null)
			{
				DynamicActivityService.Instance.TimerUpdated -= _timerLiveUpdateHandler;
			}
			if (_notchSettingsHandler != null)
			{
				NotchSettings.Instance.Changed -= _notchSettingsHandler;
			}
			if (_toolNavigatedHandler != null)
			{
				ToolRouter.Instance.Navigated -= _toolNavigatedHandler;
			}
			CapsuleMotion.AnimationsPolicyChanged -= OnTabMotionPolicyChanged;
			ToolRegistry.Instance.CapsuleSettingsChanged -= OnTabMotionPolicyChanged;
			if (_tabSystemMotionHandler != null)
			{
				SystemParameters.StaticPropertyChanged -= _tabSystemMotionHandler;
			}
			if (_toolsUpdatedHandler != null)
			{
				ToolRegistry.Instance.ToolsUpdated -= _toolsUpdatedHandler;
			}
			if (_primaryCollapsedToolChangedHandler != null)
			{
				ToolRegistry.Instance.PrimaryCollapsedToolChanged -= _primaryCollapsedToolChangedHandler;
			}
			if (_liveActivityChangedHandler != null)
			{
				CapsuleStateManager.LiveActivityChanged -= _liveActivityChangedHandler;
			}
			if (_clockSettingsChangedHandler != null)
			{
				ClockSettings.Instance.SettingsChanged -= _clockSettingsChangedHandler;
			}
			if (_clockSecondTickHandler != null)
			{
				ClockService.Instance.SecondTick -= _clockSecondTickHandler;
			}
			if (_mediaStateHandler != null)
			{
				MediaService.Instance.MediaStateChanged -= _mediaStateHandler;
			}
			if (_revealHandler != null)
			{
				PillActivityArbiter.Instance.RevealChanged -= _revealHandler;
			}
			NotchVisibilityEngine.Instance.HoverProbe = null;
			if (_hotkeyHandler != null)
			{
				HotkeyService.Instance.Pressed -= _hotkeyHandler;
			}
			if (_licenseHandler != null)
			{
				LicenseStub.Changed -= _licenseHandler;
			}
			if (_entitlementTimer != null)
			{
				_entitlementTimer.Stop();
			}
			if (_headerSubtitleHandler != null)
			{
				ToolRouter.Instance.HeaderSubtitleUpdated -= _headerSubtitleHandler;
			}
			if (_arbiterActivityChangedHandler != null)
			{
				PillActivityArbiter.Instance.ActivityChanged -= _arbiterActivityChangedHandler;
			}
			if (_notchVisibilityHandler != null)
			{
				NotchVisibilityEngine.Instance.VisibilityChanged -= _notchVisibilityHandler;
			}
		}
		catch
		{
		}
	}

	private void OnDisplaySettingsChanged(object sender, EventArgs e)
	{
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			OnEnvironmentChanged("display settings changed");
		});
	}

	private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
	{
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			NotchVisibilityEngine instance = NotchVisibilityEngine.Instance;
			switch (e.Reason)
			{
			case SessionSwitchReason.SessionLock:
				if (_machine != null)
				{
					_machine.Reset("session locked", NotchPresence.None, countAsRecovery: false);
				}
				instance.SessionLocked = true;
				break;
			case SessionSwitchReason.SessionUnlock:
				instance.SessionLocked = false;
				OnEnvironmentChanged("session unlocked");
				break;
			case SessionSwitchReason.ConsoleConnect:
			case SessionSwitchReason.ConsoleDisconnect:
			case SessionSwitchReason.RemoteConnect:
			case SessionSwitchReason.RemoteDisconnect:
				OnEnvironmentChanged("session " + e.Reason);
				break;
			case SessionSwitchReason.SessionLogon:
			case SessionSwitchReason.SessionLogoff:
				break;
			}
		});
	}

	private void ApplySettings(NotchSettingsArea area)
	{
		NotchSettings instance = NotchSettings.Instance;
		if (_machine != null)
		{
			_machine.Configure(instance.HoverDelayMs, instance.OpensOnClick);
			_machine.SetEnabled(instance.NotchEnabled && EntitlementGate.IsEntitled, "Settings → Show notch");
		}
		switch (area)
		{
		case NotchSettingsArea.Placement:
			UpdatePosition(initial: false);
			RefreshLiveBadges();
			break;
		case NotchSettingsArea.Behavior:
			UpdateCollapsedVisual();
			break;
		}
		PresentRestingNotch("settings: " + area, area == NotchSettingsArea.Appearance, area == NotchSettingsArea.Appearance);
		UpdatePetHeader();
		PetView.RefreshAll();
	}

	public void RequestQuit(string source)
	{
		TransactionLogger.Info("Quit requested (" + source + "; " + (EntitlementGate.IsEntitled ? "entitled" : "not entitled") + ").");
		if (System.Windows.Application.Current != null)
		{
			ShutdownApplication();
		}
	}

	private void ResetNotchWindows()
	{
		Process currentProcess = Process.GetCurrentProcess();
		ProcessStartInfo processStartInfo = new ProcessStartInfo(currentProcess.MainModule.FileName, "reset-notch " + currentProcess.Id);
		processStartInfo.UseShellExecute = false;
		Process.Start(processStartInfo);
		RequestQuit("Reset notch");
	}

	public async void ShutdownApplication(Action beforeCommit = null)
	{
		bool alreadyRunning = _shutdownSession.Running || _shutdownSession.Committed;
		if (!alreadyRunning)
		{
			Thread thread = new Thread((ThreadStart)delegate
			{
				Thread.Sleep(2000);
				if (!AppPaths.IsSandboxed || Program.IsDesktopApplication)
				{
					QuitWatchdog.ExitNow(0, "quit budget");
				}
				else if (!base.Dispatcher.HasShutdownStarted)
				{
					try
					{
						base.Dispatcher.BeginInvoke((Action)delegate
						{
							System.Windows.Application current = System.Windows.Application.Current;
							if (current != null && !current.Dispatcher.HasShutdownStarted)
							{
								_isShuttingDown = true;
								current.Shutdown();
							}
						});
					}
					catch (InvalidOperationException)
					{
					}
				}
			});
			thread.IsBackground = true;
			thread.Name = "Ayn bounded quit";
			Thread thread2 = thread;
			thread2.Start();
			if (!PersistenceHelper.SessionEnding)
			{
				CrashDiagnostics.MarkEnding("quit");
				CrashDiagnostics.NoteExitReason("normal-quit-storage-budget");
				CrashGuard.UnregisterForQuit();
			}
			QuitWatchdog.Trace("Quit: shutdown begins (save-and-drain)");
			QuitWatchdog.Start(() => _shutdownSession.Committed);
		}
		if (!(await _shutdownSession.RunAsync(PersistenceHelper.PrepareExit, PauseForShutdown, delegate
		{
			if (!ConvertEngine.Outputs.WaitIdle(2000))
			{
				throw new TimeoutException("A converted file is still being written.");
			}
			if (!CredentialStore.Writes.WaitIdle(2000))
			{
				throw new TimeoutException("A protected credential is still being written.");
			}
			TimerService.Instance.WaitForPersistence();
			ScreenTimeService.Instance.WaitForPersistence();
			NotesService.Instance.VerifyAsync(CancellationToken.None).GetAwaiter().GetResult();
			ClipboardService.Instance.VerifyAsync(CancellationToken.None).GetAwaiter().GetResult();
			FileShelfService.Instance.VerifyAsync(CancellationToken.None).GetAwaiter().GetResult();
			AppPreferences.Instance.VerifyAsync(CancellationToken.None).GetAwaiter().GetResult();
			return PersistenceHelper.FlushForExit();
		}, FinalizeShutdown, delegate(string message)
		{
			TransactionLogger.Warn(message);
		}, 5000, beforeCommit, delegate
		{
			if (PersistenceHelper.TrySealForCommit(out var reason))
			{
				return true;
			}
			TransactionLogger.Warn("Shutdown commit refused: " + reason);
			return false;
		}, PersistenceHelper.UnsealAfterCancelledCommit)) && !alreadyRunning)
		{
			QuitWatchdog.Cancel();
			QuitWatchdog.Trace("Quit: cancelled; Ayn keeps running so pending data is not lost");
			TransactionLogger.Warn("Quit did not complete: Ayn keeps running so pending data is not lost.");
		}
	}

	public bool SaveForSessionEnd(int budgetMs)
	{
		if (_shutdownSession.Committed)
		{
			return true;
		}
		ShutdownApplication();
		Stopwatch watch = Stopwatch.StartNew();
		try
		{
			DispatcherFrame frame = new DispatcherFrame();
			DispatcherTimer poll = new DispatcherTimer(DispatcherPriority.Send, base.Dispatcher)
			{
				Interval = TimeSpan.FromMilliseconds(15.0)
			};
			poll.Tick += delegate
			{
				if (_shutdownSession.Committed || !_shutdownSession.Running || watch.ElapsedMilliseconds >= budgetMs)
				{
					poll.Stop();
					frame.Continue = false;
				}
			};
			poll.Start();
			Dispatcher.PushFrame(frame);
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Session end: could not wait on the dispatcher (" + ex.Message + "); flushing settings directly.");
			try
			{
				Task.Run(() => PersistenceHelper.FlushForExit()).Wait(Math.Max(100, budgetMs - (int)watch.ElapsedMilliseconds));
			}
			catch
			{
			}
		}
		TransactionLogger.Info("Session end: save " + (_shutdownSession.Committed ? "committed" : "not finished") + " after " + watch.ElapsedMilliseconds + " ms.");
		return _shutdownSession.Committed;
	}

	private Action PauseForShutdown()
	{
		ShutdownRestoration restoration = new ShutdownRestoration();
		try
		{
			restoration.Add(EntitlementGate.HoldForShutdown());
			foreach (Window window in System.Windows.Application.Current.Windows)
			{
				Window captured = window;
				bool enabled = window.IsEnabled;
				window.IsEnabled = false;
				restoration.Add(delegate
				{
					captured.IsEnabled = enabled;
				});
			}
			restoration.Add(EntitlementGate.PauseUnlessSuspended("timer", () => TimerService.Instance.PauseForShutdown()));
			restoration.Add(EntitlementGate.PauseUnlessSuspended("screen-time", () => ScreenTimeService.Instance.PauseForShutdown()));
			restoration.Add(EntitlementGate.PauseUnlessSuspended("weather", () => WeatherService.Instance.PauseForShutdown()));
			restoration.Add(EntitlementGate.PauseUnlessSuspended("calendar", () => CalendarService.Instance.PauseForShutdown()));
			restoration.Add(EntitlementGate.PauseUnlessSuspended("ai-coding", AICodingService.PauseIfCreated));
			restoration.Add(StorageJanitor.PauseForShutdown());
			restoration.Add(EntitlementGate.PauseUnlessSuspended("converter", ConvertEngine.PauseOutputs));
			restoration.Add(CredentialStore.PauseForShutdown());
			NotesService instance = NotesService.Instance;
			restoration.Add(instance.PauseForShutdown(), instance.Operations);
			restoration.Add(EntitlementGate.PauseUnlessSuspended("clipboard", () => ClipboardService.Instance.PauseForShutdown()), ClipboardService.Instance.Operations);
			restoration.Add(EntitlementGate.PauseUnlessSuspended("file-shelf", () => FileShelfService.Instance.PauseForShutdown()), FileShelfService.Instance.Operations);
			restoration.Add(AppPreferences.Instance.PauseForShutdown(), AppPreferences.Instance.Operations);
			return delegate
			{
				try
				{
					restoration.Run();
				}
				catch (Exception ex3)
				{
					TransactionLogger.Error("Shutdown resume failed", ex3);
					throw;
				}
			};
		}
		catch (Exception ex2)
		{
			try
			{
				restoration.Run();
			}
			catch (Exception ex)
			{
				TransactionLogger.Error("Shutdown rollback failed", ex);
				throw new AggregateException("Shutdown pause failed and rollback was incomplete.", ex2, ex);
			}
			throw;
		}
	}

	private void FinalizeShutdown()
	{
		NotesService.Instance.SealForDisposal();
		ClipboardService.Instance.SealForDisposal();
		FileShelfService.Instance.SealForDisposal();
		AppPreferences.Instance.SealForDisposal();
		PersistenceHelper.SealWritesAfterCommit();
		if (!PersistenceHelper.SessionEnding)
		{
			CrashDiagnostics.MarkEnding("quit");
			CrashGuard.UnregisterForQuit();
		}
		_isShuttingDown = true;
		ToolRouter.Instance.StopIdleMemoryMaintenance();
		ToolRouter.Instance.StopIdleWarmup();
		SettleViewportTransition();
		TransactionLogger.Info("Persistence verified; irreversible shutdown begins.");
		QuitWatchdog.Trace("Quit: persistence verified and committed; finalizing");
		HotkeyService.Instance.Dispose();
		try
		{
			if (_scheduler != null)
			{
				_scheduler.StopAll();
			}
		}
		catch
		{
		}
		try
		{
			NotchDiagnostics.Close();
		}
		catch
		{
		}
		try
		{
			if (_telemetryTimer != null)
			{
				_telemetryTimer.Stop();
				_telemetryTimer = null;
			}
		}
		catch
		{
		}
		try
		{
			if (_firstRunHoldTimer != null)
			{
				_firstRunHoldTimer.Stop();
				_firstRunHoldTimer = null;
			}
		}
		catch
		{
		}
		try
		{
			ToolRegistry.Instance.ShutdownAll();
		}
		catch (Exception ex)
		{
			TransactionLogger.Error("Error during ToolRegistry.ShutdownAll: " + ex.Message, ex);
		}
		try
		{
			ClipboardService.Instance.DisposeAsync().GetAwaiter().GetResult();
		}
		catch (Exception ex2)
		{
			TransactionLogger.Error("Error during ClipboardService shutdown: " + ex2.Message, ex2);
		}
		try
		{
			NetworkService.Instance.Dispose();
		}
		catch
		{
		}
		try
		{
			MediaService.Instance.Dispose();
		}
		catch
		{
		}
		try
		{
			CoreAudioService.Instance.Dispose();
		}
		catch
		{
		}
		try
		{
			SoundMixerService.Instance.Dispose();
		}
		catch
		{
		}
		try
		{
			ScreenTimeService.Instance.Shutdown();
		}
		catch
		{
		}
		try
		{
			TimerService.Instance.Shutdown();
		}
		catch
		{
		}
		try
		{
			WeatherService.Instance.Shutdown();
		}
		catch
		{
		}
		try
		{
			NotesService.Instance.Flush();
		}
		catch
		{
		}
		try
		{
			FileShelfService.Instance.DisposeAsync().GetAwaiter().GetResult();
		}
		catch (Exception ex3)
		{
			TransactionLogger.Error("Shelf final disposal failed", ex3);
		}
		UnhookSystemEvents();
		if (_displayPowerNotification != IntPtr.Zero)
		{
			UnregisterPowerSettingNotification(_displayPowerNotification);
			_displayPowerNotification = IntPtr.Zero;
		}
		try
		{
			NotchVisibilityEngine.Instance.Dispose();
		}
		catch
		{
		}
		try
		{
			PillActivityArbiter.Instance.Shutdown();
		}
		catch
		{
		}
		try
		{
			if (_badgeThrottle != null)
			{
				_badgeThrottle.Stop();
			}
		}
		catch
		{
		}
		try
		{
			StopClockNotchAnalogTimer();
		}
		catch
		{
		}
		try
		{
			if (_beatActive)
			{
				StopBeat();
			}
			if (_beatOverlay != null)
			{
				_beatOverlay.Dispose();
			}
			_beatOverlay = null;
		}
		catch
		{
		}
		try
		{
			if (_edgeLight != null)
			{
				_edgeLight.Dispose();
			}
		}
		catch
		{
		}
		try
		{
			BeginAnimation(MorphProgressProperty, null);
		}
		catch
		{
		}
		if (_foregroundWinEventHook != IntPtr.Zero)
		{
			try
			{
				UnhookWinEvent(_foregroundWinEventHook);
			}
			catch
			{
			}
			_foregroundWinEventHook = IntPtr.Zero;
		}
		SystemEvents.UserPreferenceChanged -= OnTrayThemeMaybeChanged;
		if (_notifyIcon != null)
		{
			try
			{
				_notifyIcon.Visible = false;
				_notifyIcon.Dispose();
				_notifyIcon = null;
			}
			catch
			{
			}
		}
		if (_textScrollController != null)
		{
			try
			{
				_textScrollController.Dispose();
				_textScrollController = null;
			}
			catch
			{
			}
		}
		TransactionLogger.Info("Application shutdown sequence completed successfully.");
		QuitWatchdog.Trace("Quit: finalize complete");
		LogWriter.Flush(500);
		FinishExitAsync();
	}

	private async void FinishExitAsync()
	{
		if (SimulateHangAfterSaveForQa)
		{
			QuitWatchdog.Trace("Quit: (QA) simulating a stall after the save step");
			await Task.Delay(-1);
		}
		bool reported = false;
		while (!(await Task.Run(delegate
		{
			try
			{
				return PersistenceHelper.RetryFailedWrites();
			}
			catch (Exception ex)
			{
				TransactionLogger.Error("Late exit verification failed", ex);
				return false;
			}
		})))
		{
			TransactionLogger.Warn("Late shutdown persistence failure: process retained for recovery; teardown will not be rolled back.");
			if (!reported)
			{
				reported = true;
				QuitWatchdog.Cancel();
				QuitWatchdog.Trace("Quit: a late save failed; the process is retained until pending writes succeed (watchdog off)");
			}
			await Task.Delay(1000);
		}
		QuitWatchdog.Trace("Quit: calling Application.Shutdown");
		if (System.Windows.Application.Current != null)
		{
			System.Windows.Application.Current.Shutdown();
		}
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		if (!_isShuttingDown)
		{
			e.Cancel = true;
			RequestCollapse("Window close");
		}
		base.OnClosing(e);
	}

	protected override void OnClosed(EventArgs e)
	{
		base.OnClosed(e);
	}

	private void UpdateNotchSilhouette(double width, double height)
	{
		if (_notchSilhouettePath == null)
		{
			return;
		}
		NotchConfig instance = NotchConfig.Instance;
		Geometry geometry;
		Geometry data;
		if (_state == CapsuleDisplayState.Expanded || _state == CapsuleDisplayState.Expanding)
		{
			geometry = NotchGeometryBuilder.BuildExpandedGeometry(width, height);
			data = NotchGeometryBuilder.BuildExpandedSilhouetteGeometry(width, height);
		}
		else
		{
			geometry = NotchGeometryBuilder.BuildGeometry(instance.Style, width, height, instance.CornerRadius);
			data = NotchGeometryBuilder.BuildSilhouetteGeometry(instance.Style, width, height, instance.CornerRadius);
		}
		_notchSilhouettePath.Data = geometry;
		if (_notchBackdropPath != null)
		{
			_notchBackdropPath.Data = geometry;
		}
		_notchSilhouettePath.Stroke = null;
		if (_notchOutlinePath != null)
		{
			_notchOutlinePath.Data = data;
			bool flag = _state == CapsuleDisplayState.Expanded || _state == CapsuleDisplayState.Expanding;
			_notchOutlinePath.Stroke = ((instance.BorderOutlineEnabled && !flag) ? instance.GetBorderBrush() : null);
			_notchOutlinePath.StrokeThickness = instance.BorderThickness;
		}
		if (_contentContainer != null)
		{
			_contentContainer.Clip = geometry;
		}
		if (_edgeLight != null)
		{
			if (IsRestingSurface)
			{
				_edgeLight.SetOutline(instance.Style, width, height, instance.CornerRadius);
			}
			_edgeLight.SetResting(IsRestingSurface && MorphProgress <= 0.001);
		}
	}

	private DropShadowEffect CreateNotchShadowEffect(NotchConfig cfg)
	{
		if (RenderProfile.LightMode)
		{
			return null;
		}
		double shadowStrengthValue = cfg.GetShadowStrengthValue();
		if (shadowStrengthValue <= 0.02)
		{
			return null;
		}
		double blurRadius = 12.0 + shadowStrengthValue * 18.0;
		double shadowDepth = 1.5 + shadowStrengthValue * 3.0;
		double opacity = Math.Min(0.85, shadowStrengthValue * 0.75);
		DropShadowEffect dropShadowEffect = new DropShadowEffect();
		dropShadowEffect.Color = Colors.Black;
		dropShadowEffect.BlurRadius = blurRadius;
		dropShadowEffect.ShadowDepth = shadowDepth;
		dropShadowEffect.Direction = 270.0;
		dropShadowEffect.Opacity = opacity;
		return dropShadowEffect;
	}

	private static void OnAnyElementSized(object sender, SizeChangedEventArgs e)
	{
		FrameworkElement frameworkElement = sender as FrameworkElement;
		CapsuleWindow instance = Instance;
		if (frameworkElement != null && instance != null && Window.GetWindow(frameworkElement) == instance && (frameworkElement is TextBlock || frameworkElement is System.Windows.Controls.TextBox || frameworkElement is LetterReveal))
		{
			LiveMessageVisuals.Crisp(frameworkElement);
		}
	}

	public void ApplyNotchConfig()
	{
		PresentRestingNotch("settings", animate: true, rebuildEffects: true);
	}

	internal void PresentRestingNotch(string reason, bool animate, bool rebuildEffects, bool rebuildContent = true)
	{
		if (_capsuleCard == null || ((_state == CapsuleDisplayState.Expanded || _state == CapsuleDisplayState.Expanding) && !reason.StartsWith("settings")))
		{
			return;
		}
		_presentCount++;
		if (rebuildContent)
		{
			ApplyNotchConfigCore(animate, rebuildEffects);
		}
		if (IsRestingSurface || _state == CapsuleDisplayState.Suppressed)
		{
			if (!_restingResizeActive && MorphProgress <= 0.001)
			{
				UpdateMorphState(0.0);
			}
			UpdateNotchSilhouette(_capsuleCard.Width, _capsuleCard.Height);
			if (_edgeLight != null)
			{
				_edgeLight.Refresh();
			}
		}
		InvalidateBeatLayout();
		UpdateBeat();
	}

	private void ApplyNotchConfigCore(bool animate, bool rebuildEffects)
	{
		NotchConfig instance = NotchConfig.Instance;
		_restingPlan = null;
		_restingFitWidth = 0.0;
		base.Opacity = 1.0;
		bool flag = instance.ClosedNotchOpacity >= 0.999;
		if (flag != _clearTypeOpaque)
		{
			_clearTypeOpaque = flag;
			LiveMessageVisuals.Crisp(this);
		}
		string text = instance.BlurEffect ?? "None";
		double num = 0.0;
		num = text switch
		{
			"Low" => 8.0, 
			"Medium" => 16.0, 
			"High" => 28.0, 
			_ => 0.0, 
		};
		if (RenderProfile.LightMode)
		{
			num = 0.0;
		}
		bool flag2 = _state == CapsuleDisplayState.Expanded;
		_backdropBlurWanted = num > 0.0;
		if (_backdropBlurEffect != null && _notchBackdropPath != null)
		{
			if (num > 0.0)
			{
				_backdropBlurEffect.Radius = num;
				_notchBackdropPath.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(140, 20, 20, 24));
				_notchBackdropPath.Visibility = ((!flag2) ? Visibility.Collapsed : Visibility.Visible);
			}
			else
			{
				_backdropBlurEffect.Radius = 0.0;
				_notchBackdropPath.Visibility = Visibility.Collapsed;
			}
		}
		if (_notchSilhouettePath != null)
		{
			_notchSilhouettePath.Fill = PanelFill(instance);
			if (_earLeft != null)
			{
				_earLeft.Fill = _notchSilhouettePath.Fill;
				_earRight.Fill = _notchSilhouettePath.Fill;
			}
			_notchSilhouettePath.Stroke = null;
			if (_sharedNotchShadow == null)
			{
				_sharedNotchShadow = CreateNotchShadowEffect(instance);
			}
			else
			{
				double shadowStrengthValue = instance.GetShadowStrengthValue();
				_sharedNotchShadow.BlurRadius = 12.0 + shadowStrengthValue * 18.0;
				_sharedNotchShadow.ShadowDepth = 1.5 + shadowStrengthValue * 3.0;
			}
			bool flag3 = _state == CapsuleDisplayState.Expanded;
			if (_sharedNotchShadow != null)
			{
				_sharedNotchShadow.Opacity = (flag3 ? Math.Min(0.85, instance.GetShadowStrengthValue() * 0.75) : 0.0);
			}
			_notchSilhouettePath.Effect = ((flag3 && _sharedNotchShadow != null) ? _sharedNotchShadow : null);
		}
		_outlineBrush = (instance.BorderOutlineEnabled ? instance.GetBorderBrush() : null);
		if (_outlineBrush != null && _outlineBrush.CanFreeze)
		{
			_outlineBrush.Freeze();
		}
		_outlineThickness = instance.BorderThickness;
		_shadowOpacity = Math.Min(0.85, instance.GetShadowStrengthValue() * 0.75);
		if (_notchOutlinePath != null)
		{
			_notchOutlinePath.Stroke = ((_state != CapsuleDisplayState.Expanded) ? _outlineBrush : null);
			_notchOutlinePath.StrokeThickness = _outlineThickness;
		}
		UpdateMinimalLineVisual();
		if (_collapsedContent != null)
		{
			_collapsedContent.Margin = new Thickness(0.0, (instance.StyleFamily == "Island") ? 5.0 : 0.0, 0.0, 0.0);
		}
		if (_collapsedPrimaryValueText != null)
		{
			_collapsedPrimaryValueText.Visibility = ((!instance.ShowTime || instance.NotchAnalogClock) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (_collapsedMiniAnalogCanvas != null)
		{
			_collapsedMiniAnalogCanvas.Visibility = ((!instance.ShowTime || !instance.NotchAnalogClock) ? Visibility.Collapsed : Visibility.Visible);
		}
		bool flag4 = instance.ShowBattery && SystemMonitorService.GetLiveTelemetry().HasBattery;
		if (_collapsedBatteryIconHolder != null)
		{
			_collapsedBatteryIconHolder.Visibility = ((!flag4) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (_collapsedBatteryText != null)
		{
			_collapsedBatteryText.Visibility = ((!flag4) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (_collapsedBatteryBoltHolder != null)
		{
			_collapsedBatteryBoltHolder.Visibility = ((!flag4 || !_restingBatteryCharging) ? Visibility.Collapsed : Visibility.Visible);
		}
		ApplyRestingLayout();
		TimerService.Instance.RefreshNotchPresentation();
		if (_edgeLight != null && rebuildEffects)
		{
			_edgeLight.InvalidateSurface();
		}
		RefreshRestingPresentation(rebuild: true, animate);
		if (_edgeLight != null)
		{
			_edgeLight.ApplyConfig(instance);
		}
		if (_state == CapsuleDisplayState.Expanded)
		{
			UpdateMorphState(1.0);
		}
		UpdateTelemetryTimer((_machine == null) ? NotchPhase.Resting : _machine.Phase);
	}

	private void BuildUI()
	{
		CapsuleTheme current = CapsuleTheme.Current;
		NotchConfig instance = NotchConfig.Instance;
		double collapsedWidth = instance.GetCollapsedWidth();
		double collapsedHeight = instance.GetCollapsedHeight();
		_rootGrid = new Grid
		{
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(24.0, 0.0, 24.0, 36.0),
			Width = 752.0,
			Height = 352.0,
			Background = null
		};
		_capsuleCard = new Grid
		{
			Width = collapsedWidth,
			Height = collapsedHeight,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			Background = System.Windows.Media.Brushes.Transparent
		};
		_capsuleTranslateY = new TranslateTransform(0.0, 0.0);
		_capsuleCard.RenderTransform = _capsuleTranslateY;
		RenderOptions.SetClearTypeHint(_capsuleCard, ClearTypeHint.Enabled);
		_capsuleCard.SizeChanged += delegate(object s, SizeChangedEventArgs e)
		{
			if (IsRestingSurface && e.NewSize.Width > 0.0 && e.NewSize.Height > 0.0)
			{
				UpdateNotchSilhouette(double.IsNaN(_capsuleCard.Width) ? e.NewSize.Width : _capsuleCard.Width, double.IsNaN(_capsuleCard.Height) ? e.NewSize.Height : _capsuleCard.Height);
			}
		};
		_notchBackgroundLayer = new Grid
		{
			IsHitTestVisible = false
		};
		_capsuleCard.Children.Add(_notchBackgroundLayer);
		_backdropBlurEffect = new BlurEffect
		{
			Radius = 0.0,
			RenderingBias = RenderingBias.Performance
		};
		_notchBackdropPath = new System.Windows.Shapes.Path
		{
			HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
			Fill = null,
			Stroke = null,
			StrokeThickness = 0.0,
			Effect = _backdropBlurEffect,
			IsHitTestVisible = false,
			Visibility = Visibility.Collapsed
		};
		_notchBackgroundLayer.Children.Add(_notchBackdropPath);
		_sharedNotchShadow = CreateNotchShadowEffect(instance);
		if (_sharedNotchShadow != null)
		{
			_sharedNotchShadow.Opacity = 0.0;
		}
		_notchSilhouettePath = new System.Windows.Shapes.Path
		{
			HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
			Fill = PanelFill(instance),
			Stroke = null,
			StrokeThickness = 0.0,
			Effect = _sharedNotchShadow,
			IsHitTestVisible = false
		};
		_notchBackgroundLayer.Children.Add(_notchSilhouettePath);
		BuildMorphLayers(_notchSilhouettePath.Fill);
		_notchBackgroundLayer.Children.Add(_earLeft);
		_notchBackgroundLayer.Children.Add(_earRight);
		_notchOutlinePath = new System.Windows.Shapes.Path
		{
			HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
			Fill = null,
			Stroke = (instance.BorderOutlineEnabled ? instance.GetBorderBrush() : null),
			StrokeThickness = instance.BorderThickness,
			IsHitTestVisible = false
		};
		_contentContainer = new Grid
		{
			HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
			Background = System.Windows.Media.Brushes.Transparent
		};
		_collapsedContent = new Grid
		{
			HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
			Opacity = 1.0,
			Visibility = Visibility.Visible,
			Cursor = System.Windows.Input.Cursors.Hand
		};
		_defaultCollapsedStack = new Grid
		{
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
		};
		_restingLeft = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		};
		_restingRight = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		};
		_restingSeparator = new Border
		{
			Width = 1.0,
			Height = 10.0,
			Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
			Margin = new Thickness(10.0, 0.0, 10.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		_defaultCollapsedStack.Children.Add(_restingLeft);
		_defaultCollapsedStack.Children.Add(_restingSeparator);
		_defaultCollapsedStack.Children.Add(_restingRight);
		_collapsedMiniAnalogCanvas = BuildMiniAnalogCanvas();
		_collapsedMiniAnalogCanvas.Visibility = Visibility.Collapsed;
		_restingLeft.Children.Add(_collapsedMiniAnalogCanvas);
		_collapsedPrimaryValueText = new TextBlock
		{
			Text = DateTime.Now.ToString("h:mm tt"),
			FontWeight = AynType.Medium,
			Foreground = current.Foreground,
			FontFamily = AynType.Family(TypeSurface.Notch, 12.5),
			VerticalAlignment = VerticalAlignment.Center
		};
		BuddyTypography.SetTabularFigures(_collapsedPrimaryValueText);
		_restingLeft.Children.Add(_collapsedPrimaryValueText);
		_collapsedBluetoothIconHolder = new ContentControl
		{
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center,
			Visibility = Visibility.Collapsed,
			Cursor = System.Windows.Input.Cursors.Hand,
			ToolTip = "Bluetooth Status"
		};
		_collapsedBluetoothIconHolder.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			try
			{
				ToolRouter.Instance.RouteTo("system");
				ExpandCapsule();
			}
			catch
			{
			}
		};
		_restingRight.Children.Add(_collapsedBluetoothIconHolder);
		_collapsedNowPlayingIconHolder = new ContentControl
		{
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center,
			Visibility = Visibility.Collapsed,
			Cursor = System.Windows.Input.Cursors.Hand,
			ToolTip = "Media Play/Pause"
		};
		_collapsedNowPlayingIconHolder.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			try
			{
				MediaService.Instance.TogglePlayPause();
			}
			catch
			{
			}
		};
		_restingRight.Children.Add(_collapsedNowPlayingIconHolder);
		SystemTelemetryData liveTelemetry = SystemMonitorService.GetLiveTelemetry();
		_collapsedBatteryBoltHolder = new ContentControl
		{
			VerticalAlignment = VerticalAlignment.Center,
			Visibility = Visibility.Collapsed
		};
		_restingRight.Children.Add(_collapsedBatteryBoltHolder);
		_collapsedBatteryIconHolder = new ContentControl
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		_restingRight.Children.Add(_collapsedBatteryIconHolder);
		_collapsedBatteryText = new TextBlock
		{
			Text = liveTelemetry.BatteryPercent.ToString(),
			FontWeight = FontWeights.Medium,
			Foreground = current.Foreground,
			FontFamily = AynType.Family(TypeSurface.Notch, 12.5),
			VerticalAlignment = VerticalAlignment.Center
		};
		BuddyTypography.SetTabularFigures(_collapsedBatteryText);
		_restingRight.Children.Add(_collapsedBatteryText);
		ApplyRestingLayout();
		SetRestingBattery(liveTelemetry.BatteryPercent, liveTelemetry.IsCharging);
		_activityViewport = new Grid
		{
			ClipToBounds = true,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch
		};
		_slotATranslate = new TranslateTransform(0.0, 0.0);
		_slotA = new ContentControl
		{
			HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
			HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch,
			VerticalContentAlignment = VerticalAlignment.Stretch,
			RenderTransform = _slotATranslate,
			Opacity = 1.0,
			Visibility = Visibility.Visible,
			Content = _defaultCollapsedStack
		};
		_slotBTranslate = new TranslateTransform(0.0, 0.0);
		_slotB = new ContentControl
		{
			HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
			HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch,
			VerticalContentAlignment = VerticalAlignment.Stretch,
			RenderTransform = _slotBTranslate,
			Opacity = 0.0,
			Visibility = Visibility.Collapsed
		};
		_activityViewport.Children.Add(_slotA);
		_activityViewport.Children.Add(_slotB);
		_collapsedContent.Children.Add(_activityViewport);
		_activeOwner.VisualType = CollapsedVisualType.Clock;
		_activeOwner.VisualInstance = _defaultCollapsedStack;
		_activeOwner.SlotName = "A";
		_activeOwner.SnapshotId = "canonical_clock";
		_collapsedContent.MouseEnter += delegate
		{
			_isCollapsedHovered = true;
			PillActivitySnapshot currentSnapshot2 = PillActivityArbiter.Instance.CurrentSnapshot;
			if (PillActivityArbiter.IsHoldable(currentSnapshot2))
			{
				PillActivityArbiter.Instance.SetMessageHover(currentSnapshot2.Id, hovering: true);
			}
			NotchActivity currentActivity4 = DynamicActivityService.Instance.CurrentActivity;
			if (currentActivity4 != null && currentActivity4.Type == ActivityType.Media)
			{
				RenderCurrentResolvedPillActivity();
			}
		};
		_collapsedContent.MouseLeave += delegate
		{
			_isCollapsedHovered = false;
			PillActivitySnapshot currentSnapshot = PillActivityArbiter.Instance.CurrentSnapshot;
			if (PillActivityArbiter.IsHoldable(currentSnapshot))
			{
				PillActivityArbiter.Instance.SetMessageHover(currentSnapshot.Id, hovering: false);
			}
			NotchActivity currentActivity3 = DynamicActivityService.Instance.CurrentActivity;
			if (currentActivity3 != null && currentActivity3.Type == ActivityType.Media)
			{
				RenderCurrentResolvedPillActivity();
			}
		};
		_welcomeActivityStack = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			Visibility = Visibility.Collapsed
		};
		_welcomeText = new TextBlock
		{
			Text = "Welcome to Ayn",
			FontSize = PrimaryFontSize,
			FontWeight = AynType.Medium,
			Foreground = System.Windows.Media.Brushes.White,
			FontFamily = AynType.Family(TypeSurface.Notch, PrimaryFontSize),
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			MaxWidth = 152.0,
			TextTrimming = TextTrimming.CharacterEllipsis
		};
		_welcomeActivityStack.Children.Add(_welcomeText);
		_pinnedActivityStack = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			Visibility = Visibility.Collapsed
		};
		_pinnedIconHolder = new ContentControl
		{
			Width = 11.0,
			Height = 11.0,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 7.0, 0.0),
			Visibility = Visibility.Collapsed
		};
		_pinnedActivityStack.Children.Add(_pinnedIconHolder);
		_pinnedTextTranslate = new TranslateTransform();
		_pinnedPreviewText = new TextBlock
		{
			Text = "",
			FontSize = SecondaryFontSize,
			FontWeight = FontWeights.Normal,
			Foreground = current.Foreground,
			FontFamily = AynType.Family(TypeSurface.Notch, SecondaryFontSize),
			TextTrimming = TextTrimming.CharacterEllipsis,
			TextWrapping = TextWrapping.NoWrap,
			VerticalAlignment = VerticalAlignment.Center,
			RenderTransform = _pinnedTextTranslate
		};
		_pinnedTextClipContainer = new Border
		{
			ClipToBounds = true,
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			Child = _pinnedPreviewText
		};
		_pinnedActivityStack.Children.Add(_pinnedTextClipContainer);
		_textScrollController = new CapsuleTextScrollController(_pinnedTextClipContainer, _pinnedPreviewText, _pinnedTextTranslate);
		_collapsedContent.Children.Add(_welcomeActivityStack);
		_collapsedContent.Children.Add(_pinnedActivityStack);
		_collapsedContent.MouseUp += delegate(object s, MouseButtonEventArgs e)
		{
			if (IsRestingSurface)
			{
				NotchActivity currentActivity2 = DynamicActivityService.Instance.CurrentActivity;
				if (currentActivity2 != null && currentActivity2.Type == ActivityType.HydrationReminder)
				{
					TimerService.Instance.AddHydrationGlass();
					DynamicActivityService.Instance.DismissCurrentTemporaryActivity();
					e.Handled = true;
				}
				else
				{
					if (currentActivity2 != null && !string.IsNullOrEmpty(currentActivity2.NavigateToToolId))
					{
						ToolRouter.Instance.RouteTo(currentActivity2.NavigateToToolId);
					}
					else if (CapsuleStateManager.HasActiveActivity)
					{
						CapsuleLiveActivity current2 = CapsuleStateManager.Current;
						if (current2 != null && !string.IsNullOrEmpty(current2.NavigateToToolId))
						{
							ToolRouter.Instance.RouteTo(current2.NavigateToToolId);
						}
					}
					if (_machine != null)
					{
						_machine.Click("resting notch");
					}
				}
			}
		};
		_contentContainer.Children.Add(_collapsedContent);
		_expandedContent = new Grid
		{
			Width = 752.0,
			Height = 352.0,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			Opacity = 0.0,
			Visibility = Visibility.Hidden,
			Margin = new Thickness(0.0, 0.0, 0.0, 0.0)
		};
		_expandedContent.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(40.0)
		});
		_expandedContent.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(252.0)
		});
		_expandedContent.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(60.0)
		});
		_capsuleHeader = new BuddyHeader();
		_capsuleHeader.CollapseRequested += delegate
		{
			if (_machine != null)
			{
				_machine.RequestClose("close button");
			}
		};
		_capsuleHeader.SettingsRequested += delegate
		{
			OpenSettings(null);
		};
		_capsuleHeader.EditRequested += delegate
		{
			NotchEditing.Set(!NotchEditing.IsEditing);
		};
		NotchEditing.Changed += RefreshEditing;
		Action addWidget = delegate
		{
			_toolLauncher.SetWidgetMode(value: true);
			_toolLauncher.BeginOpen();
		};
		NotchEditing.AddWidgetRequested += addWidget;
		base.Closed += delegate
		{
			NotchEditing.Changed -= RefreshEditing;
			NotchEditing.AddWidgetRequested -= addWidget;
		};
		AynType.PrepareRoot(_expandedContent, TypeSurface.Capsule);
		RenderOptions.SetClearTypeHint(_expandedContent, ClearTypeHint.Enabled);
		Grid.SetRow(_capsuleHeader, 0);
		_expandedContent.Children.Add(_capsuleHeader);
		Border border = new Border();
		border.Background = System.Windows.Media.Brushes.Transparent;
		border.BorderThickness = new Thickness(0.0);
		border.ClipToBounds = true;
		border.Width = 728.0;
		border.Height = 242.0;
		border.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		border.VerticalAlignment = VerticalAlignment.Top;
		border.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
		Border border2 = border;
		_viewportHost = new Grid
		{
			ClipToBounds = true
		};
		border2.Child = _viewportHost;
		Grid.SetRow(border2, 1);
		_expandedContent.Children.Add(border2);
		_navDockBorder = new Border
		{
			Background = System.Windows.Media.Brushes.Transparent,
			BorderBrush = null,
			BorderThickness = new Thickness(0.0),
			Padding = new Thickness(0.0),
			HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
			Height = 60.0
		};
		System.Windows.Controls.Panel.SetZIndex(_navDockBorder, 145);
		Border border3 = new Border();
		border3.Background = BuddyTheme.DockSurface;
		border3.BorderBrush = null;
		border3.BorderThickness = new Thickness(0.0);
		border3.CornerRadius = new CornerRadius(24.0);
		border3.Padding = new Thickness(6.0, 5.0, 6.0, 5.0);
		border3.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		border3.VerticalAlignment = VerticalAlignment.Top;
		border3.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
		border3.Height = 48.0;
		border3.SnapsToDevicePixels = true;
		border3.UseLayoutRounding = true;
		Border border4 = border3;
		RenderOptions.SetClearTypeHint(border4, ClearTypeHint.Enabled);
		ThemeTokens.Elevate(border4);
		Grid grid = new Grid();
		grid.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		grid.VerticalAlignment = VerticalAlignment.Center;
		grid.Height = 36.0;
		Grid grid2 = grid;
		_dockIndicatorTransform = new TranslateTransform(0.0, 0.0);
		_dockIndicator = new Border
		{
			Width = 36.0,
			Height = 36.0,
			CornerRadius = new CornerRadius(18.0),
			Background = ThemeTokens.DockPill,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Center,
			IsHitTestVisible = false,
			RenderTransform = _dockIndicatorTransform
		};
		grid2.Children.Add(_dockIndicator);
		_navTabsPanel = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center
		};
		grid2.Children.Add(_navTabsPanel);
		_navTabsPanel.MouseRightButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			BuddyDropdown.Show(_navTabsPanel, new BuddyMenuItem[1]
			{
				new BuddyMenuItem("Edit dock...", null, delegate
				{
					NotchEditing.Set(value: true);
				})
			});
			e.Handled = true;
		};
		border4.Child = grid2;
		_navDockBorder.Child = border4;
		Grid.SetRow(_navDockBorder, 2);
		_expandedContent.Children.Add(_navDockBorder);
		_launcherDismisser = new Border
		{
			Background = System.Windows.Media.Brushes.Transparent,
			Visibility = Visibility.Collapsed,
			IsHitTestVisible = false
		};
		Grid.SetRow(_launcherDismisser, 0);
		Grid.SetRowSpan(_launcherDismisser, 3);
		System.Windows.Controls.Panel.SetZIndex(_launcherDismisser, 140);
		_launcherDismisser.MouseLeftButtonDown += delegate
		{
			if (_toolLauncher != null && _toolLauncher.IsLauncherOpen)
			{
				_toolLauncher.BeginClose();
			}
		};
		_expandedContent.Children.Add(_launcherDismisser);
		_toolLauncher = new BuddyToolLauncher();
		Grid.SetRow(_toolLauncher, 1);
		_toolLauncher.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		_toolLauncher.VerticalAlignment = VerticalAlignment.Top;
		_toolLauncher.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
		System.Windows.Controls.Panel.SetZIndex(_toolLauncher, 150);
		_toolLauncher.Opened += delegate
		{
			if (ToolRegistry.Instance.GetTool("home") is HomeTool homeTool2)
			{
				homeTool2.CoverGallery(covered: true);
			}
			if (_launcherDismisser != null)
			{
				_launcherDismisser.Visibility = Visibility.Visible;
				_launcherDismisser.IsHitTestVisible = true;
			}
			AnimateMoreButtonRotation(45.0);
			UpdateTabSelectionStyles();
		};
		_toolLauncher.Closed += delegate
		{
			if (ToolRegistry.Instance.GetTool("home") is HomeTool homeTool)
			{
				homeTool.CoverGallery(covered: false);
			}
			if (_launcherDismisser != null)
			{
				_launcherDismisser.Visibility = Visibility.Collapsed;
				_launcherDismisser.IsHitTestVisible = false;
			}
			AnimateMoreButtonRotation(0.0);
			UpdateTabSelectionStyles();
		};
		_toolLauncher.ToolSelected += delegate(object s, string toolId)
		{
			if (!_toolLauncher.WidgetMode)
			{
				if (_dockButtons.TryGetValue(toolId, out var value2) && CapsuleMotion.AnimationsEnabled)
				{
					System.Windows.Point point = _expandedContent.PointFromScreen(_toolLauncher.LastAddPoint);
					System.Windows.Point point2 = value2.TranslatePoint(new System.Windows.Point(value2.ActualWidth / 2.0, value2.ActualHeight / 2.0), _expandedContent);
					Border flight = new Border
					{
						Width = 28.0,
						Height = 28.0,
						CornerRadius = new CornerRadius(14.0),
						Background = ThemeTokens.Surface2,
						Child = CreateDockIcon(toolId, ThemeTokens.Text),
						HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
						VerticalAlignment = VerticalAlignment.Top,
						IsHitTestVisible = false
					};
					TranslateTransform translateTransform = new TranslateTransform(point.X - 14.0, point.Y - 14.0);
					flight.RenderTransform = translateTransform;
					Grid.SetRowSpan(flight, 3);
					System.Windows.Controls.Panel.SetZIndex(flight, 190);
					_expandedContent.Children.Add(flight);
					CapsuleMotion.AnimateDouble(translateTransform, TranslateTransform.XProperty, point2.X - 14.0, 0.28, CapsuleMotion.Spring);
					CapsuleMotion.AnimateDouble(translateTransform, TranslateTransform.YProperty, point2.Y - 14.0, 0.28, CapsuleMotion.Spring, delegate
					{
						_expandedContent.Children.Remove(flight);
					});
				}
				if (_dockButtons.TryGetValue(toolId, out var value3))
				{
					ScaleTransform scaleTransform = new ScaleTransform(0.25, 0.25);
					value3.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
					value3.RenderTransform = scaleTransform;
					CapsuleMotion.AnimateDouble(scaleTransform, ScaleTransform.ScaleXProperty, 1.0, 0.22, CapsuleMotion.Spring);
					CapsuleMotion.AnimateDouble(scaleTransform, ScaleTransform.ScaleYProperty, 1.0, 0.22, CapsuleMotion.Spring);
				}
			}
		};
		_expandedContent.Children.Add(_toolLauncher);
		_editUndo = new BuddyUndoToast(5.0)
		{
			BorderThickness = new Thickness(0.0),
			Margin = new Thickness(0.0, 0.0, 0.0, 55.0)
		};
		Grid.SetRow(_editUndo, 1);
		System.Windows.Controls.Panel.SetZIndex(_editUndo, 180);
		_expandedContent.Children.Add(_editUndo);
		_contentContainer.Children.Add(_expandedContent);
		_capsuleCard.Children.Add(_contentContainer);
		_capsuleCard.Children.Add(_notchOutlinePath);
		_edgeLight = new NotchEdgeLight(this, _capsuleCard, _contentContainer);
		UpdateNotchSilhouette(collapsedWidth, collapsedHeight);
		_edgeLight.ApplyConfig(instance);
		EventHandler value = delegate
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				ApplyStaticBeat();
				UpdateBeat();
			});
		};
		PowerSaver.Changed += value;
		CapsuleMotion.AnimationsPolicyChanged += value;
		UpdateMorphState(0.0);
		_capsuleCard.AllowDrop = true;
		_capsuleCard.DragOver += delegate(object s, System.Windows.DragEventArgs e)
		{
			if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
			{
				e.Effects = System.Windows.DragDropEffects.Copy;
				ActiveFileDropTarget()?.OnFileDragOver(over: true);
			}
			else
			{
				e.Effects = System.Windows.DragDropEffects.None;
			}
			e.Handled = true;
		};
		_capsuleCard.DragLeave += delegate(object s, System.Windows.DragEventArgs e)
		{
			System.Windows.Point position2 = e.GetPosition(_capsuleCard);
			if (!(position2.X > 0.0) || !(position2.Y > 0.0) || !(position2.X < _capsuleCard.ActualWidth) || !(position2.Y < _capsuleCard.ActualHeight))
			{
				ActiveFileDropTarget()?.OnFileDragOver(over: false);
			}
		};
		_capsuleCard.Drop += delegate(object s, System.Windows.DragEventArgs e)
		{
			if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
			{
				string[] array = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
				if (IsRestingSurface && GardenOwnsStage && _petOutside != null && _petOutside.IsVisible && GetCursorPos(out var lpPoint2) && _petOutside.TryAcceptFiles(_petOutside.PointFromScreen(new System.Windows.Point(lpPoint2.X, lpPoint2.Y)), array))
				{
					e.Handled = true;
				}
				else
				{
					IFileDropTarget fileDropTarget = ActiveFileDropTarget();
					if (fileDropTarget != null)
					{
						fileDropTarget.OnFileDragOver(over: false);
						fileDropTarget.OnFilesDropped(array);
						e.Handled = true;
					}
					else if (_state != CapsuleDisplayState.Expanded && RouteRestingDrop(array))
					{
						e.Handled = true;
					}
					else
					{
						FileShelfService.Instance.AddFiles(array);
						ToolRouter.Instance.RouteTo("shelf");
						if (_machine != null)
						{
							_machine.RequestOpen("files dropped");
						}
					}
				}
			}
		};
		_capsuleCard.AddHandler(DragDrop.DragOverEvent, (System.Windows.DragEventHandler)delegate
		{
			SetFileDragOver(on: true);
		}, handledEventsToo: true);
		_capsuleCard.AddHandler(DragDrop.DropEvent, (System.Windows.DragEventHandler)delegate
		{
			SetFileDragOver(on: false);
		}, handledEventsToo: true);
		_capsuleCard.AddHandler(DragDrop.DragLeaveEvent, (System.Windows.DragEventHandler)delegate(object s, System.Windows.DragEventArgs e)
		{
			System.Windows.Point position = e.GetPosition(_capsuleCard);
			if (position.X <= 0.0 || position.Y <= 0.0 || position.X >= _capsuleCard.ActualWidth || position.Y >= _capsuleCard.ActualHeight)
			{
				SetFileDragOver(on: false);
			}
		}, handledEventsToo: true);
		_capsuleCard.MouseEnter += delegate
		{
			if (IsRestingSurface && GardenOwnsStage && _petOutside != null && _petOutside.IsVisible)
			{
				_petOutside.Hover();
			}
			if (_machine != null)
			{
				_machine.PointerMoved(!GardenBlocksHover && IsPointerInside() && !HoldsActionCard(), "enter");
			}
		};
		_capsuleCard.MouseLeave += delegate
		{
			if (_machine != null)
			{
				_machine.PointerMoved(!GardenBlocksHover && IsPointerInside(), "leave");
			}
		};
		_capsuleCard.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			if (IsRestingSurface && GardenOwnsStage && _petOutside != null && _petOutside.IsVisible && GetCursorPos(out var lpPoint) && _petOutside.HandleStageClick(_petOutside.PointFromScreen(new System.Windows.Point(lpPoint.X, lpPoint.Y))))
			{
				e.Handled = true;
			}
			else if (GardenBlocksHover)
			{
				if (_petOutside != null)
				{
					_petOutside.React();
				}
				e.Handled = true;
			}
			else if (IsRestingSurface)
			{
				CancelFirstRunHold();
				try
				{
					DynamicActivityService.Instance.DismissCurrentTemporaryActivity();
				}
				catch
				{
				}
				if (_welcomeActivityStack != null && _welcomeActivityStack.Visibility == Visibility.Visible)
				{
					_welcomeActivityStack.Visibility = Visibility.Collapsed;
					if (_activityViewport != null)
					{
						_activityViewport.Visibility = Visibility.Visible;
					}
					if (_defaultCollapsedStack != null)
					{
						_defaultCollapsedStack.Visibility = Visibility.Visible;
					}
				}
				NotchActivity currentActivity = DynamicActivityService.Instance.CurrentActivity;
				if (currentActivity != null && !string.IsNullOrEmpty(currentActivity.NavigateToToolId))
				{
					ToolRouter.Instance.RouteTo(currentActivity.NavigateToToolId);
				}
				if (_machine != null)
				{
					_machine.Click("notch card");
				}
			}
		};
		base.PreviewMouseMove += delegate
		{
			if (_machine != null && !GardenBlocksHover && !_machine.PointerInside && _machine.Phase != 0 && IsPointerInside() && !HoldsActionCard())
			{
				_machine.PointerMoved(inside: true, "move");
			}
		};
		_rootGrid.Children.Add(_capsuleCard);
		BuildMinimalLine();
		BuildDiagnosticsOverlay();
		base.Content = _rootGrid;
		ApplyNotchConfig();
	}

	private void InitializeTimers()
	{
		_scheduler = new DispatcherNotchScheduler(base.Dispatcher);
		_machine = new NotchStateMachine(this, _scheduler, NotchSettings.Instance.HoverDelayMs, NotchSettings.Instance.OpensOnClick);
		_machine.PhaseChanged += OnPhaseChanged;
		InstallPanelHold();
		ToolRouter.Instance.Notch = _machine;
		PillActivityArbiter instance = PillActivityArbiter.Instance;
		_revealHandler = delegate(bool active, int maxMs, string reason)
		{
			if (_machine != null)
			{
				_machine.SetReveal(active, maxMs, reason);
				if (!active)
				{
					NotchVisibilityEngine.Instance.ForceReevaluate("event ended: " + reason);
				}
			}
		};
		instance.RevealChanged += _revealHandler;
		NotchVisibilityEngine.Instance.HoverProbe = () => _machine != null && (_machine.Phase == NotchPhase.Peeking || (_machine.PointerInside && _machine.IsResting));
		_machine.TransientExpiredByWatchdog += delegate
		{
			PillActivityArbiter.Instance.DismissCurrentTransient();
			PillActivityArbiter.Instance.EndReveal("watchdog");
		};
		if (instance.IsRevealActive)
		{
			_machine.SetReveal(active: true, (int)(instance.RevealRemainingSeconds * 1000.0) + 1, "startup");
		}
		_telemetryTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(1000.0)
		};
		_telemetryTimer.Tick += delegate
		{
			RequestBadgeRefresh();
		};
		UpdateTelemetryTimer(NotchPhase.Resting);
	}

	private void UpdateTelemetryTimer(NotchPhase phase)
	{
		if (_telemetryTimer == null)
		{
			return;
		}
		if ((phase != NotchPhase.Resting && phase != NotchPhase.Transient && phase != NotchPhase.Peeking) || NotchConfig.Instance == null || !NotchConfig.Instance.ShowBluetooth)
		{
			_telemetryTimer.Stop();
			return;
		}
		_telemetryTimer.Interval = TimeSpan.FromSeconds(5.0);
		if (!_telemetryTimer.IsEnabled)
		{
			_telemetryTimer.Start();
		}
	}

	private void TrackOpenTime(NotchPhase from, NotchPhase to)
	{
		try
		{
			long timestamp = Stopwatch.GetTimestamp();
			switch (to)
			{
			case NotchPhase.Peeking:
				_openT0 = timestamp;
				_openAnimT0 = 0L;
				return;
			case NotchPhase.Opening:
				_openByHover = from == NotchPhase.Peeking && _openT0 != 0;
				if (!_openByHover)
				{
					_openT0 = timestamp;
				}
				_openAnimT0 = timestamp;
				return;
			case NotchPhase.Open:
				if (_openAnimT0 != 0)
				{
					double num = 1000.0 / (double)Stopwatch.Frequency;
					long num2 = (long)((double)(timestamp - _openT0) * num);
					long num3 = (long)((double)(timestamp - _openAnimT0) * num);
					LogWriter.Enqueue("metrics.log", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " open total=" + num2 + "ms dwell=" + (num2 - num3) + "ms anim=" + num3 + "ms by=" + (_openByHover ? "hover" : "click") + " mode=" + (RenderProfile.LightMode ? "light" : "full") + ((num2 > 400) ? " SLOW" : ""));
					if (num2 > 400)
					{
						NotchLog.Info("Slow open: " + num2 + " ms (anim " + num3 + " ms)");
					}
					_openT0 = (_openAnimT0 = 0L);
					return;
				}
				break;
			}
			if (to != NotchPhase.Opening && to != NotchPhase.Peeking)
			{
				_openT0 = (_openAnimT0 = 0L);
			}
		}
		catch
		{
		}
	}

	private void SetNotchRenderMode(bool hardware)
	{
		if (_hardwareRender != hardware && !(_hwnd == IntPtr.Zero))
		{
			HwndSource hwndSource = HwndSource.FromHwnd(_hwnd);
			if (hwndSource != null && hwndSource.CompositionTarget != null)
			{
				hwndSource.CompositionTarget.RenderMode = ((!hardware) ? RenderMode.SoftwareOnly : RenderMode.Default);
				_hardwareRender = hardware;
			}
		}
	}

	private void UpdateNotchRenderMode(NotchPhase to)
	{
		if (DwmComposed)
		{
			return;
		}
		if (to == NotchPhase.Resting || to == NotchPhase.Hidden)
		{
			if (!_hardwareRender || _restRenderTimer != null)
			{
				return;
			}
			_restRenderTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(2.0)
			};
			_restRenderTimer.Tick += delegate
			{
				_restRenderTimer.Stop();
				_restRenderTimer = null;
				if (_machine != null && (_machine.Phase == NotchPhase.Resting || _machine.Phase == NotchPhase.Hidden))
				{
					SetNotchRenderMode(hardware: false);
				}
			};
			_restRenderTimer.Start();
		}
		else
		{
			if (_restRenderTimer != null)
			{
				_restRenderTimer.Stop();
				_restRenderTimer = null;
			}
			SetNotchRenderMode(hardware: true);
		}
	}

	private void OnPhaseChanged(NotchPhase from, NotchPhase to, string trigger)
	{
		if (to == NotchPhase.Closing || to == NotchPhase.Hidden)
		{
			NotchEditing.Set(value: false);
			if (_toolLauncher != null)
			{
				_toolLauncher.BeginClose();
			}
		}
		UpdateNotchRenderMode(to);
		UpdateHitRegion();
		TrackOpenTime(from, to);
		NotchVisibilityEngine engine = NotchVisibilityEngine.Instance;
		engine.IsCapsuleExpanded = to == NotchPhase.Opening || to == NotchPhase.Open || to == NotchPhase.Closing;
		engine.IsHoveringNotch = to == NotchPhase.Peeking || (_machine != null && _machine.PointerInside && to != NotchPhase.Hidden);
		SystemSampler.SetCapsuleOpen(to == NotchPhase.Opening || to == NotchPhase.Open);
		UpdateLiveUiSubscriptions(to != NotchPhase.Hidden);
		if (to != NotchPhase.Open)
		{
			ToolRouter.Instance.InvalidateActivation();
		}
		UpdateTelemetryTimer(to);
		NotchDiagnostics.NotifyChanged();
		if (_edgeLight != null)
		{
			_edgeLight.SetResting((to == NotchPhase.Resting || to == NotchPhase.Transient || to == NotchPhase.Peeking) && MorphProgress <= 0.001);
			if (to == NotchPhase.Peeking)
			{
				_edgeLight.Trigger();
			}
		}
		UpdateBeat();
		EventHandler<CapsuleState> capsuleLifecycleStateChanged = this.CapsuleLifecycleStateChanged;
		if (capsuleLifecycleStateChanged != null)
		{
			try
			{
				capsuleLifecycleStateChanged(this, _capsuleLifecycleState);
			}
			catch
			{
			}
		}
		if (to == NotchPhase.Peeking || (to == NotchPhase.Opening && from == NotchPhase.Peeking) || (from == NotchPhase.Peeking && to == NotchPhase.Resting))
		{
			GetCursorPos(out var lpPoint);
			Rect hitRectDip = GetHitRectDip();
			string text = "none";
			try
			{
				if (!hitRectDip.IsEmpty)
				{
					System.Windows.Point point = PointToScreen(hitRectDip.TopLeft);
					System.Windows.Point point2 = PointToScreen(hitRectDip.BottomRight);
					text = $"{point.X:0},{point.Y:0}-{point2.X:0},{point2.Y:0}";
				}
			}
			catch
			{
			}
			GetWindowRect(_hwnd, out var lpRect);
			NotchLog.Info($"  cursor {lpPoint.X},{lpPoint.Y} px, hit area {text} px, drawn {_shownPresence}, window {lpRect.Left},{lpRect.Top} {base.Visibility}");
		}
		if ((to == NotchPhase.Resting || to == NotchPhase.Transient) && from == NotchPhase.Closing)
		{
			base.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
			{
				engine.EvaluateVisibility("panel closed");
			});
		}
	}

	private void OpenFromOutside(string source)
	{
		if (!EntitlementGate.IsEntitled)
		{
			SettingsLauncher.OpenForNotEntitled();
			return;
		}
		if (!ToolRegistry.Instance.CapsuleEnabled)
		{
			ToolRegistry.Instance.ForceEnableCapsule();
		}
		if (_machine != null)
		{
			_machine.RequestOpen(source);
			if (_hwnd != IntPtr.Zero && _machine.IsOpenOrOpening)
			{
				SetNonActivating(noActivate: false);
				SetForegroundWindow(_hwnd);
			}
		}
	}

	public void OpenSettings(string page)
	{
		if (_machine != null)
		{
			_machine.RequestClose("opening Settings");
		}
		base.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
		{
			SettingsLauncher.Open(page);
		});
	}

	public void HandleSecondLaunch()
	{
		OpenFromOutside("second launch");
		DispatcherTimer timer = new DispatcherTimer(DispatcherPriority.Normal, base.Dispatcher)
		{
			Interval = TimeSpan.FromMilliseconds(700.0)
		};
		timer.Tick += delegate
		{
			timer.Stop();
			if (!_isShuttingDown)
			{
				OpenSettings(null);
			}
		};
		timer.Start();
	}

	public void OpenToolFromSettings(string toolId)
	{
		IBuddyTool tool = ToolRegistry.Instance.GetTool(toolId);
		if (tool != null)
		{
			ToolRegistry.Instance.EnsureInitialized(tool);
		}
		ToolRouter.Instance.RouteTo(toolId);
		OpenFromOutside("Settings: open " + toolId);
	}

	private void ToggleFromHotkey()
	{
		if (!EntitlementGate.IsEntitled)
		{
			SettingsLauncher.OpenForNotEntitled();
		}
		else if (_machine != null && _machine.IsOpenOrOpening)
		{
			_machine.RequestClose("shortcut");
		}
		else
		{
			OpenFromOutside("shortcut");
		}
	}

	public static void RegisterDockTools(ToolRegistry reg)
	{
		reg.RegisterTool(new HomeTool());
		reg.RegisterTool(new SystemTool());
		reg.RegisterTool(new ClipboardTool());
		reg.RegisterTool(new NotesTool());
		reg.RegisterTool(new TimerTool());
		reg.RegisterTool(new FileShelfTool());
		reg.RegisterTool(new DropTool());
		reg.RegisterTool(new CalendarTool());
		reg.RegisterTool(new ClockTool());
		reg.RegisterTool(new WeatherTool());
		reg.RegisterTool(new FilesConverterTool());
		reg.RegisterTool(new LinksTool());
		reg.RegisterTool(new UnitsConverterTool());
		reg.RegisterTool(new MessageBannerTool());
		reg.RegisterTool(new ScreenTimeTool());
		reg.RegisterTool(new AnalyticsTool());
		reg.RegisterTool(new RevenueTool());
		reg.RegisterTool(new AICodingTool());
		reg.RegisterTool(new WindowsBuddy.Tools.Personal.BreaksTool());
		reg.RegisterTool(new WindowsBuddy.Tools.Personal.SalatTool());
		reg.RegisterTool(new WindowsBuddy.Tools.Personal.GuardTool());
		reg.RegisterTool(new WindowsBuddy.Tools.Personal.QrWifiTool());
		reg.RegisterTool(new WindowsBuddy.Tools.Personal.DocConvertTool());
		reg.RegisterTool(new WindowsBuddy.Tools.Personal.TodoTool());
		foreach (var id in new[] { "pets", "todo", "qr" })
		{
			var tool = reg.GetTool(id);
			if (tool != null) { tool.IsEnabled = true; }
		}
		reg.SaveUserConfiguration();
	}

	private void RegisterCoreTools()
	{
		ToolRegistry instance = ToolRegistry.Instance;
		RegisterDockTools(instance);
		_toolNavigatedHandler = OnToolNavigated;
		ToolRouter.Instance.Navigated += _toolNavigatedHandler;
		CapsuleMotion.AnimationsPolicyChanged += OnTabMotionPolicyChanged;
		instance.CapsuleSettingsChanged += OnTabMotionPolicyChanged;
		_tabSystemMotionHandler = delegate(object s, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "ClientAreaAnimation")
			{
				OnTabMotionPolicyChanged(s, e);
			}
		};
		SystemParameters.StaticPropertyChanged += _tabSystemMotionHandler;
		_headerSubtitleHandler = delegate(object s, string sub)
		{
			if (_capsuleHeader != null)
			{
				_capsuleHeader.UpdateStatus(sub);
			}
		};
		ToolRouter.Instance.HeaderSubtitleUpdated += _headerSubtitleHandler;
		_toolsUpdatedHandler = delegate
		{
			RebuildNavTabs();
		};
		instance.ToolsUpdated += _toolsUpdatedHandler;
		_primaryCollapsedToolChangedHandler = delegate
		{
			RequestBadgeRefresh();
		};
		instance.PrimaryCollapsedToolChanged += _primaryCollapsedToolChangedHandler;
		RebuildNavTabs();
		ToolRouter.Instance.RouteTo(ToolRouter.Instance.ChooseUsableTabId("home") ?? "home");
		ToolRouter.Instance.StartIdleWarmup(base.Dispatcher, CanRunIdleWork);
		ToolRouter.Instance.StartIdleMemoryMaintenance(base.Dispatcher, CanRunIdleWork);
		DispatcherTimer settingsPrep = new DispatcherTimer(DispatcherPriority.SystemIdle, base.Dispatcher)
		{
			Interval = TimeSpan.FromSeconds(15.0)
		};
		settingsPrep.Tick += delegate
		{
			if (CanRunIdleWork())
			{
				settingsPrep.Stop();
				SettingsPages.PrepareWindowCode();
			}
		};
		settingsPrep.Start();
		base.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(EnsureBackgroundTools));
	}

	private bool CanRunIdleWork()
	{
		if (!_isShuttingDown && _machine != null && _machine.Phase == NotchPhase.Resting && !_machine.PointerInside && !_restingResizeActive && !GardenBlocksHover && !HoldsActionCard())
		{
			return !SettingsLauncher.IsOpen;
		}
		return false;
	}

	private void EnsureBackgroundTools()
	{
		ToolRegistry instance = ToolRegistry.Instance;
		IBuddyTool tool = instance.GetTool("calendar");
		if (tool != null && tool.IsEnabled)
		{
			using (BootTimeline.Service("calendar (UI thread)"))
			{
				instance.EnsureInitialized(tool);
			}
		}
	}

	private void OnToolNavigated(object sender, ToolNavigationEventArgs e)
	{
		bool anim = ToolRegistry.Instance.AnimationsEnabled && _state == CapsuleDisplayState.Expanded;
		string text = ((e.Tool != null) ? e.Tool.Id : null);
		if (!string.Equals(text, ToolRouter.Instance.ActiveToolId, StringComparison.OrdinalIgnoreCase) || !object.ReferenceEquals(e.View, ToolRouter.Instance.CurrentView))
		{
			return;
		}
		try
		{
			if (!e.IsSwap)
			{
				if (_navTabsPanel != null)
				{
					foreach (BuddyDockSlot item in (from b in _navTabsPanel.Children.OfType<System.Windows.Controls.Button>()
						select b.Content).OfType<BuddyDockSlot>())
					{
						item.ClearTransientState();
					}
				}
				UpdateTabSelectionStyles(anim);
			}
		}
		catch (Exception ex)
		{
			NotchLog.Error("Dock update failed (" + text + ")", ex);
		}
		if (e.View is TabPlaceholder && _currentToolView != null)
		{
			_currentToolView.IsHitTestVisible = false;
			return;
		}
		try
		{
			SwitchViewportView(e.View, anim, text);
		}
		catch (Exception ex2)
		{
			NotchLog.Error("Tab switch failed (" + text + ")", ex2);
		}
		try
		{
			if (_capsuleHeader != null && e.Tool != null)
			{
				UIElement headerActions = e.Tool.GetHeaderActions();
				string status = (string.Equals(e.Tool.Id, "home", StringComparison.OrdinalIgnoreCase) ? HomeTool.HeaderDate(DateTime.Now) : (string.Equals(e.Tool.Id, "calendar", StringComparison.OrdinalIgnoreCase) ? string.Empty : GetContextualSubtitle(e.Tool)));
				ToolRouter.DetachFromParent(headerActions);
				_capsuleHeader.SetContent(e.Tool.CreateIcon(16.0, BuddyTokens.Text100Brush), e.Tool.DisplayName, status, headerActions);
				IHeaderStatusLead headerStatusLead = e.Tool as IHeaderStatusLead;
				_capsuleHeader.SetStatusLead(headerStatusLead?.GetHeaderStatusLead());
			}
		}
		catch (Exception ex3)
		{
			NotchLog.Error("Tab header failed (" + text + ")", ex3);
		}
		RestoreFocusToActiveView(e.View, text);
		RecordInteractionState("tab switched");
	}

	private void RecordInteractionState(string reason)
	{
		try
		{
			string text = _attachedViews.FirstOrDefault((KeyValuePair<string, UIElement> p) => object.ReferenceEquals(p.Value, _currentToolView)).Key ?? "none";
			string text2 = _dockButtons.FirstOrDefault((KeyValuePair<string, System.Windows.Controls.Button> p) => p.Value.Content is BuddyDockSlot && ((BuddyDockSlot)p.Value.Content).IsActive).Key ?? "none";
			_lastInteractionSnapshot = string.Format(CultureInfo.InvariantCulture, "phase={0} tab={1} view={2} dock={3} built={4} activated={5} generation={6} indicatorX={7:0.##}", (_machine != null) ? _machine.Phase.ToString() : "none", ToolRouter.Instance.ActiveToolId, text, text2, ToolRouter.Instance.IsCurrentViewBuilt, _toolActivated, ToolRouter.Instance.ActivationGeneration, (_dockIndicatorTransform != null) ? _dockIndicatorTransform.X : 0.0);
			NotchLog.Info("Interaction (" + reason + "): " + _lastInteractionSnapshot);
		}
		catch (Exception ex)
		{
			NotchLog.Warn("Interaction snapshot failed: " + ex.GetType().Name);
		}
	}

	private UIElement EnsureViewportWrapped(UIElement rawView)
	{
		if (rawView == null)
		{
			return null;
		}
		if (rawView is ScrollViewer)
		{
			return rawView;
		}
		if (rawView is FrameworkElement frameworkElement && (object.Equals(frameworkElement.Tag, "SelfScroller") || object.Equals(frameworkElement.Tag, "NoOuterScroll")))
		{
			return rawView;
		}
		if (!_scrollWrappers.TryGetValue(rawView, out var value) || value.Content != rawView)
		{
			ToolRouter.DetachFromParent(rawView);
			BuddyScrollViewer buddyScrollViewer = new BuddyScrollViewer();
			buddyScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
			buddyScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
			buddyScrollViewer.Focusable = false;
			buddyScrollViewer.ClipToBounds = true;
			buddyScrollViewer.Content = rawView;
			value = buddyScrollViewer;
			_scrollWrappers[rawView] = value;
		}
		return value;
	}

	private void SafeAttachToViewport(UIElement element)
	{
		if (element == null || _viewportHost == null || _viewportHost.Children.Contains(element))
		{
			return;
		}
		ToolRouter.DetachFromParent(element);
		try
		{
			_viewportHost.Children.Add(element);
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("SafeAttachToViewport: Exception attaching visual, executing emergency recovery: " + ex.Message);
			ToolRouter.DetachFromParent(element);
			try
			{
				if (!_viewportHost.Children.Contains(element))
				{
					_viewportHost.Children.Add(element);
				}
			}
			catch (Exception ex2)
			{
				TransactionLogger.Error("SafeAttachToViewport: Critical visual attachment failure: " + ex2.Message, ex2);
			}
		}
	}

	private void OnTabMotionPolicyChanged(object sender, EventArgs e)
	{
		if (!base.Dispatcher.CheckAccess())
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				OnTabMotionPolicyChanged(sender, e);
			});
		}
		else if (!CapsuleMotion.AnimationsEnabled || !ToolRegistry.Instance.AnimationsEnabled)
		{
			SettleViewportTransition();
			if (_dockSettle != null)
			{
				_dockSettle.Stop();
			}
			UpdateTabSelectionStyles(anim: false);
		}
	}

	private void SettleViewportTransition()
	{
		_viewTransitionGeneration++;
		if (_viewportHost == null)
		{
			return;
		}
		foreach (UIElement child in _viewportHost.Children)
		{
			child.BeginAnimation(UIElement.OpacityProperty, null);
			child.Opacity = 1.0;
			child.IsHitTestVisible = child == _currentToolView;
			child.Visibility = ((child != _currentToolView) ? Visibility.Collapsed : Visibility.Visible);
		}
		_outgoingToolView = null;
		UIElement[] array = (from UIElement v in _viewportHost.Children
			where v != _currentToolView && v != _lockView && !_attachedViews.Values.Contains(v)
			select v).ToArray();
		foreach (UIElement view in array)
		{
			DetachViewportView(view);
		}
	}

	private void SwitchViewportView(UIElement rawView, bool anim, string toolId = null)
	{
		if (_viewportHost == null)
		{
			return;
		}
		if (!EntitlementGate.IsEntitled)
		{
			if (_navDockBorder != null)
			{
				_navDockBorder.Visibility = Visibility.Collapsed;
			}
			UIElement orCreateLockView = GetOrCreateLockView();
			if (!_viewportHost.Children.Contains(orCreateLockView))
			{
				_viewportHost.Children.Clear();
				_attachedViews.Clear();
				_viewportHost.Children.Add(orCreateLockView);
			}
			orCreateLockView.Visibility = Visibility.Visible;
			_currentToolView = orCreateLockView;
			_outgoingToolView = null;
			return;
		}
		if (_navDockBorder != null && _navDockBorder.Visibility != 0)
		{
			_navDockBorder.Visibility = Visibility.Visible;
		}
		if (rawView == null)
		{
			return;
		}
		if (rawView is TabPlaceholder && _currentToolView != null)
		{
			_currentToolView.IsHitTestVisible = false;
			return;
		}
		SettleViewportTransition();
		long transition = _viewTransitionGeneration;
		UIElement currentToolView = _currentToolView;
		UIElement newView = EnsureViewportWrapped(rawView);
		if (toolId != null)
		{
			if (_attachedViews.TryGetValue(toolId, out var value) && value != newView && value != currentToolView)
			{
				DetachViewportView(value);
			}
			_attachedViews[toolId] = newView;
		}
		if (!_viewportHost.Children.Contains(newView))
		{
			newView.Visibility = Visibility.Collapsed;
			SafeAttachToViewport(newView);
		}
		if (currentToolView == newView)
		{
			newView.BeginAnimation(UIElement.OpacityProperty, null);
			newView.Opacity = 1.0;
			newView.RenderTransform = null;
			newView.Visibility = Visibility.Visible;
			return;
		}
		newView.BeginAnimation(UIElement.OpacityProperty, null);
		newView.RenderTransform = null;
		newView.Visibility = Visibility.Hidden;
		newView.Measure(new System.Windows.Size(728.0, 242.0));
		newView.Arrange(new Rect(0.0, 0.0, 728.0, 242.0));
		bool flag = anim && CapsuleMotion.AnimationsEnabled && currentToolView != null && _viewportHost.Children.Contains(currentToolView) && currentToolView.Visibility == Visibility.Visible;
		if (currentToolView != null)
		{
			currentToolView.IsHitTestVisible = false;
			currentToolView.Visibility = Visibility.Collapsed;
		}
		_currentToolView = newView;
		newView.IsHitTestVisible = true;
		newView.Opacity = 1.0;
		newView.Visibility = Visibility.Visible;
		if (flag)
		{
			DoubleAnimation doubleAnimation = new DoubleAnimation(0.92, 1.0, TimeSpan.FromMilliseconds(90.0));
			doubleAnimation.EasingFunction = CapsuleMotion.EaseOutCubic;
			doubleAnimation.FillBehavior = FillBehavior.Stop;
			DoubleAnimation doubleAnimation2 = doubleAnimation;
			doubleAnimation2.Completed += delegate
			{
				if (transition == _viewTransitionGeneration && _currentToolView == newView)
				{
					SettleViewportTransition();
				}
			};
			newView.BeginAnimation(UIElement.OpacityProperty, doubleAnimation2, HandoffBehavior.SnapshotAndReplace);
		}
		if (currentToolView != null && !_attachedViews.Values.Contains(currentToolView))
		{
			DetachViewportView(currentToolView);
		}
		foreach (UIElement child in _viewportHost.Children)
		{
			if (child != newView && child.Visibility != Visibility.Collapsed)
			{
				child.BeginAnimation(UIElement.OpacityProperty, null);
				child.Visibility = Visibility.Collapsed;
			}
		}
	}

	private void DetachViewportView(UIElement view)
	{
		if (view != null)
		{
			view.BeginAnimation(UIElement.OpacityProperty, null);
			if (_viewportHost.Children.Contains(view))
			{
				_viewportHost.Children.Remove(view);
			}
			if (_currentToolView == view)
			{
				_currentToolView = null;
			}
			if (_outgoingToolView == view)
			{
				_outgoingToolView = null;
			}
		}
	}

	private UIElement GetOrCreateLockView()
	{
		if (_lockView != null)
		{
			return _lockView;
		}
		Border border = new Border();
		border.Background = BuddyTheme.CardFill;
		border.CornerRadius = new CornerRadius(16.0);
		border.Padding = new Thickness(32.0, 24.0, 32.0, 24.0);
		border.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		border.VerticalAlignment = VerticalAlignment.Center;
		border.MaxWidth = 560.0;
		Border border2 = border;
		StackPanel stackPanel = new StackPanel();
		stackPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		Border border3 = new Border();
		border3.Width = 48.0;
		border3.Height = 48.0;
		border3.CornerRadius = new CornerRadius(24.0);
		border3.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 245, 158, 11));
		border3.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		border3.Margin = new Thickness(0.0, 0.0, 0.0, 16.0);
		Border border4 = border3;
		TextBlock textBlock = new TextBlock();
		textBlock.Text = "\ud83d\udd12";
		textBlock.FontSize = 20.0;
		textBlock.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		TextBlock child = textBlock;
		border4.Child = child;
		stackPanel2.Children.Add(border4);
		TextBlock textBlock2 = new TextBlock();
		textBlock2.Text = "Your 3-day Ayn has ended";
		textBlock2.FontSize = 15.0;
		textBlock2.FontWeight = FontWeights.SemiBold;
		textBlock2.Foreground = BuddyTheme.Text100;
		textBlock2.TextAlignment = TextAlignment.Center;
		textBlock2.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		textBlock2.Margin = new Thickness(0.0, 0.0, 0.0, 8.0);
		TextBlock element = textBlock2;
		stackPanel2.Children.Add(element);
		TextBlock textBlock3 = new TextBlock();
		textBlock3.Text = "Everything is included";
		textBlock3.FontSize = 13.0;
		textBlock3.Foreground = BuddyTheme.Text50;
		textBlock3.TextAlignment = TextAlignment.Center;
		textBlock3.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		textBlock3.TextWrapping = TextWrapping.Wrap;
		textBlock3.MaxWidth = 420.0;
		textBlock3.Margin = new Thickness(0.0, 0.0, 0.0, 20.0);
		TextBlock element2 = textBlock3;
		stackPanel2.Children.Add(element2);
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Orientation = System.Windows.Controls.Orientation.Horizontal;
		stackPanel3.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		StackPanel stackPanel4 = stackPanel3;
		Border border5 = new Border();
		border5.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11));
		border5.CornerRadius = new CornerRadius(6.0);
		border5.Padding = new Thickness(16.0, 7.0, 16.0, 7.0);
		border5.Cursor = System.Windows.Input.Cursors.Hand;
		border5.Margin = new Thickness(0.0, 0.0, 10.0, 0.0);
		Border border6 = border5;
		TextBlock textBlock4 = new TextBlock();
		textBlock4.Text = "Ayn";
		textBlock4.FontSize = 13.0;
		textBlock4.FontWeight = AynType.Medium;
		textBlock4.Foreground = System.Windows.Media.Brushes.Black;
		TextBlock child2 = textBlock4;
		border6.Child = child2;
		border6.MouseLeftButtonUp += delegate
		{
			AppLinks.Open("https://github.com/salahuddinselim/Ayn");
		};
		stackPanel4.Children.Add(border6);
		Border border7 = new Border();
		border7.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		border7.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		border7.BorderThickness = new Thickness(1.0);
		border7.CornerRadius = new CornerRadius(6.0);
		border7.Padding = new Thickness(16.0, 7.0, 16.0, 7.0);
		border7.Cursor = System.Windows.Input.Cursors.Hand;
		Border border8 = border7;
		TextBlock textBlock5 = new TextBlock();
		textBlock5.Text = "Enter license key";
		textBlock5.FontSize = 13.0;
		textBlock5.FontWeight = FontWeights.Medium;
		textBlock5.Foreground = BuddyTheme.Text100;
		TextBlock child3 = textBlock5;
		border8.Child = child3;
		border8.MouseLeftButtonUp += delegate
		{
			SettingsLauncher.Open("license");
		};
		stackPanel4.Children.Add(border8);
		stackPanel2.Children.Add(stackPanel4);
		border2.Child = stackPanel2;
		_lockView = border2;
		return _lockView;
	}

	private void CancelPendingNavigationFocus()
	{
		_focusToken++;
		if (_focusTimer != null)
		{
			_focusTimer.Stop();
		}
	}

	private void RestoreFocusToActiveView(UIElement view, string toolId)
	{
		int token = ++_focusToken;
		if (_focusTimer == null)
		{
			_focusTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
			{
				Interval = TimeSpan.FromMilliseconds(160.0)
			};
		}
		_focusTimer.Stop();
		EventHandler[] holder = new EventHandler[1];
		holder[0] = delegate
		{
			_focusTimer.Tick -= holder[0];
			_focusTimer.Stop();
			if (token == _focusToken)
			{
				RestoreFocusNow(view, toolId);
			}
		};
		_focusTimer.Tick += holder[0];
		_focusTimer.Start();
	}

	private void RestoreFocusNow(UIElement view, string toolId)
	{
		Action action = delegate
		{
			try
			{
				if (_state == CapsuleDisplayState.Expanded)
				{
					UIElement uIElement = FindFirstEditableInput(view);
					if (uIElement != null)
					{
						Keyboard.Focus(uIElement);
					}
					else if (!string.IsNullOrEmpty(toolId) && _dockButtons.ContainsKey(toolId))
					{
						Keyboard.Focus(_dockButtons[toolId]);
					}
					else if (_moreBtn != null && _moreBtn.IsVisible)
					{
						Keyboard.Focus(_moreBtn);
					}
				}
			}
			catch
			{
			}
		};
		action();
	}

	private UIElement FindFirstEditableInput(DependencyObject parent)
	{
		if (parent == null)
		{
			return null;
		}
		int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
		for (int i = 0; i < childrenCount; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			if (child is System.Windows.Controls.TextBox { IsVisible: not false, Focusable: not false, IsReadOnly: false, IsEnabled: not false } textBox)
			{
				return textBox;
			}
			if (child is System.Windows.Controls.RichTextBox { IsVisible: not false, Focusable: not false, IsReadOnly: false, IsEnabled: not false } richTextBox)
			{
				return richTextBox;
			}
			UIElement uIElement = FindFirstEditableInput(child);
			if (uIElement != null)
			{
				return uIElement;
			}
		}
		return null;
	}

	private void OnCapsulePreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
	{
		if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control)
		{
			NotchEditing.Set(!NotchEditing.IsEditing);
			e.Handled = true;
			return;
		}
		if (e.Key == Key.Escape && NotchEditing.IsEditing && (_toolLauncher == null || !_toolLauncher.IsLauncherOpen))
		{
			NotchEditing.Set(value: false);
			e.Handled = true;
			return;
		}
		if (_state != CapsuleDisplayState.Expanded)
		{
			if (e.Key == Key.Escape)
			{
				CancelFirstRunHold();
				if (FirstRunManager.CurrentState == FirstRunState.Started)
				{
					FirstRunManager.MarkDismissed();
				}
				if (_machine != null)
				{
					_machine.RequestClose("Esc");
				}
				e.Handled = true;
			}
			return;
		}
		if (e.Key == Key.Escape)
		{
			if (_toolLauncher != null && _toolLauncher.IsLauncherOpen)
			{
				_toolLauncher.HandleEscape();
				e.Handled = true;
				return;
			}
			if (ToolRouter.Instance.ActiveTool is IEscapeAware escapeAware && escapeAware.HandleEscape())
			{
				e.Handled = true;
				return;
			}
			if (Keyboard.FocusedElement is System.Windows.Controls.TextBox textBox && !string.IsNullOrEmpty(textBox.Text))
			{
				textBox.Text = string.Empty;
				e.Handled = true;
				return;
			}
			if (Keyboard.FocusedElement is System.Windows.Controls.RichTextBox richTextBox)
			{
				TextRange textRange = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
				if (!string.IsNullOrEmpty(textRange.Text) && textRange.Text.Trim().Length > 0)
				{
					richTextBox.Document.Blocks.Clear();
					e.Handled = true;
					return;
				}
			}
			CancelFirstRunHold();
			if (FirstRunManager.CurrentState == FirstRunState.Started)
			{
				FirstRunManager.MarkDismissed();
			}
			if (_machine != null)
			{
				_machine.RequestClose("Esc");
			}
			e.Handled = true;
			return;
		}
		if ((Keyboard.Modifiers & (ModifierKeys.Alt | ModifierKeys.Control | ModifierKeys.Shift)) == ModifierKeys.Alt)
		{
			Key key = ((e.Key == Key.System) ? e.SystemKey : e.Key);
			int num = (int)((key >= Key.D1 && key <= Key.D9) ? (key - 35) : ((key >= Key.NumPad1 && key <= Key.NumPad9) ? (key - 75) : ((Key)(-1))));
			if (num >= 0 && num < _primaryToolIds.Count)
			{
				ToolRouter.Instance.RouteTo(_primaryToolIds[num]);
				e.Handled = true;
				return;
			}
		}
		if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control || (Keyboard.Modifiers & ModifierKeys.Alt) != 0)
		{
			return;
		}
		int num2 = -1;
		if ((Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift)
		{
			if (e.Key >= Key.D1 && e.Key <= Key.D9)
			{
				num2 = (int)(e.Key - 35);
			}
			else if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad9)
			{
				num2 = (int)(e.Key - 75);
			}
		}
		if (num2 >= 0 && num2 < _primaryToolIds.Count)
		{
			ToolRouter.Instance.RouteTo(_primaryToolIds[num2]);
			e.Handled = true;
			return;
		}
		if (e.Key == Key.Tab)
		{
			bool flag = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
			IReadOnlyList<IBuddyTool> enabledTools = ToolRegistry.Instance.EnabledTools;
			if (enabledTools != null && enabledTools.Count > 1)
			{
				string activeToolId = ToolRouter.Instance.ActiveToolId;
				int num3 = 0;
				for (int i = 0; i < enabledTools.Count; i++)
				{
					if (string.Equals(enabledTools[i].Id, activeToolId, StringComparison.OrdinalIgnoreCase))
					{
						num3 = i;
						break;
					}
				}
				int index = (flag ? ((num3 - 1 + enabledTools.Count) % enabledTools.Count) : ((num3 + 1) % enabledTools.Count));
				ToolRouter.Instance.RouteTo(enabledTools[index].Id);
				e.Handled = true;
				return;
			}
		}
		if (e.Key == Key.K)
		{
			ToggleMoreMenu();
			e.Handled = true;
		}
	}

	private IFileDropTarget ActiveFileDropTarget()
	{
		if (_state != CapsuleDisplayState.Expanded)
		{
			return null;
		}
		return ToolRouter.Instance.ActiveTool as IFileDropTarget;
	}

	private string GetContextualSubtitle(IBuddyTool tool)
	{
		if (tool == null)
		{
			return "Desktop Utility";
		}
		return tool.Id.ToLowerInvariant() switch
		{
			"system" => "Power · Network · Audio", 
			"home" => "System Overview", 
			"battery" => "Power Management", 
			"volume" => "Master Audio (WASAPI)", 
			"clock" => "Local Time & Date", 
			"wifi" => "Network Status", 
			"timer" => "Focus & intervals", 
			"notes" => NotesTool.FormatNoteCount(NotesService.Instance.Notes.Count), 
			"settings" => "Preferences & Configuration", 
			"notch_customization" => "Shape, behavior & live preview", 
			"notch_behavior" => "Shape, behavior & live preview", 
			"weather" => WeatherTool.HeaderSubtitle(), 
			"calendar" => string.Empty, 
			"shelf" => "Parked files", 
			"drop" => string.Empty, 
			"clipboard" => ClipboardTool.FormatItemCount(ClipboardService.Instance.Items.Count), 
			"files" => "Files", 
			"screentime" => DateTime.Now.ToString("ddd, d MMM"), 
			"units" => "Conversion", 
			"links" => "Quick Bookmarks", 
			"message" => "Notch banner", 
			"analytics" => string.Empty, 
			"revenue" => "Sales & revenue", 
			"aicoding" => "Today", 
			"claude" => "CLI Terminal Integration", 
			"focus" => "Deep Work Session", 
			_ => tool.DisplayName, 
		};
	}

	private static bool IsDockTab(IBuddyTool tool)
	{
		if (!string.Equals(tool.Id, "settings", StringComparison.OrdinalIgnoreCase))
		{
			return !string.Equals(tool.Id, "notch_customization", StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private UIElement CreateDockIcon(string id, System.Windows.Media.Brush ink)
	{
		bool flag = _toolLauncher != null && _toolLauncher.IsLauncherOpen;
		string text = ((!string.Equals(id, "more", StringComparison.OrdinalIgnoreCase)) ? BuddyToolIcons.FluentName(id) : (flag ? "dismiss" : "add"));
		if (text != null)
		{
			return FluentIcons.Create(text, 18.0, ink, filled: false);
		}
		return BuddyToolIcons.Resolve(id, 18.0, ink);
	}

	private void RebuildNavTabs()
	{
		if (_navTabsPanel == null)
		{
			return;
		}
		string text = (from b in _navTabsPanel.Children.OfType<System.Windows.Controls.Button>()
			where b.IsKeyboardFocusWithin
			select (string)b.Tag).FirstOrDefault();
		Dictionary<string, System.Windows.Point> dictionary = (from b in _navTabsPanel.Children.OfType<System.Windows.Controls.Button>()
			where b.IsVisible
			select b).ToDictionary((System.Windows.Controls.Button b) => (string)b.Tag, (System.Windows.Controls.Button b) => b.TranslatePoint(default(System.Windows.Point), _expandedContent));
		_navTabsPanel.Children.Clear();
		_primaryToolIds.Clear();
		_dockButtons.Clear();
		_moreIconRotateTransform = null;
		List<IBuddyTool> list = ToolRegistry.Instance.DockTools.Where(IsDockTab).ToList();
		double num = 716.0;
		double num2 = (double)(list.Count + 1) * 36.0 + 17.0;
		DockSlotGap = ((list.Count > 1) ? Math.Max(4.0, Math.Min(10.0, Math.Floor((num - num2) / (double)(list.Count - 1)))) : 4.0);
		for (int i = 0; i < list.Count; i++)
		{
			IBuddyTool tool = list[i];
			_primaryToolIds.Add(tool.Id);
			System.Windows.Controls.Button tabBtn = CreateDockButton(tool.Id, tool.DisplayName, i);
			if (i < list.Count - 1)
			{
				tabBtn.Margin = new Thickness(0.0, 0.0, DockSlotGap, 0.0);
			}
			_dockButtons[tool.Id] = tabBtn;
			_navTabsPanel.Children.Add(tabBtn);
			if (!NotchEditing.IsEditing)
			{
				continue;
			}
			Grid grid = new Grid();
			grid.ClipToBounds = false;
			Grid grid2 = grid;
			tabBtn.Content = grid2;
			grid2.Children.Add(new Border
			{
				CornerRadius = new CornerRadius(10.0),
				Background = ThemeTokens.Selection,
				Opacity = 0.35,
				IsHitTestVisible = false
			});
			grid2.Children.Add(new BuddyDockSlot(tool.Id, tool.DisplayName, (System.Windows.Media.Brush ink) => CreateDockIcon(tool.Id, ink)));
			if (tool.Id != "home")
			{
				Border border = EditGesture.Badge(tool.DisplayName, delegate
				{
					RemoveDock(tool.Id, tabBtn);
				});
				border.Margin = new Thickness(3.0, 2.0, 0.0, 0.0);
				grid2.Children.Add(border);
			}
			new EditGesture(tabBtn, _navTabsPanel, tool.Id, () => (from t in ToolRegistry.Instance.EnabledTools.Where(IsDockTab)
				select t.Id).ToList(), delegate(List<string> ids)
			{
				ToolRegistry.Instance.SetDockOrder(ids);
			}, (tool.Id == "home") ? null : ((Action)delegate
			{
				RemoveDock(tool.Id, tabBtn);
			}));
		}
		_navTabsPanel.Children.Add(new Border
		{
			Width = 1.0,
			Height = 16.0,
			Background = ThemeTokens.PanelRule,
			Margin = new Thickness(8.0, 0.0, 8.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		});
		_moreBtn = CreateDockButton("more", null, -1);
		_moreBtn.Click += delegate
		{
			ToggleMoreMenu();
		};
		_navTabsPanel.Children.Add(_moreBtn);
		UpdateTabSelectionStyles(anim: false);
		if (_toolLauncher != null)
		{
			_toolLauncher.PopulateTools();
		}
		if (NotchEditing.IsEditing && text != null && _dockButtons.TryGetValue(text, out var value))
		{
			value.Focus();
		}
		_navTabsPanel.UpdateLayout();
		foreach (System.Windows.Controls.Button item in _navTabsPanel.Children.OfType<System.Windows.Controls.Button>())
		{
			if (dictionary.TryGetValue((string)item.Tag, out var value2))
			{
				System.Windows.Point point = item.TranslatePoint(default(System.Windows.Point), _expandedContent);
				TranslateTransform translateTransform = (TranslateTransform)((!(item.RenderTransform is TransformGroup transformGroup)) ? (item.RenderTransform = new TranslateTransform()) : transformGroup.Children.OfType<TranslateTransform>().Last());
				translateTransform.X = value2.X - point.X;
				CapsuleMotion.AnimateDouble(translateTransform, TranslateTransform.XProperty, 0.0, 0.18, CapsuleMotion.Spring);
			}
		}
	}

	private bool IsDockButtonActive(string id)
	{
		bool flag = _toolLauncher != null && _toolLauncher.IsLauncherOpen;
		if (string.Equals(id, "more", StringComparison.OrdinalIgnoreCase))
		{
			if (!flag)
			{
				return !_primaryToolIds.Any((string tid) => string.Equals(tid, ToolRouter.Instance.ActiveToolId, StringComparison.OrdinalIgnoreCase));
			}
			return true;
		}
		if (!flag)
		{
			return string.Equals(id, ToolRouter.Instance.ActiveToolId, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private System.Windows.Controls.Button CreateDockButton(string id, string label, int slotIndex)
	{
		string slotId = id;
		BuddyDockSlot slot = new BuddyDockSlot(id, label, (System.Windows.Media.Brush ink) => CreateDockIcon(slotId, ink));
		ControlTemplate controlTemplate = new ControlTemplate(typeof(System.Windows.Controls.Button));
		controlTemplate.VisualTree = new FrameworkElementFactory(typeof(ContentPresenter));
		string text = ((label == null) ? "More tools" : ((slotIndex >= 0 && slotIndex < 9) ? (label + "  Alt+" + (slotIndex + 1)) : label));
		System.Windows.Controls.Button btn = new System.Windows.Controls.Button
		{
			Tag = id,
			Content = slot,
			Height = 36.0,
			Margin = new Thickness(0.0),
			Background = System.Windows.Media.Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			Cursor = System.Windows.Input.Cursors.Hand,
			Focusable = true,
			FocusVisualStyle = null,
			Template = controlTemplate,
			ToolTip = BuddyTooltip.Create(text, 12.0, pill: true)
		};
		ToolTipService.SetInitialShowDelay(btn, 400);
		if (id == "drop" || id == "shelf")
		{
			btn.ToolTip = BuddyTooltip.Create((id == "drop") ? "Send to phone · drop files or text here" : "File Shelf · drop files, images or text here");
			btn.AllowDrop = true;
			btn.DragOver += delegate(object s, System.Windows.DragEventArgs e)
			{
				if (ClipboardDrag.CanDrop(e.Data))
				{
					e.Effects = System.Windows.DragDropEffects.Copy;
					e.Handled = true;
				}
			};
			btn.Drop += async delegate(object s, System.Windows.DragEventArgs e)
			{
				if (ClipboardDrag.CanDrop(e.Data))
				{
					e.Handled = true;
					if (id == "drop")
					{
						ClipboardDrag.ToDrop(e.Data);
					}
					else
					{
						ToolRouter.Instance.RouteTo("shelf");
						await ShelfDataImport.ImportAsync(e.Data, FileShelfService.Instance, null);
					}
				}
			};
		}
		ToolTipService.SetPlacement(btn, PlacementMode.Top);
		btn.GotKeyboardFocus += delegate
		{
			slot.SetFocused(focused: true);
		};
		btn.LostKeyboardFocus += delegate
		{
			slot.SetFocused(focused: false);
		};
		btn.PreviewKeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e)
		{
			if (!NotchEditing.IsEditing || !(id != "more"))
			{
				List<System.Windows.Controls.Button> list = _navTabsPanel.Children.OfType<System.Windows.Controls.Button>().ToList();
				int count = list.Count;
				if (count != 0)
				{
					int num = list.IndexOf(btn);
					if (e.Key == Key.Left)
					{
						list[(num - 1 + count) % count].Focus();
						e.Handled = true;
					}
					else if (e.Key == Key.Right)
					{
						list[(num + 1) % count].Focus();
						e.Handled = true;
					}
					else if (e.Key == Key.Home)
					{
						list[0].Focus();
						e.Handled = true;
					}
					else if (e.Key == Key.End)
					{
						list[count - 1].Focus();
						e.Handled = true;
					}
					else if (e.Key == Key.Return || e.Key == Key.Space)
					{
						btn.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
						e.Handled = true;
					}
				}
			}
		};
		if (id != "more")
		{
			string toolId = id;
			btn.Click += delegate
			{
				if (!NotchEditing.IsEditing)
				{
					if (_toolLauncher != null && _toolLauncher.IsLauncherOpen)
					{
						_toolLauncher.BeginClose();
					}
					ToolRouter.Instance.RouteTo(toolId);
				}
			};
		}
		return btn;
	}

	private void RefreshDockButtonIcon(System.Windows.Controls.Button btn, System.Windows.Media.Brush inactiveInk)
	{
		((btn != null) ? (btn.Content as BuddyDockSlot) : null)?.SetInk();
	}

	private void ToggleMoreMenu()
	{
		if (_toolLauncher != null && !_toolLauncher.IsLauncherOpen)
		{
			bool flag = NotchEditing.IsEditing && ToolRouter.Instance.ActiveToolId == "home";
			_toolLauncher.SetWidgetMode(flag);
			if (flag)
			{
				NotchEditing.AddSlot = Array.IndexOf(NotchEditing.Slots, "");
			}
		}
		if (_toolLauncher != null)
		{
			_toolLauncher.Toggle();
		}
	}

	private void AnimateMoreButtonRotation(double targetAngle)
	{
		if (_moreBtn != null)
		{
			RefreshDockButtonIcon(_moreBtn, BuddyTheme.Text50);
		}
	}

	private void ScheduleDockSettle(double ms)
	{
		if (_dockSettle == null)
		{
			_dockSettle = new DispatcherTimer(DispatcherPriority.Normal);
			_dockSettle.Tick += delegate
			{
				_dockSettle.Stop();
				try
				{
					UpdateTabSelectionStyles(anim: false);
				}
				catch
				{
				}
			};
		}
		_dockSettle.Stop();
		_dockSettle.Interval = TimeSpan.FromMilliseconds(Math.Max(60.0, ms));
		_dockSettle.Start();
	}

	internal static double SnapToDevicePixels(Visual v, double dip)
	{
		double num = 1.0;
		try
		{
			num = VisualTreeHelper.GetDpi(v).DpiScaleX;
		}
		catch
		{
		}
		if (num <= 0.0 || double.IsNaN(num))
		{
			num = 1.0;
		}
		return Math.Round(dip * num) / num;
	}

	private void UpdateTabSelectionStyles(bool anim = true)
	{
		if (_navTabsPanel == null)
		{
			return;
		}
		Duration? duration = ((anim && ToolRegistry.Instance.AnimationsEnabled && CapsuleMotion.AnimationsEnabled && _state == CapsuleDisplayState.Expanded) ? new Duration?(BuddyTheme.Slide) : null);
		double num = 0.0;
		double dip = 0.0;
		double num2 = 36.0;
		bool flag = false;
		foreach (UIElement child in _navTabsPanel.Children)
		{
			if (!(child is System.Windows.Controls.Button button))
			{
				num += 17.0 - DockSlotGap;
			}
			else if (button.Content is BuddyDockSlot buddyDockSlot)
			{
				bool flag2 = IsDockButtonActive(button.Tag as string);
				buddyDockSlot.SetActive(flag2, duration);
				buddyDockSlot.SetInk();
				if (flag2 && !flag)
				{
					flag = true;
					dip = num;
					num2 = buddyDockSlot.ActiveWidth;
				}
				num += 36.0 + DockSlotGap;
			}
		}
		if (_dockIndicatorTransform != null && _dockIndicator != null)
		{
			dip = SnapToDevicePixels(_dockIndicator, dip);
			_dockIndicator.Opacity = (flag ? 1.0 : 0.0);
			if (duration.HasValue)
			{
				ScheduleDockSettle(duration.Value.TimeSpan.TotalMilliseconds + 150.0);
			}
			if (duration.HasValue)
			{
				_dockIndicatorTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(dip, duration.Value)
				{
					EasingFunction = CapsuleMotion.Spring
				});
				_dockIndicator.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(num2, duration.Value)
				{
					EasingFunction = CapsuleMotion.Spring
				});
			}
			else
			{
				_dockIndicatorTransform.BeginAnimation(TranslateTransform.XProperty, null);
				_dockIndicatorTransform = new TranslateTransform(dip, 0.0);
				_dockIndicator.RenderTransform = _dockIndicatorTransform;
				_dockIndicator.BeginAnimation(FrameworkElement.WidthProperty, null);
				_dockIndicator.Width = num2;
			}
		}
	}

	public Rect GetHitRectDip()
	{
		if (_capsuleCard == null || _rootGrid == null || base.Visibility != 0 || _machine == null)
		{
			return Rect.Empty;
		}
		NotchPhase phase = _machine.Phase;
		int num;
		switch (phase)
		{
		case NotchPhase.Hidden:
			return Rect.Empty;
		default:
			num = ((phase == NotchPhase.Peeking) ? 1 : 0);
			break;
		case NotchPhase.Resting:
		case NotchPhase.Transient:
			num = 1;
			break;
		}
		bool flag = (byte)num != 0;
		if (flag && _shownPresence == NotchPresence.Line && _lineLayer != null)
		{
			System.Windows.Size size = CollapsedHitSize();
			double width = size.Width;
			System.Windows.Point point = _rootGrid.TranslatePoint(new System.Windows.Point((_rootGrid.ActualWidth - width) / 2.0, 0.0), this);
			return new Rect(point.X, point.Y, width, size.Height);
		}
		if (flag && _shownPresence == NotchPresence.None)
		{
			return Rect.Empty;
		}
		double width2 = ((_capsuleCard.ActualWidth > 0.0) ? _capsuleCard.ActualWidth : _capsuleCard.Width);
		double height = ((_capsuleCard.ActualHeight > 0.0) ? _capsuleCard.ActualHeight : _capsuleCard.Height);
		System.Windows.Point point2 = _capsuleCard.TranslatePoint(new System.Windows.Point(0.0, 0.0), this);
		Rect result = new Rect(point2.X, point2.Y, width2, height);
		double num2 = (flag ? 4.0 : 8.0);
		result.Inflate(num2, num2);
		return result;
	}

	private bool HitTestScreenPoint(int x, int y, bool includeOwnPopups)
	{
		try
		{
			Rect hitRectDip = GetHitRectDip();
			if (!hitRectDip.IsEmpty)
			{
				System.Windows.Point point = PointFromScreen(new System.Windows.Point(x, y));
				if (hitRectDip.Contains(point))
				{
					return true;
				}
			}
			if (includeOwnPopups && _hwnd != IntPtr.Zero && (_state == CapsuleDisplayState.Expanded || _state == CapsuleDisplayState.Expanding))
			{
				POINT p = default(POINT);
				p.X = x;
				p.Y = y;
				IntPtr intPtr = WindowFromPoint(p);
				if (intPtr != IntPtr.Zero && intPtr != _hwnd && GetAncestor(intPtr, 3u) == _hwnd)
				{
					return true;
				}
			}
		}
		catch
		{
		}
		return false;
	}

	[DllImport("user32.dll")]
	private static extern IntPtr WindowFromPoint(POINT p);

	[DllImport("user32.dll")]
	private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

	private bool IsMouseNearCapsule()
	{
		return IsPointerInside();
	}

	public bool IsPointerInside()
	{
		Func<System.Windows.Point?> cursorOverride = CursorOverride;
		if (cursorOverride != null)
		{
			System.Windows.Point? point = cursorOverride();
			if (point.HasValue)
			{
				return HitTestScreenPoint((int)point.Value.X, (int)point.Value.Y, includeOwnPopups: true);
			}
			return false;
		}
		if (!GetCursorPos(out var lpPoint))
		{
			return false;
		}
		return HitTestScreenPoint(lpPoint.X, lpPoint.Y, includeOwnPopups: true);
	}

	private System.Windows.Size GetActiveCollapsedSize(PillActivitySnapshot snap)
	{
		if (OfferCanDisplay(snap))
		{
			return MessageSize(_presentationOffer.ToSnapshot());
		}
		NotchSettings instance = NotchSettings.Instance;
		System.Windows.Size restingSize = instance.RestingSize;
		double num = restingSize.Width;
		double num2 = restingSize.Height;
		if (snap != null)
		{
			if (UsesMoment(snap) || UsesStatusPill(snap) || UsesMessageCard(snap) || PillActivityArbiter.IsCalendarReminder(snap))
			{
				System.Windows.Size size = MessageSize(snap);
				num = size.Width;
				num2 = size.Height;
			}
			else if (snap.Type == ActivityType.CompanionMessage)
			{
				System.Windows.Size size2 = MessageRenderer.MeasureDimensions(snap.Title, snap.Payload as MessageSettings);
				num = size2.Width;
				num2 = size2.Height;
			}
			else if (snap.Type == ActivityType.Media)
			{
				System.Windows.Size size3 = MediaCardSize(MediaWideWanted);
				num = size3.Width;
				num2 = size3.Height;
			}
			else if (PillActivityArbiter.IsCalendarReminder(snap))
			{
				System.Windows.Size size4 = CalendarReminderCardSize();
				num = size4.Width;
				num2 = size4.Height;
			}
			else if (ReminderDelivery.IsReminder(snap.Type))
			{
				System.Windows.Size size5 = ReminderCardSize();
				num = size5.Width;
				num2 = size5.Height;
			}
			else
			{
				CollapsedActivityProfile profile = ((snap.Profile != 0 || snap.Type == ActivityType.RestingClock) ? snap.Profile : NotchProfileMetrics.FromActivityType(snap.Type));
				num = instance.ScaledProfileWidth(NotchProfileMetrics.Get(profile).TargetWidth);
				if (snap.Type == ActivityType.RestingClock || snap.Type == ActivityType.Clock)
				{
					num = Math.Max(num, _restingFitWidth);
				}
			}
		}
		if (snap == null || snap.Type == ActivityType.RestingClock || snap.Type == ActivityType.Clock)
		{
			num = Math.Max(num, _restingFitWidth);
		}
		if (EyesModeOn && (snap == null || snap.Type == ActivityType.RestingClock || snap.Type == ActivityType.Clock))
		{
			num = EyesRestingWidth(restingSize);
		}
		else if (snap.Type == ActivityType.TimerRunning && _activeOwner.VisualType == CollapsedVisualType.Timer && _activeOwner.VisualInstance != null)
		{
			_activeOwner.VisualInstance.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
			num = Math.Ceiling(Math.Max(num, _activeOwner.VisualInstance.DesiredSize.Width));
		}
		if (PetPreferences.Instance.Enabled && GardenResting(snap) && _petOutside != null)
		{
			if (_petOutside.DialogueExpands)
			{
				num = Math.Max(num, _petOutside.DialogueWidth);
			}
			num2 = Math.Max(num2, _petOutside.DialogueHeight + ((NotchConfig.Instance.StyleFamily == "Island") ? 5.0 : 0.0));
		}
		return new System.Windows.Size(num, num2);
	}

	private void RefreshRestingPresentation(bool rebuild, bool animate)
	{
		_restingAppearanceDirty |= rebuild;
		rebuild = _restingAppearanceDirty;
		SetAnimationCache(on: false);
		ApplyRestingMetrics();
		RefreshLiveBadges();
		PillActivitySnapshot currentActivity = PillActivityArbiter.Instance.CurrentActivity;
		RenderResolvedSnapshot(null, currentActivity, isUpdateOnly: false, ActivityTransitionType.RestingInstant, rebuild);
		if (IsRestingSurface || _state == CapsuleDisplayState.Collapsing)
		{
			_restingAppearanceDirty = false;
		}
		if (_capsuleCard != null)
		{
			_capsuleCard.UpdateLayout();
		}
		if (rebuild)
		{
			_restingPlan = null;
			RefreshRestingFit();
		}
		System.Windows.Size activeCollapsedSize = GetActiveCollapsedSize(currentActivity);
		NotchConfig instance = NotchConfig.Instance;
		EnsureMorphGeometryCache(activeCollapsedSize.Width, activeCollapsedSize.Height, instance.Style, instance.CornerRadius);
		if (_edgeLight != null)
		{
			_edgeLight.SetOutline(instance.Style, activeCollapsedSize.Width, activeCollapsedSize.Height, instance.CornerRadius);
		}
		if (_capsuleCard != null && (IsRestingSurface || _state == CapsuleDisplayState.Suppressed))
		{
			if (_collapsedContent != null)
			{
				_collapsedContent.Width = double.NaN;
				_collapsedContent.Height = double.NaN;
				_collapsedContent.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
				_collapsedContent.VerticalAlignment = VerticalAlignment.Stretch;
			}
			ResizeRestingSurface(activeCollapsedSize, animate);
			UpdateNotchSilhouette(_capsuleCard.Width, _capsuleCard.Height);
		}
		if (_edgeLight != null)
		{
			_edgeLight.Refresh();
		}
		UpdateBeat();
	}

	private int NextCardWidthGeneration()
	{
		_restingResizeOwner = 0;
		if (++_cardWidthGeneration == 0)
		{
			_cardWidthGeneration = 1;
		}
		return _cardWidthGeneration;
	}

	private void AnimateCardWidth(double width, double seconds, IEasingFunction easing, Action completed)
	{
		NextCardWidthGeneration();
		CapsuleMotion.AnimateDouble(_capsuleCard, FrameworkElement.WidthProperty, width, seconds, easing, completed);
	}

	private void StopCardWidthAnimation()
	{
		NextCardWidthGeneration();
		_capsuleCard.BeginAnimation(FrameworkElement.WidthProperty, null);
	}

	private void ResizeRestingSurface(System.Windows.Size size, bool animate)
	{
		_lastCapsuleTargetWidth = size.Width;
		double num = ((animate && NotchSettings.Instance.CloseSeconds > 0.0) ? 0.28 : 0.0);
		int generation = ++_restingResizeGeneration;
		int owner = NextCardWidthGeneration();
		if (num > 0.0)
		{
			_restingResizeOwner = owner;
			_restingResizeDeadline = Stopwatch.GetTimestamp() + (long)((num + 0.5) * (double)Stopwatch.Frequency);
		}
		if (_collapsedContent != null && num > 0.0)
		{
			_collapsedContent.Width = size.Width;
			_collapsedContent.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
			_collapsedContent.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
			_collapsedContent.RenderTransform = Transform.Identity;
		}
		CapsuleMotion.AnimateDouble(_capsuleCard, FrameworkElement.WidthProperty, size.Width, num, CapsuleMotion.EaseOutCubic, delegate
		{
			if (_restingResizeOwner == owner)
			{
				_restingResizeOwner = 0;
			}
			if (generation == _restingResizeGeneration && _collapsedContent != null)
			{
				_collapsedContent.RenderTransform = Transform.Identity;
				_collapsedContent.Width = double.NaN;
				_collapsedContent.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
			}
			SettleRestingShape();
			SettlePetStage();
		});
		CapsuleMotion.AnimateDouble(_capsuleCard, FrameworkElement.HeightProperty, size.Height, num, CapsuleMotion.EaseOutCubic);
	}

	private void SettleRestingShape()
	{
		if (_capsuleCard == null || _restingResizeActive || !IsRestingSurface || MorphProgress > 0.001)
		{
			return;
		}
		try
		{
			UpdateMorphState(0.0);
			UpdateNotchSilhouette(_capsuleCard.Width, _capsuleCard.Height);
			if (_edgeLight != null)
			{
				_edgeLight.Refresh();
			}
			InvalidateBeatLayout();
		}
		catch (Exception ex)
		{
			NotchLog.Error("Settling the resting notch", ex);
		}
	}

	private void EnsureMorphGeometryCache(double collapsedW, double collapsedH, string style, double cornerRadius)
	{
		if (_cachedMorphCollapsedW != collapsedW || _cachedMorphCollapsedH != collapsedH || !(_cachedMorphStyle == style) || _cachedMorphCornerRadius != cornerRadius || _cachedRestingFillGeometry == null)
		{
			_cachedMorphCollapsedW = collapsedW;
			_cachedMorphCollapsedH = collapsedH;
			_cachedMorphStyle = style;
			_cachedMorphCornerRadius = cornerRadius;
			_cachedRestingFillGeometry = NotchGeometryBuilder.BuildGeometry(style, collapsedW, collapsedH, cornerRadius);
			_cachedRestingFillGeometry.Freeze();
			_cachedRestingOutlineGeometry = NotchGeometryBuilder.BuildSilhouetteGeometry(style, collapsedW, collapsedH, cornerRadius);
			_cachedRestingOutlineGeometry.Freeze();
		}
	}

	private void BuildMorphLayers(System.Windows.Media.Brush fill)
	{
		_morphRect = new RectangleGeometry(new Rect(0.0, 0.0, 1.0, 1.0), 16.0, 16.0);
		Geometry earGeom = Geometry.Parse("M0,0 C5.4,0 12,4.5 12,10 L12,0 Z");
		earGeom.Freeze();
		Func<bool, System.Windows.Shapes.Path> func = delegate(bool mirrored)
		{
			ScaleTransform scaleTransform = new ScaleTransform((!mirrored) ? 1 : (-1), 1.0);
			TranslateTransform translateTransform = new TranslateTransform();
			TransformGroup renderTransform = new TransformGroup
			{
				Children = 
				{
					(Transform)scaleTransform,
					(Transform)translateTransform
				}
			};
			System.Windows.Shapes.Path result = new System.Windows.Shapes.Path
			{
				Data = earGeom,
				Fill = fill,
				IsHitTestVisible = false,
				Visibility = Visibility.Collapsed,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Top,
				RenderTransform = renderTransform,
				SnapsToDevicePixels = false
			};
			if (mirrored)
			{
				_earRightScale = scaleTransform;
				_earRightShift = translateTransform;
			}
			else
			{
				_earLeftScale = scaleTransform;
				_earLeftShift = translateTransform;
			}
			return result;
		};
		_earLeft = func(arg: false);
		_earRight = func(arg: true);
	}

	private void SetMorphShape(double collapsedW, double collapsedH, double t, double restRadius = 16.0, bool restEars = true, double wingScale = 1.0)
	{
		double num = collapsedW + (752.0 - collapsedW) * t;
		double num2 = collapsedH + (352.0 - collapsedH) * t;
		double num3 = (752.0 - num) / 2.0;
		double num4 = (restEars ? Math.Pow(1.0 - t, 1.5) : 0.0);
		double num5 = ((wingScale < 0.999) ? 0.15 : 0.08);
		double num6 = Math.Min(12.0 * wingScale * num4, num * num5);
		double num7 = Math.Min(10.0 * wingScale * num4, num2 * 0.35);
		double val = restRadius + (28.0 - restRadius) * t;
		val = Math.Max(0.0, Math.Min(val, Math.Min((num - 2.0 * num6) / 2.0, num2 - num7)));
		_morphRect.Rect = new Rect(num3 + num6, 0.0 - val, Math.Max(0.0, num - 2.0 * num6), num2 + val);
		_morphRect.RadiusX = val;
		_morphRect.RadiusY = val;
		bool flag = num6 > 0.15 && num7 > 0.15;
		Visibility visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		if (_earLeft.Visibility != visibility)
		{
			_earLeft.Visibility = visibility;
			_earRight.Visibility = visibility;
		}
		if (flag)
		{
			double num8 = num6 / 12.0;
			double scaleY = num7 / 10.0;
			_earLeftScale.ScaleX = num8;
			_earLeftScale.ScaleY = scaleY;
			_earLeftShift.X = num3;
			_earRightScale.ScaleX = 0.0 - num8;
			_earRightScale.ScaleY = scaleY;
			_earRightShift.X = num3 + num;
		}
	}

	private System.Windows.Media.Brush PanelFill(NotchConfig cfg)
	{
		_panelAlpha = byte.MaxValue;
		_closedNotchOpacity = cfg.ClosedNotchOpacity;
		if (_petOutside != null)
		{
			_petOutside.NotchOpacity = _closedNotchOpacity;
		}
		if (_miniOpacityFill == null)
		{
			_miniOpacityFill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(byte.MaxValue, 0, 0, 0));
		}
		_miniOpacityFill.Color = System.Windows.Media.Color.FromArgb((byte)Math.Round(255.0 * _closedNotchOpacity), 0, 0, 0);
		if (_miniShape != null)
		{
			_miniShape.Fill = _miniOpacityFill;
		}
		if (_lineShape != null)
		{
			_lineShape.Fill = _miniOpacityFill;
		}
		if (_panelFill == null)
		{
			_panelFill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(_panelAlpha, 0, 0, 0));
			ThemeTokens.Subscribe(this, delegate(CapsuleWindow w)
			{
				w.OnThemeChangedPanel();
			});
		}
		UpdatePanelFill(MorphProgress);
		return _panelFill;
	}

	private System.Windows.Media.Color PanelColorAt(double t)
	{
		System.Windows.Media.Color color = ThemeTokens.Target(ThemeTokens.Bg);
		System.Windows.Media.Color notchBlack = ThemeTokens.NotchBlack;
		double num = Math.Max(0.0, Math.Min(1.0, t / 0.7));
		return System.Windows.Media.Color.FromArgb(_panelAlpha, (byte)Math.Round((double)(int)notchBlack.R + (double)(color.R - notchBlack.R) * num), (byte)Math.Round((double)(int)notchBlack.G + (double)(color.G - notchBlack.G) * num), (byte)Math.Round((double)(int)notchBlack.B + (double)(color.B - notchBlack.B) * num));
	}

	private void UpdatePanelFill(double t)
	{
		double num = ((t <= 0.001) ? _closedNotchOpacity : 1.0);
		if (_notchBackgroundLayer != null && _notchBackgroundLayer.Opacity != num)
		{
			_notchBackgroundLayer.Opacity = num;
		}
		if (_panelFill != null)
		{
			System.Windows.Media.Color color = PanelColorAt(t);
			if (!(_panelFill.Color == color) || _panelFill.HasAnimatedProperties)
			{
				_panelFill.BeginAnimation(SolidColorBrush.ColorProperty, null);
				_panelFill.Color = color;
			}
		}
	}

	private void OnThemeChangedPanel()
	{
		if (_panelFill != null)
		{
			System.Windows.Media.Color color = _panelFill.Color;
			System.Windows.Media.Color color2 = PanelColorAt(MorphProgress);
			_panelFill.BeginAnimation(SolidColorBrush.ColorProperty, null);
			_panelFill.Color = color2;
			if (MorphProgress >= 0.999 && color != color2 && CapsuleMotion.AnimationsEnabled)
			{
				_panelFill.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(color, color2, ThemeTokens.Transition)
				{
					FillBehavior = FillBehavior.Stop
				});
			}
		}
	}

	public void UpdateMorphState(double progress)
	{
		progress = Math.Max(0.0, Math.Min(1.0, progress));
		UpdatePanelFill(progress);
		if (progress <= 0.001 || _cachedRestingFillGeometry == null)
		{
			PillActivitySnapshot currentActivity = PillActivityArbiter.Instance.CurrentActivity;
			System.Windows.Size activeCollapsedSize = GetActiveCollapsedSize(currentActivity);
			string style = ((NotchConfig.Instance != null) ? NotchConfig.Instance.Style : "Default");
			double cornerRadius = ((NotchConfig.Instance != null) ? NotchConfig.Instance.CornerRadius : 16.0);
			EnsureMorphGeometryCache(activeCollapsedSize.Width, activeCollapsedSize.Height, style, cornerRadius);
		}
		double w = _cachedMorphCollapsedW;
		double h = _cachedMorphCollapsedH;
		bool morphRestLine = _morphRestLine;
		double radius = 16.0;
		double scale = 1.0;
		if (morphRestLine)
		{
			CollapsedLookGeometry(out w, out h, out radius, out scale);
		}
		if (_capsuleCard != null)
		{
			if (progress > 0.001 || morphRestLine || _morphCardPinned)
			{
				PinCardForMorph();
			}
			else
			{
				if (_capsuleCard.Width != w)
				{
					_capsuleCard.Width = w;
				}
				if (_capsuleCard.Height != h)
				{
					_capsuleCard.Height = h;
				}
				if (_collapsedContent != null && !double.IsNaN(_collapsedContent.Width))
				{
					_collapsedContent.Width = double.NaN;
					_collapsedContent.Height = double.NaN;
					_collapsedContent.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
					_collapsedContent.VerticalAlignment = VerticalAlignment.Stretch;
				}
			}
		}
		bool flag = progress <= 0.001;
		bool flag2 = progress >= 0.999;
		Geometry geometry;
		Geometry geometry2;
		if (flag && !morphRestLine)
		{
			geometry = _cachedRestingFillGeometry;
			geometry2 = _cachedRestingOutlineGeometry;
			if (_morphCardPinned && 752.0 > w + 0.5)
			{
				double dx = (752.0 - w) / 2.0;
				geometry = ShiftedRestingGeometry(geometry, dx);
				geometry2 = ShiftedRestingGeometry(geometry2, dx);
			}
			if (_earLeft != null && _earLeft.Visibility == Visibility.Visible)
			{
				_earLeft.Visibility = Visibility.Collapsed;
				_earRight.Visibility = Visibility.Collapsed;
			}
		}
		else
		{
			SetMorphShape(w, h, progress, radius, restEars: true, morphRestLine ? scale : 1.0);
			geometry = _morphRect;
			geometry2 = _morphRect;
		}
		if (_notchSilhouettePath != null)
		{
			if (_notchSilhouettePath.Data != geometry)
			{
				_notchSilhouettePath.Data = geometry;
			}
			if (_sharedNotchShadow != null)
			{
				if (flag2)
				{
					_sharedNotchShadow.Opacity = _shadowOpacity;
					if (_notchSilhouettePath.Effect != _sharedNotchShadow)
					{
						_notchSilhouettePath.Effect = _sharedNotchShadow;
					}
				}
				else if (_notchSilhouettePath.Effect != null)
				{
					_notchSilhouettePath.Effect = null;
				}
			}
		}
		if (_notchBackdropPath != null)
		{
			bool flag3 = _backdropBlurWanted && flag2;
			if (flag3)
			{
				_notchBackdropPath.Data = geometry;
			}
			Visibility visibility = ((!flag3) ? Visibility.Collapsed : Visibility.Visible);
			if (_notchBackdropPath.Visibility != visibility)
			{
				_notchBackdropPath.Visibility = visibility;
			}
		}
		if (_notchOutlinePath != null)
		{
			if (_notchOutlinePath.Data != geometry2)
			{
				_notchOutlinePath.Data = geometry2;
			}
			System.Windows.Media.Brush brush = ((progress < 0.5 && !morphRestLine) ? _outlineBrush : null);
			if (_notchOutlinePath.Stroke != brush)
			{
				_notchOutlinePath.Stroke = brush;
			}
		}
		if (_contentContainer != null && _contentContainer.Clip != geometry)
		{
			_contentContainer.Clip = geometry;
		}
		if (_edgeLight != null)
		{
			bool resting = flag && IsRestingSurface;
			if (flag && !morphRestLine)
			{
				NotchConfig instance = NotchConfig.Instance;
				_edgeLight.SetOutline((instance != null) ? instance.Style : "Default", w, h, instance?.CornerRadius ?? (-1.0));
			}
			_edgeLight.SetResting(resting);
		}
		UpdateBeat();
	}

	public void ExpandCapsule(bool isFirstRun = false)
	{
		if (_machine != null)
		{
			_machine.RequestOpen("ExpandCapsule");
		}
	}

	public void CollapseCapsule()
	{
		if (_machine != null)
		{
			_machine.RequestClose("CollapseCapsule");
		}
	}

	public void ForceSafeCollapsed()
	{
		if (_machine != null)
		{
			_machine.Reset("ForceSafeCollapsed");
		}
		else
		{
			((INotchSurface)this).ResetVisuals(NotchPresence.Full);
		}
	}

	private void SetAnimationCache(bool on)
	{
		_restingResizeGeneration++;
		_restingResizeOwner = 0;
		if (_collapsedContent != null)
		{
			_collapsedContent.RenderTransform = Transform.Identity;
		}
		if (_expandedContent != null)
		{
			_expandedContent.CacheMode = null;
		}
		if (_collapsedContent != null)
		{
			_collapsedContent.CacheMode = null;
		}
	}

	void INotchSurface.AnimateOpen(int generation, bool fromRest)
	{
		if (fromRest)
		{
			_morphRestLine = _shownPresence == NotchPresence.Line;
			if (_morphRestLine)
			{
				ResetCardTuck();
				UpdateMorphState(MorphProgress);
			}
		}
		if (fromRest && _petOutside != null)
		{
			SuspendPetForPanel();
		}
		if (fromRest && _expandedContent != null && _expandedContent.Visibility != 0 && NotchSettings.Instance.OpenSeconds > 0.0)
		{
			_expandedContent.BeginAnimation(UIElement.OpacityProperty, null);
			_expandedContent.Opacity = 0.0;
			_expandedContent.Visibility = Visibility.Visible;
			if (!_morphRestLine)
			{
				_morphCardPinned = true;
				PinCardForMorph();
				UpdateMorphState(MorphProgress);
			}
			if (_collapsedContent != null)
			{
				_collapsedContent.IsHitTestVisible = false;
				_collapsedContent.BeginAnimation(UIElement.OpacityProperty, null);
				_collapsedContent.Opacity = 0.0;
			}
			int prepFrames = 0;
			EventHandler prepNext = null;
			DispatcherTimer prepFallback = new DispatcherTimer(DispatcherPriority.Render)
			{
				Interval = TimeSpan.FromMilliseconds(32.0)
			};
			Action begin = delegate
			{
				if (prepNext != null)
				{
					CompositionTarget.Rendering -= prepNext;
					prepNext = null;
					prepFallback.Stop();
					if (_machine != null && _machine.Generation == generation && _machine.Phase == NotchPhase.Opening)
					{
						((INotchSurface)this).AnimateOpen(generation, fromRest);
					}
					else if (_morphCardPinned)
					{
						_morphCardPinned = false;
						UpdateMorphState(MorphProgress);
					}
				}
			};
			prepNext = delegate
			{
				if (++prepFrames == 1)
				{
					base.Dispatcher.BeginInvoke(begin, DispatcherPriority.Normal);
				}
			};
			prepFallback.Tick += delegate
			{
				begin();
			};
			CompositionTarget.Rendering += prepNext;
			prepFallback.Start();
			return;
		}
		ToolRouter.Instance.EnsureUsableTab();
		NotchSettings instance = NotchSettings.Instance;
		bool flag = instance.OpenSeconds > 0.0;
		if (fromRest)
		{
			UpdatePosition(initial: false);
			CancelFirstRunHold();
			if (NotchBehaviorConfig.Instance.IdlePrivacyReset && _lastCollapsedUtc.HasValue && (DateTime.UtcNow - _lastCollapsedUtc.Value).TotalSeconds >= 60.0 && ToolRouter.Instance.ActiveTool != null)
			{
				string text = ToolRouter.Instance.ActiveTool.Id ?? "";
				if (text.IndexOf("clip", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("note", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					ToolRouter.Instance.RouteTo("home");
				}
			}
			if (_textScrollController != null)
			{
				_textScrollController.Stop();
			}
			if (_welcomeActivityStack != null)
			{
				_welcomeActivityStack.Visibility = Visibility.Collapsed;
			}
			if (_activityViewport != null)
			{
				_activityViewport.Visibility = Visibility.Visible;
			}
		}
		if (fromRest)
		{
			_typedSinceOpen = false;
		}
		StopCardWidthAnimation();
		_capsuleCard.BeginAnimation(FrameworkElement.HeightProperty, null);
		PrepareCardForOpen(flag && !_morphRestLine);
		SetNonActivating(noActivate: false);
		if (flag)
		{
			SetAnimationCache(on: true);
		}
		if (_collapsedContent != null)
		{
			_collapsedContent.IsHitTestVisible = false;
			_collapsedContent.Visibility = Visibility.Visible;
			CapsuleMotion.AnimateDouble(_collapsedContent, UIElement.OpacityProperty, 0.0, flag ? 0.08 : 0.0, CapsuleMotion.EaseOutCubic);
		}
		if (_expandedContent != null)
		{
			if (_expandedContentTranslate == null)
			{
				_expandedContentTranslate = new TranslateTransform();
				_expandedContent.RenderTransform = _expandedContentTranslate;
			}
			_expandedContent.Visibility = Visibility.Visible;
			_expandedContent.IsHitTestVisible = true;
			_expandedContent.BeginAnimation(UIElement.OpacityProperty, null);
			if (fromRest && flag)
			{
				_expandedContent.Opacity = 0.0;
				double openSeconds = instance.OpenSeconds;
				CapsuleMotion.AnimateDouble(_expandedContent, UIElement.OpacityProperty, 1.0, Math.Max(0.09, openSeconds * 0.45), CapsuleMotion.EaseOutCubic, null, TimeSpan.FromSeconds(openSeconds * 0.55));
			}
			else
			{
				_expandedContent.Opacity = 1.0;
			}
			_expandedContentTranslate.BeginAnimation(TranslateTransform.YProperty, null);
			_expandedContentTranslate.Y = 0.0;
		}
		if (_navDockBorder != null)
		{
			if (_dockTranslate == null)
			{
				_dockTranslate = new TranslateTransform();
				_navDockBorder.RenderTransform = _dockTranslate;
			}
			if (fromRest)
			{
				_navDockBorder.BeginAnimation(UIElement.OpacityProperty, null);
				_navDockBorder.Opacity = 0.0;
				_dockTranslate.BeginAnimation(TranslateTransform.YProperty, null);
				_dockTranslate.Y = 6.0;
			}
			TimeSpan? beginTime = ((flag && fromRest) ? new TimeSpan?(TimeSpan.FromMilliseconds(90.0)) : null);
			CapsuleMotion.AnimateDouble(_navDockBorder, UIElement.OpacityProperty, 1.0, flag ? 0.15 : 0.0, CapsuleMotion.EaseOutCubic, null, beginTime);
			CapsuleMotion.AnimateDouble(_dockTranslate, TranslateTransform.YProperty, 0.0, flag ? 0.15 : 0.0, CapsuleMotion.EaseOutCubic, null, beginTime);
		}
		StartOpenMorph(generation);
	}

	private void StartOpenMorph(int generation)
	{
		double morphProgress = MorphProgress;
		double durationSeconds = ((NotchSettings.Instance.OpenSeconds > 0.0) ? (NotchSettings.Instance.OpenSeconds * Math.Max(0.3, 1.0 - morphProgress)) : 0.0);
		CapsuleMotion.AnimateDouble(this, MorphProgressProperty, 1.0, durationSeconds, CapsuleMotion.Spring, delegate
		{
			OnOpenAnimationDone(generation);
		});
	}

	private void OnOpenAnimationDone(int generation)
	{
		if (_machine != null && generation == _machine.Generation)
		{
			_morphCardPinned = false;
			UpdateMorphState(1.0);
			SetAnimationCache(on: false);
			if (_expandedContent != null)
			{
				_expandedContent.Visibility = Visibility.Visible;
				_expandedContent.IsHitTestVisible = true;
			}
			if (FirstRunManager.CurrentState == FirstRunState.Started)
			{
				FirstRunManager.MarkCompleted();
			}
			_machine.OnAnimationCompleted(generation);
			ActivateToolAfterOpen();
		}
	}

	private void ActivateToolAfterOpen()
	{
		if (_toolActivated)
		{
			return;
		}
		long activationGeneration = ToolRouter.Instance.ActivationGeneration;
		if (_activationQueuedGeneration == activationGeneration)
		{
			return;
		}
		_activationQueuedGeneration = activationGeneration;
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			if (_activationQueuedGeneration == activationGeneration)
			{
				_activationQueuedGeneration = -1L;
			}
			if (_toolActivated || _machine == null || _machine.Phase != NotchPhase.Open || ToolRouter.Instance.ActiveTool == null || !ToolRouter.Instance.EnsureActiveViewBuilt() || activationGeneration != ToolRouter.Instance.ActivationGeneration)
			{
				return;
			}
			IBuddyTool activeTool = ToolRouter.Instance.ActiveTool;
			try
			{
				ToolRouter.Activate(activeTool);
				_toolActivated = _machine.Phase == NotchPhase.Open && ToolRouter.Instance.ActiveTool == activeTool;
				RecordInteractionState("activated");
			}
			catch (Exception ex)
			{
				TransactionLogger.Error("[CapsuleWindow] ActiveTool.OnActivated: " + ex.Message, ex);
			}
		}, DispatcherPriority.Loaded);
	}

	void INotchSurface.AnimateClose(int generation)
	{
		SettleViewportTransition();
		ToolRouter.Instance.InvalidateActivation();
		NotchSettings instance = NotchSettings.Instance;
		bool flag = instance.CloseSeconds > 0.0;
		bool intoLine = _machine != null && _machine.Presence == NotchPresence.Line;
		_morphRestLine = intoLine;
		CancelFirstRunHold();
		if (FirstRunManager.CurrentState == FirstRunState.Started)
		{
			FirstRunManager.MarkDismissed();
		}
		_typedSinceOpen = false;
		StopCardWidthAnimation();
		_capsuleCard.BeginAnimation(FrameworkElement.HeightProperty, null);
		if (flag)
		{
			SetAnimationCache(on: true);
		}
		if (_toolActivated)
		{
			_toolActivated = false;
			if (ToolRouter.Instance.ActiveTool != null)
			{
				try
				{
					ToolRouter.Instance.ActiveTool.OnDeactivated();
				}
				catch (Exception ex)
				{
					TransactionLogger.Error("[CapsuleWindow] ActiveTool.OnDeactivated: " + ex.Message, ex);
				}
			}
		}
		UpdateCollapsedClock(DateTime.Now);
		RefreshLiveBadges();
		RenderCurrentResolvedPillActivity(immediate: true);
		if (intoLine && _collapsedContent != null)
		{
			_collapsedContent.BeginAnimation(UIElement.OpacityProperty, null);
			_collapsedContent.Opacity = 0.0;
		}
		if (_navDockBorder != null)
		{
			CapsuleMotion.AnimateDouble(_navDockBorder, UIElement.OpacityProperty, 0.0, flag ? 0.06 : 0.0, CapsuleMotion.EaseOutCubic);
			if (_dockTranslate != null)
			{
				CapsuleMotion.AnimateDouble(_dockTranslate, TranslateTransform.YProperty, 4.0, flag ? 0.06 : 0.0, CapsuleMotion.EaseOutCubic);
			}
		}
		if (_expandedContent != null)
		{
			_expandedContent.IsHitTestVisible = false;
			CapsuleMotion.AnimateDouble(_expandedContent, UIElement.OpacityProperty, 0.0, flag ? 0.08 : 0.0, CapsuleMotion.EaseOutCubic);
		}
		if (flag && MorphProgress >= 0.999)
		{
			if (_notchSilhouettePath != null)
			{
				_notchSilhouettePath.Effect = null;
			}
			if (_notchBackdropPath != null)
			{
				_notchBackdropPath.Visibility = Visibility.Collapsed;
			}
			int prepFrames = 0;
			EventHandler prepNext = null;
			DispatcherTimer prepFallback = new DispatcherTimer(DispatcherPriority.Render)
			{
				Interval = TimeSpan.FromMilliseconds(32.0)
			};
			Action begin = delegate
			{
				if (prepNext != null)
				{
					CompositionTarget.Rendering -= prepNext;
					prepNext = null;
					prepFallback.Stop();
					if (_machine != null && _machine.Generation == generation && _machine.Phase == NotchPhase.Closing)
					{
						StartCloseMorph(generation, intoLine);
					}
					else
					{
						UpdateMorphState(MorphProgress);
					}
				}
			};
			prepNext = delegate
			{
				if (++prepFrames == 1)
				{
					base.Dispatcher.BeginInvoke(begin, DispatcherPriority.Normal);
				}
			};
			prepFallback.Tick += delegate
			{
				begin();
			};
			CompositionTarget.Rendering += prepNext;
			prepFallback.Start();
		}
		else
		{
			StartCloseMorph(generation, intoLine);
		}
	}

	private void StartCloseMorph(int generation, bool intoLine)
	{
		bool flag = NotchSettings.Instance.CloseSeconds > 0.0;
		double morphProgress = MorphProgress;
		double num = (flag ? (NotchSettings.Instance.CloseSeconds * Math.Max(0.3, morphProgress)) : 0.0);
		if (_collapsedContent != null && !intoLine)
		{
			_collapsedContent.Visibility = Visibility.Visible;
			TimeSpan? beginTime = (flag ? new TimeSpan?(TimeSpan.FromMilliseconds(num * 1000.0 * 0.6)) : null);
			CapsuleMotion.AnimateDouble(_collapsedContent, UIElement.OpacityProperty, 1.0, flag ? 0.1 : 0.0, CapsuleMotion.EaseOutCubic, null, beginTime);
		}
		CapsuleMotion.AnimateDouble(this, MorphProgressProperty, 0.0, num, CapsuleMotion.Spring, delegate
		{
			OnCloseAnimationDone(generation);
		});
	}

	private void OnCloseAnimationDone(int generation)
	{
		if (_machine != null && generation == _machine.Generation)
		{
			_lastCollapsedUtc = DateTime.UtcNow;
			if (_morphRestLine)
			{
				ApplyPresenceVisual(NotchPresence.Line, animate: false);
				ResetCardTuck();
				_morphRestLine = false;
			}
			SettleRestingVisuals();
			_machine.OnAnimationCompleted(generation);
		}
	}

	private void SettleRestingVisuals()
	{
		SchedulePetResume();
		SetAnimationCache(on: false);
		SetNonActivating(noActivate: true);
		_morphCardPinned = false;
		UpdateMorphState(0.0);
		SettleInFlightSlotTransition();
		if (_edgeLight != null)
		{
			_edgeLight.Refresh();
		}
		if (_welcomeActivityStack != null)
		{
			_welcomeActivityStack.Visibility = Visibility.Collapsed;
			if (_activityViewport != null)
			{
				_activityViewport.Visibility = Visibility.Visible;
			}
			if (_defaultCollapsedStack != null)
			{
				_defaultCollapsedStack.Visibility = Visibility.Visible;
			}
		}
		if (_expandedContent != null)
		{
			_expandedContent.BeginAnimation(UIElement.OpacityProperty, null);
			_expandedContent.Opacity = 0.0;
			_expandedContent.Visibility = Visibility.Hidden;
			_expandedContent.IsHitTestVisible = false;
		}
		if (_navDockBorder != null)
		{
			_navDockBorder.BeginAnimation(UIElement.OpacityProperty, null);
			_navDockBorder.Opacity = 0.0;
		}
		if (_collapsedContent != null)
		{
			_collapsedContent.BeginAnimation(UIElement.OpacityProperty, null);
			_collapsedContent.IsHitTestVisible = true;
			_collapsedContent.Visibility = Visibility.Visible;
			_collapsedContent.Opacity = 1.0;
		}
		if (_activityViewport != null)
		{
			_activityViewport.BeginAnimation(UIElement.OpacityProperty, null);
			_activityViewport.Visibility = Visibility.Visible;
			_activityViewport.Opacity = 1.0;
		}
		PresentRestingNotch("capsule closed", animate: false, rebuildEffects: false);
		EnforceSlotSettledInvariants("RestingSettled");
	}

	void INotchSurface.ResetVisuals(NotchPresence presence)
	{
		SettleViewportTransition();
		CancelFirstRunHold();
		BeginAnimation(MorphProgressProperty, null);
		_morphRestLine = false;
		MorphProgress = 0.0;
		if (_toolActivated)
		{
			_toolActivated = false;
			try
			{
				if (ToolRouter.Instance.ActiveTool != null)
				{
					ToolRouter.Instance.ActiveTool.OnDeactivated();
				}
			}
			catch
			{
			}
		}
		if (_capsuleCard != null)
		{
			StopCardWidthAnimation();
			_capsuleCard.BeginAnimation(FrameworkElement.HeightProperty, null);
		}
		if (_slotA != null)
		{
			_slotA.BeginAnimation(UIElement.OpacityProperty, null);
			if (_slotATranslate != null)
			{
				_slotATranslate.BeginAnimation(TranslateTransform.XProperty, null);
			}
		}
		if (_slotB != null)
		{
			_slotB.BeginAnimation(UIElement.OpacityProperty, null);
			if (_slotBTranslate != null)
			{
				_slotBTranslate.BeginAnimation(TranslateTransform.XProperty, null);
			}
		}
		ShowPeekVisual(on: false, animate: false);
		try
		{
			SettleRestingVisuals();
		}
		catch (Exception ex)
		{
			NotchLog.Error("Settling the resting notch during reset", ex);
		}
		UpdatePosition(initial: false);
		ApplyPresenceVisual(presence, animate: false);
		AssertTopmost();
	}

	void INotchSurface.ShowPeek(bool on)
	{
		ShowPeekVisual(on, animate: true);
	}

	void INotchSurface.ApplyPresence(NotchPresence presence, bool animate)
	{
		ApplyPresenceVisual(presence, animate);
	}

	bool INotchSurface.IsPointerInside()
	{
		if (!GardenBlocksHover && IsPointerInside())
		{
			return !HoldsActionCard();
		}
		return false;
	}

	bool INotchSurface.IsContentReady()
	{
		if (_petOutside != null && _petOutside.IsVisible && _petOutside.SceneName == "exit" && !_petOutside.HasExited)
		{
			return true;
		}
		if (_expandedContent == null || _viewportHost == null)
		{
			return false;
		}
		if (_expandedContent.Visibility != 0)
		{
			return false;
		}
		UIElement currentToolView = _currentToolView;
		if (currentToolView == null || !_viewportHost.Children.Contains(currentToolView))
		{
			return false;
		}
		if (currentToolView is FrameworkElement frameworkElement)
		{
			if (frameworkElement.IsVisible && frameworkElement.ActualWidth > 1.0)
			{
				return frameworkElement.ActualHeight > 1.0;
			}
			return false;
		}
		return true;
	}

	public bool IsTextInputActive()
	{
		if (!_typedSinceOpen || !base.IsActive)
		{
			return false;
		}
		return IsTextBoxFocused();
	}

	private bool IsTextBoxFocused()
	{
		DependencyObject dependencyObject = Keyboard.FocusedElement as DependencyObject;
		if (!(dependencyObject is System.Windows.Controls.Primitives.TextBoxBase) && !(dependencyObject is PasswordBox))
		{
			return false;
		}
		return Window.GetWindow(dependencyObject) == this;
	}

	private void NoteTyping(Key key)
	{
		if (!_typedSinceOpen && _machine != null && _machine.IsOpenOrOpening && key != Key.Escape && key != Key.Tab && key != Key.LeftShift && key != Key.RightShift && key != Key.LeftCtrl && key != Key.RightCtrl && key != Key.LeftAlt && key != Key.RightAlt && key != Key.System && key != Key.LWin && key != Key.RWin && IsTextBoxFocused())
		{
			_typedSinceOpen = true;
			_machine.NotifyTextInputChanged();
		}
	}

	string INotchSurface.CheckConsistency(NotchPhase phase, NotchPresence presence)
	{
		if (_hwnd == IntPtr.Zero || _capsuleCard == null)
		{
			return null;
		}
		double morphProgress = MorphProgress;
		bool flag = base.Visibility == Visibility.Visible;
		bool flag2 = (DateTime.UtcNow - _presenceAnimStartedUtc).TotalMilliseconds < 600.0;
		if (PetPreferences.Instance.Enabled && presence != NotchPresence.None && !flag2)
		{
			if (!PetNativeVisible(_hwnd))
			{
				PetNativeShow(_hwnd, 4);
				return "pet watchdog: native notch hidden";
			}
			if (base.WindowState == WindowState.Minimized)
			{
				base.WindowState = WindowState.Normal;
				return "pet watchdog: notch minimized";
			}
			string text = PetPlacementProblem();
			if (text != null)
			{
				return text;
			}
			if (IsRestingSurface && GardenOwnsStage)
			{
				ArrangeOutsidePet();
			}
		}
		switch (phase)
		{
		case NotchPhase.Hidden:
			if (flag && !flag2 && _capsuleCard.Opacity > 0.05)
			{
				return "hidden, but the notch is still on screen";
			}
			return null;
		case NotchPhase.Resting:
		case NotchPhase.Peeking:
		case NotchPhase.Transient:
			if (morphProgress > 0.001)
			{
				return $"resting, but the panel morph is at {morphProgress:0.00}";
			}
			if (_expandedContent != null && _expandedContent.Visibility == Visibility.Visible && _expandedContent.Opacity > 0.05)
			{
				return "resting, but the open panel is still visible";
			}
			if (!flag)
			{
				return "resting, but the window is hidden";
			}
			if (!flag2)
			{
				if (presence == NotchPresence.Full && _capsuleCard.Opacity < 0.9)
				{
					return "full notch expected, but it is transparent";
				}
				if (presence == NotchPresence.Line && (_lineLayer == null || _lineLayer.Opacity < 0.9))
				{
					return "collapsed tab expected, but it is not shown";
				}
			}
			if (!IsTopmostNow())
			{
				return "not topmost";
			}
			return CheckWindowPlacement();
		case NotchPhase.Open:
			if (morphProgress < 0.999)
			{
				return $"open, but the panel morph is at {morphProgress:0.00}";
			}
			if (_expandedContent != null && _expandedContent.Visibility != 0)
			{
				return "open, but the panel content is hidden";
			}
			if (!flag)
			{
				return "open, but the window is hidden";
			}
			return null;
		default:
			return null;
		}
	}

	private string CheckWindowPlacement()
	{
		if (!TryGetTargetPosition(out var x, out var y) || !GetWindowRect(_hwnd, out var lpRect))
		{
			return null;
		}
		if (Math.Abs(lpRect.Left - x) > 2 || Math.Abs(lpRect.Top - y) > 2)
		{
			return $"window at {lpRect.Left},{lpRect.Top} but should be at {x},{y}";
		}
		return null;
	}

	private void EnsureWindowShown()
	{
		if (!EntitlementGate.IsEntitled)
		{
			if (base.Visibility == Visibility.Visible)
			{
				base.Visibility = Visibility.Hidden;
			}
			return;
		}
		if (PetPreferences.Instance.Enabled && _hwnd != IntPtr.Zero && !PetNativeVisible(_hwnd))
		{
			PetNativeShow(_hwnd, 4);
		}
		if (base.Visibility != 0)
		{
			base.Visibility = Visibility.Visible;
			UpdatePosition(initial: false);
			RenderCurrentResolvedPillActivity(immediate: true);
			RefreshLiveBadges();
		}
		AssertTopmost();
		if (_edgeLight != null)
		{
			_edgeLight.SyncWindow();
		}
	}

	private void ApplyPresenceVisual(NotchPresence presence, bool animate)
	{
		if (_capsuleCard == null)
		{
			return;
		}
		if (_edgeLight != null)
		{
			_edgeLight.SetPresenceFull(presence == NotchPresence.Full);
		}
		_beatPresenceFull = presence == NotchPresence.Full;
		UpdateBeat();
		bool flag = _lineLayer == null || (_lineLayer.Opacity < 0.001 && _lineLayer.Visibility != Visibility.Visible);
		if (presence == _shownPresence && base.Visibility == Visibility.Visible && ((presence == NotchPresence.Full && _capsuleCard.Opacity > 0.999 && Math.Abs(_capsuleTranslateY.Y) < 0.01 && flag) || (presence == NotchPresence.Line && _capsuleCard.Opacity < 0.001 && _lineLayer != null && _lineLayer.Opacity > 0.999)))
		{
			ShowPeekVisual(on: false, animate: false);
			AssertTopmost();
			PresentRestingNotch(string.Concat("presence ", presence, " unchanged"), animate: false, rebuildEffects: false, rebuildContent: false);
			return;
		}
		int gen = ++_presenceGen;
		_shownPresence = presence;
		EyesRefresh();
		_presenceAnimStartedUtc = (animate ? DateTime.UtcNow : DateTime.MinValue);
		double num = ((animate && CapsuleMotion.AnimationsEnabled) ? 0.18 : 0.0);
		double num2 = Math.Max(10.0, (_capsuleCard.ActualHeight > 0.0) ? _capsuleCard.ActualHeight : _capsuleCard.Height);
		ShowPeekVisual(on: false, animate: false);
		switch (presence)
		{
		case NotchPresence.Full:
			EnsureWindowShown();
			_capsuleCard.IsHitTestVisible = true;
			CapsuleMotion.AnimateDouble(_capsuleCard, UIElement.OpacityProperty, 1.0, num, CapsuleMotion.EaseOutCubic);
			CapsuleMotion.AnimateDouble(_capsuleTranslateY, TranslateTransform.YProperty, 0.0, num, CapsuleMotion.EaseOutCubic);
			FadeLine(0.0, num, gen);
			break;
		case NotchPresence.Line:
		{
			EnsureWindowShown();
			_capsuleCard.IsHitTestVisible = false;
			UpdateCollapsedVisual();
			double num3 = ((num > 0.0) ? 0.07 : 0.0);
			CapsuleMotion.AnimateDouble(_capsuleCard, UIElement.OpacityProperty, 0.0, num3, CapsuleMotion.EaseOutCubic);
			CapsuleMotion.AnimateDouble(_capsuleTranslateY, TranslateTransform.YProperty, (0.0 - num2) * 0.6, num3, CapsuleMotion.EaseInCubic);
			FadeLine(1.0, num3, gen);
			break;
		}
		default:
			_capsuleCard.IsHitTestVisible = false;
			CapsuleMotion.AnimateDouble(_capsuleTranslateY, TranslateTransform.YProperty, -20.0, num, CapsuleMotion.EaseInCubic);
			FadeLine(0.0, num, gen);
			CapsuleMotion.AnimateDouble(_capsuleCard, UIElement.OpacityProperty, 0.0, num, CapsuleMotion.EaseInCubic, delegate
			{
				if (gen == _presenceGen && _shownPresence == NotchPresence.None)
				{
					base.Topmost = false;
					base.Visibility = Visibility.Hidden;
				}
			});
			break;
		}
		PresentRestingNotch("presence " + presence, animate: false, presence == NotchPresence.Full);
	}

	private void FadeLine(double to, double dur, int gen)
	{
		if (_lineLayer == null)
		{
			return;
		}
		if (to <= 0.0 && _lineLayer.Visibility != 0)
		{
			_lineLayer.BeginAnimation(UIElement.OpacityProperty, null);
			_lineLayer.Opacity = 0.0;
			return;
		}
		if (to > 0.0)
		{
			_lineLayer.Visibility = Visibility.Visible;
		}
		CapsuleMotion.AnimateDouble(_lineLayer, UIElement.OpacityProperty, to, dur, CapsuleMotion.EaseOutCubic, delegate
		{
			if (gen == _presenceGen && to <= 0.0 && _lineLayer != null)
			{
				_lineLayer.Visibility = Visibility.Collapsed;
			}
		});
	}

	private void PrepareCardForOpen(bool anim)
	{
		EnsureWindowShown();
		if (_shownPresence != 0 || !(_capsuleCard.Opacity > 0.99) || !(Math.Abs(_capsuleTranslateY.Y) < 0.5))
		{
			int gen = ++_presenceGen;
			_presenceAnimStartedUtc = DateTime.UtcNow;
			_capsuleCard.IsHitTestVisible = true;
			_capsuleTranslateY.BeginAnimation(TranslateTransform.YProperty, null);
			_capsuleTranslateY.Y = 0.0;
			CapsuleMotion.AnimateDouble(_capsuleCard, UIElement.OpacityProperty, 1.0, anim ? 0.09 : 0.0, CapsuleMotion.EaseOutCubic);
			FadeLine(0.0, anim ? 0.09 : 0.0, gen);
		}
	}

	internal static CollapsedLook ComputeCollapsedLook(bool mini, bool eyes, System.Windows.Size rest, double restRadius, double dpi)
	{
		if (dpi <= 0.01)
		{
			dpi = 1.0;
		}
		CollapsedLook collapsedLook = default(CollapsedLook);
		collapsedLook.Mini = mini;
		collapsedLook.Eyes = eyes;
		CollapsedLook result = collapsedLook;
		double num = ((!eyes) ? (mini ? 20.0 : 14.0) : (mini ? 22.0 : 18.0));
		double num3;
		if (eyes)
		{
			result.EyesHeight = (mini ? 15.0 : 12.0);
			double num2 = 1.6363636363636365 * result.EyesHeight * 1.3;
			num3 = Math.Max(num2 + 1.1 * num, 2.727272727272727 * result.EyesHeight);
		}
		else
		{
			num3 = (mini ? 50.0 : 30.0);
		}
		double num4 = ((rest.Height > 1.0) ? rest.Height : 32.0);
		result.Scale = Math.Max(0.35, Math.Min(1.0, num / num4));
		result.Scale = Math.Max(1.0, Math.Round(12.0 * result.Scale * dpi)) / (12.0 * dpi);
		double num5 = Math.Min(12.0 * result.Scale, 0.15 * (num3 + 24.0 * result.Scale));
		double num6 = Math.Ceiling((num3 + 2.0 * num5) * dpi - 1E-06);
		if (((int)num6 & 1) == 1)
		{
			num6 += 1.0;
		}
		result.W = num6 / dpi;
		result.H = Math.Ceiling(num * dpi - 1E-06) / dpi;
		result.Radius = Math.Max(2.0, ((restRadius >= 0.0) ? restRadius : 16.0) * result.Scale);
		return result;
	}

	private static Geometry ShiftedRestingGeometry(Geometry g, double dx)
	{
		if (g == null)
		{
			return null;
		}
		GeometryGroup geometryGroup = new GeometryGroup();
		geometryGroup.Transform = new TranslateTransform(dx, 0.0);
		GeometryGroup geometryGroup2 = geometryGroup;
		geometryGroup2.Children.Add(g);
		geometryGroup2.Freeze();
		return geometryGroup2;
	}

	private void PinCardForMorph()
	{
		if (_capsuleCard != null)
		{
			if (_collapsedContent != null && double.IsNaN(_collapsedContent.Width) && _collapsedContent.ActualWidth > 0.0)
			{
				_collapsedContent.Width = _collapsedContent.ActualWidth;
				_collapsedContent.Height = _collapsedContent.ActualHeight;
				_collapsedContent.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
				_collapsedContent.VerticalAlignment = VerticalAlignment.Top;
			}
			if (_capsuleCard.Width != 752.0)
			{
				_capsuleCard.Width = 752.0;
			}
			if (_capsuleCard.Height != 352.0)
			{
				_capsuleCard.Height = 352.0;
			}
		}
	}

	private void ResetCardTuck()
	{
		if (_capsuleTranslateY != null)
		{
			_capsuleTranslateY.BeginAnimation(TranslateTransform.YProperty, null);
			_capsuleTranslateY.Y = 0.0;
		}
	}

	private void CollapsedLookGeometry(out double w, out double h, out double radius, out double scale)
	{
		if (_collapsedLook.W <= 0.0)
		{
			_collapsedLook = CurrentCollapsedLook();
		}
		w = _collapsedLook.W;
		h = _collapsedLook.H;
		radius = _collapsedLook.Radius;
		scale = _collapsedLook.Scale;
	}

	private CollapsedLook CurrentCollapsedLook()
	{
		bool mini = NotchSettings.Instance.VisibilityMode == NotchVisibilityMode.Smart && NotchSettings.Instance.OverMaximizedLook == OverMaximizedLook.MiniNotch;
		double dpi = 1.0;
		try
		{
			dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
		}
		catch
		{
		}
		double restRadius = ((NotchConfig.Instance != null) ? NotchConfig.Instance.CornerRadius : 16.0);
		return ComputeCollapsedLook(mini, EyesModeOn, NotchSettings.Instance.RestingSize, restRadius, dpi);
	}

	private Border BuildCollapsedShape(ScaleTransform scale, System.Windows.Media.Brush fill, out System.Windows.Shapes.Path shape, out Grid slot, UIElement content)
	{
		shape = new System.Windows.Shapes.Path
		{
			Fill = fill,
			IsHitTestVisible = false,
			SnapsToDevicePixels = false,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Top
		};
		slot = new Grid
		{
			IsHitTestVisible = false
		};
		Grid grid = new Grid();
		grid.IsHitTestVisible = false;
		Grid grid2 = grid;
		grid2.Children.Add(shape);
		if (content != null)
		{
			grid2.Children.Add(content);
		}
		grid2.Children.Add(slot);
		Border border = new Border();
		border.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		border.VerticalAlignment = VerticalAlignment.Top;
		border.RenderTransformOrigin = new System.Windows.Point(0.5, 0.0);
		border.RenderTransform = scale;
		border.IsHitTestVisible = false;
		border.Child = grid2;
		return border;
	}

	private void BuildMinimalLine()
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(byte.MaxValue, 0, 0, 0));
		solidColorBrush.Freeze();
		_lineLayer = new Grid
		{
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			Height = 22.0,
			Background = System.Windows.Media.Brushes.Transparent,
			Opacity = 0.0,
			Visibility = Visibility.Collapsed,
			Cursor = System.Windows.Input.Cursors.Hand
		};
		SolidColorBrush solidColorBrush2 = new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		solidColorBrush2.Freeze();
		Geometry geometry = Geometry.Parse("M0,0 L3.5,3.25 L7,0");
		geometry.Freeze();
		_lineChevron = new System.Windows.Shapes.Path
		{
			Data = geometry,
			Stroke = solidColorBrush2,
			StrokeThickness = 1.35,
			StrokeStartLineCap = PenLineCap.Round,
			StrokeEndLineCap = PenLineCap.Round,
			StrokeLineJoin = PenLineJoin.Round,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 1.0, 0.0, 0.0),
			Opacity = 0.7,
			IsHitTestVisible = false
		};
		_lineTabScale = new ScaleTransform(1.0, 1.0);
		_lineTab = BuildCollapsedShape(_lineTabScale, _miniOpacityFill ?? solidColorBrush, out _lineShape, out _lineSlot, _lineChevron);
		_lineLayer.Children.Add(_lineTab);
		_miniNotchScale = new ScaleTransform(1.0, 1.0);
		_miniNotch = BuildCollapsedShape(_miniNotchScale, _miniOpacityFill ?? solidColorBrush, out _miniShape, out _miniSlot, null);
		_miniNotch.Visibility = Visibility.Collapsed;
		_lineLayer.Children.Add(_miniNotch);
		_lineLayer.MouseEnter += delegate
		{
			if (_machine != null)
			{
				_machine.PointerMoved(IsPointerInside(), "tab enter");
			}
		};
		_lineLayer.MouseLeave += delegate
		{
			if (_machine != null)
			{
				_machine.PointerMoved(IsPointerInside(), "tab leave");
			}
		};
		_lineLayer.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			if (_machine != null)
			{
				_machine.Click("collapsed tab");
				e.Handled = true;
			}
		};
		_rootGrid.Children.Add(_lineLayer);
		SolidColorBrush solidColorBrush3 = new SolidColorBrush(System.Windows.Media.Color.FromArgb(110, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		solidColorBrush3.Freeze();
		_peekRing = new System.Windows.Shapes.Path
		{
			Stroke = solidColorBrush3,
			StrokeThickness = 1.2,
			IsHitTestVisible = false,
			Opacity = 0.0,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch
		};
		_capsuleCard.Children.Add(_peekRing);
		UpdateMinimalLineVisual();
	}

	private void UpdateCollapsedVisual()
	{
		if (_lineLayer != null)
		{
			UpdateCollapsedLookOnly();
			EyesRefresh();
		}
	}

	private void UpdateCollapsedLookOnly()
	{
		if (_lineLayer == null)
		{
			return;
		}
		bool flag = (_collapsedIsMini = NotchSettings.Instance.VisibilityMode == NotchVisibilityMode.Smart && NotchSettings.Instance.OverMaximizedLook == OverMaximizedLook.MiniNotch);
		CollapsedLook collapsedLook = CurrentCollapsedLook();
		bool flag2 = collapsedLook.W != _collapsedLook.W || collapsedLook.H != _collapsedLook.H || collapsedLook.Scale != _collapsedLook.Scale || collapsedLook.Radius != _collapsedLook.Radius || _miniShape.Data == null;
		_collapsedLook = collapsedLook;
		if (flag2)
		{
			PathGeometry pathGeometry = NotchGeometryBuilder.BuildMiniGeometry(collapsedLook.W, collapsedLook.H, collapsedLook.Scale, collapsedLook.Radius);
			Border[] array = new Border[2] { _lineTab, _miniNotch };
			foreach (Border border in array)
			{
				border.Width = collapsedLook.W;
				border.Height = collapsedLook.H;
			}
			_lineShape.Data = pathGeometry;
			_miniShape.Data = pathGeometry;
			_lineSlot.Clip = pathGeometry;
			_miniSlot.Clip = pathGeometry;
		}
		_lineTab.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
		_miniNotch.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		_lineChevron.Visibility = (collapsedLook.Eyes ? Visibility.Collapsed : Visibility.Visible);
		_lineLayer.Width = CollapsedHitSize().Width;
		_lineLayer.Height = CollapsedHitSize().Height;
	}

	private System.Windows.Size CollapsedHitSize()
	{
		double num = ((_collapsedLook.W > 0.0) ? _collapsedLook.W : 0.0);
		double num2 = ((_collapsedLook.H > 0.0) ? _collapsedLook.H : 0.0);
		return new System.Windows.Size(Math.Max(100.0, num + 8.0), Math.Max(22.0, num2 + 4.0));
	}

	private void UpdateMinimalLineVisual()
	{
		UpdateCollapsedVisual();
	}

	private void ShowPeekVisual(bool on, bool animate)
	{
		double durationSeconds = ((animate && CapsuleMotion.AnimationsEnabled) ? 0.16 : 0.0);
		bool flag = on && _shownPresence == NotchPresence.Line;
		if (_lineTabScale != null)
		{
			double num = ((!flag || _collapsedIsMini) ? 1.0 : (_collapsedLook.Eyes ? 1.12 : 1.3));
			CapsuleMotion.AnimateDouble(_lineTabScale, ScaleTransform.ScaleXProperty, num, durationSeconds, CapsuleMotion.EaseOutCubic);
			CapsuleMotion.AnimateDouble(_lineTabScale, ScaleTransform.ScaleYProperty, num, durationSeconds, CapsuleMotion.EaseOutCubic);
			if (_lineChevron != null)
			{
				CapsuleMotion.AnimateDouble(_lineChevron, UIElement.OpacityProperty, (num > 1.0) ? 1.0 : 0.7, durationSeconds, CapsuleMotion.EaseOutCubic);
			}
		}
		if (_miniNotchScale != null)
		{
			double toValue = ((flag && _collapsedIsMini) ? 1.12 : 1.0);
			CapsuleMotion.AnimateDouble(_miniNotchScale, ScaleTransform.ScaleXProperty, toValue, durationSeconds, CapsuleMotion.EaseOutCubic);
			CapsuleMotion.AnimateDouble(_miniNotchScale, ScaleTransform.ScaleYProperty, toValue, durationSeconds, CapsuleMotion.EaseOutCubic);
		}
		if (_peekRing != null)
		{
			bool flag2 = on && _shownPresence == NotchPresence.Full;
			if (flag2)
			{
				_peekRing.Data = _cachedRestingOutlineGeometry;
			}
			CapsuleMotion.AnimateDouble(_peekRing, UIElement.OpacityProperty, flag2 ? 1.0 : 0.0, durationSeconds, CapsuleMotion.EaseOutCubic);
		}
	}

	private void BuildDiagnosticsOverlay()
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(230, byte.MaxValue, 64, 129));
		solidColorBrush.Freeze();
		SolidColorBrush solidColorBrush2 = new SolidColorBrush(System.Windows.Media.Color.FromArgb(28, byte.MaxValue, 64, 129));
		solidColorBrush2.Freeze();
		_diagHitRect = new System.Windows.Shapes.Rectangle
		{
			Stroke = solidColorBrush,
			Fill = solidColorBrush2,
			StrokeThickness = 1.0,
			StrokeDashArray = new DoubleCollection { 3.0, 2.0 },
			IsHitTestVisible = false
		};
		_diagCanvas = new Canvas
		{
			IsHitTestVisible = false,
			Visibility = Visibility.Collapsed,
			ClipToBounds = false
		};
		_diagCanvas.Children.Add(_diagHitRect);
		System.Windows.Controls.Panel.SetZIndex(_diagCanvas, 1000);
		_rootGrid.Children.Add(_diagCanvas);
	}

	private bool IsDiagnosticsVisual(DependencyObject d)
	{
		if (d != null)
		{
			if (d != _diagCanvas)
			{
				return d == _diagHitRect;
			}
			return true;
		}
		return false;
	}

	internal void UpdateDiagnosticsHitArea(bool show)
	{
		if (_diagCanvas == null)
		{
			return;
		}
		if (!show)
		{
			_diagCanvas.Visibility = Visibility.Collapsed;
			return;
		}
		Rect hitRectDip = GetHitRectDip();
		if (hitRectDip.IsEmpty)
		{
			_diagHitRect.Visibility = Visibility.Collapsed;
		}
		else
		{
			System.Windows.Point point = TranslatePoint(new System.Windows.Point(hitRectDip.X, hitRectDip.Y), _rootGrid);
			Canvas.SetLeft(_diagHitRect, point.X);
			Canvas.SetTop(_diagHitRect, point.Y);
			_diagHitRect.Width = hitRectDip.Width;
			_diagHitRect.Height = hitRectDip.Height;
			_diagHitRect.Visibility = Visibility.Visible;
		}
		_diagCanvas.Visibility = Visibility.Visible;
	}

	internal string DescribeDiagnostics()
	{
		StringBuilder stringBuilder = new StringBuilder();
		if (_machine != null)
		{
			stringBuilder.Append(_machine.Describe());
		}
		NotchVisibilityEngine instance = NotchVisibilityEngine.Instance;
		NotchSettings instance2 = NotchSettings.Instance;
		PresenceInputs lastInputs = instance.LastInputs;
		stringBuilder.AppendFormat("Mode       {0}   policy {1} ({2})\n", instance2.VisibilityMode, instance.CurrentPresence, instance.LastReason);
		stringBuilder.AppendFormat("Inputs     fullscreen {0}  maximized {1}  desktop {2}  event {3}  locked {4}\n", lastInputs.FullscreenOnMonitor, lastInputs.MaximizedOnMonitor, lastInputs.ForegroundIsDesktop, lastInputs.TransientActive, lastInputs.SessionLocked);
		stringBuilder.AppendFormat("Drawn      {0}   card {1:0}x{2:0} op {3:0.00}   morph {4:0.000}\n", _shownPresence, (_capsuleCard != null) ? _capsuleCard.ActualWidth : 0.0, (_capsuleCard != null) ? _capsuleCard.ActualHeight : 0.0, (_capsuleCard != null) ? _capsuleCard.Opacity : 0.0, MorphProgress);
		GetCursorPos(out var lpPoint);
		Rect hitRectDip = GetHitRectDip();
		string arg = "none";
		if (!hitRectDip.IsEmpty)
		{
			System.Windows.Point point = PointToScreen(hitRectDip.TopLeft);
			System.Windows.Point point2 = PointToScreen(hitRectDip.BottomRight);
			arg = $"{point.X:0},{point.Y:0} -> {point2.X:0},{point2.Y:0} px ({hitRectDip.Width:0}x{hitRectDip.Height:0} DIP)";
		}
		stringBuilder.AppendFormat("Hit area   {0}\n", arg);
		stringBuilder.AppendFormat("Cursor     {0},{1} px   on notch: {2}\n", lpPoint.X, lpPoint.Y, IsPointerInside());
		GetWindowRect(_hwnd, out var lpRect);
		uint num = 96u;
		try
		{
			num = GetDpiForWindow(_hwnd);
		}
		catch
		{
		}
		stringBuilder.AppendFormat("Window     {0},{1} {2}x{3} px   DPI {4} ({5:0}%)   topmost {6}   visible {7}\n", lpRect.Left, lpRect.Top, lpRect.Right - lpRect.Left, lpRect.Bottom - lpRect.Top, num, (double)num * 100.0 / 96.0, IsTopmostNow(), base.Visibility == Visibility.Visible);
		stringBuilder.AppendFormat("Display    {0}   environment repairs {1}\n", instance2.Display, EnvironmentRepairs);
		return stringBuilder.ToString();
	}

	private void StartFirstRunArrival()
	{
		FirstRunManager.MarkCompleted();
		UpdateMorphState(0.0);
		_firstRunHoldTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1.2)
		};
		_firstRunHoldTimer.Tick += delegate
		{
			_firstRunHoldTimer.Stop();
			_firstRunHoldTimer = null;
			if (_state == CapsuleDisplayState.Collapsed)
			{
				MessageService.Instance.ShowNotice("Welcome to Ayn", TimeSpan.FromSeconds(7.0));
			}
		};
		_firstRunHoldTimer.Start();
	}

	private void CancelFirstRunHold()
	{
		if (_firstRunHoldTimer != null)
		{
			_firstRunHoldTimer.Stop();
			_firstRunHoldTimer = null;
		}
	}

	private UIElement CreateBadgeIcon(string primarySource, CapsuleTheme theme, SystemTelemetryData data)
	{
		switch (primarySource)
		{
		case "battery":
		{
			System.Windows.Media.Brush fill = ((data.BatteryPercent <= 15) ? NotchInk.Danger : NotchInk.Success);
			return VectorIcons.CreateBatteryIcon(11.0, theme.IconStroke, fill, data.BatteryPercent, data.IsCharging);
		}
		case "timer":
			return VectorIcons.CreateTimerIcon(11.0, theme.IconStroke);
		case "focus":
			return VectorIcons.CreateFocusIcon(11.0, theme.IconStroke);
		case "wifi":
			return VectorIcons.CreateWiFiIcon(11.0, theme.IconStroke);
		case "active":
		{
			IBuddyTool activeTool = ToolRegistry.Instance.ActiveTool;
			if (activeTool != null)
			{
				return activeTool.CreateIcon(11.0, theme.IconStroke);
			}
			break;
		}
		}
		return VectorIcons.CreateClockIcon(11.0, theme.IconStroke);
	}

	internal void RequestBadgeRefresh()
	{
		if ((_machine != null && _machine.Phase == NotchPhase.Hidden) || _isShuttingDown || Interlocked.Exchange(ref _badgeRefreshQueued, 1) == 1)
		{
			return;
		}
		try
		{
			base.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(ScheduleBadgeRefresh));
		}
		catch
		{
			Interlocked.Exchange(ref _badgeRefreshQueued, 0);
		}
	}

	private void ScheduleBadgeRefresh()
	{
		if (_isShuttingDown || (_machine != null && _machine.Phase == NotchPhase.Hidden))
		{
			Interlocked.Exchange(ref _badgeRefreshQueued, 0);
			return;
		}
		long num = (Stopwatch.GetTimestamp() - _lastBadgeRefreshTimestamp) * 1000 / Stopwatch.Frequency;
		int num2 = (int)Math.Max(0L, 250 - num);
		if (num2 == 0)
		{
			RunBadgeRefresh();
			return;
		}
		if (_badgeThrottle == null)
		{
			_badgeThrottle = new DispatcherTimer(DispatcherPriority.Background, base.Dispatcher);
			_badgeThrottle.Tick += delegate
			{
				_badgeThrottle.Stop();
				RunBadgeRefresh();
			};
		}
		if (!_badgeThrottle.IsEnabled)
		{
			_badgeThrottle.Interval = TimeSpan.FromMilliseconds(num2);
			_badgeThrottle.Start();
		}
	}

	private void RunBadgeRefresh()
	{
		Interlocked.Exchange(ref _badgeRefreshQueued, 0);
		_lastBadgeRefreshTimestamp = Stopwatch.GetTimestamp();
		if (!_isShuttingDown && (_machine == null || _machine.Phase != 0))
		{
			Guard.Run("resting icons", RefreshLiveBadges);
		}
	}

	private void RefreshLiveBadges()
	{
		SystemTelemetryData liveTelemetry = SystemMonitorService.GetLiveTelemetry();
		_ = CapsuleTheme.Current;
		NotchConfig instance = NotchConfig.Instance;
		System.Windows.Media.Brush foregroundBrush = instance.GetForegroundBrush();
		UpdateCollapsedClock(DateTime.Now);
		if (_collapsedPrimaryValueText != null)
		{
			_collapsedPrimaryValueText.Foreground = foregroundBrush;
			_collapsedPrimaryValueText.Visibility = ((!instance.ShowTime || instance.NotchAnalogClock) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (_collapsedMiniAnalogCanvas != null)
		{
			_collapsedMiniAnalogCanvas.Visibility = ((!instance.ShowTime || !instance.NotchAnalogClock) ? Visibility.Collapsed : Visibility.Visible);
		}
		double restingGlyphSize = RestingGlyphSize;
		SetRestingBattery(liveTelemetry.BatteryPercent, liveTelemetry.IsCharging);
		if (_collapsedBluetoothIconHolder != null)
		{
			bool flag = instance.ShowBluetooth && IsBluetoothDeviceConnectedCached();
			_collapsedBluetoothIconHolder.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
			string text = "1_" + restingGlyphSize + foregroundBrush;
			if (flag && (_lastRenderedBluetoothKey != text || _collapsedBluetoothIconHolder.Content == null))
			{
				_lastRenderedBluetoothKey = text;
				_collapsedBluetoothIconHolder.Content = NotchGlyphs.Create("bluetooth", restingGlyphSize, foregroundBrush);
				_collapsedBluetoothIconHolder.ToolTip = "Bluetooth device connected";
			}
		}
		if (_collapsedNowPlayingIconHolder != null)
		{
			bool flag2 = instance.ShowNowPlaying && NotchBehaviorConfig.Instance.ShowMediaWhenPlaybackStarts && MediaService.Instance != null && MediaService.Instance.IsPlaying && (!MediaService.IsBrowserSource((MediaService.Instance.View != null) ? MediaService.Instance.View.Source : null) || NotchBehaviorConfig.Instance.ShowBrowserMediaInNotch);
			_collapsedNowPlayingIconHolder.Visibility = ((!flag2) ? Visibility.Collapsed : Visibility.Visible);
			string text2 = "1_" + restingGlyphSize + foregroundBrush;
			if (flag2 && (_lastRenderedNowPlayingKey != text2 || _collapsedNowPlayingIconHolder.Content == null))
			{
				_lastRenderedNowPlayingKey = text2;
				_collapsedNowPlayingIconHolder.Content = NotchGlyphs.Create("music.note", restingGlyphSize, foregroundBrush);
				_collapsedNowPlayingIconHolder.ToolTip = "Now Playing (Click to Play/Pause)";
			}
		}
		RefreshRestingFit();
		RefreshRestingContentSize();
	}

	private void UpdateCollapsedLiveActivity(CapsuleLiveActivity act)
	{
		if (base.Dispatcher != null && !base.Dispatcher.CheckAccess())
		{
			try
			{
				base.Dispatcher.BeginInvoke((Action)delegate
				{
					UpdateCollapsedLiveActivity(act);
				});
				return;
			}
			catch
			{
				return;
			}
		}
		if (act != null && act.IsPinned)
		{
			DynamicActivityService.Instance.PublishActivity(new NotchActivity
			{
				Id = "pinned_" + (act.OwnerId ?? "message"),
				Type = ActivityType.Custom,
				Priority = ActivityPriority.P1_Media,
				Mode = ActivityMode.Persistent,
				Title = (act.Title ?? "Message"),
				Subtitle = (act.PreviewText ?? ""),
				NavigateToToolId = (act.NavigateToToolId ?? "message")
			});
		}
		else
		{
			DynamicActivityService.Instance.ClearPersistentActivity(ActivityType.Custom, 0L);
		}
	}

	private void CleanSlotVisual(ContentControl slot)
	{
		if (slot != null && slot.Content != _defaultCollapsedStack)
		{
			if (slot.Content is System.Windows.Controls.Panel panel)
			{
				panel.Children.Clear();
			}
			slot.Content = null;
		}
	}

	private void ClearLiveWidgetReferences(CollapsedVisualType nextVisualType = CollapsedVisualType.None)
	{
		if (nextVisualType != CollapsedVisualType.Timer)
		{
			_timerLiveText = null;
			_timerPlayPauseButton = null;
			_timerPlayPauseIconHost = null;
		}
		if (nextVisualType != CollapsedVisualType.Media)
		{
			ClearMusicReferences();
		}
		if (nextVisualType != CollapsedVisualType.Volume)
		{
			_volumeLivePercentText = null;
			_volumeLiveSliderFill = null;
			_volumeLiveIconHost = null;
			_volumeLiveBar = null;
		}
		if (nextVisualType != CollapsedVisualType.Clock)
		{
			_clockNotchMiniHourRotate = null;
			_clockNotchMiniMinuteRotate = null;
			_clockNotchLiveTimeText = null;
		}
	}

	public void RenderCurrentResolvedPillActivity(bool immediate = false)
	{
		PillActivitySnapshot currentActivity = PillActivityArbiter.Instance.CurrentActivity;
		RenderResolvedSnapshot(null, currentActivity, immediate, immediate ? ActivityTransitionType.RestingInstant : ActivityTransitionType.InPlaceUpdate);
	}

	private void RenderResolvedSnapshot(PillActivitySnapshot oldSnap, PillActivitySnapshot newSnap, bool isUpdateOnly, ActivityTransitionType transition, bool rebuild = false)
	{
		if (base.Dispatcher != null && !base.Dispatcher.CheckAccess())
		{
			try
			{
				base.Dispatcher.BeginInvoke((Action)delegate
				{
					RenderResolvedSnapshot(oldSnap, newSnap, isUpdateOnly, transition, rebuild);
				});
				return;
			}
			catch
			{
				return;
			}
		}
		if (_capsuleLifecycleState == CapsuleState.Welcome)
		{
			return;
		}
		if (newSnap == null)
		{
			newSnap = PillActivityArbiter.Instance.CurrentActivity;
		}
		if (DeferGardenActivity(newSnap))
		{
			return;
		}
		if (GardenResting(newSnap) && newSnap.Type == ActivityType.Media)
		{
			newSnap = new NotchActivity
			{
				Id = "pet_music_stage",
				Type = ActivityType.RestingClock,
				Mode = ActivityMode.Persistent
			}.ToSnapshot();
		}
		NotchStabilityCounters.RenderCalls++;
		if (_presentationOfferView != null)
		{
			_presentationOfferView.Visibility = ((!OfferCanDisplay(newSnap)) ? Visibility.Collapsed : Visibility.Visible);
		}
		UpdateExpandedReminder(newSnap);
		if (!IsRestingSurface && _state != CapsuleDisplayState.Collapsing)
		{
			_pendingSnapshotWhileSuppressed = newSnap;
			return;
		}
		_pendingSnapshotWhileSuppressed = null;
		SettleInFlightSlotTransition();
		ContentControl contentControl = (_isSlotAActive ? _slotA : _slotB);
		ContentControl contentControl2 = (_isSlotAActive ? _slotB : _slotA);
		TranslateTransform translateTransform = (_isSlotAActive ? _slotATranslate : _slotBTranslate);
		TranslateTransform translateTransform2 = (_isSlotAActive ? _slotBTranslate : _slotATranslate);
		if (contentControl == null || contentControl2 == null)
		{
			return;
		}
		if (newSnap == null || newSnap.Type != ActivityType.Clock || !ClockSettings.Instance.ShowInNotch)
		{
			StopClockNotchAnalogTimer();
		}
		_ = NotchBehaviorConfig.Instance;
		CollapsedActivityProfile collapsedActivityProfile = CollapsedActivityProfile.Resting;
		if (newSnap != null)
		{
			collapsedActivityProfile = ((newSnap.Profile != 0 || newSnap.Type == ActivityType.RestingClock) ? newSnap.Profile : NotchProfileMetrics.FromActivityType(newSnap.Type));
		}
		if (newSnap.Type == ActivityType.RestingClock || newSnap.Type == ActivityType.Clock)
		{
			UpdateCollapsedClock(DateTime.Now);
		}
		if (IsRestingSurface && !rebuild)
		{
			System.Windows.Size activeCollapsedSize = GetActiveCollapsedSize(newSnap);
			double width = activeCollapsedSize.Width;
			double height = activeCollapsedSize.Height;
			if (collapsedActivityProfile != _currentWidthProfile || Math.Abs(_capsuleCard.Width - width) > 1.5)
			{
				_currentWidthProfile = collapsedActivityProfile;
				_lastCapsuleTargetWidth = width;
				if (transition == ActivityTransitionType.RestingInstant && (!PetPreferences.Instance.Enabled || !(_capsuleCard.Width - width > 4.0) || !CapsuleMotion.AnimationsEnabled))
				{
					StopCardWidthAnimation();
					_capsuleCard.Width = width;
					SettleRestingShape();
				}
				else
				{
					AnimateCardWidth(width, 0.28, CapsuleMotion.Spring, SettleRestingShape);
				}
			}
			if (Math.Abs(_capsuleCard.Height - height) > 1.5)
			{
				if (transition == ActivityTransitionType.RestingInstant)
				{
					_capsuleCard.BeginAnimation(FrameworkElement.HeightProperty, null);
					_capsuleCard.Height = height;
					SettleRestingShape();
				}
				else
				{
					CapsuleMotion.AnimateDouble(_capsuleCard, FrameworkElement.HeightProperty, height, 0.28, CapsuleMotion.Spring, SettleRestingShape);
				}
			}
		}
		FrameworkElement targetVisual = null;
		CollapsedVisualType targetVisualType = CollapsedVisualType.None;
		if (_edgeLight != null && newSnap != null && newSnap.Type != 0)
		{
			bool flag = ((newSnap.Tier == ActivityTier.Tier1_Transient) ? (newSnap.SequenceNumber != _edgeLightLastSequence) : (newSnap.Tier == ActivityTier.Tier2_Session && newSnap.Id != _edgeLightLastSession));
			_edgeLightLastSequence = newSnap.SequenceNumber;
			if (newSnap.Tier == ActivityTier.Tier2_Session)
			{
				_edgeLightLastSession = newSnap.Id;
			}
			if (flag)
			{
				if (newSnap.Type == ActivityType.BatteryCharging)
				{
					_edgeLight.PlayCharging();
				}
				else
				{
					_edgeLight.Trigger();
				}
			}
		}
		switch (newSnap.Type)
		{
		case ActivityType.RestingClock:
		case ActivityType.Clock:
			targetVisualType = CollapsedVisualType.Clock;
			targetVisual = _defaultCollapsedStack;
			if (_defaultCollapsedStack != null)
			{
				_defaultCollapsedStack.Visibility = Visibility.Visible;
				_defaultCollapsedStack.Opacity = 1.0;
			}
			if (_collapsedPrimaryValueText != null)
			{
				string text = ClockSettings.Instance.FormatTime(DateTime.Now, ClockSettings.Instance.ShowSeconds);
				if (newSnap.Type == ActivityType.Clock)
				{
					_collapsedPrimaryValueText.Text = ((!string.IsNullOrEmpty(newSnap.Title)) ? newSnap.Title : text);
				}
			}
			break;
		case ActivityType.TimerRunning:
			targetVisualType = CollapsedVisualType.Timer;
			break;
		case ActivityType.TimerComplete:
		case ActivityType.HydrationReminder:
			targetVisualType = CollapsedVisualType.Reminder;
			break;
		case ActivityType.Media:
			targetVisualType = CollapsedVisualType.Media;
			break;
		case ActivityType.ClipboardCopied:
			targetVisualType = CollapsedVisualType.Clipboard;
			break;
		case ActivityType.BatteryCharging:
		case ActivityType.BatteryFull:
			targetVisualType = CollapsedVisualType.Charging;
			break;
		case ActivityType.Volume:
			targetVisualType = CollapsedVisualType.Volume;
			break;
		case ActivityType.BluetoothConnected:
		case ActivityType.BluetoothDisconnected:
			targetVisualType = CollapsedVisualType.Bluetooth;
			break;
		case ActivityType.NetworkConnected:
		case ActivityType.NetworkDisconnected:
			targetVisualType = CollapsedVisualType.Network;
			break;
		case ActivityType.BatteryLow:
		case ActivityType.ScreenTimeLimit:
			targetVisualType = CollapsedVisualType.SystemAlert;
			break;
		case ActivityType.CompanionMessage:
			targetVisualType = CollapsedVisualType.Message;
			break;
		case ActivityType.CalendarAlert:
			targetVisualType = CollapsedVisualType.Calendar;
			break;
		default:
			targetVisualType = CollapsedVisualType.Custom;
			break;
		}
		bool flag2 = _activeOwner.VisualType == targetVisualType && _activeOwner.SnapshotId == newSnap.Id && (_activeOwner.VisualInstance != null || contentControl.Content != null);
		if (!rebuild && (flag2 || isUpdateOnly || transition == ActivityTransitionType.InPlaceUpdate) && _activeOwner.VisualType == targetVisualType && (_activeOwner.VisualInstance != null || contentControl.Content != null))
		{
			if (targetVisualType == CollapsedVisualType.Calendar && PillActivityArbiter.IsCalendarReminder(newSnap) && _activeOwner.SnapshotId == newSnap.Id)
			{
				UpdateCollapsedReminderInPlace();
				EnforceSlotSettledInvariants("InPlaceUpdate");
				return;
			}
			if (targetVisualType == CollapsedVisualType.Timer)
			{
				if (_timerLiveText == null && _activeOwner.VisualInstance is System.Windows.Controls.Panel panel)
				{
					foreach (UIElement child in panel.Children)
					{
						if (child is TextBlock timerLiveText)
						{
							_timerLiveText = timerLiveText;
							break;
						}
					}
				}
				if (_timerLiveText != null)
				{
					_timerLiveText.Text = newSnap.Title;
				}
				if (_timerPlayPauseIconHost != null && _timerIsPausedVisual != !newSnap.IsPlaying)
				{
					_timerIsPausedVisual = !newSnap.IsPlaying;
					_timerPlayPauseIconHost.Content = (_timerIsPausedVisual ? VectorIcons.CreatePlayMediaIcon(9.0, System.Windows.Media.Brushes.White) : VectorIcons.CreatePauseMediaIcon(9.0, System.Windows.Media.Brushes.White));
					if (_timerPlayPauseButton != null)
					{
						_timerPlayPauseButton.ToolTip = (_timerIsPausedVisual ? "Resume timer" : "Pause timer");
					}
				}
				NotchStabilityCounters.VisualInstancesReused++;
				EnforceSlotSettledInvariants("InPlaceUpdate");
				return;
			}
			if (targetVisualType == CollapsedVisualType.Media)
			{
				UpdateMediaVisualInPlace(newSnap);
				NotchStabilityCounters.VisualInstancesReused++;
				EnforceSlotSettledInvariants("InPlaceUpdate");
				return;
			}
			if (targetVisualType == CollapsedVisualType.Volume)
			{
				UpdateVolumeVisualInPlace(newSnap);
				NotchStabilityCounters.VisualInstancesReused++;
				EnforceSlotSettledInvariants("InPlaceUpdate");
				return;
			}
			if (targetVisualType == CollapsedVisualType.Clock)
			{
				if (_collapsedPrimaryValueText != null && (_activeOwner.VisualInstance == _defaultCollapsedStack || contentControl.Content == _defaultCollapsedStack))
				{
					_collapsedPrimaryValueText.Text = newSnap.Title;
				}
				NotchStabilityCounters.VisualInstancesReused++;
				EnforceSlotSettledInvariants("InPlaceUpdate");
				return;
			}
		}
		if (!rebuild && transition == ActivityTransitionType.RestingInstant && !_isSlotTransitionInProgress && _activeOwner.VisualType == targetVisualType && _activeOwner.SnapshotId == newSnap.Id && _activeOwner.SnapshotSequence == newSnap.SequenceNumber && _activeOwner.VisualInstance != null && contentControl.Content == _activeOwner.VisualInstance)
		{
			contentControl.BeginAnimation(UIElement.OpacityProperty, null);
			contentControl.Opacity = 1.0;
			contentControl.Visibility = Visibility.Visible;
			EnforceSlotSettledInvariants("SameSnapshotKept");
			return;
		}
		ClearLiveWidgetReferences(targetVisualType);
		if (targetVisual == null)
		{
			targetVisual = BuildActivityVisual(newSnap, _isCollapsedHovered);
			NotchStabilityCounters.VisualInstancesCreated++;
		}
		if (PetAtomicSwap(newSnap))
		{
			if (targetVisual != null)
			{
				targetVisual.Measure(GetActiveCollapsedSize(newSnap));
			}
			transition = ActivityTransitionType.RestingInstant;
		}
		if (transition == ActivityTransitionType.RestingInstant || (contentControl.Content == targetVisual && contentControl.Opacity >= 0.99 && contentControl.Visibility == Visibility.Visible))
		{
			SettleInFlightSlotTransition();
			_isSlotTransitionInProgress = false;
			long renderSequence = ++_currentRenderSequence;
			_slotA.BeginAnimation(UIElement.OpacityProperty, null);
			if (_slotATranslate != null)
			{
				_slotATranslate.BeginAnimation(TranslateTransform.XProperty, null);
			}
			_slotB.BeginAnimation(UIElement.OpacityProperty, null);
			if (_slotBTranslate != null)
			{
				_slotBTranslate.BeginAnimation(TranslateTransform.XProperty, null);
			}
			if (contentControl2.Content == targetVisual)
			{
				contentControl2.Content = null;
			}
			if (contentControl2.Content != _defaultCollapsedStack)
			{
				CleanSlotVisual(contentControl2);
			}
			contentControl2.Visibility = Visibility.Collapsed;
			contentControl2.Opacity = 0.0;
			if (targetVisual != null && targetVisual.Parent is ContentControl contentControl3 && contentControl3 != contentControl)
			{
				contentControl3.Content = null;
			}
			contentControl.Content = targetVisual;
			if (targetVisual != null)
			{
				targetVisual.Visibility = Visibility.Visible;
				targetVisual.Opacity = 1.0;
			}
			contentControl.Opacity = 1.0;
			if (translateTransform != null)
			{
				translateTransform.X = 0.0;
			}
			contentControl.Visibility = Visibility.Visible;
			_activeOwner.VisualType = targetVisualType;
			_activeOwner.VisualInstance = targetVisual;
			_activeOwner.SnapshotId = newSnap.Id;
			_activeOwner.SnapshotSequence = newSnap.SequenceNumber;
			_activeOwner.RenderSequence = renderSequence;
			_activeOwner.SlotName = (_isSlotAActive ? "A" : "B");
			if (_capsuleLifecycleState == CapsuleState.Idle || _capsuleLifecycleState == CapsuleState.LiveActivity || _capsuleLifecycleState == CapsuleState.ReturningToIdle)
			{
				bool flag3 = targetVisualType == CollapsedVisualType.Clock;
				TransitionToState(flag3 ? CapsuleState.Idle : CapsuleState.LiveActivity, "Instant render settled: " + targetVisualType);
			}
			EnforceSlotSettledInvariants("InstantRender");
			return;
		}
		long renderId = ++_currentRenderSequence;
		_isSlotTransitionInProgress = true;
		_activeOwner.VisualType = targetVisualType;
		_activeOwner.VisualInstance = targetVisual;
		_activeOwner.SnapshotId = newSnap.Id;
		_activeOwner.SnapshotSequence = newSnap.SequenceNumber;
		_activeOwner.RenderSequence = renderId;
		_activeOwner.SlotName = (_isSlotAActive ? "B" : "A");
		NotchStabilityCounters.OwnershipTransitions++;
		NotchStabilityCounters.SlotAnimationsStarted++;
		_slotA.BeginAnimation(UIElement.OpacityProperty, null);
		if (_slotATranslate != null)
		{
			_slotATranslate.BeginAnimation(TranslateTransform.XProperty, null);
		}
		_slotB.BeginAnimation(UIElement.OpacityProperty, null);
		if (_slotBTranslate != null)
		{
			_slotBTranslate.BeginAnimation(TranslateTransform.XProperty, null);
		}
		contentControl.Opacity = 1.0;
		if (translateTransform != null)
		{
			translateTransform.X = 0.0;
		}
		contentControl.Visibility = Visibility.Visible;
		if (contentControl2.Content != _defaultCollapsedStack)
		{
			CleanSlotVisual(contentControl2);
		}
		contentControl2.Content = null;
		if (targetVisual != null && targetVisual.Parent is ContentControl contentControl4 && contentControl4 != contentControl2)
		{
			contentControl4.Content = null;
		}
		contentControl2.Content = targetVisual;
		if (targetVisual != null)
		{
			targetVisual.Visibility = Visibility.Visible;
			targetVisual.Opacity = 1.0;
		}
		contentControl2.Visibility = Visibility.Visible;
		contentControl2.Opacity = 0.0;
		double x = 0.0;
		double toValue = 0.0;
		double durationSeconds = 0.12;
		double durationSeconds2 = 0.22;
		if (transition == ActivityTransitionType.TrackCrossfade)
		{
			x = -8.0;
			toValue = 8.0;
			durationSeconds = 0.18;
			durationSeconds2 = 0.2;
		}
		if (translateTransform2 != null)
		{
			translateTransform2.X = x;
		}
		CapsuleMotion.AnimateDouble(contentControl, UIElement.OpacityProperty, 0.0, durationSeconds, CapsuleMotion.EaseInCubic);
		if (translateTransform != null)
		{
			CapsuleMotion.AnimateDouble(translateTransform, TranslateTransform.XProperty, toValue, durationSeconds, CapsuleMotion.EaseInCubic);
		}
		Action onCompleted = delegate
		{
			if (renderId != _currentRenderSequence)
			{
				NotchStabilityCounters.SlotAnimationsCancelled++;
			}
			else
			{
				SettleInFlightSlotTransition();
				_isSlotTransitionInProgress = false;
				_activeOwner.VisualType = targetVisualType;
				_activeOwner.VisualInstance = targetVisual;
				_activeOwner.SnapshotId = newSnap.Id;
				_activeOwner.SnapshotSequence = newSnap.SequenceNumber;
				_activeOwner.RenderSequence = renderId;
				_activeOwner.SlotName = (_isSlotAActive ? "A" : "B");
				if (_capsuleLifecycleState == CapsuleState.Idle || _capsuleLifecycleState == CapsuleState.LiveActivity || _capsuleLifecycleState == CapsuleState.ReturningToIdle)
				{
					bool flag4 = targetVisualType == CollapsedVisualType.Clock;
					TransitionToState(flag4 ? CapsuleState.Idle : CapsuleState.LiveActivity, "Anim transition completed: " + targetVisualType);
				}
				EnforceSlotSettledInvariants("AnimTransitionCompleted");
			}
		};
		CapsuleMotion.AnimateDouble(contentControl2, UIElement.OpacityProperty, 1.0, durationSeconds2, CapsuleMotion.EaseOutCubic, onCompleted);
		if (translateTransform2 != null)
		{
			CapsuleMotion.AnimateDouble(translateTransform2, TranslateTransform.XProperty, 0.0, durationSeconds2, CapsuleMotion.EaseOutCubic);
		}
	}

	private void SettleInFlightSlotTransition()
	{
		if (!_isSlotTransitionInProgress)
		{
			return;
		}
		if (_slotA != null)
		{
			_slotA.BeginAnimation(UIElement.OpacityProperty, null);
		}
		if (_slotATranslate != null)
		{
			_slotATranslate.BeginAnimation(TranslateTransform.XProperty, null);
		}
		if (_slotB != null)
		{
			_slotB.BeginAnimation(UIElement.OpacityProperty, null);
		}
		if (_slotBTranslate != null)
		{
			_slotBTranslate.BeginAnimation(TranslateTransform.XProperty, null);
		}
		ContentControl contentControl = (_isSlotAActive ? _slotA : _slotB);
		ContentControl contentControl2 = (_isSlotAActive ? _slotB : _slotA);
		TranslateTransform translateTransform = (_isSlotAActive ? _slotATranslate : _slotBTranslate);
		TranslateTransform translateTransform2 = (_isSlotAActive ? _slotBTranslate : _slotATranslate);
		if (contentControl2 != null)
		{
			contentControl2.Visibility = Visibility.Visible;
			contentControl2.Opacity = 1.0;
			if (translateTransform2 != null)
			{
				translateTransform2.X = 0.0;
			}
		}
		if (contentControl != null)
		{
			contentControl.Visibility = Visibility.Collapsed;
			contentControl.Opacity = 0.0;
			if (translateTransform != null)
			{
				translateTransform.X = 0.0;
			}
			if (contentControl.Content != _defaultCollapsedStack)
			{
				CleanSlotVisual(contentControl);
				contentControl.Content = null;
			}
		}
		_isSlotAActive = !_isSlotAActive;
		_isSlotTransitionInProgress = false;
	}

	private void EnforceSlotSettledInvariants(string context)
	{
		UpdateBeat();
		UpdatePetResting();
		if (_morphRestLine || _isSlotTransitionInProgress || (!IsRestingSurface && _state != CapsuleDisplayState.Collapsing) || _capsuleLifecycleState == CapsuleState.Welcome)
		{
			return;
		}
		if (_collapsedContent != null && (_collapsedContent.Opacity < 0.95 || _collapsedContent.Visibility != 0))
		{
			_collapsedContent.BeginAnimation(UIElement.OpacityProperty, null);
			_collapsedContent.Visibility = Visibility.Visible;
			_collapsedContent.Opacity = 1.0;
			_collapsedContent.IsHitTestVisible = true;
		}
		if (_activityViewport != null && (_activityViewport.Opacity < 0.95 || _activityViewport.Visibility != 0))
		{
			_activityViewport.BeginAnimation(UIElement.OpacityProperty, null);
			_activityViewport.Visibility = Visibility.Visible;
			_activityViewport.Opacity = 1.0;
		}
		ContentControl contentControl = (_isSlotAActive ? _slotA : _slotB);
		ContentControl contentControl2 = (_isSlotAActive ? _slotB : _slotA);
		bool flag = contentControl != null && contentControl.Content != null;
		bool flag2 = contentControl != null && contentControl.Visibility == Visibility.Visible && contentControl.Opacity > 0.05;
		bool flag3 = true;
		if (contentControl != null && contentControl.Content != null && contentControl.Content is UIElement uIElement)
		{
			flag3 = uIElement.Visibility == Visibility.Visible;
		}
		if (contentControl2 != null && (contentControl2.Visibility != Visibility.Collapsed || contentControl2.Opacity > 0.001))
		{
			contentControl2.Visibility = Visibility.Collapsed;
			contentControl2.Opacity = 0.0;
		}
		if (flag && flag2 && flag3)
		{
			return;
		}
		if (PillActivityArbiter.Instance.CurrentActivity != null)
		{
			_ = PillActivityArbiter.Instance.CurrentActivity.Id;
		}
		if (contentControl2 != null && contentControl2.Content == _defaultCollapsedStack)
		{
			contentControl2.Content = null;
		}
		if (_defaultCollapsedStack != null)
		{
			_defaultCollapsedStack.Visibility = Visibility.Visible;
			_defaultCollapsedStack.Opacity = 1.0;
		}
		if (contentControl != null)
		{
			contentControl.Content = _defaultCollapsedStack;
			contentControl.Visibility = Visibility.Visible;
			contentControl.Opacity = 1.0;
			if (_isSlotAActive && _slotATranslate != null)
			{
				_slotATranslate.X = 0.0;
			}
			if (!_isSlotAActive && _slotBTranslate != null)
			{
				_slotBTranslate.X = 0.0;
			}
		}
		if (contentControl2 != null)
		{
			contentControl2.Visibility = Visibility.Collapsed;
			contentControl2.Opacity = 0.0;
		}
		_activeOwner.VisualType = CollapsedVisualType.Clock;
		_activeOwner.VisualInstance = _defaultCollapsedStack;
		_activeOwner.SlotName = (_isSlotAActive ? "A" : "B");
		_activeOwner.SnapshotId = "canonical_clock";
	}

	private FrameworkElement BuildActivityVisual(PillActivitySnapshot snap, bool isHovered)
	{
		if (snap == null)
		{
			return null;
		}
		return BuildActivityVisual(snap.ToLegacyActivity(), isHovered);
	}

	private FrameworkElement BuildActivityVisual(NotchActivity act, bool isHovered)
	{
		if (act == null || act.Type == ActivityType.RestingClock)
		{
			return null;
		}
		CapsuleTheme current = CapsuleTheme.Current;
		FrameworkElement frameworkElement = BuildPetActivity(act);
		if (frameworkElement != null)
		{
			return frameworkElement;
		}
		NotchConfig instance = NotchConfig.Instance;
		System.Windows.Media.Brush foregroundBrush = instance.GetForegroundBrush();
		System.Windows.Media.Brush secondaryForeground = current.SecondaryForeground;
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		switch (act.Type)
		{
		case ActivityType.Media:
			return BuildMusicVisual(act);
		case ActivityType.BluetoothConnected:
		case ActivityType.BluetoothDisconnected:
			return BluetoothPresentation.Create(act.Title, (act.Type == ActivityType.BluetoothConnected) ? "Connected" : "Disconnected", MessageScale, StatusMessageHeight, MessageSideInset(card: false), delegate
			{
				RefreshRestingContentSize();
			});
		case ActivityType.NetworkConnected:
		case ActivityType.NetworkDisconnected:
			return BuildGlyphRow((act.Type == ActivityType.NetworkConnected) ? "wifi" : "wifi.slash", act.Title, act.Subtitle, CollapsedActivityProfile.WiFi, stacked: false);
		case ActivityType.Volume:
			return BuildVolumeVisual(act);
		case ActivityType.BatteryLow:
			return BuildBatteryLowVisual(act);
		case ActivityType.BatteryCharging:
		case ActivityType.BatteryFull:
			return BuildChargingVisual(act);
		case ActivityType.ClipboardCopied:
			return BuildCopiedVisual(act);
		case ActivityType.TimerRunning:
		{
			ContentControl contentControl = new ContentControl();
			contentControl.Content = NotchGlyphs.Create("timer", RestingGlyphSize, foregroundBrush);
			contentControl.VerticalAlignment = VerticalAlignment.Center;
			contentControl.Margin = new Thickness(0.0, 0.0, 7.0, 0.0);
			ContentControl element = contentControl;
			stackPanel2.Children.Add(element);
			_timerLiveText = new TextBlock
			{
				Text = act.Title,
				FontSize = PrimaryFontSize,
				FontWeight = AynType.Medium,
				Foreground = foregroundBrush,
				FontFamily = AynType.Family(TypeSurface.Notch, PrimaryFontSize),
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
			};
			stackPanel2.Children.Add(_timerLiveText);
			_timerIsPausedVisual = !act.IsPlaying;
			_timerPlayPauseIconHost = new ContentControl
			{
				Content = (_timerIsPausedVisual ? VectorIcons.CreatePlayMediaIcon(9.0, System.Windows.Media.Brushes.White) : VectorIcons.CreatePauseMediaIcon(9.0, System.Windows.Media.Brushes.White)),
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Center
			};
			_timerPlayPauseButton = new Border
			{
				Width = 18.0,
				Height = 18.0,
				CornerRadius = new CornerRadius(9.0),
				Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
				Cursor = System.Windows.Input.Cursors.Hand,
				VerticalAlignment = VerticalAlignment.Center,
				Child = _timerPlayPauseIconHost,
				ToolTip = (_timerIsPausedVisual ? "Resume timer" : "Pause timer")
			};
			_timerPlayPauseButton.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
				TimerService instance2 = TimerService.Instance;
				if (instance2.CountIsRunning)
				{
					if (instance2.CountIsPaused)
					{
						instance2.ResumeCountdown();
					}
					else
					{
						instance2.PauseCountdown();
					}
				}
				else if (instance2.PomoIsRunning)
				{
					if (instance2.PomoIsPaused)
					{
						instance2.ResumePomodoro();
					}
					else
					{
						instance2.PausePomodoro();
					}
				}
				else if (instance2.StopwatchIsRunning)
				{
					instance2.PauseStopwatch();
				}
			};
			stackPanel2.Children.Add(_timerPlayPauseButton);
			return stackPanel2;
		}
		case ActivityType.TimerComplete:
		case ActivityType.HydrationReminder:
			return BuildReminderVisual(act);
		case ActivityType.ScreenTimeLimit:
		{
			Action action = delegate
			{
				PillActivityArbiter.Instance.DismissCurrentTransient();
				ToolRouter.Instance.RouteTo("screentime");
				RequestExpand("Screen time alert View clicked");
			};
			return BuildMessageCard(act, "hourglass", LiveMessageVisuals.Indigo, act.Title ?? "Daily limit reached", DisplaySubtitle(act), new MessageAction[2]
			{
				new MessageAction("View", primary: true, action),
				new MessageAction("Dismiss", primary: false, delegate
				{
					PillActivityArbiter.Instance.DismissCurrentTransient();
				})
			}, action);
		}
		case ActivityType.CompanionMessage:
			if (!(act.Payload is MessageSettings settings) || (act.Id ?? "").StartsWith("notice_", StringComparison.Ordinal))
			{
				return BuildMoment(act);
			}
			return MessageRenderer.CreateRealCapsuleVisual(act.Title, settings);
		case ActivityType.CalendarAlert:
			if (((act.Payload as string) ?? "").StartsWith("r|", StringComparison.Ordinal))
			{
				return BuildCalendarReminderVisual(act);
			}
			return BuildMessageCard(act, "calendar", LiveMessageVisuals.Red, act.Title ?? "Calendar", act.Subtitle, null, null);
		case ActivityType.Clock:
			return BuildClockVisual(act, isHovered, current, foregroundBrush, secondaryForeground);
		case ActivityType.Custom:
		{
			if (act.Id == "drop_complete")
			{
				List<MessageAction> list = new List<MessageAction>();
				if (act.PlayPauseAction != null)
				{
					list.Add(new MessageAction("Open", primary: true, act.PlayPauseAction));
					list.Add(new MessageAction("Show in folder", primary: false, act.NextAction));
				}
				else
				{
					list.Add(new MessageAction("Done", primary: true, delegate
					{
						PillActivityArbiter.Instance.DismissCurrentTransient();
					}));
				}
				return BuildMessageCard(act, "phone_arrow_up", LiveMessageVisuals.Blue, act.Title, act.Subtitle, list, null);
			}
			if (act.Id == "update_ready" || act.Id == "backup_restore_offer")
			{
				return BuildMessageCard(act, (act.Id == "update_ready") ? "download" : "backup", LiveMessageVisuals.Blue, act.Title, act.Subtitle, act.Payload as IList<MessageAction>, null);
			}
			if ((act.Id ?? "").StartsWith("analytics", StringComparison.Ordinal))
			{
				return BuildMessagePill(null, LiveMessageVisuals.Green, act.Title, act.Subtitle, act.Profile, LiveMessageVisuals.VisitorsDot(22.0 * MessageScale));
			}
			if (!(act.Id == "aicoding_budget_alert"))
			{
				break;
			}
			Action run = delegate
			{
				if (EntitlementGate.IsEntitled)
				{
					ToolRegistry.Instance.SetToolEnabled("aicoding", enabled: true);
					ToolRouter.Instance.RouteTo("aicoding");
					if (_machine != null)
					{
						_machine.Click("AI coding: View");
					}
					PillActivityArbiter.Instance.DismissCurrentTransient();
				}
			};
			return BuildMessageCard(act, "sparkle", LiveMessageVisuals.Amber, act.Title ?? "AI budget", DisplaySubtitle(act), new MessageAction[2]
			{
				new MessageAction("View", primary: true, run),
				new MessageAction("Dismiss", primary: false, delegate
				{
					PillActivityArbiter.Instance.DismissCurrentTransient();
				})
			}, null);
		}
		}
		if (act.IconFactory != null)
		{
			stackPanel2.Children.Add(act.IconFactory(12.0, foregroundBrush));
		}
		TextBlock textBlock = new TextBlock();
		textBlock.Text = act.Title ?? "";
		textBlock.FontSize = PrimaryFontSize;
		textBlock.FontWeight = AynType.Medium;
		textBlock.Foreground = foregroundBrush;
		textBlock.FontFamily = AynType.Family(TypeSurface.Notch, PrimaryFontSize);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		textBlock.Margin = new Thickness(5.0, 0.0, 0.0, 0.0);
		TextBlock element2 = textBlock;
		stackPanel2.Children.Add(element2);
		return stackPanel2;
	}

	private FrameworkElement BuildClockVisual(NotchActivity act, bool isHovered, CapsuleTheme theme, System.Windows.Media.Brush fg, System.Windows.Media.Brush secFg)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		if ((act != null && string.Equals(act.Payload as string, "analog", StringComparison.OrdinalIgnoreCase)) || ClockSettings.Instance.DisplayMode == ClockDisplayMode.Analog)
		{
			double num = 15.0;
			double num2 = num / 2.0;
			double num3 = num / 2.0;
			Canvas canvas = new Canvas();
			canvas.Width = num;
			canvas.Height = num;
			canvas.VerticalAlignment = VerticalAlignment.Center;
			canvas.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
			canvas.ClipToBounds = false;
			Canvas canvas2 = canvas;
			Ellipse ellipse = new Ellipse();
			ellipse.Width = num;
			ellipse.Height = num;
			ellipse.Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(90, byte.MaxValue, byte.MaxValue, byte.MaxValue));
			ellipse.StrokeThickness = 1.0;
			ellipse.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 20, 20, 24));
			Ellipse ellipse2 = ellipse;
			ellipse2.Stroke.Freeze();
			ellipse2.Fill.Freeze();
			Canvas.SetLeft(ellipse2, 0.0);
			Canvas.SetTop(ellipse2, 0.0);
			canvas2.Children.Add(ellipse2);
			DateTime now = DateTime.Now;
			double angle = ((double)(now.Hour % 12) + (double)now.Minute / 60.0) * 30.0;
			double angle2 = ((double)now.Minute + (double)now.Second / 60.0) * 6.0;
			Line line = new Line();
			line.X1 = 0.0;
			line.Y1 = 0.5;
			line.X2 = 0.0;
			line.Y2 = -3.8;
			line.Stroke = System.Windows.Media.Brushes.White;
			line.StrokeThickness = 1.2;
			line.StrokeStartLineCap = PenLineCap.Round;
			line.StrokeEndLineCap = PenLineCap.Round;
			Line line2 = line;
			_clockNotchMiniHourRotate = new RotateTransform(angle, 0.0, 0.0);
			line2.RenderTransform = _clockNotchMiniHourRotate;
			Canvas.SetLeft(line2, num2);
			Canvas.SetTop(line2, num3);
			canvas2.Children.Add(line2);
			Line line3 = new Line();
			line3.X1 = 0.0;
			line3.Y1 = 0.5;
			line3.X2 = 0.0;
			line3.Y2 = -5.4;
			line3.Stroke = System.Windows.Media.Brushes.White;
			line3.StrokeThickness = 1.0;
			line3.StrokeStartLineCap = PenLineCap.Round;
			line3.StrokeEndLineCap = PenLineCap.Round;
			Line line4 = line3;
			_clockNotchMiniMinuteRotate = new RotateTransform(angle2, 0.0, 0.0);
			line4.RenderTransform = _clockNotchMiniMinuteRotate;
			Canvas.SetLeft(line4, num2);
			Canvas.SetTop(line4, num3);
			canvas2.Children.Add(line4);
			Ellipse ellipse3 = new Ellipse();
			ellipse3.Width = 1.8;
			ellipse3.Height = 1.8;
			ellipse3.Fill = System.Windows.Media.Brushes.White;
			Ellipse element = ellipse3;
			Canvas.SetLeft(element, num2 - 0.9);
			Canvas.SetTop(element, num3 - 0.9);
			canvas2.Children.Add(element);
			stackPanel2.Children.Add(canvas2);
			StartClockNotchAnalogTimer();
		}
		else
		{
			StopClockNotchAnalogTimer();
			string text = ClockSettings.Instance.FormatTime(DateTime.Now, includeSeconds: false);
			_clockNotchLiveTimeText = new TextBlock
			{
				Text = text,
				FontSize = PrimaryFontSize,
				FontWeight = AynType.Medium,
				Foreground = fg,
				FontFamily = AynType.Family(TypeSurface.Notch, PrimaryFontSize),
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Center
			};
			BuddyTypography.SetTabularFigures(_clockNotchLiveTimeText);
			stackPanel2.Children.Add(_clockNotchLiveTimeText);
		}
		return stackPanel2;
	}

	private void StartClockNotchAnalogTimer()
	{
		if (_clockNotchAnalogTimer == null)
		{
			_clockNotchAnalogTimer = new DispatcherTimer(DispatcherPriority.Background);
			_clockNotchAnalogTimer.Interval = TimeSpan.FromSeconds(10.0);
			_clockNotchAnalogTimer.Tick += delegate
			{
				UpdateClockNotchAnalogFace();
			};
		}
		if (!_clockNotchAnalogTimer.IsEnabled)
		{
			_clockNotchAnalogTimer.Start();
		}
	}

	private void StopClockNotchAnalogTimer()
	{
		if (_clockNotchAnalogTimer != null && _clockNotchAnalogTimer.IsEnabled)
		{
			_clockNotchAnalogTimer.Stop();
		}
	}

	private void UpdateClockNotchAnalogFace()
	{
		if (!IsRestingSurface && _state != CapsuleDisplayState.Collapsing)
		{
			StopClockNotchAnalogTimer();
			return;
		}
		NotchActivity currentActivity = DynamicActivityService.Instance.CurrentActivity;
		if (currentActivity == null || currentActivity.Type != ActivityType.Clock || !ClockSettings.Instance.ShowInNotch || ClockSettings.Instance.DisplayMode != ClockDisplayMode.Analog)
		{
			StopClockNotchAnalogTimer();
			return;
		}
		DateTime now = DateTime.Now;
		double angle = ((double)(now.Hour % 12) + (double)now.Minute / 60.0) * 30.0;
		double angle2 = ((double)now.Minute + (double)now.Second / 60.0) * 6.0;
		if (_clockNotchMiniHourRotate != null)
		{
			_clockNotchMiniHourRotate.Angle = angle;
		}
		if (_clockNotchMiniMinuteRotate != null)
		{
			_clockNotchMiniMinuteRotate.Angle = angle2;
		}
	}

	private static string CleanSongTitle(string raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return "Now Playing";
		}
		string text = raw.Trim();
		int num = text.IndexOf(" - Remastered", StringComparison.OrdinalIgnoreCase);
		if (num > 0)
		{
			text = text.Substring(0, num).Trim();
		}
		num = text.IndexOf(" (Remastered", StringComparison.OrdinalIgnoreCase);
		if (num > 0)
		{
			text = text.Substring(0, num).Trim();
		}
		num = text.IndexOf(" (feat.", StringComparison.OrdinalIgnoreCase);
		if (num > 0)
		{
			text = text.Substring(0, num).Trim();
		}
		num = text.IndexOf(" [Official", StringComparison.OrdinalIgnoreCase);
		if (num > 0)
		{
			text = text.Substring(0, num).Trim();
		}
		return text;
	}

	private void CheckDesktopVisibility()
	{
		try
		{
			NotchVisibilityEngine.Instance.EvaluateVisibility("foreground changed");
		}
		catch (Exception ex)
		{
			NotchLog.Error("Visibility evaluation", ex);
		}
	}

	private bool IsExclusiveFullscreen(IntPtr hWnd)
	{
		if (hWnd == IntPtr.Zero)
		{
			return false;
		}
		if (IsDesktopWindow(hWnd))
		{
			return false;
		}
		try
		{
			if (GetWindowRect(hWnd, out var lpRect))
			{
				POINT pOINT = default(POINT);
				pOINT.X = lpRect.Left + 10;
				pOINT.Y = lpRect.Top + 10;
				POINT pt = pOINT;
				IntPtr intPtr = MonitorFromPoint(pt, 2u);
				if (intPtr != IntPtr.Zero)
				{
					MONITORINFO lpmi = default(MONITORINFO);
					lpmi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
					if (GetMonitorInfo(intPtr, ref lpmi) && lpRect.Left <= lpmi.rcMonitor.Left && lpRect.Top <= lpmi.rcMonitor.Top && lpRect.Right >= lpmi.rcMonitor.Right && lpRect.Bottom >= lpmi.rcMonitor.Bottom)
					{
						int windowLong = GetWindowLong(hWnd, -16);
						if ((windowLong & 0xC00000) != 12582912)
						{
							return true;
						}
					}
				}
			}
		}
		catch
		{
		}
		return false;
	}

	private bool IsDesktopWindow(IntPtr hWnd)
	{
		if (hWnd == IntPtr.Zero)
		{
			return true;
		}
		StringBuilder stringBuilder = new StringBuilder(256);
		GetClassName(hWnd, stringBuilder, stringBuilder.Capacity);
		switch (stringBuilder.ToString())
		{
		case "Progman":
		case "WorkerW":
		case "Shell_TrayWnd":
		case "Shell_SecondaryTrayWnd":
		case "Windows.UI.Core.CoreWindow":
		case "XamlExplorerHostIslandWindow":
		case "TopLevelWindowForOverflowXamlIsland":
			return true;
		default:
			return false;
		}
	}

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

	private static IntPtr GetNotchMonitor()
	{
		string text = ToolRegistry.Instance.PinnedDisplay ?? "primary";
		if (string.Equals(text, "cursor", StringComparison.OrdinalIgnoreCase) && GetCursorPos(out var lpPoint))
		{
			IntPtr intPtr = MonitorFromPoint(lpPoint, 2u);
			if (intPtr != IntPtr.Zero)
			{
				return intPtr;
			}
		}
		else if (text.StartsWith("\\\\.\\", StringComparison.Ordinal))
		{
			try
			{
				Screen[] allScreens = Screen.AllScreens;
				foreach (Screen screen in allScreens)
				{
					if (string.Equals(screen.DeviceName, text, StringComparison.OrdinalIgnoreCase))
					{
						System.Drawing.Rectangle bounds = screen.Bounds;
						POINT pt = default(POINT);
						pt.X = bounds.Left + bounds.Width / 2;
						pt.Y = bounds.Top + bounds.Height / 2;
						IntPtr intPtr2 = MonitorFromPoint(pt, 2u);
						if (intPtr2 != IntPtr.Zero)
						{
							return intPtr2;
						}
					}
				}
			}
			catch
			{
			}
		}
		POINT pt2 = default(POINT);
		pt2.X = 0;
		pt2.Y = 0;
		return MonitorFromPoint(pt2, 1u);
	}

	private bool TryGetTargetPosition(out int x, out int y)
	{
		x = (y = 0);
		IntPtr notchMonitor = GetNotchMonitor();
		MONITORINFO lpmi = default(MONITORINFO);
		lpmi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
		if (notchMonitor == IntPtr.Zero || !GetMonitorInfo(notchMonitor, ref lpmi))
		{
			return false;
		}
		uint dpiX = 96u;
		uint dpiY = 96u;
		try
		{
			GetDpiForMonitor(notchMonitor, 0, out dpiX, out dpiY);
		}
		catch
		{
		}
		if (dpiX == 0)
		{
			dpiX = 96u;
		}
		RECT rcWork = lpmi.rcWork;
		double num = (double)dpiX / 96.0;
		double num2 = Math.Min(1.0, Math.Min((double)(rcWork.Right - rcWork.Left) / (800.0 * num), (double)(rcWork.Bottom - rcWork.Top) / (388.0 * num)));
		if (_rootGrid != null && Math.Abs(_panelFitScale - num2) > 0.0001)
		{
			_panelFitScale = num2;
			_rootGrid.LayoutTransform = ((num2 < 1.0) ? new ScaleTransform(num2, num2) : Transform.Identity);
			_rootGrid.Margin = new Thickness(24.0 * num2, 0.0, 24.0 * num2, 36.0 * num2);
		}
		int num3 = (int)Math.Round(800.0 * num2 * num);
		x = rcWork.Left + (rcWork.Right - rcWork.Left - num3) / 2;
		y = rcWork.Top;
		return true;
	}

	public void UpdatePosition(bool initial)
	{
		try
		{
			IntPtr handle = new WindowInteropHelper(this).Handle;
			if (handle != IntPtr.Zero && TryGetTargetPosition(out var x, out var y))
			{
				base.Width = 800.0 * _panelFitScale;
				base.Height = 388.0 * _panelFitScale;
				SetWindowPos(handle, IntPtr.Zero, x, y, 0, 0, 21u);
				return;
			}
		}
		catch
		{
		}
		Rect workArea = SystemParameters.WorkArea;
		base.Width = 800.0;
		base.Height = 388.0;
		base.Left = Math.Round(workArea.Left + (workArea.Width - 800.0) / 2.0);
		base.Top = workArea.Top;
	}

	private void UpdateCollapsedClock(DateTime now)
	{
		if (_capsuleLifecycleState == CapsuleState.Welcome || (!IsRestingSurface && _state != CapsuleDisplayState.Collapsing))
		{
			return;
		}
		NotchConfig instance = NotchConfig.Instance;
		if (instance == null || !instance.ShowTime)
		{
			return;
		}
		if (_collapsedPrimaryValueText != null)
		{
			ClockSettings instance2 = ClockSettings.Instance;
			string text = RestingFit.TimeText(now, RestingTimeForm, instance.ShowDateInNotch, instance2.ShowSeconds, instance2.FormatTime);
			if (_collapsedPrimaryValueText.Text != text)
			{
				_collapsedPrimaryValueText.Text = text;
			}
			if (now.Second % 5 == 0)
			{
				_ = (_isSlotAActive ? _slotA : _slotB)?.Content;
			}
		}
		if (_chargingTimeText != null && _collapsedPrimaryValueText != null && _chargingTimeText.Text != _collapsedPrimaryValueText.Text)
		{
			_chargingTimeText.Text = _collapsedPrimaryValueText.Text;
		}
		UpdateMediaClock();
		if (_clockNotchLiveTimeText != null)
		{
			string text2 = ClockSettings.Instance.FormatTime(now, includeSeconds: false);
			if (_clockNotchLiveTimeText.Text != text2)
			{
				_clockNotchLiveTimeText.Text = text2;
			}
		}
		if (instance.NotchAnalogClock)
		{
			if (_collapsedMiniAnalogCanvas != null && _collapsedMiniAnalogCanvas.Visibility != 0)
			{
				_collapsedMiniAnalogCanvas.Visibility = Visibility.Visible;
			}
			if (_collapsedPrimaryValueText != null && _collapsedPrimaryValueText.Visibility != Visibility.Collapsed)
			{
				_collapsedPrimaryValueText.Visibility = Visibility.Collapsed;
			}
			double angle = (double)now.Second * 6.0;
			double angle2 = ((double)now.Minute + (double)now.Second / 60.0) * 6.0;
			double angle3 = ((double)(now.Hour % 12) + (double)now.Minute / 60.0) * 30.0;
			if (_miniAnalogSecondRotate != null)
			{
				_miniAnalogSecondRotate.Angle = angle;
			}
			if (_miniAnalogMinuteRotate != null)
			{
				_miniAnalogMinuteRotate.Angle = angle2;
			}
			if (_miniAnalogHourRotate != null)
			{
				_miniAnalogHourRotate.Angle = angle3;
			}
		}
		else
		{
			if (_collapsedMiniAnalogCanvas != null && _collapsedMiniAnalogCanvas.Visibility != Visibility.Collapsed)
			{
				_collapsedMiniAnalogCanvas.Visibility = Visibility.Collapsed;
			}
			if (_collapsedPrimaryValueText != null && _collapsedPrimaryValueText.Visibility != 0)
			{
				_collapsedPrimaryValueText.Visibility = Visibility.Visible;
			}
		}
		string batteryText = ((_collapsedBatteryText != null) ? _collapsedBatteryText.Text : "");
		if (PetPreferences.Instance.Enabled)
		{
			UpdatePetResting();
		}
		DynamicActivityService.Instance.SetRestingClock((_collapsedPrimaryValueText != null) ? _collapsedPrimaryValueText.Text : "", now.ToString("ddd, MMM d"), batteryText);
		if (_activeOwner.VisualType == CollapsedVisualType.Clock)
		{
			EnforceSlotSettledInvariants("ClockTick");
			RefreshRestingContentSize();
		}
	}

	private void RefreshRestingContentSize()
	{
		if (IsRestingSurface && _capsuleCard != null)
		{
			System.Windows.Size activeCollapsedSize = GetActiveCollapsedSize(PillActivityArbiter.Instance.CurrentActivity);
			if (!(Math.Abs(activeCollapsedSize.Width - _lastCapsuleTargetWidth) < 0.5))
			{
				NotchConfig instance = NotchConfig.Instance;
				EnsureMorphGeometryCache(activeCollapsedSize.Width, activeCollapsedSize.Height, instance.Style, instance.CornerRadius);
				ResizeRestingSurface(activeCollapsedSize, animate: true);
			}
		}
	}

	private Canvas BuildMiniAnalogCanvas()
	{
		double num = 24.0;
		double num2 = num / 2.0;
		double num3 = num / 2.0;
		Canvas canvas = new Canvas();
		canvas.Width = num;
		canvas.Height = num;
		canvas.VerticalAlignment = VerticalAlignment.Center;
		canvas.Margin = new Thickness(0.0, 0.0, 0.0, 0.0);
		canvas.ClipToBounds = false;
		Canvas canvas2 = canvas;
		Ellipse ellipse = new Ellipse();
		ellipse.Width = num;
		ellipse.Height = num;
		ellipse.Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(90, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		ellipse.StrokeThickness = 1.0;
		ellipse.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 20, 20, 24));
		Ellipse element = ellipse;
		Canvas.SetLeft(element, 0.0);
		Canvas.SetTop(element, 0.0);
		canvas2.Children.Add(element);
		for (int i = 1; i <= 12; i++)
		{
			double num4 = (double)i * 30.0 * Math.PI / 180.0;
			bool flag = i % 3 == 0;
			double num5 = (flag ? 2.5 : 1.2);
			double num6 = 10.5;
			double num7 = num6 - num5;
			Line line = new Line();
			line.X1 = num2 + num6 * Math.Sin(num4);
			line.Y1 = num3 - num6 * Math.Cos(num4);
			line.X2 = num2 + num7 * Math.Sin(num4);
			line.Y2 = num3 - num7 * Math.Cos(num4);
			line.Stroke = (flag ? System.Windows.Media.Brushes.White : new SolidColorBrush(System.Windows.Media.Color.FromArgb(140, byte.MaxValue, byte.MaxValue, byte.MaxValue)));
			line.StrokeThickness = (flag ? 1.1 : 0.65);
			line.StrokeStartLineCap = PenLineCap.Square;
			line.StrokeEndLineCap = PenLineCap.Square;
			Line element2 = line;
			canvas2.Children.Add(element2);
		}
		Line line2 = new Line();
		line2.X1 = 0.0;
		line2.Y1 = 1.0;
		line2.X2 = 0.0;
		line2.Y2 = -5.8;
		line2.Stroke = System.Windows.Media.Brushes.White;
		line2.StrokeThickness = 1.5;
		line2.StrokeStartLineCap = PenLineCap.Round;
		line2.StrokeEndLineCap = PenLineCap.Round;
		Line line3 = line2;
		_miniAnalogHourRotate = new RotateTransform(0.0, 0.0, 0.0);
		line3.RenderTransform = _miniAnalogHourRotate;
		Canvas.SetLeft(line3, num2);
		Canvas.SetTop(line3, num3);
		canvas2.Children.Add(line3);
		Line line4 = new Line();
		line4.X1 = 0.0;
		line4.Y1 = 1.2;
		line4.X2 = 0.0;
		line4.Y2 = -8.6;
		line4.Stroke = System.Windows.Media.Brushes.White;
		line4.StrokeThickness = 1.0;
		line4.StrokeStartLineCap = PenLineCap.Round;
		line4.StrokeEndLineCap = PenLineCap.Round;
		Line line5 = line4;
		_miniAnalogMinuteRotate = new RotateTransform(0.0, 0.0, 0.0);
		line5.RenderTransform = _miniAnalogMinuteRotate;
		Canvas.SetLeft(line5, num2);
		Canvas.SetTop(line5, num3);
		canvas2.Children.Add(line5);
		Line line6 = new Line();
		line6.X1 = 0.0;
		line6.Y1 = 1.8;
		line6.X2 = 0.0;
		line6.Y2 = -9.6;
		line6.Stroke = System.Windows.Media.Brushes.White;
		line6.StrokeThickness = 0.75;
		line6.StrokeStartLineCap = PenLineCap.Round;
		line6.StrokeEndLineCap = PenLineCap.Round;
		Line line7 = line6;
		_miniAnalogSecondRotate = new RotateTransform(0.0, 0.0, 0.0);
		line7.RenderTransform = _miniAnalogSecondRotate;
		Canvas.SetLeft(line7, num2);
		Canvas.SetTop(line7, num3);
		canvas2.Children.Add(line7);
		Ellipse ellipse2 = new Ellipse();
		ellipse2.Width = 2.4;
		ellipse2.Height = 2.4;
		ellipse2.Fill = System.Windows.Media.Brushes.White;
		Ellipse element3 = ellipse2;
		Canvas.SetLeft(element3, num2 - 1.2);
		Canvas.SetTop(element3, num3 - 1.2);
		canvas2.Children.Add(element3);
		return canvas2;
	}

	private TextBlock CreateBadgeTextBlock(string text, FontWeight weight, System.Windows.Media.Color color)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.FontSize = SecondaryFontSize;
		textBlock.FontWeight = weight;
		textBlock.Foreground = new SolidColorBrush(color);
		textBlock.FontFamily = AynType.Family(TypeSurface.Notch, SecondaryFontSize);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		textBlock.Margin = new Thickness(2.0, 0.0, 2.0, 0.0);
		return textBlock;
	}

	private static double Half(double v)
	{
		return Math.Round(v * 2.0) / 2.0;
	}

	internal static double NotchSize(double scaleSize)
	{
		return Math.Min(13.0, Half(scaleSize * ActivityScale));
	}

	private static double Gap(double dip)
	{
		return Math.Round(dip * ActivityScale);
	}

	private static System.Windows.Size CardSizeFor(CollapsedActivityProfile profile)
	{
		NotchSettings instance = NotchSettings.Instance;
		return new System.Windows.Size(instance.ScaledProfileWidth(NotchProfileMetrics.Get(profile).TargetWidth), instance.RestingSize.Height);
	}

	internal static Thickness RowMargin(double cardW, double cardH, double rowHeight)
	{
		NotchConfig instance = NotchConfig.Instance;
		double num = ((instance.StyleFamily == "Island") ? 5.0 : 0.0);
		double num2 = num + (cardH - num) / 2.0;
		double a = NotchGeometryBuilder.ContentInset(instance.Style, cardW, cardH, instance.CornerRadius, num2 - rowHeight / 2.0, num2 + rowHeight / 2.0);
		a = Math.Ceiling(a);
		return new Thickness(a, 0.0, a, 0.0);
	}

	internal static void CenterCaps(TextBlock tb)
	{
		if (tb == null)
		{
			return;
		}
		string key = string.Concat(tb.FontFamily, "|", tb.FontSize.ToString("0.##", CultureInfo.InvariantCulture), "|", tb.FontWeight);
		double value;
		lock (CapsOffsets)
		{
			if (!CapsOffsets.TryGetValue(key, out value))
			{
				try
				{
					FormattedText formattedText = new FormattedText("H0", CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight, new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch), tb.FontSize, System.Windows.Media.Brushes.White);
					Rect bounds = formattedText.BuildGeometry(new System.Windows.Point(0.0, 0.0)).Bounds;
					value = (bounds.IsEmpty ? 0.0 : (formattedText.Height / 2.0 - (bounds.Top + bounds.Bottom) / 2.0));
				}
				catch
				{
					value = 0.0;
				}
				CapsOffsets[key] = value;
			}
		}
		double num = 1.0;
		try
		{
			num = VisualTreeHelper.GetDpi(tb).DpiScaleY;
		}
		catch
		{
		}
		double num2 = Math.Round(value * num) / num;
		tb.RenderTransform = ((Math.Abs(num2) < 0.01) ? Transform.Identity : new TranslateTransform(0.0, num2));
	}

	internal static TextBlock NotchText(string text, double size, FontWeight weight, System.Windows.Media.Brush fg)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text ?? string.Empty;
		textBlock.FontSize = size;
		textBlock.FontWeight = weight;
		textBlock.Foreground = fg;
		textBlock.FontFamily = AynType.Family(TypeSurface.Notch, size);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		TextBlock textBlock2 = textBlock;
		BuddyTypography.SetTabularFigures(textBlock2);
		CenterCaps(textBlock2);
		return textBlock2;
	}

	private void ApplyRestingLayout()
	{
		UpdatePetResting();
		if (_defaultCollapsedStack != null)
		{
			NotchConfig instance = NotchConfig.Instance;
			bool flag = instance.RestingLayout != "Center";
			Grid defaultCollapsedStack = _defaultCollapsedStack;
			defaultCollapsedStack.ColumnDefinitions.Clear();
			if (flag)
			{
				defaultCollapsedStack.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = GridLength.Auto
				});
				defaultCollapsedStack.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				});
				defaultCollapsedStack.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = GridLength.Auto
				});
				Grid.SetColumn(_restingLeft, 0);
				Grid.SetColumn(_restingSeparator, 1);
				Grid.SetColumn(_restingRight, 2);
				_restingSeparator.Visibility = Visibility.Collapsed;
			}
			else
			{
				defaultCollapsedStack.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				});
				defaultCollapsedStack.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = GridLength.Auto
				});
				defaultCollapsedStack.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = GridLength.Auto
				});
				defaultCollapsedStack.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = GridLength.Auto
				});
				defaultCollapsedStack.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				});
				Grid.SetColumn(_restingLeft, 1);
				Grid.SetColumn(_restingSeparator, 2);
				Grid.SetColumn(_restingRight, 3);
				_restingSeparator.Visibility = ((!instance.ShowTime || !instance.ShowBattery || !SystemMonitorService.GetLiveTelemetry().HasBattery) ? Visibility.Collapsed : Visibility.Visible);
			}
			_restingLeft.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
			_restingRight.HorizontalAlignment = (flag ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Left);
			ApplyRestingMetrics();
		}
	}

	private void ApplyRestingMetrics()
	{
		if (_defaultCollapsedStack == null)
		{
			return;
		}
		System.Windows.Size restingSize = NotchSettings.Instance.RestingSize;
		double num = restingSize.Width * 1000.0 + restingSize.Height + ActivityScale / 10.0 + (double)(NotchConfig.Instance.Style ?? "").GetHashCode() * 1E-07;
		if (_collapsedPrimaryValueText != null)
		{
			_collapsedPrimaryValueText.FontSize = PrimaryFontSize;
			_collapsedPrimaryValueText.FontFamily = AynType.Family(TypeSurface.Notch, PrimaryFontSize);
			CenterCaps(_collapsedPrimaryValueText);
		}
		if (_collapsedBatteryText != null)
		{
			_collapsedBatteryText.FontSize = NumberFontSize;
			_collapsedBatteryText.FontFamily = AynType.Family(TypeSurface.Notch, NumberFontSize);
			CenterCaps(_collapsedBatteryText);
		}
		if (_restingSeparator != null)
		{
			_restingSeparator.Height = Gap(10.0);
		}
		double rowHeight = Math.Max(PrimaryFontSize * 1.35, RestingGlyphSize);
		_defaultCollapsedStack.Margin = RowMargin(Math.Max(restingSize.Width, _restingFitWidth), restingSize.Height, rowHeight);
		if (num != _restingMetricsKey)
		{
			_restingMetricsKey = num;
			_lastRenderedBatteryKey = null;
			_lastRenderedBluetoothKey = (_lastRenderedNowPlayingKey = null);
			if (_restingBatteryPercent >= 0)
			{
				SetRestingBattery(_restingBatteryPercent, _restingBatteryCharging);
			}
		}
	}

	private RestingWants ComputeRestingWants()
	{
		if (RestingWantsOverride != null)
		{
			return RestingWants.Parse(RestingWantsOverride);
		}
		if (EyesModeOn)
		{
			return default(RestingWants);
		}
		NotchConfig instance = NotchConfig.Instance;
		SystemTelemetryData liveTelemetry = SystemMonitorService.GetLiveTelemetry();
		RestingWants result = default(RestingWants);
		result.Time = instance.ShowTime;
		result.Analog = instance.ShowTime && instance.NotchAnalogClock;
		result.Date = instance.ShowTime && !instance.NotchAnalogClock && instance.ShowDateInNotch;
		result.Bluetooth = instance.ShowBluetooth && IsBluetoothDeviceConnectedCached();
		NotchBehaviorConfig instance2 = NotchBehaviorConfig.Instance;
		result.NowPlaying = instance.ShowNowPlaying && instance2.ShowMediaWhenPlaybackStarts && MediaService.Instance != null && MediaService.Instance.IsPlaying && (!MediaService.IsBrowserSource((MediaService.Instance.View != null) ? MediaService.Instance.View.Source : null) || instance2.ShowBrowserMediaInNotch);
		result.Battery = instance.ShowBattery && liveTelemetry.HasBattery;
		result.Charging = result.Battery && _restingBatteryCharging;
		return result;
	}

	internal static double NotchMonitorWidthDip()
	{
		Func<double> monitorWidthOverride = MonitorWidthOverride;
		if (monitorWidthOverride != null)
		{
			return monitorWidthOverride();
		}
		try
		{
			IntPtr notchMonitor = GetNotchMonitor();
			MONITORINFO lpmi = default(MONITORINFO);
			lpmi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
			if (notchMonitor != IntPtr.Zero && GetMonitorInfo(notchMonitor, ref lpmi))
			{
				uint dpiX = 96u;
				uint dpiY = 96u;
				try
				{
					GetDpiForMonitor(notchMonitor, 0, out dpiX, out dpiY);
				}
				catch
				{
				}
				if (dpiX == 0)
				{
					dpiX = 96u;
				}
				return (double)(lpmi.rcWork.Right - lpmi.rcWork.Left) / ((double)dpiX / 96.0);
			}
		}
		catch
		{
		}
		return SystemParameters.WorkArea.Width;
	}

	private double TextWidth(string text, TextBlock like)
	{
		if (_restingProbe == null)
		{
			_restingProbe = new TextBlock();
		}
		TextBlock restingProbe = _restingProbe;
		restingProbe.FontFamily = like.FontFamily;
		restingProbe.FontSize = like.FontSize;
		restingProbe.FontWeight = like.FontWeight;
		restingProbe.FontStyle = like.FontStyle;
		restingProbe.FontStretch = like.FontStretch;
		BuddyTypography.SetTabularFigures(restingProbe);
		restingProbe.Text = text;
		restingProbe.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
		return restingProbe.DesiredSize.Width;
	}

	private static double MeasuredWidth(FrameworkElement el)
	{
		if (el == null)
		{
			return 0.0;
		}
		el.Visibility = Visibility.Visible;
		el.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
		return el.DesiredSize.Width;
	}

	private RestingPlan BuildRestingPlan(RestingWants wants, System.Windows.Size rest, double monitorDip, string key)
	{
		NotchConfig instance = NotchConfig.Instance;
		ClockSettings instance2 = ClockSettings.Instance;
		List<RestingFitItem> list = new List<RestingFitItem>();
		RestingFitItem restingFitItem = null;
		RestingFitItem restingFitItem2 = null;
		RestingFitItem restingFitItem3 = null;
		RestingFitItem restingFitItem4 = null;
		List<int> list2 = new List<int>();
		if (wants.Time)
		{
			if (wants.Analog)
			{
				list.Add(new RestingFitItem
				{
					Id = "clock",
					FullWidth = MeasuredWidth(_collapsedMiniAnalogCanvas)
				});
			}
			else
			{
				double[] array = new double[4];
				for (int i = 0; i < 7; i++)
				{
					int[] array2 = new int[2] { 10, 23 };
					foreach (int hour in array2)
					{
						DateTime now = new DateTime(2000, 1, 24 + i, hour, 8, 8);
						for (int k = 0; k < 4; k++)
						{
							array[k] = Math.Max(array[k], TextWidth(RestingFit.TimeText(now, k, wants.Date, instance2.ShowSeconds, instance2.FormatTime), _collapsedPrimaryValueText));
						}
					}
				}
				RestingFitItem restingFitItem5 = new RestingFitItem();
				restingFitItem5.Id = "time";
				restingFitItem5.FullWidth = array[0];
				restingFitItem5.DropOrder = 2;
				restingFitItem = restingFitItem5;
				double num = array[0];
				int[] array3 = new int[4] { 0, 1, 3, 4 };
				for (int l = 1; l < 4; l++)
				{
					if (!(array[l] >= num - 0.5))
					{
						restingFitItem.Shrinks(array3[l], array[l]);
						list2.Add(l);
						num = array[l];
					}
				}
				list.Add(restingFitItem);
			}
		}
		if (wants.Bluetooth)
		{
			RestingFitItem restingFitItem6 = new RestingFitItem();
			restingFitItem6.Id = "bluetooth";
			restingFitItem6.FullWidth = MeasuredWidth(_collapsedBluetoothIconHolder);
			restingFitItem6.DropOrder = 1;
			restingFitItem3 = restingFitItem6.Drops(3);
			list.Add(restingFitItem3);
		}
		if (wants.NowPlaying)
		{
			RestingFitItem restingFitItem7 = new RestingFitItem();
			restingFitItem7.Id = "nowplaying";
			restingFitItem7.FullWidth = MeasuredWidth(_collapsedNowPlayingIconHolder);
			restingFitItem7.DropOrder = 0;
			restingFitItem4 = restingFitItem7.Drops(3);
			list.Add(restingFitItem4);
		}
		if (wants.Battery)
		{
			double num2 = MeasuredWidth(_collapsedBatteryIconHolder) + (wants.Charging ? MeasuredWidth(_collapsedBatteryBoltHolder) : 0.0);
			double num3 = TextWidth("100", _collapsedBatteryText);
			RestingFitItem restingFitItem8 = new RestingFitItem();
			restingFitItem8.Id = "battery";
			restingFitItem8.FullWidth = num2 + num3;
			restingFitItem8.DropOrder = 3;
			restingFitItem2 = restingFitItem8.Shrinks(2, num2);
			list.Add(restingFitItem2);
		}
		double gap = ((instance.RestingLayout != "Center") ? Gap(12.0) : ((wants.Time && wants.Battery) ? 21.0 : 0.0));
		double rowH = Math.Max(PrimaryFontSize * 1.35, RestingGlyphSize);
		double maxWidth = RestingFit.MaxWidth(rest.Width, monitorDip);
		RestingFitResult restingFitResult = RestingFit.Plan(list, rest.Width, maxWidth, gap, (double cw) => RowMargin(cw, rest.Height, rowH).Left);
		RestingPlan restingPlan = new RestingPlan();
		restingPlan.Key = key;
		restingPlan.Width = restingFitResult.CardWidth;
		restingPlan.Fits = restingFitResult.Fits;
		RestingPlan restingPlan2 = restingPlan;
		if (restingFitItem != null && restingFitItem.StepsApplied > 0)
		{
			restingPlan2.TimeForm = list2[restingFitItem.StepsApplied - 1];
		}
		restingPlan2.HideBatteryText = restingFitItem2 != null && restingFitItem2.StepsApplied > 0;
		restingPlan2.DropBluetooth = restingFitItem3?.Dropped ?? false;
		restingPlan2.DropNowPlaying = restingFitItem4?.Dropped ?? false;
		return restingPlan2;
	}

	private void ApplyRestingPlan(RestingWants wants)
	{
		RestingPlan restingPlan = _restingPlan;
		if (restingPlan != null)
		{
			Func<bool, Visibility> func = (bool v) => (!v) ? Visibility.Collapsed : Visibility.Visible;
			if (_collapsedPrimaryValueText != null)
			{
				_collapsedPrimaryValueText.Visibility = func(wants.Time && !wants.Analog);
			}
			if (_collapsedMiniAnalogCanvas != null)
			{
				_collapsedMiniAnalogCanvas.Visibility = func(wants.Analog);
			}
			if (_collapsedBluetoothIconHolder != null)
			{
				_collapsedBluetoothIconHolder.Visibility = func(wants.Bluetooth && !restingPlan.DropBluetooth);
			}
			if (_collapsedNowPlayingIconHolder != null)
			{
				_collapsedNowPlayingIconHolder.Visibility = func(wants.NowPlaying && !restingPlan.DropNowPlaying);
			}
			if (_collapsedBatteryIconHolder != null)
			{
				_collapsedBatteryIconHolder.Visibility = func(wants.Battery);
			}
			if (_collapsedBatteryBoltHolder != null)
			{
				_collapsedBatteryBoltHolder.Visibility = func(wants.Charging);
			}
			if (_collapsedBatteryText != null)
			{
				_collapsedBatteryText.Visibility = func(wants.Battery && !restingPlan.HideBatteryText);
			}
		}
	}

	private void RefreshRestingFit()
	{
		UpdatePetResting();
		if (_defaultCollapsedStack != null && _collapsedPrimaryValueText != null)
		{
			NotchConfig instance = NotchConfig.Instance;
			ClockSettings instance2 = ClockSettings.Instance;
			RestingWants wants = ComputeRestingWants();
			System.Windows.Size restingSize = NotchSettings.Instance.RestingSize;
			double num = NotchMonitorWidthDip();
			string text = wants.Key + "|" + restingSize.Width + "x" + restingSize.Height + "|" + RestingGlyphSize + "|" + PrimaryFontSize + "|" + NumberFontSize + "|" + num + "|" + instance.RestingLayout + "|" + instance.Style + "|" + instance2.ShowSeconds + "|" + instance2.FormatTime(new DateTime(2000, 1, 1, 13, 5, 9), includeSeconds: true) + "|" + CultureInfo.CurrentCulture.Name;
			double restingFitWidth = _restingFitWidth;
			int restingTimeForm = RestingTimeForm;
			if (_restingPlan == null || _restingPlan.Key != text)
			{
				_restingPlan = BuildRestingPlan(wants, restingSize, num, text);
			}
			ApplyRestingPlan(wants);
			_restingFitWidth = (EyesModeOn ? EyesRestingWidth(restingSize) : _restingPlan.Width);
			if (RestingTimeForm != restingTimeForm)
			{
				UpdateCollapsedClock(DateTime.Now);
			}
			if (Math.Abs(restingFitWidth - _restingFitWidth) > 0.5)
			{
				ApplyRestingMetrics();
				ApplyRestingFitWidth();
			}
		}
	}

	private void ApplyRestingFitWidth()
	{
		if (_capsuleCard == null || !IsRestingSurface)
		{
			return;
		}
		PillActivitySnapshot currentActivity = PillActivityArbiter.Instance.CurrentActivity;
		if (currentActivity == null || currentActivity.Type == ActivityType.RestingClock || currentActivity.Type == ActivityType.Clock)
		{
			System.Windows.Size activeCollapsedSize = GetActiveCollapsedSize(currentActivity);
			if (!(Math.Abs(_capsuleCard.Width - activeCollapsedSize.Width) <= 1.5))
			{
				_lastCapsuleTargetWidth = activeCollapsedSize.Width;
				ResizeRestingSurface(activeCollapsedSize, !_restingFitImmediate);
			}
		}
	}

	private void SetRestingBattery(int percent, bool charging)
	{
		if (_collapsedBatteryIconHolder != null)
		{
			NotchConfig instance = NotchConfig.Instance;
			System.Windows.Media.Brush foregroundBrush = instance.GetForegroundBrush();
			_restingBatteryPercent = percent;
			_restingBatteryCharging = charging;
			double batteryHeight = BatteryHeight;
			SystemTelemetryData liveTelemetry = SystemMonitorService.GetLiveTelemetry();
			bool flag = liveTelemetry.IsACConnected && (liveTelemetry.IsFullyCharged || percent == 100);
			string text = percent + "_" + charging + "_" + flag + "_" + liveTelemetry.IsACConnected + "_" + batteryHeight + "_" + foregroundBrush;
			if (text != _lastRenderedBatteryKey || _collapsedBatteryIconHolder.Content == null)
			{
				_lastRenderedBatteryKey = text;
				_collapsedBatteryIconHolder.Content = NotchGlyphs.CreateBattery(batteryHeight, percent, charging, foregroundBrush, flag, liveTelemetry.IsACConnected);
				_collapsedBatteryBoltHolder.Content = NotchGlyphs.Create("bolt", Math.Round(RestingGlyphSize * 0.9), NotchGlyphs.GreenBrush);
				_collapsedBatteryBoltHolder.Margin = new Thickness(0.0, 0.0, Gap(3.0), 0.0);
				_collapsedBatteryIconHolder.Margin = new Thickness(0.0, 0.0, Gap(5.0), 0.0);
			}
			string text2 = ((percent >= 0) ? percent.ToString(CultureInfo.InvariantCulture) : "—");
			if (_collapsedBatteryText.Text != text2)
			{
				_collapsedBatteryText.Text = text2;
			}
			_collapsedBatteryText.Foreground = foregroundBrush;
			bool flag2 = instance.ShowBattery && SystemMonitorService.GetLiveTelemetry().HasBattery;
			Visibility visibility = ((!flag2) ? Visibility.Collapsed : Visibility.Visible);
			_collapsedBatteryIconHolder.Visibility = visibility;
			_collapsedBatteryText.Visibility = ((!flag2 || (_restingPlan != null && _restingPlan.HideBatteryText)) ? Visibility.Collapsed : Visibility.Visible);
			_collapsedBatteryBoltHolder.Visibility = Visibility.Collapsed;
			if (_restingSeparator != null)
			{
				_restingSeparator.Visibility = ((!(instance.RestingLayout == "Center") || !instance.ShowTime || !flag2) ? Visibility.Collapsed : Visibility.Visible);
			}
		}
	}

	internal static Grid BuildRestingStyleRow(UIElement left, UIElement right, System.Windows.Size card)
	{
		bool flag = NotchConfig.Instance.RestingLayout != "Center";
		Grid grid = new Grid();
		grid.VerticalAlignment = VerticalAlignment.Center;
		grid.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
		Grid grid2 = grid;
		grid2.Margin = RowMargin(card.Width, card.Height, Math.Max(PrimaryFontSize * 1.35, RestingGlyphSize));
		if (flag)
		{
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			if (left != null)
			{
				Grid.SetColumn(left, 0);
				grid2.Children.Add(left);
			}
			if (right != null)
			{
				Grid.SetColumn(right, 2);
				grid2.Children.Add(right);
			}
		}
		else
		{
			StackPanel stackPanel = new StackPanel();
			stackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
			stackPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
			stackPanel.VerticalAlignment = VerticalAlignment.Center;
			StackPanel stackPanel2 = stackPanel;
			if (left != null)
			{
				stackPanel2.Children.Add(left);
			}
			if (left != null && right != null)
			{
				stackPanel2.Children.Add(new Border
				{
					Width = 1.0,
					Height = Gap(10.0),
					Margin = new Thickness(10.0, 0.0, 10.0, 0.0),
					VerticalAlignment = VerticalAlignment.Center,
					Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, byte.MaxValue, byte.MaxValue, byte.MaxValue))
				});
			}
			if (right != null)
			{
				stackPanel2.Children.Add(right);
			}
			grid2.Children.Add(stackPanel2);
		}
		return grid2;
	}

	private static int DigitsOf(string s, int fallback)
	{
		string s2 = new string((s ?? "").Where(char.IsDigit).ToArray());
		if (!int.TryParse(s2, out var result))
		{
			return fallback;
		}
		return result;
	}

	internal static double MessageRowHeight(bool actions)
	{
		if (!actions)
		{
			return StatusMessageHeight;
		}
		return CardMessageHeight;
	}

	internal static double MessageGlyph(double row)
	{
		return Math.Floor(Math.Min(22.0 * MessageScale, row - 6.0 * MessageScale));
	}

	internal static double MessageRobot(double row)
	{
		return Math.Floor(Math.Min(22.0 * MessageScale, row - 6.0 * MessageScale));
	}

	internal static double MessageSideInset(bool card)
	{
		NotchConfig instance = NotchConfig.Instance;
		double num = ((instance.StyleFamily == "Island") ? 5.0 : 0.0);
		double num2 = MessageRowHeight(card);
		double num3 = (card ? MessageButtonHeight : Math.Min(num2, MessageGlyph(num2) + 4.0));
		double num4 = num + num2 / 2.0;
		return Math.Ceiling(NotchGeometryBuilder.ContentInset(instance.Style, 360.0 * MessageScale, num2 + num, instance.CornerRadius, num4 - num3 / 2.0, num4 + num3 / 2.0)) + 1.0;
	}

	private static Border RowIcon(string glyph, double row, System.Windows.Media.Color tint, FrameworkElement custom = null)
	{
		double num = MessageGlyph(row);
		double circle = ((glyph == "battery.charge") ? Math.Min(30.0 * MessageScale, row - 4.0 * MessageScale) : (num * 30.0 / 22.0));
		return MessageIcon(glyph, circle, tint, custom);
	}

	private static Grid MessageLine(string title, string detail, out TextBlock heading)
	{
		double messageScale = MessageScale;
		Grid grid = new Grid();
		grid.VerticalAlignment = VerticalAlignment.Center;
		grid.ClipToBounds = true;
		grid.Tag = "message-line";
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		heading = MessageTitle(title, 15.0 * messageScale);
		heading.MaxWidth = 260.0 * messageScale;
		heading.ToolTip = title;
		grid2.Children.Add(heading);
		TextArrival(heading, 70);
		if (!string.IsNullOrWhiteSpace(detail))
		{
			TextBlock textBlock = MessageDetail("· " + detail);
			textBlock.FontSize = 13.5 * messageScale;
			textBlock.Margin = new Thickness(6.0 * messageScale, 0.0, 0.0, 0.0);
			textBlock.Tag = "message-detail";
			Grid.SetColumn(textBlock, 1);
			grid2.Children.Add(textBlock);
			TextArrival(textBlock, 130);
		}
		return grid2;
	}

	internal static double MessageLineWidth(string title, string detail)
	{
		double messageScale = MessageScale;
		return Math.Min(260.0 * messageScale, CopyWidth(title, 15.0 * messageScale, AynType.Semibold)) + (string.IsNullOrWhiteSpace(detail) ? 0.0 : (6.0 * messageScale + CopyWidth("· " + detail, 13.5 * messageScale, AynType.Regular)));
	}

	private static double ButtonsWidth(IEnumerable<string> labels, bool calendar)
	{
		double i = MessageScale;
		List<string> list = labels.ToList();
		if (list.Count == 0)
		{
			return 0.0;
		}
		return 12.0 * i + list.Sum((string l) => CopyWidth(l, 13.0 * i, AynType.Medium) + 22.0 * i) + (double)((list.Count - 1) * 8) * i + (calendar ? (18.0 * i) : 0.0);
	}

	private FrameworkElement BuildChargingVisual(NotchActivity act)
	{
		int num = DigitsOf((act.Payload as string) ?? act.Subtitle ?? act.ValueText, _restingBatteryPercent);
		bool flag = act.Type == ActivityType.BatteryFull;
		if (flag)
		{
			num = Math.Max(num, 100);
		}
		_chargingTimeText = null;
		return BuildMessagePill("battery.charge", LiveMessageVisuals.Green, flag ? "Fully charged" : "Charging", (num >= 0) ? (num.ToString(CultureInfo.InvariantCulture) + "%") : null, CollapsedActivityProfile.Charging, flag ? null : LiveMessageVisuals.Charging(MessageGlyph(StatusMessageHeight)));
	}

	private FrameworkElement BuildBatteryLowVisual(NotchActivity act)
	{
		int num = DigitsOf((act.Payload as string) ?? act.Subtitle ?? act.Title, (_restingBatteryPercent >= 0) ? _restingBatteryPercent : 10);
		FrameworkElement customGlyph = LiveMessageVisuals.Glyph("battery.warning", MessageGlyph(StatusMessageHeight), LiveMessageVisuals.Red);
		return BuildMessagePill(null, NotchGlyphs.Red, "Battery low", num.ToString(CultureInfo.InvariantCulture) + "% · Plug in soon", CollapsedActivityProfile.Resting, customGlyph);
	}

	private static void ReadVolume(PillActivitySnapshot snap, NotchActivity act, out int percent, out bool muted)
	{
		object obj = ((snap != null) ? snap.Payload : act.Payload);
		string text = ((snap != null) ? snap.Title : act.Title);
		percent = 50;
		if (obj != null)
		{
			try
			{
				percent = Convert.ToInt32(obj, CultureInfo.InvariantCulture);
			}
			catch
			{
				percent = DigitsOf(text, 50);
			}
		}
		else
		{
			percent = DigitsOf(text, 50);
		}
		percent = Math.Max(0, Math.Min(100, percent));
		muted = string.Equals(text, "Muted", StringComparison.OrdinalIgnoreCase);
	}

	private FrameworkElement BuildVolumeVisual(NotchActivity act)
	{
		ReadVolume(null, act, out var percent, out var muted);
		double messageScale = MessageScale;
		Grid grid = new Grid();
		grid.Margin = new Thickness(MessageSideInset(card: false), 0.0, MessageSideInset(card: false), 0.0);
		grid.Height = StatusMessageHeight;
		grid.VerticalAlignment = VerticalAlignment.Center;
		Grid grid2 = grid;
		for (int i = 0; i < 4; i++)
		{
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = ((i == 1) ? new GridLength(1.0, GridUnitType.Star) : GridLength.Auto)
			});
		}
		_volumeLiveGlyph = (muted ? "speaker.slash" : ((percent < 34) ? "speaker.wave.1" : "speaker.wave.3"));
		double num = MessageGlyph(StatusMessageHeight);
		_volumeLiveIconHost = new ContentControl
		{
			Content = LiveMessageVisuals.Glyph(_volumeLiveGlyph, num, muted ? LiveMessageVisuals.Grey : Colors.White)
		};
		object obj;
		if (!PetPreferences.Instance.Enabled)
		{
			obj = MessageIcon(null, num * 30.0 / 22.0, muted ? LiveMessageVisuals.Grey : Colors.White, _volumeLiveIconHost);
		}
		else
		{
			AynRobotMark aynRobotMark = new AynRobotMark(MessageRobot(StatusMessageHeight));
			aynRobotMark.VerticalAlignment = VerticalAlignment.Center;
			obj = aynRobotMark;
		}
		FrameworkElement element = (FrameworkElement)obj;
		grid2.Children.Add(element);
		TextBlock textBlock = MessageTitle(muted ? "Muted" : "Volume", 15.0 * messageScale);
		textBlock.Margin = new Thickness(10.0 * messageScale, 0.0, 10.0 * messageScale, 0.0);
		textBlock.Tag = "volume-title";
		Grid.SetColumn(textBlock, 1);
		grid2.Children.Add(textBlock);
		TextArrival(textBlock, 70);
		_volumeLiveBar = new Grid
		{
			Width = 64.0 * messageScale,
			Height = 4.0 * messageScale,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 12.0 * messageScale, 0.0)
		};
		_volumeLiveBar.Children.Add(new Border
		{
			CornerRadius = new CornerRadius(2.0 * messageScale),
			Background = Ink(Colors.White, 46)
		});
		_volumeLiveSliderFill = new Border
		{
			CornerRadius = new CornerRadius(2.0 * messageScale),
			Background = (muted ? Ink(LiveMessageVisuals.Grey, byte.MaxValue) : NotchInk.Primary),
			RenderTransform = new ScaleTransform((double)percent / 100.0, 1.0),
			RenderTransformOrigin = new System.Windows.Point(0.0, 0.5)
		};
		_volumeLiveBar.Children.Add(_volumeLiveSliderFill);
		Grid.SetColumn(_volumeLiveBar, 2);
		grid2.Children.Add(_volumeLiveBar);
		_volumeLivePercentText = MessageDetail(muted ? "" : (percent + "%"));
		_volumeLivePercentText.FontSize = 13.5 * messageScale;
		_volumeLivePercentText.MinWidth = (muted ? 0.0 : (30.0 * messageScale));
		Grid.SetColumn(_volumeLivePercentText, 3);
		grid2.Children.Add(_volumeLivePercentText);
		return grid2;
	}

	private void UpdateVolumeVisualInPlace(PillActivitySnapshot snap)
	{
		if (snap == null || _volumeLiveBar == null)
		{
			return;
		}
		ReadVolume(snap, null, out var percent, out var muted);
		if (_volumeLiveSliderFill.RenderTransform is ScaleTransform { ScaleX: var scaleX } scaleTransform)
		{
			scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
			scaleTransform.ScaleX = (double)percent / 100.0;
			if (LiveMessageVisuals.Motion)
			{
				scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scaleX, (double)percent / 100.0, TimeSpan.FromMilliseconds(130.0))
				{
					EasingFunction = CapsuleMotion.EaseOutCubic,
					FillBehavior = FillBehavior.Stop
				});
			}
		}
		_volumeLiveSliderFill.Background = (muted ? Ink(LiveMessageVisuals.Grey, byte.MaxValue) : NotchInk.Primary);
		_volumeLivePercentText.Text = (muted ? "" : (percent + "%"));
		_volumeLivePercentText.MinWidth = (muted ? 0.0 : (30.0 * MessageScale));
		if (_volumeLiveBar.Parent is Grid grid)
		{
			TextBlock textBlock = grid.Children.OfType<TextBlock>().FirstOrDefault((TextBlock t) => object.Equals(t.Tag, "volume-title"));
			if (textBlock != null)
			{
				textBlock.Text = (muted ? "Muted" : "Volume");
			}
		}
		string text = (muted ? "speaker.slash" : ((percent < 34) ? "speaker.wave.1" : "speaker.wave.3"));
		if (_volumeLiveIconHost != null && text != _volumeLiveGlyph)
		{
			_volumeLiveGlyph = text;
			_volumeLiveIconHost.Content = LiveMessageVisuals.Glyph(text, MessageGlyph(StatusMessageHeight), muted ? LiveMessageVisuals.Grey : Colors.White);
		}
	}

	private FrameworkElement BuildCopiedVisual(NotchActivity act)
	{
		return BuildMessagePill("clipboard.check", LiveMessageVisuals.Blue, "Copied", act.Subtitle, CollapsedActivityProfile.Clipboard);
	}

	internal static System.Windows.Size CalendarReminderCardSize()
	{
		PillActivitySnapshot currentSnapshot = PillActivityArbiter.Instance.CurrentSnapshot;
		if (PillActivityArbiter.IsCalendarReminder(currentSnapshot))
		{
			return MessageSize(currentSnapshot);
		}
		return new System.Windows.Size(Math.Round(420.0 * MessageScale), Math.Round(CardMessageHeight));
	}

	internal static System.Windows.Media.Color ReminderAccent()
	{
		ThemePalette current = ThemeTokens.Current;
		System.Windows.Media.Color result = current?.Accent ?? Colors.White;
		if (current == null || current.IsLight || (result.R > 200 && result.G > 200 && result.B > 200))
		{
			return LiveMessageVisuals.Blue;
		}
		return result;
	}

	private static SolidColorBrush Ink(System.Windows.Media.Color c, byte alpha)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, c.R, c.G, c.B));
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	private static System.Windows.Media.Brush ActionInk(System.Windows.Media.Color accent)
	{
		Func<byte, double> func = (byte v) => (!((double)(int)v / 255.0 <= 0.04045)) ? Math.Pow(((double)(int)v / 255.0 + 0.055) / 1.055, 2.4) : ((double)(int)v / 255.0 / 12.92);
		double num = 0.2126 * func(accent.R) + 0.7152 * func(accent.G) + 0.0722 * func(accent.B);
		if (!(1.05 / (num + 0.05) >= 4.5))
		{
			return NotchInk.Black;
		}
		return NotchInk.Primary;
	}

	internal static string ReminderSubtitle(string key, DateTime now)
	{
		string[] array = (key ?? "").Split('|');
		if (array.Length != 3 || !long.TryParse(array[2], out var result) || result < DateTime.MinValue.Ticks || result > DateTime.MaxValue.Ticks)
		{
			return "Due now";
		}
		DateTime dateTime = new DateTime(result);
		if (Math.Abs((now - dateTime).TotalSeconds) < 60.0)
		{
			return "Due now";
		}
		return "Reminder · " + Cal.Time(dateTime);
	}

	private static DropShadowEffect CreateBannerShadow()
	{
		DropShadowEffect dropShadowEffect = new DropShadowEffect();
		dropShadowEffect.Color = Colors.Black;
		dropShadowEffect.Direction = 270.0;
		dropShadowEffect.ShadowDepth = 6.0;
		dropShadowEffect.BlurRadius = 26.0;
		dropShadowEffect.Opacity = 0.45;
		dropShadowEffect.RenderingBias = RenderingBias.Performance;
		DropShadowEffect dropShadowEffect2 = dropShadowEffect;
		dropShadowEffect2.Freeze();
		return dropShadowEffect2;
	}

	private void UpdateExpandedReminder(PillActivitySnapshot snap)
	{
		if (_expandedContent == null)
		{
			return;
		}
		if (!PillActivityArbiter.IsCalendarReminder(snap))
		{
			if (_expandedReminderBanner != null)
			{
				_expandedContent.Children.Remove(_expandedReminderBanner);
			}
			_expandedReminderBanner = null;
			_expandedReminderId = null;
			_expandedReminderMoreText = null;
			return;
		}
		if (_expandedReminderId == snap.Id)
		{
			UpdateReminderMore(_expandedReminderMoreText);
			return;
		}
		if (_expandedReminderBanner != null)
		{
			_expandedContent.Children.Remove(_expandedReminderBanner);
		}
		string id = snap.Id;
		System.Windows.Size size = CalendarReminderCardSize();
		TextBlock moreText;
		Border banner = new Border
		{
			Width = size.Width,
			CornerRadius = new CornerRadius(24.0),
			Background = NotchInk.Black,
			Effect = BannerShadow,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0),
			Child = BuildCalendarReminderContent(snap.ToLegacyActivity(), onNotch: false, out moreText)
		};
		banner.MouseEnter += delegate
		{
			PillActivityArbiter.Instance.SetCalendarReminderHover(id, hovering: true);
		};
		banner.MouseLeave += delegate
		{
			PillActivityArbiter.Instance.SetCalendarReminderHover(id, hovering: false);
		};
		banner.IsVisibleChanged += delegate
		{
			if (!banner.IsVisible)
			{
				PillActivityArbiter.Instance.SetCalendarReminderHover(id, hovering: false);
			}
		};
		Grid.SetRowSpan(banner, 3);
		System.Windows.Controls.Panel.SetZIndex(banner, 100);
		_expandedReminderBanner = banner;
		_expandedReminderId = id;
		_expandedReminderMoreText = moreText;
		_expandedContent.Children.Add(banner);
	}

	private static void UpdateReminderMore(TextBlock more)
	{
		if (more != null)
		{
			int pendingReminderCount = PillActivityArbiter.Instance.PendingReminderCount;
			more.Text = "+" + pendingReminderCount + " more";
			if (more.Parent is FrameworkElement frameworkElement)
			{
				frameworkElement.Visibility = ((pendingReminderCount <= 0) ? Visibility.Collapsed : Visibility.Visible);
			}
		}
	}

	internal void UpdateCollapsedReminderInPlace()
	{
		UpdateReminderMore(_reminderMoreText);
	}

	private FrameworkElement BuildCalendarReminderVisual(NotchActivity act)
	{
		TextBlock moreText;
		FrameworkElement result = BuildCalendarReminderContent(act, onNotch: true, out moreText);
		_reminderMoreText = moreText;
		if (_isCollapsedHovered && IsRestingSurface)
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				PillActivityArbiter.Instance.SetCalendarReminderHover(act.Id, _isCollapsedHovered);
			});
		}
		return result;
	}

	internal FrameworkElement BuildCalendarReminderContent(NotchActivity act, bool onNotch, out TextBlock moreText)
	{
		PillActivityArbiter arb = PillActivityArbiter.Instance;
		string id = act.Id;
		string key = act.Payload as string;
		double i = MessageScale;
		Action<DateTime> snoozeTo = delegate(DateTime until)
		{
			if (arb.CurrentSnapshot != null && !(arb.CurrentSnapshot.Id != id))
			{
				CalendarService.Instance.SnoozeReminder(key, until);
				arb.DismissCurrentTransient();
			}
		};
		MessageAction messageAction = new MessageAction("Snooze", primary: false, delegate
		{
			snoozeTo(DateTime.Now.AddMinutes(10.0));
		});
		messageAction.Decorate = delegate(Border button)
		{
			UIElement child = button.Child;
			button.Child = null;
			button.Padding = new Thickness(12.0 * i, 0.0, 3.0 * i, 0.0);
			StackPanel stackPanel = new StackPanel
			{
				Orientation = System.Windows.Controls.Orientation.Horizontal,
				VerticalAlignment = VerticalAlignment.Center
			};
			stackPanel.Children.Add(child);
			Border chevron = new Border
			{
				Width = 18.0 * i,
				Height = MessageButtonHeight,
				Background = System.Windows.Media.Brushes.Transparent,
				ToolTip = "More snooze options",
				Child = FluentIcons.Create("chevron_down", 10.0 * i, NotchInk.Primary, filled: true)
			};
			((FrameworkElement)chevron.Child).HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
			((FrameworkElement)chevron.Child).VerticalAlignment = VerticalAlignment.Center;
			AutomationProperties.SetName(chevron, "More snooze options");
			CalendarClick(chevron, delegate
			{
				System.Windows.Controls.ContextMenu contextMenu = new System.Windows.Controls.ContextMenu
				{
					PlacementTarget = chevron,
					Background = NotchInk.Black,
					Foreground = NotchInk.Primary,
					BorderBrush = Ink(Colors.White, 31),
					Padding = new Thickness(4.0),
					FontFamily = BuddyFonts.Geist
				};
				LiveMessageVisuals.Crisp(contextMenu);
				foreach (KeyValuePair<string, DateTime> item in CalendarService.SnoozeChoices(DateTime.Now))
				{
					KeyValuePair<string, DateTime> selected = item;
					System.Windows.Controls.MenuItem menuItem = new System.Windows.Controls.MenuItem
					{
						Header = item.Key,
						Background = NotchInk.Black,
						Foreground = NotchInk.Primary,
						FontSize = 13.0,
						Padding = new Thickness(12.0, 7.0, 12.0, 7.0)
					};
					menuItem.Click += delegate
					{
						snoozeTo((selected.Key == "Tomorrow 9 AM") ? DateTime.Today.AddDays(1.0).AddHours(9.0) : selected.Value);
					};
					contextMenu.Items.Add(menuItem);
				}
				contextMenu.IsOpen = true;
			});
			stackPanel.Children.Add(chevron);
			button.Child = stackPanel;
		};
		MessageAction messageAction2 = new MessageAction("Done", primary: true, delegate
		{
			if (arb.CurrentSnapshot != null && !(arb.CurrentSnapshot.Id != id))
			{
				CalendarService.Instance.CompleteReminderKey(key);
				arb.DismissCurrentTransient();
			}
		});
		Grid grid = (Grid)BuildMessageCard(act, "bell", LiveMessageVisuals.Yellow, act.Title, ReminderSubtitle(key, DateTime.Now), new MessageAction[2] { messageAction, messageAction2 }, null);
		if (!onNotch)
		{
			grid.Margin = new Thickness(20.0, 0.0, 20.0, 0.0);
		}
		Grid grid2 = ((Grid)grid.Children[0]).Children.OfType<Grid>().First((Grid g) => object.Equals(g.Tag, "message-line"));
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		TextBlock textBlock = MessageDetail("");
		textBlock.FontSize = 11.0 * i;
		Border border = new Border();
		border.Child = textBlock;
		border.Margin = new Thickness(6.0 * i, 0.0, 0.0, 0.0);
		border.VerticalAlignment = VerticalAlignment.Center;
		Border element = border;
		Grid.SetColumn(element, 2);
		grid2.Children.Add(element);
		moreText = textBlock;
		UpdateReminderMore(textBlock);
		return grid;
	}

	private static void Hover(Border b, System.Windows.Media.Brush rest, System.Windows.Media.Brush over)
	{
		b.MouseEnter += delegate
		{
			b.Background = over;
		};
		b.MouseLeave += delegate
		{
			b.Background = rest;
		};
	}

	private static void CalendarClick(FrameworkElement el, Action click)
	{
		bool pressed = false;
		el.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			pressed = true;
			e.Handled = true;
		};
		el.MouseLeave += delegate
		{
			pressed = false;
		};
		el.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			if (pressed)
			{
				pressed = false;
				e.Handled = true;
				click();
			}
		};
	}

	private static Border PillButton(string label, System.Windows.Media.Brush fill, System.Windows.Media.Brush ink, double h, CornerRadius radius, Action click)
	{
		double messageScale = MessageScale;
		TextBlock textBlock = new TextBlock();
		textBlock.Text = label;
		textBlock.FontSize = 13.0 * messageScale;
		textBlock.FontWeight = AynType.Medium;
		textBlock.Foreground = ink;
		textBlock.FontFamily = AynType.Family(TypeSurface.Notch, 13.0);
		textBlock.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		TextBlock textBlock2 = textBlock;
		LiveMessageVisuals.Crisp(textBlock2);
		Border border = new Border();
		border.Height = h;
		border.CornerRadius = radius;
		border.Background = fill;
		border.Cursor = System.Windows.Input.Cursors.Hand;
		border.Child = textBlock2;
		Border border2 = border;
		System.Windows.Media.Brush over = ((fill is SolidColorBrush { Color: var color } solidColorBrush) ? Ink(System.Windows.Media.Color.FromRgb(color.R, solidColorBrush.Color.G, solidColorBrush.Color.B), (byte)Math.Min(255, (solidColorBrush.Color.A < byte.MaxValue) ? (solidColorBrush.Color.A + 20) : 224)) : fill);
		Hover(border2, fill, over);
		CalendarClick(border2, click);
		AutomationProperties.SetName(border2, label);
		border2.Focusable = true;
		border2.KeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == Key.Return || e.Key == Key.Space)
			{
				e.Handled = true;
				click();
			}
		};
		return border2;
	}

	internal static System.Windows.Size ReminderCardSize()
	{
		_ = NotchSettings.Instance;
		return new System.Windows.Size(Math.Round(420.0 * MessageScale), Math.Round(CardMessageHeight));
	}

	private bool OfferCanDisplay(PillActivitySnapshot snapshot)
	{
		if (_presentationOffer != null)
		{
			if (snapshot != null && snapshot.Tier != ActivityTier.Tier2_Session)
			{
				return snapshot.Tier == ActivityTier.Tier3_Ambient;
			}
			return true;
		}
		return false;
	}

	private bool HoldsActionCard()
	{
		PillActivitySnapshot currentSnapshot = PillActivityArbiter.Instance.CurrentSnapshot;
		if (IsRestingSurface)
		{
			if (!OfferCanDisplay(currentSnapshot) && !UsesMessageCard(currentSnapshot))
			{
				return PillActivityArbiter.IsCalendarReminder(currentSnapshot);
			}
			return true;
		}
		return false;
	}

	internal bool TryShowBackupOffer(string subtitle, Action notNow, Action<Action> restore)
	{
		if (!EntitlementGate.IsEntitled || !base.IsVisible || !IsRestingSurface || _collapsedContent == null || _presentationOffer != null)
		{
			return false;
		}
		if (PetPreferences.Instance.Enabled && _petOutside != null && _petOutside.IsVisible && !_petOutside.HasExited)
		{
			if (_gardenOfferPending)
			{
				return true;
			}
			_gardenOfferPending = true;
			if (_petOutside.Exit(delegate
			{
				try
				{
					TryShowBackupOffer(subtitle, notNow, restore);
				}
				finally
				{
					_gardenOfferPending = false;
					UpdatePetResting();
				}
			}))
			{
				return true;
			}
			_gardenOfferPending = false;
		}
		_presentationOffer = new NotchActivity
		{
			Id = "backup_restore_offer",
			Type = ActivityType.Custom,
			Title = "Restore your data?",
			Subtitle = subtitle
		};
		MessageAction[] actions = new MessageAction[2]
		{
			new MessageAction("Not now", primary: false, delegate
			{
				notNow();
				ClosePresentationOffer();
			}),
			new MessageAction("Restore", primary: true, delegate
			{
				restore(ClosePresentationOffer);
			})
		};
		_presentationOfferView = new Border
		{
			Background = NotchInk.Black,
			Child = BuildMessageCard(_presentationOffer, "backup", LiveMessageVisuals.Blue, _presentationOffer.Title, subtitle, actions, null)
		};
		System.Windows.Controls.Panel.SetZIndex(_presentationOfferView, 500);
		_collapsedContent.Children.Add(_presentationOfferView);
		_presentationOfferView.Visibility = ((!OfferCanDisplay(PillActivityArbiter.Instance.CurrentSnapshot)) ? Visibility.Collapsed : Visibility.Visible);
		RefreshRestingPresentation(rebuild: false, animate: true);
		return true;
	}

	internal void ClosePresentationOffer()
	{
		if (_presentationOfferView != null)
		{
			_collapsedContent.Children.Remove(_presentationOfferView);
		}
		_presentationOfferView = null;
		_presentationOffer = null;
		RefreshRestingPresentation(rebuild: true, animate: true);
	}

	internal static bool UsesMessageCard(PillActivitySnapshot snap)
	{
		if (snap == null)
		{
			return false;
		}
		if (IsPetStatus(snap) || IsPetSuggestion(snap))
		{
			return true;
		}
		if (ReminderDelivery.IsReminder(snap.Type) || snap.Type == ActivityType.ScreenTimeLimit)
		{
			return true;
		}
		if (snap.Type == ActivityType.Custom && snap.Id == "aicoding_budget_alert")
		{
			return true;
		}
		if (snap.Type == ActivityType.Custom && (snap.Id == "update_ready" || snap.Id == "backup_restore_offer" || snap.Id == "drop_complete"))
		{
			return true;
		}
		if (snap.Type == ActivityType.CalendarAlert)
		{
			return !PillActivityArbiter.IsCalendarReminder(snap);
		}
		return false;
	}

	private static Border MessageIcon(string glyph, double circle, System.Windows.Media.Color tint, FrameworkElement custom = null)
	{
		return LiveMessageVisuals.Icon(glyph, circle, tint, custom);
	}

	private static TextBlock MessageTitle(string text, double size)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text ?? "";
		textBlock.FontSize = size;
		textBlock.FontWeight = AynType.Semibold;
		textBlock.Foreground = NotchInk.Primary;
		textBlock.FontFamily = AynType.Family(TypeSurface.Notch, size);
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		textBlock.TextWrapping = TextWrapping.NoWrap;
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		TextBlock textBlock2 = textBlock;
		LiveMessageVisuals.Crisp(textBlock2);
		return textBlock2;
	}

	private static TextBlock MessageDetail(string text)
	{
		TextBlock textBlock = NotchText(text ?? "", 13.5 * MessageScale, AynType.Regular, Detail60);
		LiveMessageVisuals.Crisp(textBlock);
		textBlock.RenderTransform = Transform.Identity;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		return textBlock;
	}

	internal static void MessageEntry(FrameworkElement el)
	{
		if (el != null && CapsuleMotion.AnimationsEnabled)
		{
			TranslateTransform translateTransform = (TranslateTransform)(el.RenderTransform = new TranslateTransform(0.0, 0.0));
			el.Opacity = 1.0;
			el.BeginAnimation(UIElement.OpacityProperty, EntryFrames(0.0, 1.0));
			translateTransform.BeginAnimation(TranslateTransform.YProperty, EntryFrames(6.0, 0.0));
		}
	}

	private static DoubleAnimationUsingKeyFrames EntryFrames(double from, double to)
	{
		DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames = new DoubleAnimationUsingKeyFrames();
		doubleAnimationUsingKeyFrames.FillBehavior = FillBehavior.Stop;
		DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames2 = doubleAnimationUsingKeyFrames;
		doubleAnimationUsingKeyFrames2.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
		doubleAnimationUsingKeyFrames2.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70.0))));
		doubleAnimationUsingKeyFrames2.KeyFrames.Add(new EasingDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(290.0)), CapsuleMotion.EaseOutCubic));
		return doubleAnimationUsingKeyFrames2;
	}

	internal static bool UsesMoment(PillActivitySnapshot snap)
	{
		if (IsPetSuggestion(snap))
		{
			return false;
		}
		if (snap != null && snap.Type == ActivityType.CompanionMessage)
		{
			if (snap.Payload is MessageSettings)
			{
				return (snap.Id ?? "").StartsWith("notice_", StringComparison.Ordinal);
			}
			return true;
		}
		return false;
	}

	internal static bool UsesStatusPill(PillActivitySnapshot snap)
	{
		if (IsPetStatus(snap))
		{
			return false;
		}
		if (snap == null)
		{
			return false;
		}
		switch (snap.Type)
		{
		case ActivityType.BluetoothConnected:
		case ActivityType.BluetoothDisconnected:
		case ActivityType.Volume:
		case ActivityType.BatteryCharging:
		case ActivityType.BatteryChargingStopped:
		case ActivityType.BatteryFull:
		case ActivityType.BatteryLow:
		case ActivityType.ClipboardCopied:
		case ActivityType.NetworkState:
		case ActivityType.NetworkConnected:
		case ActivityType.NetworkDisconnected:
			return true;
		default:
			if (snap.Type == ActivityType.Custom)
			{
				return (snap.Id ?? "").StartsWith("analytics", StringComparison.Ordinal);
			}
			return false;
		}
	}

	private static string DisplaySubtitle(NotchActivity act)
	{
		string text = act.Subtitle ?? "";
		if (act.Type == ActivityType.NetworkDisconnected)
		{
			return "No connection";
		}
		if (act.Type == ActivityType.HydrationReminder)
		{
			return text.Replace(" glasses today", " today");
		}
		if (act.Type == ActivityType.ScreenTimeLimit)
		{
			return text.Replace(" on screen today", " today");
		}
		if (act.Id == "aicoding_budget_alert")
		{
			return text.Replace(" of your ", " of ").Replace(" budget", "");
		}
		return text;
	}

	private static double CopyWidth(string copy, double size, FontWeight weight)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = copy ?? "";
		textBlock.FontSize = size;
		textBlock.FontWeight = weight;
		textBlock.FontFamily = AynType.Family(TypeSurface.Notch, size);
		TextBlock textBlock2 = textBlock;
		LiveMessageVisuals.Crisp(textBlock2);
		textBlock2.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
		return textBlock2.DesiredSize.Width + 2.0;
	}

	private static string[] MessageActionLabels(NotchActivity act)
	{
		if (act.Type == ActivityType.CalendarAlert)
		{
			if (!PillActivityArbiter.IsCalendarReminder(act.ToSnapshot()))
			{
				return new string[0];
			}
			return new string[2] { "Snooze", "Done" };
		}
		if (act.Type == ActivityType.HydrationReminder)
		{
			return new string[2] { "+1 glass", "Later" };
		}
		if (act.Type == ActivityType.TimerComplete)
		{
			if ((act.Title ?? "").Contains("Focus") || (act.Title ?? "").Contains("Cycles"))
			{
				return new string[2] { "Start break", "Skip" };
			}
			if (!(act.Title ?? "").Contains("Timer"))
			{
				return new string[1] { "Dismiss" };
			}
			return new string[2] { "Restart", "Done" };
		}
		if (act.Id == "update_ready")
		{
			return new string[2] { "Update", "Later" };
		}
		if (act.Id == "backup_restore_offer")
		{
			return new string[2] { "Not now", "Restore" };
		}
		if (act.Id == "drop_complete")
		{
			if (act.PlayPauseAction != null)
			{
				return new string[2] { "Open", "Show in folder" };
			}
			return new string[1] { "Done" };
		}
		return new string[2] { "View", "Dismiss" };
	}

	internal static System.Windows.Size MessageSize(PillActivitySnapshot snap)
	{
		if (IsPetStatus(snap) || snap.Payload is PetMoment)
		{
			return PetMessageSize(snap);
		}
		double messageScale = MessageScale;
		NotchActivity notchActivity = snap.ToLegacyActivity();
		string text = DisplaySubtitle(notchActivity);
		double num;
		double val;
		if (UsesMoment(snap))
		{
			num = 64.0;
			string copy = MomentSubtitle(notchActivity);
			val = 32.0 * messageScale + 16.0 * messageScale + Math.Max(CopyWidth(MomentTitle(notchActivity), 16.0 * messageScale, AynType.Semibold), CopyWidth(copy, 13.0 * messageScale, AynType.Regular)) + 56.0 * messageScale;
			val = Math.Max(300.0 * messageScale, Math.Min(480.0 * messageScale, val));
		}
		else if (UsesStatusPill(snap))
		{
			num = StatusMessageHeight / messageScale;
			string title = ((notchActivity.Type == ActivityType.ClipboardCopied) ? "Copied" : notchActivity.Title);
			if (notchActivity.Type == ActivityType.BatteryLow)
			{
				text = DigitsOf((notchActivity.Payload as string) ?? notchActivity.Subtitle ?? notchActivity.Title, 10) + "% · Plug in soon";
			}
			if (notchActivity.Type == ActivityType.NetworkDisconnected)
			{
				text = "No connection";
			}
			bool flag = notchActivity.Type == ActivityType.BatteryCharging || notchActivity.Type == ActivityType.BatteryFull;
			if (flag)
			{
				title = ((notchActivity.Type == ActivityType.BatteryFull) ? "Fully charged" : "Charging");
				text = "100%";
			}
			double num2 = (flag ? Math.Min(30.0 * messageScale, StatusMessageHeight - 4.0 * messageScale) : MessageGlyph(StatusMessageHeight));
			val = num2 + 10.0 * messageScale + MessageLineWidth(title, text) + 2.0 * MessageSideInset(card: false) + 6.0 * messageScale;
			if (notchActivity.Type == ActivityType.BluetoothConnected || notchActivity.Type == ActivityType.BluetoothDisconnected)
			{
				val = MessageGlyph(StatusMessageHeight) + 10.0 * messageScale + CopyWidth(notchActivity.Title, 15.0 * messageScale, AynType.Semibold) + 8.0 * messageScale + CopyWidth(text, 13.5 * messageScale, AynType.Regular) + 2.0 * MessageSideInset(card: false) + 4.0 * messageScale + BluetoothPresentation.BatteryRoom(notchActivity.Title, messageScale);
			}
			if (notchActivity.Type == ActivityType.Volume)
			{
				val = 22.0 * messageScale + 10.0 * messageScale + CopyWidth("Volume", 15.0 * messageScale, AynType.Semibold) + 20.0 * messageScale + 64.0 * messageScale + 12.0 * messageScale + CopyWidth("100%", 13.5 * messageScale, AynType.Regular) + 2.0 * MessageSideInset(card: false);
			}
			val = Math.Max(NotchConfig.Instance.GetCollapsedWidth(), Math.Min(CompactMessages ? 360.0 : (480.0 * messageScale), val));
		}
		else
		{
			string[] array = MessageActionLabels(notchActivity);
			bool flag2 = PillActivityArbiter.IsCalendarReminder(snap);
			num = MessageRowHeight(array.Length > 0) / messageScale;
			string title2 = ((notchActivity.Type == ActivityType.HydrationReminder && string.IsNullOrEmpty(notchActivity.Title)) ? "Drink some water" : notchActivity.Title);
			if (flag2)
			{
				text = ReminderSubtitle(notchActivity.Payload as string, DateTime.Now) + " +9 more";
			}
			double num3 = (PetPreferences.Instance.Enabled ? MessageRobot(num * messageScale) : MessageGlyph(num * messageScale));
			val = num3 + 10.0 * messageScale + MessageLineWidth(title2, text) + ButtonsWidth(array, flag2) + 2.0 * MessageSideInset(array.Length > 0) + 6.0 * messageScale;
			val = Math.Max(NotchConfig.Instance.GetCollapsedWidth(), Math.Min(560.0 * messageScale, val));
		}
		double num4 = ((NotchConfig.Instance.StyleFamily == "Island") ? 5.0 : 0.0);
		return new System.Windows.Size(Math.Ceiling(val), Math.Round(num * messageScale) + num4);
	}

	private static string MomentTitle(NotchActivity act)
	{
		if (!(act.Id ?? "").StartsWith("notice_", StringComparison.Ordinal))
		{
			return act.Title;
		}
		return "Welcome to Ayn";
	}

	private static string MomentSubtitle(NotchActivity act)
	{
		if ((act.Id ?? "").StartsWith("notice_", StringComparison.Ordinal))
		{
			return "Move your pointer to the top of the screen";
		}
		string text = (act.Subtitle ?? "").Replace("Move to the top of the screen to open Ayn", "Move to the top to open Ayn");
		if (!text.StartsWith("Press ", StringComparison.Ordinal))
		{
			return text;
		}
		return text.Replace(" to open Ayn", "");
	}

	private static void TextArrival(FrameworkElement el, int delay)
	{
		if (el == null || !LiveMessageVisuals.Motion)
		{
			return;
		}
		el.Loaded += delegate
		{
			if (LiveMessageVisuals.Motion)
			{
				TranslateTransform translateTransform = new TranslateTransform();
				el.RenderTransform = translateTransform;
				DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames = new DoubleAnimationUsingKeyFrames
				{
					FillBehavior = FillBehavior.Stop
				};
				doubleAnimationUsingKeyFrames.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
				doubleAnimationUsingKeyFrames.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay))));
				doubleAnimationUsingKeyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay + 220)), CapsuleMotion.EaseOutCubic));
				DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames2 = new DoubleAnimationUsingKeyFrames
				{
					FillBehavior = FillBehavior.Stop
				};
				doubleAnimationUsingKeyFrames2.KeyFrames.Add(new DiscreteDoubleKeyFrame(6.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
				doubleAnimationUsingKeyFrames2.KeyFrames.Add(new DiscreteDoubleKeyFrame(6.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay))));
				doubleAnimationUsingKeyFrames2.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay + 220)), CapsuleMotion.EaseOutCubic));
				el.BeginAnimation(UIElement.OpacityProperty, doubleAnimationUsingKeyFrames);
				translateTransform.BeginAnimation(TranslateTransform.YProperty, doubleAnimationUsingKeyFrames2);
			}
		};
	}

	private FrameworkElement BuildMoment(NotchActivity act)
	{
		double messageScale = MessageScale;
		Grid grid = new Grid();
		grid.Margin = new Thickness(28.0 * messageScale, 0.0, 28.0 * messageScale, 0.0);
		grid.VerticalAlignment = VerticalAlignment.Center;
		grid.Height = 64.0 * messageScale;
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.Children.Add(new AynRobotMark(32.0 * messageScale)
		{
			VerticalAlignment = VerticalAlignment.Center
		});
		StackPanel stackPanel = new StackPanel();
		stackPanel.Margin = new Thickness(16.0 * messageScale, 0.0, 0.0, 0.0);
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.ClipToBounds = true;
		StackPanel stackPanel2 = stackPanel;
		string text = MomentTitle(act);
		LetterReveal reveal = new LetterReveal(text, 16.0 * messageScale, 0.0, NotchInk.Primary, 35.0, 4.0, 220.0)
		{
			HorizontalAlignment = System.Windows.HorizontalAlignment.Left
		};
		reveal.Loaded += delegate
		{
			if (LiveMessageVisuals.Motion)
			{
				reveal.Start(70.0);
			}
			else
			{
				reveal.Settle();
			}
		};
		stackPanel2.Children.Add(reveal);
		TextBlock textBlock = MessageDetail(MomentSubtitle(act));
		textBlock.Margin = new Thickness(0.0, 3.0 * messageScale, 0.0, 0.0);
		TextArrival(textBlock, Math.Min(850, 70 + (int)reveal.DurationMs));
		stackPanel2.Children.Add(textBlock);
		Grid.SetColumn(stackPanel2, 1);
		grid2.Children.Add(stackPanel2);
		return grid2;
	}

	private FrameworkElement BuildMessagePill(string glyph, System.Windows.Media.Color tint, string title, string detail, CollapsedActivityProfile profile, FrameworkElement customGlyph = null)
	{
		double messageScale = MessageScale;
		double statusMessageHeight = StatusMessageHeight;
		Grid grid = new Grid();
		grid.Margin = new Thickness(MessageSideInset(card: false), 0.0, MessageSideInset(card: false), 0.0);
		grid.VerticalAlignment = VerticalAlignment.Center;
		grid.Height = statusMessageHeight;
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		Border border = RowIcon(glyph, statusMessageHeight, tint, customGlyph);
		border.Margin = new Thickness(0.0, 0.0, 10.0 * messageScale, 0.0);
		grid2.Children.Add(border);
		TextBlock heading;
		Grid element = MessageLine(title, detail, out heading);
		Grid.SetColumn(element, 1);
		grid2.Children.Add(element);
		return grid2;
	}

	private FrameworkElement BuildMessageCard(NotchActivity act, string glyph, System.Windows.Media.Color tint, string title, string subtitle, IList<MessageAction> actions, Action onClick, FrameworkElement customGlyph = null)
	{
		double messageScale = MessageScale;
		System.Windows.Media.Color color = ReminderAccent();
		PillActivityArbiter arb = PillActivityArbiter.Instance;
		string id = act?.Id;
		bool flag = actions != null && actions.Count > 0;
		double num = MessageRowHeight(flag);
		double num2 = MessageSideInset(flag);
		Grid grid = new Grid();
		grid.Height = num;
		grid.Margin = new Thickness(num2, 0.0, num2, 0.0);
		grid.VerticalAlignment = VerticalAlignment.Center;
		grid.Background = System.Windows.Media.Brushes.Transparent;
		grid.UseLayoutRounding = true;
		Grid grid2 = grid;
		Grid grid3 = new Grid();
		grid3.VerticalAlignment = VerticalAlignment.Center;
		Grid grid4 = grid3;
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		object obj;
		if (!PetPreferences.Instance.Enabled)
		{
			obj = RowIcon(glyph, num, tint, customGlyph);
		}
		else
		{
			AynRobotMark aynRobotMark = new AynRobotMark(MessageRobot(num));
			aynRobotMark.VerticalAlignment = VerticalAlignment.Center;
			obj = aynRobotMark;
		}
		FrameworkElement frameworkElement = (FrameworkElement)obj;
		frameworkElement.Margin = new Thickness(0.0, 0.0, 10.0 * messageScale, 0.0);
		grid4.Children.Add(frameworkElement);
		TextBlock heading;
		Grid grid5 = MessageLine(title, subtitle, out heading);
		grid5.Margin = new Thickness(0.0, 0.0, flag ? (12.0 * messageScale) : 0.0, 0.0);
		Grid.SetColumn(grid5, 1);
		grid4.Children.Add(grid5);
		if (flag)
		{
			StackPanel stackPanel = new StackPanel();
			stackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
			stackPanel.VerticalAlignment = VerticalAlignment.Center;
			StackPanel stackPanel2 = stackPanel;
			double messageButtonHeight = MessageButtonHeight;
			foreach (MessageAction item in actions.Take(2))
			{
				MessageAction a = item;
				Border border = PillButton(a.Label, a.Primary ? Ink(color, byte.MaxValue) : Ink(Colors.White, 31), a.Primary ? ActionInk(color) : NotchInk.Primary, messageButtonHeight, new CornerRadius(messageButtonHeight / 2.0), delegate
				{
					if (a.Run != null)
					{
						a.Run();
					}
				});
				border.Padding = new Thickness(11.0 * messageScale, 0.0, 11.0 * messageScale, 0.0);
				border.Margin = new Thickness((stackPanel2.Children.Count == 0) ? 0.0 : (8.0 * messageScale), 0.0, 0.0, 0.0);
				if (a.Decorate != null)
				{
					a.Decorate(border);
				}
				stackPanel2.Children.Add(border);
			}
			Grid.SetColumn(stackPanel2, 2);
			grid4.Children.Add(stackPanel2);
		}
		grid2.Children.Add(grid4);
		Border border2 = new Border();
		border2.Height = 2.0 * messageScale;
		border2.Background = Ink(Colors.White, 23);
		border2.CornerRadius = new CornerRadius(1.0 * messageScale);
		border2.VerticalAlignment = VerticalAlignment.Bottom;
		border2.Margin = new Thickness(0.0, 0.0, 0.0, 1.0 * messageScale);
		Border border3 = border2;
		Border border4 = new Border();
		border4.CornerRadius = new CornerRadius(1.0 * messageScale);
		border4.Background = Ink(tint, byte.MaxValue);
		Border border5 = border4;
		ScaleTransform scale = new ScaleTransform(1.0, 1.0);
		border5.RenderTransform = scale;
		border5.RenderTransformOrigin = new System.Windows.Point(0.0, 0.5);
		border3.Child = border5;
		grid2.Children.Add(border3);
		TimeSpan total;
		TimeSpan remaining = (total = TimeSpan.Zero);
		bool paused = false;
		bool timed = id != null && arb.GetMessageProgress(id, out remaining, out total, out paused);
		bool budget = id == "aicoding_budget_alert";
		double scaleX = (budget ? Math.Max(0.0, Math.Min(1.0, (double)DigitsOf(title, 0) / 100.0)) : (timed ? (remaining.TotalMilliseconds / Math.Max(1.0, total.TotalMilliseconds)) : 1.0));
		scale.ScaleX = scaleX;
		border3.Visibility = ((PetPreferences.Instance.Enabled || ((!timed || !LiveMessageVisuals.Motion) && !budget)) ? Visibility.Collapsed : Visibility.Visible);
		Action run = delegate
		{
			if (LiveMessageVisuals.Motion && timed && !budget)
			{
				scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scale.ScaleX, 0.0, TimeSpan.FromMilliseconds(Math.Max(1.0, scale.ScaleX * total.TotalMilliseconds))));
			}
		};
		if (!paused)
		{
			run();
		}
		if (timed && PillActivityArbiter.IsHoldable(arb.CurrentSnapshot) && arb.CurrentSnapshot.Id == id)
		{
			grid2.MouseEnter += delegate
			{
				double scaleX2 = scale.ScaleX;
				scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
				scale.ScaleX = scaleX2;
			};
			grid2.MouseLeave += delegate
			{
				run();
			};
			if (_isCollapsedHovered && IsRestingSurface)
			{
				base.Dispatcher.BeginInvoke((Action)delegate
				{
					arb.SetMessageHover(id, _isCollapsedHovered);
				});
			}
		}
		if (onClick != null)
		{
			grid2.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
				onClick();
			};
		}
		return grid2;
	}

	private FrameworkElement BuildReminderVisual(NotchActivity act)
	{
		bool flag = act.Type == ActivityType.HydrationReminder;
		PillActivityArbiter arb = PillActivityArbiter.Instance;
		Action onClick = delegate
		{
			arb.DismissCurrentTransient();
			ToolRouter.Instance.RouteTo("timer");
			RequestExpand("Reminder clicked");
		};
		List<MessageAction> list = new List<MessageAction>();
		string text = ((!string.IsNullOrEmpty(act.Title)) ? act.Title : (flag ? "Drink some water" : "Timer done"));
		string glyph;
		System.Windows.Media.Color tint;
		if (flag)
		{
			glyph = "drop.fill";
			tint = LiveMessageVisuals.Cyan;
			list.Add(new MessageAction("+1 glass", primary: true, delegate
			{
				TimerService.Instance.AddHydrationGlass();
				arb.DismissCurrentTransient();
			}));
			list.Add(new MessageAction("Later", primary: false, delegate
			{
				arb.DismissCurrentTransient();
			}));
		}
		else if (text == "Focus done" || text == "Cycles complete")
		{
			glyph = "focus";
			tint = LiveMessageVisuals.Purple;
			list.Add(new MessageAction("Start break", primary: true, delegate
			{
				arb.DismissCurrentTransient();
				TimerService instance = TimerService.Instance;
				if (instance.PomoIsPaused)
				{
					instance.ResumePomodoro();
				}
				else if (!instance.PomoIsRunning)
				{
					instance.StartPomodoro();
				}
			}));
			list.Add(new MessageAction("Skip", primary: false, delegate
			{
				arb.DismissCurrentTransient();
				TimerService.Instance.SkipPomodoro();
			}));
		}
		else
		{
			bool flag2 = text == "Timer done";
			glyph = (flag2 ? "alarm" : "coffee");
			tint = (flag2 ? LiveMessageVisuals.Orange : LiveMessageVisuals.Teal);
			if (flag2)
			{
				list.Add(new MessageAction("Restart", primary: true, delegate
				{
					arb.DismissCurrentTransient();
					TimerService.Instance.StartCountdown();
				}));
			}
			list.Add(new MessageAction(flag2 ? "Done" : "Dismiss", !flag2, delegate
			{
				arb.DismissCurrentTransient();
			}));
		}
		return BuildMessageCard(act, glyph, tint, text, DisplaySubtitle(act), list, onClick);
	}

	private FrameworkElement BuildGlyphRow(string glyph, string title, string detail, CollapsedActivityProfile profile, bool stacked)
	{
		return BuildMessagePill(glyph, (glyph == "wifi.slash") ? LiveMessageVisuals.Grey : LiveMessageVisuals.Blue, title, (glyph == "wifi.slash") ? "No connection" : detail, profile);
	}

	internal static System.Windows.Size MediaCardSize(bool wide)
	{
		NotchSettings instance = NotchSettings.Instance;
		double activityScale = ActivityScale;
		double num = ((NotchConfig.Instance.StyleFamily == "Island") ? 5.0 : 0.0);
		double num2 = Math.Round((double)(wide ? 5 : 4) * activityScale);
		double num3 = Math.Round((double)(wide ? 50 : 26) * activityScale);
		double num4 = Math.Round((double)(wide ? 15 : 7) * activityScale);
		return new System.Windows.Size(instance.ScaledProfileWidth(wide ? 380.0 : 290.0), Math.Max(instance.RestingSize.Height + Math.Round((wide ? 52.0 : 10.0) * activityScale), num + num3 + 3.0 * num2 + Math.Round((double)((!wide) ? 1 : 2) * activityScale) + num4));
	}

	private static Thickness SideMargin(System.Windows.Size card, double yTop, double yBottom)
	{
		NotchConfig instance = NotchConfig.Instance;
		double num = Math.Ceiling(NotchGeometryBuilder.ContentInset(instance.Style, card.Width, card.Height, instance.CornerRadius, yTop, yBottom));
		return new Thickness(num, 0.0, num, 0.0);
	}

	internal static System.Windows.Media.Color MusicBeatColor(ImageSource art)
	{
		System.Windows.Media.Color? fixedMusicColor = NotchConfig.Instance.GetFixedMusicColor();
		if (fixedMusicColor.HasValue)
		{
			return fixedMusicColor.Value;
		}
		if (art == null)
		{
			return Colors.White;
		}
		if (ArtColors.TryGetValue(art, out var value))
		{
			return (System.Windows.Media.Color)value;
		}
		System.Windows.Media.Color color = DominantColor(art as BitmapSource);
		try
		{
			ArtColors.Add(art, color);
		}
		catch
		{
		}
		return color;
	}

	internal static System.Windows.Media.Color DominantColor(BitmapSource src)
	{
		try
		{
			if (src == null || src.PixelWidth < 1)
			{
				return Colors.White;
			}
			BitmapSource source = new TransformedBitmap(src, new ScaleTransform(24.0 / (double)src.PixelWidth, 24.0 / (double)src.PixelHeight));
			source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0.0);
			int pixelWidth = source.PixelWidth;
			int pixelHeight = source.PixelHeight;
			byte[] array = new byte[pixelWidth * pixelHeight * 4];
			source.CopyPixels(array, pixelWidth * 4, 0);
			double[] array2 = new double[18];
			double[] array3 = new double[18];
			double[] array4 = new double[18];
			double[] array5 = new double[18];
			double num = 0.0;
			for (int i = 0; i < array.Length; i += 4)
			{
				if (array[i + 3] < 128)
				{
					continue;
				}
				double num2 = (double)(int)array[i] / 255.0;
				double num3 = (double)(int)array[i + 1] / 255.0;
				double num4 = (double)(int)array[i + 2] / 255.0;
				double num5 = Math.Max(num4, Math.Max(num3, num2));
				double num6 = Math.Min(num4, Math.Min(num3, num2));
				double num7 = ((num5 <= 0.0) ? 0.0 : ((num5 - num6) / num5));
				num += 1.0;
				if (!(num7 < 0.28) && !(num5 < 0.18))
				{
					double num8 = num5 - num6;
					double num9 = ((num5 == num4) ? (60.0 * ((num3 - num2) / num8 % 6.0)) : ((num5 != num3) ? (60.0 * ((num4 - num3) / num8 + 4.0)) : (60.0 * ((num2 - num4) / num8 + 2.0))));
					if (num9 < 0.0)
					{
						num9 += 360.0;
					}
					int num10 = (int)(num9 / 360.0 * 18.0) % 18;
					double num11 = num7 * num7 * num5;
					array2[num10] += num11;
					array3[num10] += num4 * num11;
					array4[num10] += num3 * num11;
					array5[num10] += num2 * num11;
				}
			}
			int num12 = 0;
			for (int j = 1; j < 18; j++)
			{
				if (array2[j] > array2[num12])
				{
					num12 = j;
				}
			}
			if (num <= 0.0 || array2[num12] < num * 0.04)
			{
				return Colors.White;
			}
			double num13 = array3[num12] / array2[num12];
			double num14 = array4[num12] / array2[num12];
			double num15 = array5[num12] / array2[num12];
			return Lighten(System.Windows.Media.Color.FromRgb((byte)(num13 * 255.0), (byte)(num14 * 255.0), (byte)(num15 * 255.0)));
		}
		catch
		{
			return Colors.White;
		}
	}

	internal static System.Windows.Media.Color Lighten(System.Windows.Media.Color c)
	{
		double num = (double)(int)c.R / 255.0;
		double num2 = (double)(int)c.G / 255.0;
		double num3 = (double)(int)c.B / 255.0;
		double num4 = Math.Max(num, Math.Max(num2, num3));
		double num5 = Math.Min(num, Math.Min(num2, num3));
		double num6 = 0.0;
		double val = ((num4 <= 0.0) ? 0.0 : ((num4 - num5) / num4));
		double val2 = num4;
		double num7 = num4 - num5;
		if (num7 > 1E-06)
		{
			num6 = ((num4 == num) ? (60.0 * ((num2 - num3) / num7 % 6.0)) : ((num4 != num2) ? (60.0 * ((num - num2) / num7 + 4.0)) : (60.0 * ((num3 - num) / num7 + 2.0))));
		}
		if (num6 < 0.0)
		{
			num6 += 360.0;
		}
		val2 = Math.Max(val2, 0.85);
		val = Math.Max(0.45, Math.Min(0.85, val));
		double num8 = val2 * val;
		double num9 = num8 * (1.0 - Math.Abs(num6 / 60.0 % 2.0 - 1.0));
		double num10 = val2 - num8;
		double num11 = 0.0;
		double num12 = 0.0;
		double num13 = 0.0;
		if (num6 < 60.0)
		{
			num11 = num8;
			num12 = num9;
		}
		else if (num6 < 120.0)
		{
			num11 = num9;
			num12 = num8;
		}
		else if (num6 < 180.0)
		{
			num12 = num8;
			num13 = num9;
		}
		else if (num6 < 240.0)
		{
			num12 = num9;
			num13 = num8;
		}
		else if (num6 < 300.0)
		{
			num11 = num9;
			num13 = num8;
		}
		else
		{
			num11 = num8;
			num13 = num9;
		}
		return System.Windows.Media.Color.FromRgb((byte)Math.Round((num11 + num10) * 255.0), (byte)Math.Round((num12 + num10) * 255.0), (byte)Math.Round((num13 + num10) * 255.0));
	}

	private static string ArtistOf(NotchActivity act)
	{
		string text = ((act.Payload is MediaTrackInfo mediaTrackInfo) ? mediaTrackInfo.Artist : null);
		if (string.IsNullOrWhiteSpace(text))
		{
			text = act.Subtitle;
		}
		switch (text)
		{
		case "Paused":
		case "Playing":
		case "Media":
			text = null;
			break;
		}
		return text;
	}

	private static string FormatRemaining(double seconds)
	{
		TimeSpan timeSpan = TimeSpan.FromSeconds(Math.Ceiling(seconds));
		if (!(timeSpan.TotalHours >= 1.0))
		{
			return string.Format(CultureInfo.InvariantCulture, "-{0}:{1:00}", (int)timeSpan.TotalMinutes, timeSpan.Seconds);
		}
		return string.Format(CultureInfo.InvariantCulture, "-{0}:{1:00}:{2:00}", (int)timeSpan.TotalHours, timeSpan.Minutes, timeSpan.Seconds);
	}

	private static ImageSource MediaArtFor(ImageSource fromActivity, string title)
	{
		if (fromActivity != null)
		{
			return fromActivity;
		}
		try
		{
			MediaViewState view = MediaService.Instance.View;
			if (view != null && view.Present && view.Artwork != null && string.Equals(view.Title, title, StringComparison.Ordinal))
			{
				return view.Artwork;
			}
		}
		catch
		{
		}
		return null;
	}

	private void RefreshMediaArtIfStale()
	{
		if (_activeOwner.VisualType == CollapsedVisualType.Media && _mediaRoot != null && IsRestingSurface)
		{
			PillActivitySnapshot currentActivity = PillActivityArbiter.Instance.CurrentActivity;
			if (currentActivity != null && currentActivity.Type == ActivityType.Media && !object.ReferenceEquals(MediaArtFor(currentActivity.Artwork, currentActivity.Title), _mediaArtShown))
			{
				UpdateMediaVisualInPlace(currentActivity);
			}
		}
	}

	private FrameworkElement BuildArtTile(ImageSource art, double size, System.Windows.Media.Color beat)
	{
		double uniformRadius = Math.Round(size * 0.24);
		Border border = new Border();
		border.Width = size;
		border.Height = size;
		border.CornerRadius = new CornerRadius(uniformRadius);
		border.VerticalAlignment = VerticalAlignment.Center;
		border.ClipToBounds = true;
		Border border2 = border;
		if (art != null)
		{
			ImageBrush imageBrush = new ImageBrush(art);
			imageBrush.Stretch = Stretch.UniformToFill;
			ImageBrush imageBrush2 = imageBrush;
			RenderOptions.SetBitmapScalingMode(imageBrush2, BitmapScalingMode.HighQuality);
			border2.Background = imageBrush2;
			border2.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(28, byte.MaxValue, byte.MaxValue, byte.MaxValue));
			border2.BorderThickness = new Thickness(0.75);
		}
		else
		{
			border2.Background = NotchInk.Surface2;
			FrameworkElement frameworkElement = NotchGlyphs.Create("music.note", Math.Round(size * 0.55), NotchInk.Secondary);
			frameworkElement.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
			frameworkElement.VerticalAlignment = VerticalAlignment.Center;
			border2.Child = frameworkElement;
		}
		return border2;
	}

	private FrameworkElement BuildMusicVisual(NotchActivity act)
	{
		bool mediaWideWanted = MediaWideWanted;
		System.Windows.Size card = MediaCardSize(mediaWideWanted);
		NotchConfig instance = NotchConfig.Instance;
		double activityScale = ActivityScale;
		double num = ((instance.StyleFamily == "Island") ? 5.0 : 0.0);
		System.Windows.Media.Brush primaryBrush = PrimaryBrush;
		ImageSource imageSource = MediaArtFor(act.Artwork, act.Title);
		System.Windows.Media.Color color = MusicBeatColor(imageSource);
		string text = CleanSongTitle(act.Title);
		if (string.IsNullOrWhiteSpace(text))
		{
			text = "Now playing";
		}
		string text2 = ArtistOf(act);
		_mediaTimeline = act.Payload as MediaTrackInfo;
		_mediaPlaying = act.IsPlaying;
		double num2 = Math.Round((double)(mediaWideWanted ? 5 : 4) * activityScale);
		double num3 = Math.Round((double)(mediaWideWanted ? 15 : 7) * activityScale);
		double num4 = card.Height - num2;
		double num5 = num4 - num3;
		double num6 = num5 - Math.Round((double)((!mediaWideWanted) ? 1 : 2) * activityScale);
		double num7 = num + (num6 - num) / 2.0;
		double num8 = Math.Round((double)(mediaWideWanted ? 50 : 26) * activityScale);
		Grid grid = new Grid();
		grid.Width = card.Width;
		grid.Height = card.Height;
		grid.ClipToBounds = true;
		grid.Background = System.Windows.Media.Brushes.Transparent;
		grid.Cursor = System.Windows.Input.Cursors.Hand;
		grid.UseLayoutRounding = true;
		Grid grid2 = grid;
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(num6)
		});
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(num5 - num6)
		});
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(num3)
		});
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			ToolRouter.Instance.RouteTo("media");
			ExpandCapsule();
		};
		Grid grid3 = new Grid();
		grid3.VerticalAlignment = VerticalAlignment.Center;
		Grid grid4 = grid3;
		Thickness thickness = SideMargin(card, num7 - num8 / 2.0, num7 + num8 / 2.0);
		grid4.Margin = new Thickness(thickness.Left, num, thickness.Right, 0.0);
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		FrameworkElement element = BuildArtTile(imageSource, num8, color);
		grid4.Children.Add(element);
		_mediaTitleRun = (_mediaArtistRun = null);
		_mediaCompactText = null;
		_mediaFitWidth = -1.0;
		_mediaTransport = null;
		_mediaWideTitle = (_mediaWideArtist = null);
		_mediaRemainingText = null;
		_mediaProgressBar = null;
		_beatMiniSlot = null;
		_beatMiniStatic = null;
		if (!mediaWideWanted)
		{
			TextBlock textBlock = new TextBlock();
			textBlock.FontSize = PrimaryFontSize;
			textBlock.FontFamily = AynType.Family(TypeSurface.Notch, PrimaryFontSize);
			textBlock.TextTrimming = TextTrimming.WordEllipsis;
			textBlock.TextWrapping = TextWrapping.NoWrap;
			textBlock.VerticalAlignment = VerticalAlignment.Center;
			textBlock.Margin = new Thickness(Math.Round(9.0 * activityScale), 0.0, Math.Round(10.0 * activityScale), 0.0);
			TextBlock textBlock2 = textBlock;
			_mediaTitleRun = new Run(text)
			{
				FontWeight = AynType.Medium,
				Foreground = primaryBrush
			};
			textBlock2.Inlines.Add(_mediaTitleRun);
			_mediaArtistRun = new Run(string.IsNullOrEmpty(text2) ? "" : ("  ·  " + text2))
			{
				FontSize = SecondaryFontSize,
				FontFamily = AynType.Family(TypeSurface.Notch, SecondaryFontSize),
				FontWeight = AynType.Regular,
				Foreground = NotchGlyphs.SecondaryBrush
			};
			textBlock2.Inlines.Add(_mediaArtistRun);
			_mediaCompactText = textBlock2;
			_mediaCompactArtist = text2;
			_mediaCompactTitle = text;
			textBlock2.SizeChanged += delegate
			{
				FitCompactMediaText();
			};
			CenterCaps(textBlock2);
			Grid.SetColumn(textBlock2, 1);
			grid4.Children.Add(textBlock2);
			_beatMiniBarW = Half(2.5 * activityScale);
			double height = Math.Round(14.0 * activityScale);
			Border border = new Border();
			border.Width = Math.Round(_beatMiniBarW * 4.0 + Half(2.5 * activityScale) * 3.0);
			border.Height = height;
			border.VerticalAlignment = VerticalAlignment.Center;
			Border border2 = border;
			_beatMiniStatic = new StaticBeatBars(4, _beatMiniBarW, mini: true, color);
			border2.Child = _beatMiniStatic;
			Grid.SetColumn(border2, 2);
			grid4.Children.Add(border2);
			_beatMiniSlot = border2;
		}
		else
		{
			Grid grid5 = new Grid();
			grid5.Height = num8;
			grid5.Margin = new Thickness(Math.Round(12.0 * activityScale), 0.0, 0.0, 0.0);
			grid5.VerticalAlignment = VerticalAlignment.Center;
			Grid grid6 = grid5;
			grid6.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			grid6.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			grid6.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			grid6.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			grid6.RowDefinitions.Add(new RowDefinition
			{
				Height = new GridLength(1.0, GridUnitType.Star)
			});
			grid6.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			_mediaWideTitle = new TextBlock
			{
				Text = text,
				FontSize = NotchSize(13.0),
				FontWeight = AynType.Medium,
				Foreground = primaryBrush,
				FontFamily = AynType.Family(TypeSurface.Notch, NotchSize(13.0)),
				TextTrimming = TextTrimming.CharacterEllipsis,
				TextWrapping = TextWrapping.NoWrap,
				Margin = new Thickness(0.0, Math.Round(-1.0 * activityScale), Math.Round(8.0 * activityScale), 0.0)
			};
			grid6.Children.Add(_mediaWideTitle);
			_mediaWideArtist = new TextBlock
			{
				Text = (text2 ?? ""),
				FontSize = SecondaryFontSize,
				FontWeight = AynType.Regular,
				Foreground = NotchGlyphs.SecondaryBrush,
				FontFamily = AynType.Family(TypeSurface.Notch, SecondaryFontSize),
				TextTrimming = TextTrimming.CharacterEllipsis,
				TextWrapping = TextWrapping.NoWrap,
				Margin = new Thickness(0.0, Math.Round(1.0 * activityScale), Math.Round(8.0 * activityScale), 0.0)
			};
			Grid.SetRow(_mediaWideArtist, 1);
			grid6.Children.Add(_mediaWideArtist);
			_mediaRemainingText = new TextBlock
			{
				FontSize = SecondaryFontSize,
				FontWeight = AynType.Regular,
				Foreground = NotchGlyphs.SecondaryBrush,
				FontFamily = AynType.Family(TypeSurface.Notch, SecondaryFontSize),
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0.0, Math.Round(1.0 * activityScale), 0.0, 0.0)
			};
			AynType.Tabular(_mediaRemainingText);
			Grid.SetColumn(_mediaRemainingText, 1);
			grid6.Children.Add(_mediaRemainingText);
			_mediaRemainingText.MinWidth = Math.Round(42.0 * activityScale);
			_mediaRemainingText.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
			_mediaTransport = new MediaTransportControls(NotchInk.Primary, Math.Round(20.0 * activityScale))
			{
				HorizontalAlignment = System.Windows.HorizontalAlignment.Right
			};
			_mediaTransport.Update(MediaService.Instance.View);
			Grid.SetColumn(_mediaTransport, 1);
			Grid.SetRow(_mediaTransport, 1);
			grid6.Children.Add(_mediaTransport);
			double num9 = Math.Max(2.5, Half(3.0 * activityScale));
			_mediaProgressBar = new MediaSeekSurface
			{
				Height = Math.Max(8.0, num9),
				Background = System.Windows.Media.Brushes.Transparent,
				VerticalAlignment = VerticalAlignment.Bottom,
				Margin = new Thickness(0.0, 0.0, 0.0, Math.Round(2.0 * activityScale))
			};
			_mediaProgressBar.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(0.0, GridUnitType.Star)
			});
			_mediaProgressBar.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			Border border3 = new Border();
			border3.Height = num9;
			border3.VerticalAlignment = VerticalAlignment.Center;
			border3.CornerRadius = new CornerRadius(num9 / 2.0);
			border3.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(56, byte.MaxValue, byte.MaxValue, byte.MaxValue));
			Border element2 = border3;
			Grid.SetColumnSpan(element2, 2);
			_mediaProgressBar.Children.Add(element2);
			_mediaProgressBar.Children.Add(new Border
			{
				Height = num9,
				VerticalAlignment = VerticalAlignment.Center,
				CornerRadius = new CornerRadius(num9 / 2.0),
				Background = primaryBrush
			});
			Grid seekBar = _mediaProgressBar;
			MediaSeekInteraction.Attach(seekBar, () => _mediaTransport.State, delegate(double f)
			{
				seekBar.ColumnDefinitions[0].Width = new GridLength(f, GridUnitType.Star);
				seekBar.ColumnDefinitions[1].Width = new GridLength(1.0 - f, GridUnitType.Star);
			});
			Grid.SetRow(_mediaProgressBar, 3);
			Grid.SetColumnSpan(_mediaProgressBar, 2);
			grid6.Children.Add(_mediaProgressBar);
			UpdateMediaClock();
			Grid.SetColumn(grid6, 1);
			Grid.SetColumnSpan(grid6, 2);
			grid4.Children.Add(grid6);
		}
		grid2.Children.Add(grid4);
		_beatLineBars = (mediaWideWanted ? 56 : 40);
		_beatLineBarW = Half((mediaWideWanted ? 2.5 : 2.0) * activityScale);
		Border border4 = new Border();
		border4.Margin = SideMargin(card, num5, num4);
		border4.Height = num3;
		border4.VerticalAlignment = VerticalAlignment.Bottom;
		Border border5 = border4;
		_beatLineStatic = new StaticBeatBars(_beatLineBars, _beatLineBarW, mini: false, color);
		border5.Child = _beatLineStatic;
		Grid.SetRow(border5, 2);
		grid2.Children.Add(border5);
		_beatLineSlot = border5;
		_mediaRoot = grid2;
		_mediaWideShown = mediaWideWanted;
		_mediaArtShown = imageSource;
		_mediaColorShown = color;
		ApplyStaticBeat();
		return grid2;
	}

	private void UpdateMediaClock()
	{
		if (_mediaRemainingText == null && _mediaProgressBar == null)
		{
			return;
		}
		MediaTrackInfo mediaTimeline = _mediaTimeline;
		double num = mediaTimeline?.RemainingSeconds(DateTime.UtcNow) ?? (-1.0);
		double num2 = mediaTimeline?.Progress(DateTime.UtcNow) ?? (-1.0);
		if (_mediaRemainingText != null)
		{
			string text = ((num >= 0.0) ? FormatRemaining(num) : "");
			if (_mediaRemainingText.Text != text)
			{
				_mediaRemainingText.Text = text;
			}
		}
		if (_mediaProgressBar != null)
		{
			MediaSeekInteraction.Update(_mediaProgressBar, (_mediaTransport != null) ? _mediaTransport.State : null);
			_mediaProgressBar.Visibility = ((!(num2 >= 0.0)) ? Visibility.Hidden : Visibility.Visible);
			if (num2 >= 0.0)
			{
				_mediaProgressBar.ColumnDefinitions[0].Width = new GridLength(Math.Max(0.0, Math.Min(1.0, num2)), GridUnitType.Star);
				_mediaProgressBar.ColumnDefinitions[1].Width = new GridLength(Math.Max(0.0, 1.0 - num2), GridUnitType.Star);
			}
		}
	}

	private void UpdateMediaVisualInPlace(PillActivitySnapshot snap)
	{
		if (snap == null)
		{
			return;
		}
		NotchActivity act = snap.ToLegacyActivity();
		bool mediaWideWanted = MediaWideWanted;
		ImageSource imageSource = MediaArtFor(snap.Artwork, snap.Title);
		System.Windows.Media.Color color = MusicBeatColor(imageSource);
		if (_mediaRoot == null || mediaWideWanted != _mediaWideShown || !object.ReferenceEquals(imageSource, _mediaArtShown) || color != _mediaColorShown)
		{
			FrameworkElement frameworkElement = BuildMusicVisual(act);
			ContentControl contentControl = (_isSlotAActive ? _slotA : _slotB);
			if (contentControl != null)
			{
				contentControl.Content = frameworkElement;
				_activeOwner.VisualInstance = frameworkElement;
			}
			UpdateBeat();
			return;
		}
		string text = CleanSongTitle(snap.Title);
		string text2 = ArtistOf(act);
		if (string.IsNullOrWhiteSpace(text))
		{
			text = "Now playing";
		}
		if (_mediaTitleRun != null)
		{
			_mediaCompactTitle = text;
		}
		if (_mediaArtistRun != null)
		{
			_mediaCompactArtist = text2;
			FitCompactMediaText();
		}
		if (_mediaWideTitle != null && _mediaWideTitle.Text != text)
		{
			_mediaWideTitle.Text = text;
		}
		if (_mediaWideArtist != null && _mediaWideArtist.Text != (text2 ?? ""))
		{
			_mediaWideArtist.Text = text2 ?? "";
		}
		_mediaTimeline = snap.Payload as MediaTrackInfo;
		_mediaPlaying = snap.IsPlaying;
		if (_mediaTransport != null)
		{
			_mediaTransport.Update(MediaService.Instance.View);
		}
		UpdateMediaClock();
		ApplyStaticBeat();
		UpdateBeat();
	}

	private void FitCompactMediaText()
	{
		if (_mediaCompactText == null || _mediaTitleRun == null || _mediaArtistRun == null || _mediaCompactText.ActualWidth <= 0.0)
		{
			return;
		}
		TextBlock text = _mediaCompactText;
		string text2 = _mediaCompactTitle ?? "";
		if (_mediaFitWidth == text.ActualWidth && _mediaFitTitle == text2 && _mediaFitArtist == _mediaCompactArtist)
		{
			return;
		}
		_mediaFitWidth = text.ActualWidth;
		_mediaFitTitle = text2;
		_mediaFitArtist = _mediaCompactArtist;
		Typeface face = new Typeface(text.FontFamily, FontStyles.Normal, AynType.Medium, FontStretches.Normal);
		double dpi = VisualTreeHelper.GetDpi(text).PixelsPerDip;
		Func<string, double> func = (string value) => new FormattedText(value, CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight, face, text.FontSize, PrimaryBrush, dpi).WidthIncludingTrailingWhitespace;
		double num = func(text2);
		string text3 = text2;
		if (num > text.ActualWidth)
		{
			StringInfo stringInfo = new StringInfo(text2);
			int num2 = 0;
			int num3 = stringInfo.LengthInTextElements;
			while (num2 < num3)
			{
				int num4 = (num2 + num3 + 1) / 2;
				if (func(stringInfo.SubstringByTextElements(0, num4) + "…") <= text.ActualWidth)
				{
					num2 = num4;
				}
				else
				{
					num3 = num4 - 1;
				}
			}
			text3 = ((num2 == 0) ? "…" : stringInfo.SubstringByTextElements(0, num2).TrimEnd());
			int num5 = text3.LastIndexOf(' ');
			if (num5 > 0)
			{
				text3 = text3.Substring(0, num5);
			}
			text3 += ((num2 == 0) ? "" : "…");
		}
		if (_mediaTitleRun.Text != text3)
		{
			_mediaTitleRun.Text = text3;
		}
		string text4 = ((num + 40.0 * ActivityScale <= text.ActualWidth && !string.IsNullOrEmpty(_mediaCompactArtist)) ? ("  ·  " + _mediaCompactArtist) : "");
		if (_mediaArtistRun.Text != text4)
		{
			_mediaArtistRun.Text = text4;
		}
	}

	private void ClearMusicReferences()
	{
		_mediaRoot = null;
		_mediaTitleRun = (_mediaArtistRun = null);
		_mediaWideTitle = (_mediaWideArtist = null);
		_mediaRemainingText = null;
		_mediaProgressBar = null;
		_beatLineSlot = (_beatMiniSlot = null);
		_beatLineStatic = (_beatMiniStatic = null);
		_mediaTimeline = null;
		UpdateBeat();
	}

	private bool BeatCanRun()
	{
		if (!PetPreferences.Instance.Enabled && _beatLineSlot != null && _mediaPlaying && _activeOwner.VisualType == CollapsedVisualType.Media && IsRestingSurface && MorphProgress <= 0.001 && _beatPresenceFull && base.IsVisible)
		{
			return base.Visibility == Visibility.Visible;
		}
		return false;
	}

	private void ApplyStaticBeat()
	{
		StaticBeatBars.Pattern pattern = (_mediaPlaying ? StaticBeatBars.Pattern.Still : StaticBeatBars.Pattern.Rest);
		if (_beatLineStatic != null)
		{
			_beatLineStatic.SetPattern(pattern);
		}
		if (_beatMiniStatic != null)
		{
			_beatMiniStatic.SetPattern(pattern);
		}
	}

	private void UpdateBeat()
	{
		try
		{
			bool flag = BeatCanRun() && BeatAnimationsAllowed;
			if (flag && _beatOverlay == null && !_beatOverlayFailed)
			{
				IntPtr handle = new WindowInteropHelper(this).Handle;
				string error = null;
				_beatOverlay = ((handle == IntPtr.Zero) ? null : CreateBeatOverlay(handle, out error));
				if (_beatOverlay == null && handle != IntPtr.Zero)
				{
					_beatOverlayFailed = true;
					TransactionLogger.Warn("[Beat] compositor overlay unavailable, still beat line: " + error);
				}
			}
			if (flag && _beatOverlay != null)
			{
				_beatOverlay.SetColor(_mediaColorShown);
				if (!_beatActive)
				{
					_beatActive = true;
					_beatStable = 0;
					_beatLastLine = (_beatLastMini = Rect.Empty);
					if (_beatFrameHandler == null)
					{
						_beatFrameHandler = OnBeatFrameReady;
					}
					AudioSpectrum instance = AudioSpectrum.Instance;
					instance.FrameReady -= _beatFrameHandler;
					instance.FrameReady += _beatFrameHandler;
					instance.ExpectSound = true;
					instance.Start();
					_beatOverlay.Show(on: true);
				}
				if (_edgeLight != null)
				{
					_edgeLight.SetBeat(on: true, _mediaColorShown);
				}
			}
			else if (_beatActive)
			{
				StopBeat();
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("[Beat] disabled after error: " + ex.GetType().Name + ": " + ex.Message);
			try
			{
				StopBeat();
			}
			catch
			{
			}
			_beatOverlayFailed = true;
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static NotchBeatOverlay CreateBeatOverlay(IntPtr hwnd, out string error)
	{
		error = null;
		try
		{
			return NotchBeatOverlay.TryCreate(hwnd, out error);
		}
		catch (Exception ex)
		{
			error = ex.GetType().Name + ": " + ex.Message;
			return null;
		}
	}

	private void InvalidateBeatLayout()
	{
		_beatStable = 0;
		_beatLastLine = (_beatLastMini = Rect.Empty);
		if (_beatActive && _beatOverlay != null)
		{
			_beatOverlay.SetBarsVisible(on: false);
			if (_beatLineStatic != null)
			{
				_beatLineStatic.Opacity = 1.0;
			}
			if (_beatMiniStatic != null)
			{
				_beatMiniStatic.Opacity = 1.0;
			}
		}
	}

	private void StopBeat()
	{
		_beatActive = false;
		AudioSpectrum instance = AudioSpectrum.Instance;
		if (_beatFrameHandler != null)
		{
			instance.FrameReady -= _beatFrameHandler;
		}
		instance.ExpectSound = false;
		instance.Stop();
		if (_beatOverlay != null)
		{
			_beatOverlay.Show(on: false);
		}
		if (_edgeLight != null)
		{
			_edgeLight.SetBeat(on: false, _mediaColorShown);
		}
		if (_beatLineStatic != null)
		{
			_beatLineStatic.Opacity = 1.0;
		}
		if (_beatMiniStatic != null)
		{
			_beatMiniStatic.Opacity = 1.0;
		}
	}

	private void OnBeatFrameReady()
	{
		if (RenderProfile.LightMode)
		{
			long timestamp = Stopwatch.GetTimestamp();
			if (timestamp - _beatLastAcceptedTicks < Stopwatch.Frequency / 15)
			{
				return;
			}
			_beatLastAcceptedTicks = timestamp;
		}
		if (Interlocked.Exchange(ref _beatFramePending, 1) == 1)
		{
			return;
		}
		try
		{
			base.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(ApplyBeatFrame));
		}
		catch
		{
			_beatFramePending = 0;
		}
	}

	private Rect SlotPixels(FrameworkElement slot, double scale)
	{
		if (slot == null || !slot.IsVisible || !slot.IsDescendantOf(this))
		{
			return Rect.Empty;
		}
		Rect rect = slot.TransformToAncestor(this).TransformBounds(new Rect(0.0, 0.0, slot.ActualWidth, slot.ActualHeight));
		return new Rect(Math.Round(rect.X * scale), Math.Round(rect.Y * scale), Math.Round(rect.Width * scale), Math.Round(rect.Height * scale));
	}

	private static bool Near(Rect a, Rect b)
	{
		if (!a.IsEmpty && !b.IsEmpty && Math.Abs(a.X - b.X) < 0.5 && Math.Abs(a.Y - b.Y) < 0.5 && Math.Abs(a.Width - b.Width) < 0.5)
		{
			return Math.Abs(a.Height - b.Height) < 0.5;
		}
		return false;
	}

	private void ApplyBeatFrame()
	{
		long timestamp = Stopwatch.GetTimestamp();
		try
		{
			ApplyBeatFrameCore();
		}
		finally
		{
			_beatUiTicks += Stopwatch.GetTimestamp() - timestamp;
			_beatUiFrames++;
		}
	}

	private void ApplyBeatFrameCore()
	{
		_beatFramePending = 0;
		if (!_beatActive || _beatOverlay == null)
		{
			return;
		}
		if (!BeatCanRun() || !BeatAnimationsAllowed)
		{
			UpdateBeat();
			return;
		}
		PresentationSource presentationSource = PresentationSource.FromVisual(this);
		double num = ((presentationSource != null && presentationSource.CompositionTarget != null) ? presentationSource.CompositionTarget.TransformToDevice.M11 : 1.0);
		Rect rect = SlotPixels(_beatLineSlot, num);
		Rect rect2 = SlotPixels(_beatMiniSlot, num);
		bool flag = !Near(rect, _beatLastLine) || ((!rect2.IsEmpty || !_beatLastMini.IsEmpty) && !Near(rect2, _beatLastMini));
		_beatLastLine = rect;
		_beatLastMini = rect2;
		if (flag || rect.IsEmpty)
		{
			_beatStable = 0;
			_beatOverlay.SetBarsVisible(on: false);
			if (_beatLineStatic != null)
			{
				_beatLineStatic.Opacity = 1.0;
			}
			if (_beatMiniStatic != null)
			{
				_beatMiniStatic.Opacity = 1.0;
			}
		}
		else if (++_beatStable == 2)
		{
			_beatOverlay.SyncWindow();
			_beatOverlay.SetLayout(rect, _beatLineBars, Math.Max(1.0, Math.Round(_beatLineBarW * num)), rect2, (!rect2.IsEmpty) ? 4 : 0, Math.Max(1.0, Math.Round(_beatMiniBarW * num)));
			_beatOverlay.SetBarsVisible(on: true);
			if (_beatLineStatic != null)
			{
				_beatLineStatic.Opacity = 0.0;
			}
			if (_beatMiniStatic != null)
			{
				_beatMiniStatic.Opacity = 0.0;
			}
		}
		bool live;
		float num2 = AudioSpectrum.Instance.Read(_beatBands, out live);
		if (!live)
		{
			_beatOverlay.SetBarsVisible(on: false);
			_beatStable = 0;
			if (_beatLineStatic != null)
			{
				_beatLineStatic.SetPattern(StaticBeatBars.Pattern.Rest);
				_beatLineStatic.Opacity = 1.0;
			}
			if (_beatMiniStatic != null)
			{
				_beatMiniStatic.SetPattern(StaticBeatBars.Pattern.Rest);
				_beatMiniStatic.Opacity = 1.0;
			}
			if (_edgeLight != null)
			{
				_edgeLight.SetBeatLevel(0f);
			}
		}
		else
		{
			MapBands(_beatBands, _beatLine, _beatLineBars);
			_beatMini[0] = Avg(_beatBands, 6, 10);
			_beatMini[1] = Avg(_beatBands, 0, 4);
			_beatMini[2] = Avg(_beatBands, 14, 20);
			_beatMini[3] = Avg(_beatBands, 9, 14);
			if (_beatStable >= 2)
			{
				_beatOverlay.SetLevels(_beatLine, _beatMini);
			}
			if (_edgeLight != null)
			{
				_edgeLight.SetBeatLevel((live && _beatStable >= 2) ? num2 : 0f);
			}
		}
	}

	private static float Avg(float[] a, int from, int to)
	{
		float num = 0f;
		int num2 = 0;
		int num3 = from;
		while (num3 <= to && num3 < a.Length)
		{
			num += a[num3];
			num3++;
			num2++;
		}
		if (num2 <= 0)
		{
			return 0f;
		}
		return num / (float)num2;
	}

	internal static void MapBands(float[] bands, float[] bars, int n)
	{
		double num = (double)(n - 1) / 2.0;
		for (int i = 0; i < n; i++)
		{
			double num2 = Math.Abs((double)i - num) / Math.Max(1.0, num);
			double num3 = num2 * (double)(bands.Length - 1) * 0.92;
			int num4 = (int)num3;
			double num5 = num3 - (double)num4;
			float num6 = (float)((double)bands[num4] * (1.0 - num5) + (double)bands[Math.Min(bands.Length - 1, num4 + 1)] * num5);
			bars[i] = num6;
		}
	}

	[DllImport("user32.dll", EntryPoint = "IsWindowVisible")]
	private static extern bool PetNativeVisible(IntPtr hwnd);

	[DllImport("user32.dll", EntryPoint = "ShowWindow")]
	private static extern bool PetNativeShow(IntPtr hwnd, int command);

	private string PetPlacementProblem()
	{
		if (IsRestingSurface)
		{
			return CheckWindowPlacement();
		}
		MONITORINFO mONITORINFO = default(MONITORINFO);
		mONITORINFO.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
		MONITORINFO lpmi = mONITORINFO;
		if (GetWindowRect(_hwnd, out var lpRect) && GetMonitorInfo(MonitorFromWindow(_hwnd, 2u), ref lpmi) && (lpRect.Right <= lpmi.rcMonitor.Left || lpRect.Left >= lpmi.rcMonitor.Right || lpRect.Bottom <= lpmi.rcMonitor.Top || lpRect.Top >= lpmi.rcMonitor.Bottom))
		{
			return "pet panel off-screen";
		}
		return null;
	}

	private bool PetAtomicSwap(PillActivitySnapshot snapshot)
	{
		bool flag = PetPreferences.Instance.Enabled || snapshot.Payload is PetMoment;
		bool result = flag || _petAtomicOwner;
		_petAtomicOwner = flag;
		return result;
	}

	private void InitializePet()
	{
		if (_petInitialized)
		{
			return;
		}
		_petInitialized = true;
		if (_machine != null)
		{
			_machine.ImmediateRecovery = () => PetPreferences.Instance.Enabled;
		}
		if (_machine != null)
		{
			_machine.Recovered += delegate(string reason)
			{
				if (PetPreferences.Instance.Enabled)
				{
					TransactionLogger.Warn("Pet notch recovery: " + reason);
				}
			};
		}
		PetEngine.Instance.Start();
		PetEngine.Instance.Changed += PetChanged;
		PetPreferences.Instance.Changed += PetChanged;
		_capsuleHeader.IsVisibleChanged += delegate
		{
			UpdatePetHeader();
		};
		_lastPetEnabled = PetPreferences.Instance.Enabled;
		_lastPetEnvironmentVisible = PetEngine.Instance.EnvironmentVisible;
		UpdatePetResting();
		UpdatePetHeader();
		base.Closed += delegate
		{
			if (_petStageWindow != null && _petStageWindow.IsLoaded)
			{
				_petStageWindow.Close();
			}
			PetView.Shutdown();
			PetPreferences.Instance.Changed -= PetChanged;
			PetEngine.Instance.Changed -= PetChanged;
			PetEngine.Instance.Dispose();
		};
	}

	private void PetChanged(object s, EventArgs e)
	{
		if (_isShuttingDown)
		{
			return;
		}
		bool environmentVisible = PetEngine.Instance.EnvironmentVisible;
		if (!(s is PetEngine) || _lastPetEnabled != PetPreferences.Instance.Enabled || _lastPetEnvironmentVisible != environmentVisible)
		{
			_lastPetEnvironmentVisible = environmentVisible;
			if (PetPreferences.Instance.Enabled)
			{
				PetEngine.Instance.Start();
			}
			else
			{
				PetEngine.Instance.Dispose();
			}
			bool flag = _lastPetEnabled != PetPreferences.Instance.Enabled;
			if (flag)
			{
				_petLayoutKey = null;
			}
			_lastPetEnabled = PetPreferences.Instance.Enabled;
			UpdatePetResting();
			UpdatePetHeader();
			ArrangeOutsidePet();
			PetView.RefreshAll();
			if (_edgeLight != null)
			{
				_edgeLight.Refresh();
			}
			UpdateBeat();
			if (flag)
			{
				_restingPlan = null;
				_restingFitWidth = 0.0;
				_restingMetricsKey = double.NaN;
				_collapsedContent.Width = double.NaN;
				_defaultCollapsedStack.ClearValue(UIElement.ClipProperty);
				_slotA.ClearValue(UIElement.ClipProperty);
				_slotB.ClearValue(UIElement.ClipProperty);
				_activityViewport.ClearValue(UIElement.ClipProperty);
				RefreshRestingPresentation(rebuild: true, animate: false);
			}
			else if (IsRestingSurface)
			{
				RefreshRestingContentSize();
				RenderCurrentResolvedPillActivity(immediate: true);
			}
		}
	}

	private void UpdatePetHeader()
	{
		if (!_capsuleHeader.IsVisible)
		{
			if (_petHeader != null)
			{
				_capsuleHeader.SetPet(null);
				_petHeader = null;
			}
		}
		else if (_petHeader == null)
		{
			_petHeader = new AynRobotMark(28.0)
			{
				Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
				VerticalAlignment = VerticalAlignment.Center
			};
			_capsuleHeader.SetPet(_petHeader);
		}
	}

	private static bool GardenResting(PillActivitySnapshot snap)
	{
		if (PetPreferences.Instance.Enabled)
		{
			if (snap != null && snap.Type != 0)
			{
				if (snap.Type == ActivityType.Media && snap.IsPlaying)
				{
					return PetPreferences.Instance.DanceToMusic;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	private bool UpdatePetResting()
	{
		if (_collapsedContent == null)
		{
			return false;
		}
		if (_defaultCollapsedStack != null && PetPreferences.Instance.Enabled)
		{
			if (!_petRowSuppressed)
			{
				_petRowOpacity = _defaultCollapsedStack.Opacity;
				_petRowHitTest = _defaultCollapsedStack.IsHitTestVisible;
				_petRowSuppressed = true;
			}
			_defaultCollapsedStack.Opacity = 0.0;
			_defaultCollapsedStack.IsHitTestVisible = false;
		}
		else if (_defaultCollapsedStack != null && _petRowSuppressed)
		{
			_defaultCollapsedStack.Opacity = _petRowOpacity;
			_defaultCollapsedStack.IsHitTestVisible = _petRowHitTest;
			_petRowSuppressed = false;
		}
		if (!PetPreferences.Instance.Enabled)
		{
			_petPromptResize = false;
			SetPetStageVisible(visible: false);
			return false;
		}
		if (_petOutside == null)
		{
			_petOutside = new GardenView
			{
				NotchOpacity = _closedNotchOpacity,
				NotchSurface = _notchSilhouettePath,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Top,
				Visibility = Visibility.Collapsed
			};
			_petOutside.CanPresentDialogue = () => IsRestingSurface && !_restingResizeActive && !_petPausedForMorph && MorphProgress <= 0.001;
			_petStageWindow = new PetStageWindow(this, _petOutside);
			_petStageWindow.PointerInput = delegate(System.Windows.Point point, bool click)
			{
				if (IsRestingSurface && GardenOwnsStage)
				{
					if (click)
					{
						if (!_petOutside.HandleStageClick(point) && _machine != null)
						{
							_machine.Click("pet stage");
						}
					}
					else
					{
						_petOutside.ObserveStagePointer(point);
						if (_machine != null)
						{
							_machine.PointerMoved(!GardenBlocksHover && IsPointerInside() && !HoldsActionCard(), "pet stage hover");
						}
					}
				}
			};
			_petOutside.DialogueChanged += delegate
			{
				if (!_isShuttingDown && IsRestingSurface)
				{
					if (_petOutside.HasActionPrompt)
					{
						_petPromptResize = true;
						if (_machine != null)
						{
							_machine.PointerMoved(inside: false, "pet prompt owns input");
						}
					}
					RefreshRestingContentSize();
				}
			};
			_capsuleCard.LayoutUpdated += delegate
			{
				ArrangeOutsidePet();
			};
			base.LocationChanged += delegate
			{
				_petPlacementDirty = true;
				ArrangeOutsidePet();
			};
			base.SizeChanged += delegate
			{
				_petPlacementDirty = true;
				ArrangeOutsidePet();
			};
			_capsuleCard.SizeChanged += delegate
			{
				_petPlacementDirty = true;
				ArrangeOutsidePet();
			};
			base.IsVisibleChanged += delegate
			{
				ArrangeOutsidePet();
			};
		}
		ArrangeOutsidePet();
		return GardenOwnsStage;
	}

	private void SuspendPetForPanel()
	{
		if (_petOutside == null || _petPausedForMorph)
		{
			return;
		}
		_petPausedForMorph = true;
		if (_petStageWindow != null)
		{
			_petStageWindow.Suspended = true;
			_petStageWindow.SetNativeHidden(hidden: true);
		}
		_petOutside.Visibility = Visibility.Collapsed;
		if (_petResume == null)
		{
			_petResume = new DispatcherTimer(DispatcherPriority.Background)
			{
				Interval = TimeSpan.FromMilliseconds(160.0)
			};
			_petResume.Tick += delegate
			{
				if (_machine == null || _machine.Phase != NotchPhase.Resting || Math.Abs(MorphProgress) > 0.001)
				{
					_petResume.Interval = TimeSpan.FromMilliseconds(500.0);
				}
				else
				{
					_petResume.Stop();
					ResumePetAfterPanel();
				}
			};
		}
		_petResume.Stop();
	}

	private void SchedulePetResume()
	{
		if (_petPausedForMorph && _petResume != null)
		{
			_petResume.Interval = TimeSpan.FromMilliseconds(160.0);
			_petResume.Stop();
			_petResume.Start();
		}
	}

	private void ResumePetAfterPanel()
	{
		if (_petPausedForMorph)
		{
			_petPausedForMorph = false;
			if (_petStageWindow != null)
			{
				_petStageWindow.Suspended = false;
			}
			_petPlacementDirty = true;
			UpdatePetResting();
		}
	}

	private void SettlePetStage()
	{
		ArrangeOutsidePet();
		if (!_restingResizeActive && (_petOutside == null || !_petOutside.HasActionPrompt))
		{
			_petPromptResize = false;
		}
	}

	private void ArrangeOutsidePet()
	{
		if (_petOutside == null || _activityViewport == null)
		{
			return;
		}
		if (_petPausedForMorph)
		{
			if (!PetPreferences.Instance.Enabled && _petStageWindow != null && _petStageWindow.IsVisible)
			{
				_petStageWindow.Hide();
			}
		}
		else if (_restingResizeActive && _petPromptResize)
		{
			if (_petStageWindow != null)
			{
				_petStageWindow.SetNativeHidden(hidden: true);
			}
			_petPlacementDirty = true;
		}
		else
		{
			if (PresentationSource.FromVisual(this) == null || _arrangingPet)
			{
				return;
			}
			_arrangingPet = true;
			try
			{
				bool flag = _petOutside.SceneName == "exit" && !_petOutside.HasExited;
				if (flag && !IsRestingSurface && !NotchSettings.Instance.OpensOnClick)
				{
					return;
				}
				bool flag2 = IsRestingSurface && _petOutside.IsVisible && !_petOutside.HasExited;
				if (!base.IsVisible || base.WindowState == WindowState.Minimized || !PetEngine.Instance.EnvironmentVisible || !PetPreferences.Instance.Enabled || (!IsRestingSurface && !flag) || (!GardenOwnsStage && !flag && !flag2))
				{
					SetPetStageVisible(visible: false);
				}
				else
				{
					if (_notchSilhouettePath.Data == null)
					{
						return;
					}
					Rect bounds = _notchSilhouettePath.Data.Bounds;
					_petOutside.Width = bounds.Width;
					_petOutside.Height = bounds.Height;
					if (_petPlacementDirty || _petStageShapeSource != _notchSilhouettePath.Data)
					{
						if (_petStageShapeSource != null && !_petStageShapeSource.IsFrozen)
						{
							_petStageShapeSource.Changed -= PetShapeChanged;
						}
						_petStageShapeSource = _notchSilhouettePath.Data;
						if (!_petStageShapeSource.IsFrozen)
						{
							_petStageShapeSource.Changed += PetShapeChanged;
						}
						Geometry geometry = _petStageShapeSource.CloneCurrentValue();
						Matrix matrix = ((geometry.Transform == null) ? Matrix.Identity : geometry.Transform.Value);
						matrix.Translate(0.0 - bounds.Left, 0.0 - bounds.Top);
						geometry.Transform = new MatrixTransform(matrix);
						geometry.Freeze();
						_petOutside.StageShape = geometry;
						_petOutside.InvalidateStageClip();
					}
					if (!_petStageWindow.IsVisible)
					{
						_petStageWindow.Width = bounds.Width;
						_petStageWindow.Height = bounds.Height;
					}
					SetPetStageVisible(visible: true);
					_petStageWindow.Place(_notchSilhouettePath.PointToScreen(bounds.TopLeft), _notchSilhouettePath.PointToScreen(bounds.BottomRight));
					_petPlacementDirty = false;
					if (GardenOwnsStage)
					{
						_petOutside.ReturnToStage();
					}
				}
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Pet surface recovery: " + ex);
				_petPlacementDirty = true;
			}
			finally
			{
				_arrangingPet = false;
			}
		}
	}

	private void PetShapeChanged(object sender, EventArgs args)
	{
		_petPlacementDirty = true;
	}

	private void SetPetStageVisible(bool visible)
	{
		if (_petOutside == null || (_petPausedForMorph && visible))
		{
			return;
		}
		_petOutside.Visibility = ((!visible) ? Visibility.Collapsed : Visibility.Visible);
		if (_petStageWindow == null || (_petPausedForMorph && PetPreferences.Instance.Enabled))
		{
			return;
		}
		if (visible)
		{
			if (!_petStageWindow.IsVisible)
			{
				_petStageWindow.Owner = this;
				_petStageWindow.Show();
			}
		}
		else if (_petStageWindow.IsVisible)
		{
			_petStageWindow.Hide();
		}
	}

	private bool DeferGardenActivity(PillActivitySnapshot next)
	{
		if (_gardenHandoff || _petOutside == null || !PetPreferences.Instance.Enabled || GardenResting(next) || !IsRestingSurface)
		{
			return false;
		}
		TransactionLogger.Info("Pet Garden: hand-off for " + ((next == null) ? "nothing" : string.Concat(next.Type, " ", next.Id)));
		return _petOutside.Exit(delegate
		{
			_gardenHandoff = true;
			try
			{
				RenderCurrentResolvedPillActivity(immediate: true);
				ArrangeOutsidePet();
			}
			finally
			{
				_gardenHandoff = false;
			}
		});
	}

	internal static bool IsPetStatus(PillActivitySnapshot snap)
	{
		if (PetPreferences.Instance.Enabled && snap != null && snap.Type != 0 && snap.Type != ActivityType.Clock && snap.Type != ActivityType.Media && snap.Type != ActivityType.TimerRunning && snap.Type != ActivityType.Volume && snap.Type != ActivityType.BluetoothConnected && snap.Type != ActivityType.BluetoothDisconnected && !ReminderDelivery.IsReminder(snap.Type) && snap.Type != ActivityType.CalendarAlert && snap.Type != ActivityType.ScreenTimeLimit)
		{
			if (snap.Type == ActivityType.Custom)
			{
				if (!(snap.Id == "update_ready") && !(snap.Id == "backup_restore_offer") && !(snap.Id == "aicoding_budget_alert"))
				{
					return !(snap.Id == "drop_complete");
				}
				return false;
			}
			return true;
		}
		return false;
	}

	internal static bool IsPetSuggestion(PillActivitySnapshot snap)
	{
		return ((snap == null) ? null : (snap.Payload as PetMoment))?.Suggestion ?? false;
	}

	private static System.Windows.Size PetMessageSize(PillActivitySnapshot snap)
	{
		double messageScale = MessageScale;
		PetMoment petMoment = snap.Payload as PetMoment;
		bool flag = petMoment?.Suggestion ?? false;
		string title = ((petMoment != null) ? petMoment.Title : snap.Title);
		string detail = ((petMoment != null) ? petMoment.Subtitle : snap.ToLegacyActivity().Subtitle);
		double num = MessageRowHeight(flag);
		double num2 = (flag ? ButtonsWidth(new string[2] { petMoment.PrimaryLabel, "Later" }, calendar: false) : 0.0);
		double val = 2.0 * MessageSideInset(flag) + MessageRobot(num) + 10.0 * messageScale + MessageLineWidth(title, detail) + num2 + 6.0 * messageScale;
		val = Math.Min(560.0 * messageScale, Math.Max(NotchConfig.Instance.GetCollapsedWidth(), val));
		return new System.Windows.Size(Math.Ceiling(val), Math.Round(num) + ((NotchConfig.Instance.StyleFamily == "Island") ? 5.0 : 0.0));
	}

	internal static void PetEventLook(ActivityType type, string id, bool battery, out string glyph, out System.Windows.Media.Color tint, out string pose)
	{
		glyph = null;
		tint = LiveMessageVisuals.Blue;
		pose = "wave";
		switch (type)
		{
		case ActivityType.ClipboardCopied:
			glyph = "clipboard.check";
			pose = "paper";
			return;
		case ActivityType.BatteryLow:
			glyph = "battery.warning";
			tint = LiveMessageVisuals.Red;
			pose = "low-battery";
			return;
		}
		if (battery)
		{
			glyph = "battery.charge";
			tint = LiveMessageVisuals.Green;
			pose = "plug";
			return;
		}
		switch (id)
		{
		case "screenshot":
			glyph = "camera";
			pose = "photo";
			return;
		case "download":
			glyph = "download";
			pose = "paper";
			return;
		case "shelf":
			glyph = "backup";
			pose = "paper";
			return;
		}
		if (type == ActivityType.HydrationReminder || id == "ask-water")
		{
			glyph = "drop.fill";
			tint = LiveMessageVisuals.Cyan;
			pose = "wave";
		}
	}

	internal static FrameworkElement PetWithBadge(FrameworkElement robot, string glyph, System.Windows.Media.Color tint, double size)
	{
		if (glyph == null)
		{
			return robot;
		}
		Grid grid = new Grid();
		grid.Width = size;
		grid.Height = size;
		grid.VerticalAlignment = VerticalAlignment.Center;
		Grid grid2 = grid;
		grid2.Children.Add(robot);
		double num = size * 0.42;
		Border border = new Border();
		border.Width = num;
		border.Height = num;
		border.CornerRadius = new CornerRadius(num / 2.0);
		border.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(235, 28, 28, 32));
		border.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		border.VerticalAlignment = VerticalAlignment.Bottom;
		border.Child = ((glyph == "camera") ? PetCameraGlyph(num * 0.72) : LiveMessageVisuals.Glyph(glyph, num * 0.72, tint));
		Border border2 = border;
		((FrameworkElement)border2.Child).HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		((FrameworkElement)border2.Child).VerticalAlignment = VerticalAlignment.Center;
		grid2.Children.Add(border2);
		return grid2;
	}

	internal static FrameworkElement PetCameraGlyph(double size)
	{
		DrawingGroup drawingGroup = new DrawingGroup();
		using (DrawingContext drawingContext = drawingGroup.Open())
		{
			drawingContext.DrawRoundedRectangle(new SolidColorBrush(System.Windows.Media.Color.FromRgb(142, 142, 147)), null, new Rect(1.0, 6.0, 22.0, 15.0), 3.5, 3.5);
			drawingContext.DrawRoundedRectangle(new SolidColorBrush(System.Windows.Media.Color.FromRgb(142, 142, 147)), null, new Rect(7.0, 3.0, 10.0, 5.0), 1.5, 1.5);
			drawingContext.DrawEllipse(new SolidColorBrush(System.Windows.Media.Color.FromRgb(28, 28, 30)), null, new System.Windows.Point(12.0, 13.5), 5.6, 5.6);
			drawingContext.DrawEllipse(new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 210, byte.MaxValue)), null, new System.Windows.Point(12.0, 13.5), 3.4, 3.4);
			drawingContext.DrawEllipse(System.Windows.Media.Brushes.White, null, new System.Windows.Point(13.4, 12.0), 1.1, 1.1);
			drawingContext.DrawEllipse(new SolidColorBrush(System.Windows.Media.Color.FromRgb(byte.MaxValue, 214, 10)), null, new System.Windows.Point(19.5, 9.2), 1.3, 1.3);
		}
		drawingGroup.Freeze();
		System.Windows.Controls.Image image = new System.Windows.Controls.Image();
		image.Source = new DrawingImage(drawingGroup);
		image.Width = size;
		image.Height = size;
		image.IsHitTestVisible = false;
		return image;
	}

	private FrameworkElement BuildPetActivity(NotchActivity activity)
	{
		PetMoment moment = activity.Payload as PetMoment;
		_ = PetPreferences.Instance.Enabled;
		bool flag = IsPetStatus(activity.ToSnapshot());
		if (moment == null && !flag)
		{
			return null;
		}
		bool flag2 = activity.Type == ActivityType.BatteryLow || activity.Type == ActivityType.BatteryCharging || activity.Type == ActivityType.BatteryFull;
		double messageScale = MessageScale;
		string title = ((moment != null) ? moment.Title : activity.Title);
		string detail = ((moment != null) ? moment.Subtitle : activity.Subtitle);
		PetEventLook(activity.Type, (moment != null) ? moment.Id : activity.Id, flag2, out var _, out var _, out var pose);
		if (moment != null && pose == "wave" && moment.Pose != null)
		{
			pose = moment.Pose;
		}
		bool flag3 = moment != null && moment.Suggestion;
		double num = MessageRowHeight(flag3);
		double num2 = MessageSideInset(flag3);
		Grid grid = new Grid();
		grid.Width = Math.Max(0.0, PetMessageSize(activity.ToSnapshot()).Width - 2.0 * num2);
		grid.Height = num;
		grid.Margin = new Thickness(num2, 0.0, num2, 0.0);
		grid.VerticalAlignment = VerticalAlignment.Center;
		grid.Background = System.Windows.Media.Brushes.Transparent;
		grid.UseLayoutRounding = true;
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		AynRobotMark aynRobotMark = new AynRobotMark(MessageRobot(num));
		aynRobotMark.VerticalAlignment = VerticalAlignment.Center;
		aynRobotMark.Margin = new Thickness(0.0, 0.0, 10.0 * messageScale, 0.0);
		AynRobotMark element = aynRobotMark;
		grid2.Children.Add(element);
		TextBlock heading;
		Grid grid3 = MessageLine(title, detail, out heading);
		if (flag2 && activity.Type != ActivityType.BatteryLow)
		{
			heading.Foreground = NotchInk.Success;
		}
		Grid.SetColumn(grid3, 1);
		grid2.Children.Add(grid3);
		if (flag3)
		{
			StackPanel stackPanel = new StackPanel();
			stackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
			stackPanel.VerticalAlignment = VerticalAlignment.Center;
			StackPanel stackPanel2 = stackPanel;
			Action close = delegate
			{
				PillActivityArbiter.Instance.DismissCurrentTransient();
			};
			double messageButtonHeight = MessageButtonHeight;
			grid3.Margin = new Thickness(0.0, 0.0, 12.0 * messageScale, 0.0);
			Border border = PillButton(moment.PrimaryLabel, Ink(ReminderAccent(), byte.MaxValue), ActionInk(ReminderAccent()), messageButtonHeight, new CornerRadius(messageButtonHeight / 2.0), delegate
			{
				close();
				if (moment.Primary != null)
				{
					moment.Primary();
				}
			});
			Border border2 = PillButton("Later", Ink(Colors.White, 31), NotchInk.Primary, messageButtonHeight, new CornerRadius(messageButtonHeight / 2.0), close);
			Thickness padding = (border2.Padding = new Thickness(11.0 * messageScale, 0.0, 11.0 * messageScale, 0.0));
			border.Padding = padding;
			border2.Margin = new Thickness(8.0 * messageScale, 0.0, 0.0, 0.0);
			stackPanel2.Children.Add(border);
			stackPanel2.Children.Add(border2);
			Grid.SetColumn(stackPanel2, 2);
			grid2.Children.Add(stackPanel2);
		}
		return grid2;
	}

	internal static double EyesRestingWidth(System.Windows.Size rest)
	{
		double num = Math.Floor(rest.Height * 0.96);
		double num2 = 1.6363636363636365 * num * 1.3;
		double num3 = Math.Max(rest.Height * 0.7, 20.0);
		return Math.Min(Math.Ceiling(num2 + 2.0 * num3), rest.Width);
	}

	private void InitializeEyes()
	{
		if (_eyesInitialized)
		{
			return;
		}
		_eyesInitialized = true;
		_eyesModeSeen = EyesModeOn;
		EyesPreferences.Instance.Changed += EyesPrefsChanged;
		PetPreferences.Instance.Changed += EyesPrefsChanged;
		if (_machine != null)
		{
			_machine.PhaseChanged += delegate
			{
				EyesRefresh();
			};
		}
		if (_collapsedContent != null)
		{
			_collapsedContent.SizeChanged += delegate
			{
				EyesRefresh();
			};
		}
		if (_defaultCollapsedStack != null)
		{
			_defaultCollapsedStack.IsVisibleChanged += delegate
			{
				EyesApplyRowSuppression();
			};
		}
		base.Closed += delegate
		{
			EyesPreferences.Instance.Changed -= EyesPrefsChanged;
			PetPreferences.Instance.Changed -= EyesPrefsChanged;
			EyesTeardown("shutdown");
		};
		if (_eyesModeSeen)
		{
			EyesRelayout();
		}
		EyesRefresh();
	}

	private void EyesApplyRowSuppression()
	{
		if (_defaultCollapsedStack == null)
		{
			return;
		}
		if (EyesModeOn)
		{
			_eyesRowHidden = true;
			if (_defaultCollapsedStack.Visibility != Visibility.Collapsed)
			{
				_defaultCollapsedStack.Visibility = Visibility.Collapsed;
			}
		}
		else if (_eyesRowHidden)
		{
			_eyesRowHidden = false;
			_defaultCollapsedStack.Visibility = Visibility.Visible;
		}
	}

	private void EyesRelayout()
	{
		EyesApplyRowSuppression();
		_restingPlan = null;
		_restingFitWidth = 0.0;
		_restingMetricsKey = double.NaN;
		if (_collapsedContent != null)
		{
			_collapsedContent.Width = double.NaN;
		}
		RefreshRestingPresentation(rebuild: true, animate: false);
	}

	private void EyesEnvironmentChanged()
	{
		if (_eyesResting != null)
		{
			_eyesResting.RefreshCenter();
		}
		if (_eyesMini != null)
		{
			_eyesMini.RefreshCenter();
		}
		UpdateCollapsedLookOnly();
		EyesRefresh();
	}

	private void EyesPrefsChanged(object sender, EventArgs e)
	{
		Action action = delegate
		{
			bool eyesModeOn = EyesModeOn;
			if (eyesModeOn != _eyesModeSeen)
			{
				_eyesModeSeen = eyesModeOn;
				EyesRelayout();
			}
			UpdateCollapsedLookOnly();
			EyesRefresh();
		};
		if (base.Dispatcher.CheckAccess())
		{
			action();
		}
		else
		{
			base.Dispatcher.BeginInvoke(action);
		}
	}

	private void EyesEnsureSentinel()
	{
		if (_eyesSentinel == null)
		{
			_eyesSentinel = new DispatcherTimer(DispatcherPriority.Background)
			{
				Interval = TimeSpan.FromSeconds(1.0)
			};
			_eyesSentinel.Tick += delegate
			{
				EyesRefresh();
			};
		}
		if (!_eyesSentinel.IsEnabled)
		{
			_eyesSentinel.Start();
		}
	}

	private void EyesTeardown(string state)
	{
		if (_eyesSentinel != null)
		{
			_eyesSentinel.Stop();
			_eyesSentinel = null;
		}
		if (_eyesResting != null)
		{
			_eyesResting.Release();
			if (_collapsedContent != null)
			{
				_collapsedContent.Children.Remove(_eyesResting);
			}
			_eyesResting = null;
		}
		if (_eyesMini != null)
		{
			_eyesMini.Release();
			if (_eyesMini.Parent is System.Windows.Controls.Panel panel)
			{
				panel.Children.Remove(_eyesMini);
			}
			_eyesMini = null;
		}
		EyesState = state;
	}

	private static void EyesHide(EyesView v)
	{
		if (v != null)
		{
			v.Stop();
			v.BeginAnimation(UIElement.OpacityProperty, null);
			v.Visibility = Visibility.Collapsed;
		}
	}

	private static void EyesReveal(EyesView v)
	{
		if (v.Visibility != 0)
		{
			v.Opacity = 0.0;
			v.Visibility = Visibility.Visible;
			v.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(260.0))
			{
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				},
				FillBehavior = FillBehavior.Stop
			});
			v.Opacity = 1.0;
		}
	}

	private bool EyesActivityIsResting()
	{
		PillActivitySnapshot currentActivity = PillActivityArbiter.Instance.CurrentActivity;
		if (currentActivity != null)
		{
			return currentActivity.Type == ActivityType.RestingClock;
		}
		return true;
	}

	private void ShowCompanionPet(string id)
	{
		if (_collapsedContent == null) return;
		if (_companionPet == null)
		{
			_companionPet = new CompanionPetCanvas
			{
				Width = 92,
				Height = 40,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				IsHitTestVisible = false
			};
			_collapsedContent.Children.Add(_companionPet);
		}
		_companionPet.Kind = id;
		_companionPet.Visibility = Visibility.Visible;
	}

	private void HideCompanionPet()
	{
		if (_companionPet != null) _companionPet.Visibility = Visibility.Collapsed;
	}

	internal void EyesRefresh()
	{
		if (_isShuttingDown)
		{
			EyesTeardown("shutdown");
			return;
		}
		var companion = EyesPreferences.Instance.Companion;
		if (NotchCompanions.IsPet(companion))
		{
			EyesTeardown("companion");
			ShowCompanionPet(companion);
			return;
		}
		HideCompanionPet();
		if (!EyesPreferences.Instance.Enabled)
		{
			EyesTeardown("off");
			return;
		}
		if (PetPreferences.Instance.Enabled)
		{
			EyesTeardown("pet");
			return;
		}
		EyesEnsureSentinel();
		NotchVisibilityEngine instance = NotchVisibilityEngine.Instance;
		PresenceInputs lastInputs = instance.LastInputs;
		string text = null;
		bool flag = false;
		bool flag2 = false;
		if (_machine == null || !base.IsVisible || base.Visibility != 0)
		{
			text = "window hidden";
		}
		else if (instance.SessionLocked || lastInputs.SessionLocked)
		{
			text = "session locked";
		}
		else if (lastInputs.FullscreenOnMonitor)
		{
			text = "fullscreen";
		}
		else if (_shownPresence == NotchPresence.None)
		{
			text = "notch hidden";
		}
		else if (_shownPresence == NotchPresence.Line)
		{
			flag = true;
		}
		else if (!IsRestingSurface)
		{
			text = "notch open";
		}
		else if (!EyesActivityIsResting())
		{
			text = "activity";
		}
		else
		{
			flag2 = true;
		}
		bool flag3 = flag && EyesShowMini();
		bool flag4 = flag2 && EyesShowResting();
		if (!flag)
		{
			EyesHide(_eyesMini);
		}
		if (!flag2 && text == "notch open" && _eyesResting != null && _eyesResting.Visibility == Visibility.Visible)
		{
			_eyesResting.Stop();
		}
		else if (!flag2)
		{
			EyesHide(_eyesResting);
		}
		if (text != null)
		{
			EyesState = "paused:" + text;
		}
		else if (flag)
		{
			EyesState = (flag3 ? EyesMode(_eyesMini, "mini") : "nospace");
		}
		else
		{
			EyesState = (flag4 ? EyesMode(_eyesResting, "resting") : "nospace");
		}
	}

	private static string EyesMode(EyesView v, string where)
	{
		return ((v != null && v.IsAnimated) ? "running:" : "static:") + where;
	}

	private double EyesGazeWidth()
	{
		if (_capsuleCard == null || !(_capsuleCard.ActualWidth > 20.0))
		{
			return 240.0;
		}
		return _capsuleCard.ActualWidth;
	}

	private bool EyesShowMini()
	{
		Grid grid = (_collapsedIsMini ? _miniSlot : _lineSlot);
		if (grid == null)
		{
			return false;
		}
		if (!_collapsedLook.Eyes || _collapsedLook.EyesHeight <= 0.0)
		{
			UpdateCollapsedLookOnly();
		}
		if (_collapsedLook.EyesHeight <= 0.0)
		{
			return false;
		}
		if (_eyesMini == null)
		{
			_eyesMini = new EyesView
			{
				HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
		}
		if (_eyesMini.Parent != grid)
		{
			if (_eyesMini.Parent is System.Windows.Controls.Panel panel)
			{
				panel.Children.Remove(_eyesMini);
			}
			grid.Children.Add(_eyesMini);
		}
		double dpiScaleX = VisualTreeHelper.GetDpi(this).DpiScaleX;
		_eyesMini.CardWidthDip = Math.Max(120.0, _collapsedLook.W * 2.0);
		_eyesMini.HoverHalfWidthDip = _collapsedLook.W / 2.0;
		_eyesMini.Configure(EyesPreferences.Instance.Skin, _collapsedLook.EyesHeight, dpiScaleX);
		EyesReveal(_eyesMini);
		if (_eyesMini.IsAnimated)
		{
			_eyesMini.Start();
		}
		else
		{
			_eyesMini.Stop();
		}
		return true;
	}

	internal static double EyesRestingHeight(double cw, double ch, double dpi, double previous)
	{
		if (cw < 20.0 || ch < 10.0)
		{
			return double.NaN;
		}
		if (dpi <= 0.01)
		{
			dpi = 1.0;
		}
		double val = Math.Floor(cw * dpi + 1E-06) * 44.0 / (120.0 * dpi) - 1E-06;
		double num = Math.Min(Math.Min(ch * 0.96, 44.0 * cw * 0.8 / 93.60000000000001), val);
		double num2 = Math.Floor(num);
		if (num2 / 44.0 < 0.3)
		{
			return double.NaN;
		}
		if (previous > 0.0 && Math.Abs(num2 - previous) < 1.0 && previous <= num)
		{
			num2 = previous;
		}
		return num2;
	}

	private bool EyesShowResting()
	{
		if (_collapsedContent == null)
		{
			return false;
		}
		double actualWidth = _collapsedContent.ActualWidth;
		double actualHeight = _collapsedContent.ActualHeight;
		if (actualWidth < 20.0 || actualHeight < 10.0)
		{
			return false;
		}
		double dpiScaleX = VisualTreeHelper.GetDpi(this).DpiScaleX;
		double num = EyesRestingHeight(actualWidth, actualHeight, dpiScaleX, (_eyesResting != null && _eyesResting.IsBuilt) ? _eyesResting.HeightDip : 0.0);
		if (double.IsNaN(num))
		{
			EyesHide(_eyesResting);
			return false;
		}
		if (_eyesResting == null)
		{
			_eyesResting = new EyesView
			{
				HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				Visibility = Visibility.Collapsed
			};
			_collapsedContent.Children.Add(_eyesResting);
		}
		_eyesResting.CardWidthDip = EyesGazeWidth();
		_eyesResting.Configure(EyesPreferences.Instance.Skin, num, dpiScaleX);
		EyesReveal(_eyesResting);
		if (_eyesResting.IsAnimated)
		{
			_eyesResting.Start();
		}
		else
		{
			_eyesResting.Stop();
		}
		return true;
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmExtendFrameIntoClientArea(IntPtr h, ref MARGINS m);

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);

	[DllImport("gdi32.dll")]
	private static extern IntPtr CreateRectRgn(int l, int t, int r, int b);

	[DllImport("gdi32.dll")]
	private static extern bool DeleteObject(IntPtr o);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern int SetWindowRgn(IntPtr h, IntPtr rgn, bool redraw);

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(WindowVisitor visitor, IntPtr state);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr hwnd, int command);

	[DllImport("kernel32.dll")]
	private static extern bool GetSystemPowerStatus(out PowerState state);

	private static bool CompositionSupported()
	{
		try
		{
			using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize"))
			{
				if (registryKey != null && Convert.ToInt32(registryKey.GetValue("EnableTransparency", 1)) == 0)
				{
					return false;
				}
			}
			PowerState state;
			bool flag = GetSystemPowerStatus(out state) && state.Saver == 1;
			return !RenderProfile.LightMode && !SystemParameters.IsRemoteSession && Environment.OSVersion.Version.Build < 26300 && !flag;
		}
		catch
		{
			return false;
		}
	}

	private void InitializeComposition(IntPtr handle, HwndSource source)
	{
		StartSurfaceWatchdog();
		base.LayoutUpdated += delegate
		{
			UpdateHitRegion();
		};
		if (!DwmComposed)
		{
			source.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
			return;
		}
		source.CompositionTarget.RenderMode = RenderMode.Default;
		source.CompositionTarget.BackgroundColor = Colors.Transparent;
		RefreshCompositionFrame(handle, source);
		int value = 1;
		DwmSetWindowAttribute(handle, 33, ref value, 4);
		int value2 = -2;
		DwmSetWindowAttribute(handle, 34, ref value2, 4);
		UpdateHitRegion();
	}

	private void RefreshCompositionFrame(IntPtr handle, HwndSource source)
	{
		if (DwmComposed && source != null && source.CompositionTarget != null)
		{
			MARGINS mARGINS = default(MARGINS);
			mARGINS.L = -1;
			mARGINS.R = -1;
			mARGINS.T = -1;
			mARGINS.B = -1;
			MARGINS m = mARGINS;
			int num = DwmExtendFrameIntoClientArea(handle, ref m);
			_compositionFrameFailed = num != 0;
			source.CompositionTarget.BackgroundColor = ((!_compositionFrameFailed && CompositionSupported()) ? Colors.Transparent : Colors.Black);
			if (num != 0)
			{
				TransactionLogger.Warn("Notch composition: DwmExtendFrameIntoClientArea failed (0x" + num.ToString("X") + ").");
			}
			UpdateHitRegion(verifyNativeRegion: true);
		}
	}

	private void StartSurfaceWatchdog()
	{
		base.IsVisibleChanged += delegate
		{
			UpdateHitRegion();
		};
		_surfaceWatchdog = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(500.0)
		};
		_surfaceWatchdog.Tick += delegate
		{
			if ((!EntitlementGate.IsEntitled || (_machine != null && _machine.Phase == NotchPhase.Hidden)) && base.IsVisible)
			{
				Hide();
			}
			if (_machine != null && _machine.IsResting && MorphProgress > 0.001)
			{
				BeginAnimation(MorphProgressProperty, null);
				SetCurrentValue(MorphProgressProperty, 0.0);
				UpdateMorphState(0.0);
			}
			UpdateHitRegion(verifyNativeRegion: true);
			if (DwmComposed)
			{
				HwndSource hwndSource = HwndSource.FromHwnd(_hwnd);
				if (hwndSource != null && hwndSource.CompositionTarget != null)
				{
					hwndSource.CompositionTarget.BackgroundColor = ((!_compositionFrameFailed && CompositionSupported()) ? Colors.Transparent : Colors.Black);
				}
			}
			if (_edgeLight != null)
			{
				_edgeLight.SyncWindow();
			}
			if (_beatOverlay != null)
			{
				_beatOverlay.SyncWindow();
			}
			AuditTopLevelSurfaces();
		};
		_surfaceWatchdog.Start();
		base.Closed += delegate
		{
			_surfaceWatchdog.Stop();
		};
	}

	private void AuditTopLevelSurfaces()
	{
		uint ownPid = (uint)Process.GetCurrentProcess().Id;
		EnumWindows(delegate(IntPtr h, IntPtr state)
		{
			try
			{
				GetWindowThreadProcessId(h, out var pid);
				if (pid != ownPid || h == _hwnd)
				{
					return true;
				}
				HwndSource hwndSource = HwndSource.FromHwnd(h);
				UIElement root = ((hwndSource == null) ? null : (hwndSource.RootVisual as UIElement));
				if (root == null)
				{
					return true;
				}
				if (!root.IsVisible)
				{
					ShowWindow(h, 0);
					if (_hiddenSurfaceRegions.Add(h))
					{
						IntPtr intPtr = CreateRectRgn(0, 0, 0, 0);
						if (SetWindowRgn(h, intPtr, redraw: false) == 0)
						{
							DeleteObject(intPtr);
						}
						DependencyPropertyChangedEventHandler restore = null;
						restore = delegate
						{
							if (root.IsVisible)
							{
								_hiddenSurfaceRegions.Remove(h);
								SetWindowRgn(h, IntPtr.Zero, redraw: true);
								root.IsVisibleChanged -= restore;
							}
						};
						root.IsVisibleChanged += restore;
					}
				}
				else if (_hiddenSurfaceRegions.Remove(h))
				{
					SetWindowRgn(h, IntPtr.Zero, redraw: true);
				}
			}
			catch
			{
			}
			return true;
		}, IntPtr.Zero);
	}

	internal void UpdateHitRegion(bool verifyNativeRegion = false)
	{
		IntPtr handle = new WindowInteropHelper(this).Handle;
		if (handle == IntPtr.Zero)
		{
			return;
		}
		try
		{
			Int32Rect int32Rect = (base.IsVisible ? RestingHitRect() : new Int32Rect(0, 0, 0, 0));
			bool flag = MorphProgress > 0.001 || _state == CapsuleDisplayState.Expanded || _state == CapsuleDisplayState.Expanding;
			if (base.IsVisible && flag)
			{
				DpiScale dpi = VisualTreeHelper.GetDpi(this);
				int32Rect = new Int32Rect(0, 0, (int)Math.Ceiling(base.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(base.ActualHeight * dpi.DpiScaleY));
			}
			if (!int32Rect.Equals(_hitRegion) || (verifyNativeRegion && !NativeSurfaceRegion.Matches(handle, int32Rect)))
			{
				IntPtr intPtr = CreateRectRgn(int32Rect.X, int32Rect.Y, int32Rect.X + int32Rect.Width, int32Rect.Y + int32Rect.Height);
				if (SetWindowRgn(handle, intPtr, redraw: false) == 0)
				{
					DeleteObject(intPtr);
					_hitRegion = new Int32Rect(-1, -1, -1, -1);
				}
				else
				{
					_hitRegion = int32Rect;
				}
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Notch hit region: " + ex.GetType().Name);
		}
	}

	private Int32Rect RestingHitRect()
	{
		if (_capsuleCard == null || !_capsuleCard.IsVisible || _capsuleCard.ActualWidth < 1.0)
		{
			return new Int32Rect(0, 0, 0, 0);
		}
		Rect rect = new Rect(_capsuleCard.TranslatePoint(new System.Windows.Point(0.0, 0.0), this), _capsuleCard.TranslatePoint(new System.Windows.Point(_capsuleCard.ActualWidth, _capsuleCard.ActualHeight), this));
		if (_notchSilhouettePath != null && _notchSilhouettePath.IsVisible)
		{
			Rect descendantBounds = VisualTreeHelper.GetDescendantBounds(_notchSilhouettePath);
			if (!descendantBounds.IsEmpty)
			{
				rect.Union(new Rect(_notchSilhouettePath.TranslatePoint(descendantBounds.TopLeft, this), _notchSilhouettePath.TranslatePoint(descendantBounds.BottomRight, this)));
			}
		}
		DpiScale dpi = VisualTreeHelper.GetDpi(this);
		int val = (int)Math.Floor(rect.Left * dpi.DpiScaleX) - 1;
		int num = 0;
		int num2 = (int)Math.Ceiling(rect.Right * dpi.DpiScaleX) + 1;
		int num3 = (int)Math.Ceiling(rect.Bottom * dpi.DpiScaleY) + 1;
		return new Int32Rect(Math.Max(0, val), num, Math.Max(0, num2 - Math.Max(0, val)), Math.Max(0, num3 - num));
	}

	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int key);

	internal static bool HoldsPanel(string activeToolId, bool fileDragOver, bool dropLive)
	{
		if (fileDragOver || dropLive)
		{
			return true;
		}
		if (!string.Equals(activeToolId, "drop", StringComparison.OrdinalIgnoreCase))
		{
			return string.Equals(activeToolId, "shelf", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private bool PanelHeld()
	{
		if (_machine == null || !_machine.IsOpenOrOpening)
		{
			return false;
		}
		return HoldsPanel(ToolRouter.Instance.ActiveToolId, _fileDragOver || ClipboardDrag.Active, DropTool.IsLive);
	}

	private void InstallPanelHold()
	{
		_machine.HoldOpen = PanelHeld;
		_machine.PhaseChanged += delegate(NotchPhase from, NotchPhase to, string trigger)
		{
			UpdateDesktopClickPoll(to);
		};
	}

	private void SetFileDragOver(bool on)
	{
		if (_fileDragOver != on)
		{
			_fileDragOver = on;
			if (!on && _machine != null)
			{
				_machine.HoldReleased("file drag ended");
			}
		}
	}

	internal static bool RouteRestingDrop(string[] files)
	{
		if (files == null || files.Length == 0 || !DropTool.Available)
		{
			return false;
		}
		DropTool.SendFiles(files);
		return true;
	}

	private void UpdateDesktopClickPoll(NotchPhase phase)
	{
		if (phase == NotchPhase.Open)
		{
			if (_desktopClickPoll == null)
			{
				_desktopClickPoll = new DispatcherTimer(DispatcherPriority.Input)
				{
					Interval = TimeSpan.FromMilliseconds(30.0)
				};
				_desktopClickPoll.Tick += delegate
				{
					PollDesktopClick();
				};
			}
			_buttonDown = MouseButtonDown();
			_pressOnDesktop = false;
			_desktopClickPoll.Start();
		}
		else if (_desktopClickPoll != null)
		{
			_desktopClickPoll.Stop();
		}
	}

	private static bool MouseButtonDown()
	{
		if ((GetAsyncKeyState(1) & 0x8000) == 0)
		{
			return (GetAsyncKeyState(2) & 0x8000) != 0;
		}
		return true;
	}

	private void PollDesktopClick()
	{
		bool flag = MouseButtonDown();
		if (flag == _buttonDown)
		{
			return;
		}
		_buttonDown = flag;
		if (!GetCursorPos(out var lpPoint))
		{
			return;
		}
		if (flag)
		{
			_pressAt = lpPoint;
			_pressOnDesktop = PanelHeld() && !IsPointerInside() && IsDesktopUnder(WindowFromPoint(lpPoint));
		}
		else if (_pressOnDesktop)
		{
			_pressOnDesktop = false;
			if (Math.Abs(lpPoint.X - _pressAt.X) <= 4 && Math.Abs(lpPoint.Y - _pressAt.Y) <= 4 && !_fileDragOver && PanelHeld() && _machine != null)
			{
				_machine.RequestClose("clicked the desktop");
			}
		}
	}

	internal static bool IsDesktopClass(string name)
	{
		if (!(name == "Progman"))
		{
			return name == "WorkerW";
		}
		return true;
	}

	private static bool IsDesktopUnder(IntPtr hwnd)
	{
		if (hwnd == IntPtr.Zero)
		{
			return false;
		}
		IntPtr ancestor = GetAncestor(hwnd, 2u);
		StringBuilder stringBuilder = new StringBuilder(64);
		if (GetClassName((ancestor == IntPtr.Zero) ? hwnd : ancestor, stringBuilder, stringBuilder.Capacity) > 0)
		{
			return IsDesktopClass(stringBuilder.ToString());
		}
		return false;
	}

	[DllImport("user32.dll")]
	private static extern uint SendInput(uint count, PetQaInput[] inputs, int size);

	[DllImport("user32.dll")]
	private static extern int GetSystemMetrics(int index);

	internal void StartPetContactSheet(string destination)
	{
		string output = System.IO.Path.GetFullPath(destination);
		Directory.CreateDirectory(output);
		DispatcherTimer timer = new DispatcherTimer(DispatcherPriority.Background)
		{
			Interval = TimeSpan.FromSeconds(3.0)
		};
		timer.Tick += delegate
		{
			timer.Stop();
			try
			{
				double num = GetDpiForWindow(_hwnd);
				if (num <= 0.0)
				{
					num = 96.0;
				}
				string[][] array = new string[4][]
				{
					new string[3] { "Compact", "0.9", "contact-90.png" },
					new string[3] { "Compact", "1.2", "contact-120.png" },
					new string[3] { "Line", "0.9", "contact-line-90.png" },
					new string[3] { "Island", "0.9", "contact-island-90.png" }
				};
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.AppendLine(string.Concat("{\"binary\":\"", typeof(CapsuleWindow).Assembly.Location.Replace("\\", "\\\\"), "\",\"version\":\"", typeof(CapsuleWindow).Assembly.GetName().Version, "\",\"dpi\":", num, ",\"sheets\":["));
				for (int i = 0; i < array.Length; i++)
				{
					string value = GardenView.WriteContactSheet(System.IO.Path.Combine(output, array[i][2]), array[i][0], double.Parse(array[i][1], CultureInfo.InvariantCulture), num);
					stringBuilder.Append(value).AppendLine((i < array.Length - 1) ? "," : "");
				}
				stringBuilder.AppendLine("]}");
				File.WriteAllText(System.IO.Path.Combine(output, "contact.json"), stringBuilder.ToString());
			}
			catch (Exception ex)
			{
				File.WriteAllText(System.IO.Path.Combine(output, "contact-error.txt"), ex.ToString());
			}
			RequestQuit("Pet contact sheet completed");
		};
		timer.Start();
	}

	internal void StartPetAcceptanceQa(string destination, int normalSeconds)
	{
		string output = System.IO.Path.GetFullPath(destination);
		Directory.CreateDirectory(output);
		_ = PetPreferences.Instance.Enabled;
		string wasOpen = ToolRegistry.Instance.OpenOn;
		Dictionary<string, object> report = new Dictionary<string, object>();
		report["binary"] = typeof(CapsuleWindow).Assembly.Location;
		report["version"] = typeof(CapsuleWindow).Assembly.GetName().Version.ToString();
		report["licensed"] = EntitlementGate.IsEntitled;
		report["dpi"] = GetDpiForWindow(_hwnd);
		DispatcherTimer timer = new DispatcherTimer(DispatcherPriority.Background)
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		Stopwatch clock = Stopwatch.StartNew();
		Process process = Process.GetCurrentProcess();
		StreamWriter metrics = new StreamWriter(System.IO.Path.Combine(output, "metrics.csv"))
		{
			AutoFlush = true
		};
		metrics.WriteLine("seconds,phase,cpuPercentOneCore,privateBytes,workingSet,managedBytes,scene,frames,sceneClockMs,quiet,cachedSceneFrames,dynamicRasterFrames,lifeFrames,bodyBuilds");
		int phase = 0;
		int count = 0;
		double previousTime = 0.0;
		double previousCpu = 0.0;
		long beforeClick = 0L;
		long previousSceneFrames = 0L;
		PetPreferences.Instance.SetQaEnabled(false);
		DispatcherTimer movement = new DispatcherTimer(DispatcherPriority.Background)
		{
			Interval = TimeSpan.FromMilliseconds(100.0)
		};
		double movementStart = 0.0;
		double movementSeconds = 0.0;
		int movementSamples = 0;
		movement.Tick += delegate
		{
			double num5 = clock.Elapsed.TotalSeconds - movementStart;
			if (num5 >= 120.0)
			{
				movementSeconds = num5;
				movement.Stop();
			}
			else
			{
				PetQaMove(new System.Windows.Point((double)GetSystemMetrics(0) * (0.5 + 0.4 * Math.Sin(num5 * 1.7)), (double)GetSystemMetrics(1) * (0.5 + 0.35 * Math.Cos(num5 * 1.1))));
				movementSamples++;
			}
		};
		PillActivityArbiter arbiter = PillActivityArbiter.Instance;
		bool heldEvents = false;
		bool finished = false;
		DispatcherTimer deadline = new DispatcherTimer(DispatcherPriority.Background)
		{
			Interval = TimeSpan.FromSeconds(normalSeconds + 420)
		};
		Action finish = delegate
		{
			if (!finished)
			{
				finished = true;
				deadline.Stop();
				timer.Stop();
				movement.Stop();
				metrics.Dispose();
				if (heldEvents)
				{
					arbiter.SetSuppressed(suppressed: false);
					heldEvents = false;
				}
				report["ordinaryEventsHeld"] = true;
				report["cursorMovingSeconds"] = movementSeconds;
				report["cursorMovementSamples"] = movementSamples;
				PetPreferences.Instance.SetQaEnabled(null);
				report["openOnUnchanged"] = ToolRegistry.Instance.OpenOn == wasOpen;
				File.WriteAllText(System.IO.Path.Combine(output, "installed-result.json"), new JavaScriptSerializer().Serialize(report));
				try
				{
					File.WriteAllLines(System.IO.Path.Combine(output, "engine-evidence.txt"), from line in File.ReadAllLines(System.IO.Path.Combine(AppPaths.DataDir, "logs", "engine.log"))
						where line.Contains("Pet") || line.Contains("Startup:") || line.Contains("Boot: first")
						select line);
				}
				catch
				{
				}
				RequestQuit("Installed pet acceptance QA completed");
			}
		};
		deadline.Tick += delegate
		{
			if (!finished)
			{
				report["error"] = "deadline: the run did not finish in time";
				finish();
			}
		};
		deadline.Start();
		timer.Tick += delegate
		{
			try
			{
				process.Refresh();
				double totalSeconds = clock.Elapsed.TotalSeconds;
				double totalSeconds2 = process.TotalProcessorTime.TotalSeconds;
				if (phase == 0)
				{
					if (!(totalSeconds < 30.0))
					{
						RequestExpand("Warm ordinary panel before pet baseline");
						phase = 8;
						count = 0;
					}
				}
				else if (phase == 1 || phase == 6)
				{
					string text = ((phase == 1) ? "Off" : "Normal");
					metrics.WriteLine(string.Join(",", totalSeconds.ToString("F3", CultureInfo.InvariantCulture), text, (100.0 * (totalSeconds2 - previousCpu) / (totalSeconds - previousTime)).ToString("F4", CultureInfo.InvariantCulture), process.PrivateMemorySize64.ToString(), process.WorkingSet64.ToString(), GC.GetTotalMemory(forceFullCollection: false).ToString(), (_petOutside == null) ? "off" : ((_petOutside.SceneName == "idle" && _petOutside.SceneFrames != previousSceneFrames) ? "transition" : _petOutside.SceneName), (_petOutside == null) ? "0" : _petOutside.SceneFrames.ToString(), (_petOutside == null) ? "0" : _petOutside.SceneClockMilliseconds.ToString("F3", CultureInfo.InvariantCulture), PetEngine.Instance.Quiet.ToString(), GardenView.CachedSceneFrames.ToString(), GardenView.DynamicRasterFrames.ToString(), (_petOutside == null) ? "0" : _petOutside.LifeFrames.ToString(), (_petOutside == null) ? "0" : _petOutside.BodyBuilds.ToString()));
					previousTime = totalSeconds;
					previousCpu = totalSeconds2;
					previousSceneFrames = ((_petOutside == null) ? 0 : _petOutside.SceneFrames);
					count++;
					if (phase == 6 && count == 1)
					{
						movementStart = clock.Elapsed.TotalSeconds;
						movement.Start();
					}
					if (phase == 1 && count >= 60)
					{
						PetPreferences.Instance.SetQaEnabled(true);
						phase = 2;
						count = 0;
					}
					else if (phase == 6 && count >= normalSeconds)
					{
						report["completed"] = true;
						finish();
					}
				}
				else
				{
					count++;
					if (phase == 8 && count >= 4)
					{
						PetQaMove(PointToScreen(new System.Windows.Point(20.0, 300.0)));
						RequestCollapse("Warm ordinary panel complete");
						phase = 9;
						count = 0;
					}
					else if (phase == 9 && count >= 10)
					{
						if (!arbiter.IsSuppressed)
						{
							arbiter.SetSuppressed(suppressed: true);
							heldEvents = true;
						}
						report["coldOffPrivateBytes"] = process.PrivateMemorySize64;
						report["offBaselineBeforePet"] = true;
						phase = 1;
						count = 0;
						previousTime = totalSeconds;
						previousCpu = totalSeconds2;
					}
					else if (phase == 2 && count >= 10)
					{
						report["stageExists"] = _petOutside != null;
						report["stageVisible"] = _petOutside != null && _petOutside.IsVisible;
						report["presence"] = NotchVisibilityEngine.Instance.CurrentPresence.ToString();
						report["fullscreen"] = NotchVisibilityEngine.Instance.LastInputs.FullscreenOnMonitor;
						report["sessionLocked"] = NotchVisibilityEngine.Instance.SessionLocked;
						report["batterySaver"] = PowerSaver.IsBatterySaverOn;
						report["focusTimer"] = TimerService.Instance.PomoIsRunning;
						report["quiet"] = PetEngine.Instance.Quiet;
						if (_petOutside == null || !_petOutside.IsVisible || PetEngine.Instance.Quiet)
						{
							throw new Exception("Real stage unavailable or silence policy active");
						}
						if (_petOutside.RobotX < 25.0 || _petOutside.RobotX > 255.0 || _petOutside.SceneName != "idle")
						{
							if (count >= 45)
							{
								throw new Exception("No on-stage resting pose available for an honest screen capture");
							}
						}
						else
						{
							report["capturedScene"] = _petOutside.SceneName;
							report["capturedRobotX"] = _petOutside.RobotX;
							System.Windows.Point point = _petOutside.PointToScreen(default(System.Windows.Point));
							System.Windows.Point point2 = _petOutside.PointToScreen(new System.Windows.Point(_petOutside.ActualWidth, _petOutside.ActualHeight));
							int num = (int)Math.Round(point2.X - point.X);
							int num2 = (int)Math.Round(point2.Y - point.Y);
							int num3 = 0;
							System.Windows.Point point3 = _petOutside.PointToScreen(new System.Windows.Point(_petOutside.RobotX * _petOutside.ActualWidth / 280.0, 20.0 * _petOutside.ActualHeight / 40.0));
							using (Bitmap bitmap = new Bitmap(num, num2))
							{
								using (Graphics graphics = Graphics.FromImage(bitmap))
								{
									graphics.CopyFromScreen((int)point.X, (int)point.Y, 0, 0, new System.Drawing.Size(num, num2));
								}
								int num4 = (int)(point3.X - point.X);
								for (int i = 3; i < num2; i++)
								{
									for (int j = Math.Max(0, num4 - 20); j < Math.Min(num, num4 + 20); j++)
									{
										System.Drawing.Color pixel = bitmap.GetPixel(j, i);
										if (pixel.R > 190 && pixel.G > 185 && pixel.B > 175 && Math.Abs(pixel.R - pixel.G) < 18)
										{
											num3++;
										}
									}
								}
								bitmap.Save(System.IO.Path.Combine(output, "installed-stage-150.png"), ImageFormat.Png);
							}
							report["robotPixels"] = num3;
							report["stageWidthPixels"] = num;
							report["stageHeightPixels"] = num2;
							if (num3 < 60)
							{
								throw new Exception("Robot pixels absent from the physical installed-app screen capture");
							}
							if (wasOpen == "hover")
							{
								PetQaMove(_petOutside.PointToScreen(new System.Windows.Point(_petOutside.ActualWidth * 0.8, _petOutside.ActualHeight * 0.5)));
								phase = 4;
								count = 0;
							}
							else
							{
								PetQaMove(point3);
								beforeClick = _petOutside.ClickCount;
								phase = 3;
								count = 0;
							}
						}
					}
					else if (phase == 3 && count == 1)
					{
						SendInput(2u, new PetQaInput[2]
						{
							new PetQaInput
							{
								Mouse = new PetQaMouse
								{
									Flags = 2u
								}
							},
							new PetQaInput
							{
								Mouse = new PetQaMouse
								{
									Flags = 4u
								}
							}
						}, Marshal.SizeOf(typeof(PetQaInput)));
					}
					else if (phase == 3 && count >= 2)
					{
						report["robotClick"] = _petOutside.ClickCount > beforeClick;
						if (_petOutside.ClickCount <= beforeClick)
						{
							throw new Exception("Native robot click did not reach the notch");
						}
						PetQaMove(_petOutside.PointToScreen(new System.Windows.Point(_petOutside.ActualWidth * 0.8, _petOutside.ActualHeight * 0.5)));
						phase = 4;
						count = 0;
					}
					else if (phase == 4 && count >= 4)
					{
						report["stageHoverOpened"] = !_machine.IsResting;
						report["noPetOverPanel"] = !_petOutside.IsVisible;
						if (wasOpen == "hover" && (_machine.IsResting || _petOutside.IsVisible))
						{
							throw new Exception("Native stage hover did not hand off to the panel");
						}
						PetQaMove(PointToScreen(new System.Windows.Point(20.0, 300.0)));
						RequestCollapse("Installed input QA complete");
						phase = 5;
						count = 0;
					}
					else
					{
						if (phase == 5 && count >= 10)
						{
							phase = 7;
							count = 0;
						}
						if (phase == 7 && count >= 10)
						{
							phase = 6;
							count = 0;
							previousTime = totalSeconds;
							previousCpu = totalSeconds2;
						}
					}
				}
			}
			catch (Exception ex)
			{
				report["error"] = ex.ToString();
				report["completed"] = false;
				finish();
			}
		};
		timer.Start();
	}

	private void PetQaMove(System.Windows.Point point)
	{
		int systemMetrics = GetSystemMetrics(0);
		int systemMetrics2 = GetSystemMetrics(1);
		SendInput(1u, new PetQaInput[1]
		{
			new PetQaInput
			{
				Mouse = new PetQaMouse
				{
					X = (int)Math.Round(point.X * 65535.0 / (double)(systemMetrics - 1)),
					Y = (int)Math.Round(point.Y * 65535.0 / (double)(systemMetrics2 - 1)),
					Flags = 32769u
				}
			}
		}, Marshal.SizeOf(typeof(PetQaInput)));
	}
}
