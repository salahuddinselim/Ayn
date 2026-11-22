using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Threading;
using Windows.Foundation;
using Windows.Services.Store;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services;

public static class StoreUpdateService
{
	public sealed class Result
	{
		public bool Available;

		public string Version;

		public object Handle;
	}

	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1")]
	private interface IInitializeWithWindow
	{
		void Initialize(IntPtr hwnd);
	}

	public const string StorePage = "ms-windows-store://pdp/?ProductId=9PJ5B1HXNSKV";

	public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6.0);

	public static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(45.0);

	internal static Func<Task<Result>> Source = QueryStoreAsync;

	internal static Func<object, Task<bool>> Installer = InstallFromStoreAsync;

	internal static Action<string> OpenPage = AppLinks.Open;

	internal static Func<DateTime> Today = () => DateTime.Now.Date;

	internal static bool ForceEnabled;

	private static DispatcherTimer _timer;

	private static object _handle;

	private static bool _checking;

	private static bool _installing;

	public static StoreUpdateState State { get; private set; }

	public static string AvailableVersion { get; private set; }

	public static DateTime LastCheckUtc { get; private set; }

	public static bool UpdateAvailable => State == StoreUpdateState.Available;

	public static string StatusTitle => State switch
	{
		StoreUpdateState.Available => "Update available", 
		StoreUpdateState.UpToDate => "You're up to date", 
		StoreUpdateState.Checking => "Checking for updates…", 
		_ => "Updates come from Microsoft Store", 
	};

	public static string MessageTitle => "Update available · Ayn" + (string.IsNullOrWhiteSpace(AvailableVersion) ? "" : (" " + AvailableVersion));

	public static event EventHandler Changed;

	public static void Start()
	{
		if (_timer == null && (AppPaths.IsPackaged || ForceEnabled))
		{
			DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Background);
			dispatcherTimer.Interval = FirstCheckDelay;
			_timer = dispatcherTimer;
			_timer.Tick += async delegate
			{
				_timer.Interval = CheckInterval;
				await CheckNowAsync();
			};
			_timer.Start();
		}
	}

	public static void Stop()
	{
		if (_timer != null)
		{
			_timer.Stop();
			_timer = null;
		}
	}

	public static async Task CheckNowAsync()
	{
		if (_checking)
		{
			return;
		}
		_checking = true;
		StoreUpdateState before = State;
		if (State == StoreUpdateState.Unknown)
		{
			SetState(StoreUpdateState.Checking, null);
		}
		try
		{
			int num = default(int);
			_ = num;
			_ = 0;
			try
			{
				Result r = await Source();
				if (r == null)
				{
					throw new InvalidOperationException("Store update response unavailable.");
				}
				LastCheckUtc = DateTime.UtcNow;
				if (r.Available)
				{
					_handle = r.Handle;
					bool flag = State != StoreUpdateState.Available || AvailableVersion != r.Version;
					SetState(StoreUpdateState.Available, r.Version);
					if (flag)
					{
						TransactionLogger.Info("Store update available: Ayn " + r.Version + ".");
					}
					MaybeShowMessage();
				}
				else
				{
					_handle = null;
					SetState(StoreUpdateState.UpToDate, null);
				}
			}
			catch (Exception ex)
			{
				TransactionLogger.Info("Store update check skipped (" + ex.GetType().Name + ").");
				SetState((before == StoreUpdateState.Checking || before == StoreUpdateState.Unknown) ? StoreUpdateState.Unavailable : before, AvailableVersion);
			}
		}
		finally
		{
			_checking = false;
		}
	}

	private static void SetState(StoreUpdateState state, string version)
	{
		if (State == state && AvailableVersion == version)
		{
			return;
		}
		State = state;
		AvailableVersion = version;
		EventHandler changed = StoreUpdateService.Changed;
		if (changed == null)
		{
			return;
		}
		Delegate[] invocationList = changed.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			EventHandler eventHandler = (EventHandler)invocationList[i];
			try
			{
				eventHandler(null, EventArgs.Empty);
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Update status listener failed: " + ex.GetType().Name);
			}
		}
	}

	internal static bool MaybeShowMessage()
	{
		string text = Today().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		if (!UpdateAvailable || AppPreferences.Instance.UpdateNoticeDay == text)
		{
			return false;
		}
		if (!EntitlementGate.IsEntitled)
		{
			return false;
		}
		AppPreferences.Instance.UpdateNoticeDay = text;
		PillActivityArbiter arb = PillActivityArbiter.Instance;
		List<CapsuleWindow.MessageAction> list = new List<CapsuleWindow.MessageAction>();
		list.Add(new CapsuleWindow.MessageAction("Update", primary: true, delegate
		{
			arb.DismissCurrentTransient();
			UpdateNowAsync();
		}));
		list.Add(new CapsuleWindow.MessageAction("Later", primary: false, delegate
		{
			arb.DismissCurrentTransient();
		}));
		List<CapsuleWindow.MessageAction> payload = list;
		DynamicActivityService.Instance.PublishActivity(new NotchActivity
		{
			Id = "update_ready",
			Type = ActivityType.Custom,
			Title = MessageTitle,
			Subtitle = "Microsoft Store",
			Duration = TimeSpan.FromSeconds(12.0),
			Payload = payload
		});
		return true;
	}

	public static async Task UpdateNowAsync()
	{
		if (_installing)
		{
			return;
		}
		_installing = true;
		try
		{
			bool started = false;
			if (_handle != null)
			{
				try
				{
					started = await Installer(_handle);
				}
				catch (Exception ex)
				{
					TransactionLogger.Info("In-app Store update did not start (" + ex.GetType().Name + "); opening the Store page.");
				}
			}
			if (!started)
			{
				OpenPage("ms-windows-store://pdp/?ProductId=9PJ5B1HXNSKV");
			}
		}
		finally
		{
			_installing = false;
		}
	}

	private static StoreContext Context()
	{
		StoreContext @default = StoreContext.GetDefault();
		IntPtr intPtr = IntPtr.Zero;
		try
		{
			CapsuleWindow instance = CapsuleWindow.Instance;
			if (instance != null)
			{
				intPtr = new WindowInteropHelper(instance).Handle;
			}
		}
		catch
		{
		}
		if (intPtr != IntPtr.Zero)
		{
			((IInitializeWithWindow)(object)@default).Initialize(intPtr);
		}
		return @default;
	}

	private static Task<T> Await<T>(IAsyncOperation<T> op)
	{
		TaskCompletionSource<T> done = new TaskCompletionSource<T>();
		op.Completed = delegate(IAsyncOperation<T> r, AsyncStatus status)
		{
			try
			{
				if (status == AsyncStatus.Canceled)
				{
					done.TrySetCanceled();
				}
				else
				{
					done.TrySetResult(r.GetResults());
				}
			}
			catch (Exception exception)
			{
				done.TrySetException(exception);
			}
		};
		return done.Task;
	}

	private static Task<T> AwaitWithProgress<T, P>(IAsyncOperationWithProgress<T, P> op)
	{
		TaskCompletionSource<T> done = new TaskCompletionSource<T>();
		op.Completed = delegate(IAsyncOperationWithProgress<T, P> r, AsyncStatus status)
		{
			try
			{
				if (status == AsyncStatus.Canceled)
				{
					done.TrySetCanceled();
				}
				else
				{
					done.TrySetResult(r.GetResults());
				}
			}
			catch (Exception exception)
			{
				done.TrySetException(exception);
			}
		};
		return done.Task;
	}

	private static async Task<Result> QueryStoreAsync()
	{
		IReadOnlyList<StorePackageUpdate> updates = await Await(Context().GetAppAndOptionalStorePackageUpdatesAsync());
		if (updates == null)
		{
			throw new InvalidOperationException("Store update response unavailable.");
		}
		if (updates.Count == 0)
		{
			return new Result
			{
				Available = false
			};
		}
		return new Result
		{
			Available = true,
			Version = null,
			Handle = updates
		};
	}

	private static async Task<bool> InstallFromStoreAsync(object handle)
	{
		if (!(handle is IReadOnlyList<StorePackageUpdate> { Count: not 0 } updates))
		{
			return false;
		}
		StorePackageUpdateResult result = await AwaitWithProgress(Context().RequestDownloadAndInstallStorePackageUpdatesAsync(updates));
		TransactionLogger.Info(string.Concat("Store update request: ", result.OverallState, "."));
		return result.OverallState != StorePackageUpdateState.ErrorLowBattery && result.OverallState != StorePackageUpdateState.OtherError && result.OverallState != StorePackageUpdateState.ErrorWiFiRecommended && result.OverallState != StorePackageUpdateState.ErrorWiFiRequired;
	}
}
