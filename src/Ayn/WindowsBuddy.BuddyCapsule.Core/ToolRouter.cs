using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.Tools;
using WindowsBuddy.Tools.Clipboard;
using WindowsBuddy.UI.Settings;

namespace WindowsBuddy.BuddyCapsule.Core;

public class ToolRouter
{
	private static ToolRouter _instance;

	private readonly Dictionary<string, UIElement> _viewCache = new Dictionary<string, UIElement>(StringComparer.OrdinalIgnoreCase);

	private IBuddyTool _activeTool;

	private UIElement _currentView;

	private long _activationGeneration;

	private long _queuedBuildGeneration = -1L;

	private bool _isNavigating;

	private string _pendingToolId;

	private bool _pendingForceRecreate;

	private bool _toolSuspended;

	private readonly List<string> _visited = new List<string>();

	private volatile string _visitedSnapshot = "";

	private OrderedSaveWorker _visitedWriter;

	private DispatcherTimer _warmupTimer;

	private DispatcherTimer _idleMemoryTimer;

	public static double LastRouteMs;

	public static double LastNavigatedMs;

	public static readonly Dictionary<string, double> ActivationMs = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

	public static ToolRouter Instance
	{
		get
		{
			if (_instance == null)
			{
				_instance = new ToolRouter();
			}
			return _instance;
		}
	}

	public bool IsCurrentViewBuilt
	{
		get
		{
			if (_currentView != null)
			{
				return !(_currentView is TabPlaceholder);
			}
			return false;
		}
	}

	public long ActivationGeneration => _activationGeneration;

	public NotchStateMachine Notch { get; set; }

	public IBuddyTool ActiveTool => _activeTool;

	public string ActiveToolId
	{
		get
		{
			if (_activeTool == null)
			{
				return string.Empty;
			}
			return _activeTool.Id;
		}
	}

	public UIElement CurrentView => _currentView;

	public double CurrentTargetWidth => 540.0;

	public double CurrentTargetHeight => 360.0;

	public bool IsToolSuspended => _toolSuspended;

	private static string VisitedFile => Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, "settings", "visited_tabs.txt");

	public event EventHandler<ToolNavigationEventArgs> Navigated;

	public event EventHandler<string> HeaderSubtitleUpdated;

	public bool EnsureActiveViewBuilt()
	{
		if (IsCurrentViewBuilt)
		{
			return true;
		}
		if (_activeTool != null && Notch != null && Notch.IsOpenOrOpening && _queuedBuildGeneration != _activationGeneration)
		{
			QueueBuild(_activeTool, _activationGeneration);
		}
		return false;
	}

	private void QueueBuild(IBuddyTool tool, long generation)
	{
		_queuedBuildGeneration = generation;
		AfterNextFrame(delegate
		{
			if (_queuedBuildGeneration == generation)
			{
				_queuedBuildGeneration = -1L;
			}
			BuildDeferred(tool, generation);
		});
	}

	public void InvalidateActivation()
	{
		_activationGeneration++;
	}

	private bool CanActivate(IBuddyTool tool, long generation)
	{
		if (generation == _activationGeneration && _activeTool == tool && Notch != null)
		{
			return Notch.Phase == NotchPhase.Open;
		}
		return false;
	}

	public void UpdateHeaderSubtitle(string subtitle)
	{
		this.HeaderSubtitleUpdated?.Invoke(this, subtitle);
	}

	public bool RouteTo(string toolId, bool forceRecreate = false)
	{
		if (string.IsNullOrEmpty(toolId))
		{
			return false;
		}
		if (_isNavigating)
		{
			_pendingToolId = toolId;
			_pendingForceRecreate = forceRecreate;
			return true;
		}
		_isNavigating = true;
		long timestamp = Stopwatch.GetTimestamp();
		try
		{
			return RouteToInternal(toolId, forceRecreate);
		}
		finally
		{
			LastRouteMs = (double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
			_isNavigating = false;
			if (!string.IsNullOrEmpty(_pendingToolId))
			{
				string nextId = _pendingToolId;
				bool nextForce = _pendingForceRecreate;
				_pendingToolId = null;
				_pendingForceRecreate = false;
				Dispatcher.CurrentDispatcher.BeginInvoke((Action)delegate
				{
					RouteTo(nextId, nextForce);
				});
			}
		}
	}

	public void SuspendActiveTool()
	{
		if (_toolSuspended || _activeTool == null)
		{
			return;
		}
		_toolSuspended = true;
		_activationGeneration++;
		try
		{
			_activeTool.OnDeactivated();
		}
		catch (Exception ex)
		{
			TransactionLogger.Error($"ToolRouter: Error deactivating '{_activeTool.Id}': {ex.Message}", ex);
		}
	}

	public bool ResumeSuspendedTool()
	{
		if (!_toolSuspended || _activeTool == null)
		{
			return false;
		}
		return RouteTo(_activeTool.Id, forceRecreate: true);
	}

	public string ChooseUsableTabId(string preferredId, string excludeId = null)
	{
		ToolRegistry reg = ToolRegistry.Instance;
		Func<string, bool> usable = (string id) => !string.IsNullOrEmpty(id) && !string.Equals(id, excludeId, StringComparison.OrdinalIgnoreCase) && (reg.GetTool(id)?.IsEnabled ?? false);
		if (usable(preferredId))
		{
			return preferredId;
		}
		string[] array = new string[0];
		try
		{
			string text = _visitedSnapshot;
			if (string.IsNullOrEmpty(text))
			{
				text = PersistenceHelper.SafeReadAllText(VisitedFile) ?? "";
			}
			array = text.Trim('\ufeff', ' ', '\r', '\n').Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
		}
		catch
		{
		}
		string[] array2 = array;
		foreach (string text2 in array2)
		{
			if (usable(text2.Trim()))
			{
				return text2.Trim();
			}
		}
		if (usable("home"))
		{
			return "home";
		}
		return reg.EnabledTools.FirstOrDefault((IBuddyTool t) => usable(t.Id))?.Id;
	}

	public bool EnsureUsableTab()
	{
		if (!EntitlementGate.IsEntitled)
		{
			return false;
		}
		if (_activeTool != null && _activeTool.IsEnabled && _currentView != null)
		{
			EnsureActiveViewBuilt();
			return true;
		}
		string text = ChooseUsableTabId((_activeTool != null) ? _activeTool.Id : null);
		if (text == null)
		{
			return false;
		}
		TransactionLogger.Info("ToolRouter: no usable tab selected; showing '" + text + "'.");
		return RouteTo(text, _activeTool != null && _activeTool.Id == text);
	}

	public void ResetNavigationState()
	{
		_isNavigating = false;
		_pendingToolId = null;
	}

	private bool RouteToInternal(string toolId, bool forceRecreate)
	{
		ToolRegistry instance = ToolRegistry.Instance;
		if (string.Equals(toolId, "scratchpad", StringComparison.OrdinalIgnoreCase))
		{
			toolId = ((instance.GetTool("message") != null) ? "message" : ((instance.GetTool("notes") != null) ? "notes" : "home"));
		}
		else if (string.Equals(toolId, "stats", StringComparison.OrdinalIgnoreCase))
		{
			toolId = ((instance.GetTool("system") != null) ? "system" : "home");
		}
		else if (string.Equals(toolId, "sounds", StringComparison.OrdinalIgnoreCase))
		{
			toolId = "home";
		}
		if ((string.Equals(toolId, "battery", StringComparison.OrdinalIgnoreCase) || string.Equals(toolId, "wifi", StringComparison.OrdinalIgnoreCase) || string.Equals(toolId, "volume", StringComparison.OrdinalIgnoreCase)) && instance.GetTool("system") != null)
		{
			toolId = "system";
		}
		if (string.Equals(toolId, "settings", StringComparison.OrdinalIgnoreCase) || string.Equals(toolId, "notch_customization", StringComparison.OrdinalIgnoreCase) || string.Equals(toolId, "notch_behavior", StringComparison.OrdinalIgnoreCase) || string.Equals(toolId, "notch", StringComparison.OrdinalIgnoreCase))
		{
			string page = (string.Equals(toolId, "settings", StringComparison.OrdinalIgnoreCase) ? null : "notch");
			CapsuleWindow instance2 = CapsuleWindow.Instance;
			if (instance2 != null)
			{
				instance2.OpenSettings(page);
			}
			else
			{
				SettingsLauncher.Open(page);
			}
			return false;
		}
		if (!EntitlementGate.IsEntitled)
		{
			SuspendActiveTool();
			return false;
		}
		_toolSuspended = false;
		IBuddyTool tool = instance.GetTool(toolId);
		if (tool != null && !tool.IsEnabled)
		{
			return false;
		}
		if (!forceRecreate && _activeTool != null && string.Equals(_activeTool.Id, toolId, StringComparison.OrdinalIgnoreCase) && _currentView != null)
		{
			EnsureActiveViewBuilt();
			return true;
		}
		if (Notch != null)
		{
			Notch.NotifyToolSwitching(150);
		}
		IBuddyTool buddyTool = instance.GetTool(toolId);
		if (buddyTool == null)
		{
			TransactionLogger.Warn($"ToolRouter: Tool '{toolId}' not found in registry. Falling back to default tool.");
			buddyTool = instance.GetTool("home") ?? instance.EnabledTools.FirstOrDefault() ?? instance.AllTools.FirstOrDefault();
			if (buddyTool == null)
			{
				TransactionLogger.Warn("ToolRouter: No valid tool found in registry to fall back to.");
				return false;
			}
			toolId = buddyTool.Id;
		}
		if (!buddyTool.IsEnabled)
		{
			return false;
		}
		if (_activeTool != null && _activeTool != buddyTool)
		{
			try
			{
				_activeTool.OnDeactivated();
			}
			catch (Exception ex)
			{
				TransactionLogger.Error($"ToolRouter: Error deactivating '{_activeTool.Id}': {ex.Message}", ex);
			}
		}
		long activationGeneration = ++_activationGeneration;
		_activeTool = buddyTool;
		instance.ActiveTool = buddyTool;
		UIElement value = null;
		bool flag = _viewCache.TryGetValue(buddyTool.Id, out value) && value != null;
		bool flag2 = !flag && !forceRecreate && Notch != null && (Notch.Phase == NotchPhase.Open || Notch.Phase == NotchPhase.Opening);
		if (!flag2)
		{
			try
			{
				if (!buddyTool.IsInitialized)
				{
					buddyTool.Initialize();
				}
			}
			catch (Exception ex2)
			{
				TransactionLogger.Error($"ToolRouter: Error activating '{buddyTool.Id}': {ex2.Message}", ex2);
			}
		}
		if (forceRecreate || !flag)
		{
			if (flag2)
			{
				value = new TabPlaceholder();
			}
			else
			{
				try
				{
					value = buddyTool.CreateView();
				}
				catch (Exception ex3)
				{
					TransactionLogger.Error($"ToolRouter: Error creating view for '{buddyTool.Id}': {ex3.Message}", ex3);
					value = CreateErrorFallback(buddyTool, ex3.Message);
				}
				if (value == null)
				{
					value = CreateEmptyFallback(buddyTool.DisplayName);
				}
				_viewCache[buddyTool.Id] = value;
			}
		}
		_currentView = value;
		if (Notch != null && Notch.Phase == NotchPhase.Open)
		{
			RecordVisited(buddyTool.Id);
		}
		double targetWidth = 680.0;
		double targetHeight = 328.0;
		if (this.Navigated != null)
		{
			try
			{
				long timestamp = Stopwatch.GetTimestamp();
				this.Navigated(this, new ToolNavigationEventArgs(buddyTool, value, targetWidth, targetHeight));
				LastNavigatedMs = (double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
			}
			catch (Exception ex4)
			{
				TransactionLogger.Error("ToolRouter: Error dispatching Navigated event: " + ex4.Message, ex4);
			}
		}
		if (flag2)
		{
			IBuddyTool tool2 = buddyTool;
			QueueBuild(tool2, activationGeneration);
			return true;
		}
		if (Notch == null || Notch.Phase == NotchPhase.Open)
		{
			IBuddyTool activated = buddyTool;
			AfterNextFrame(delegate
			{
				if (!CanActivate(activated, activationGeneration))
				{
					return;
				}
				try
				{
					Activate(activated);
				}
				catch (Exception ex5)
				{
					TransactionLogger.Error($"ToolRouter: Error activating '{activated.Id}': {ex5.Message}", ex5);
				}
			});
		}
		return true;
	}

	private void RecordVisited(string id)
	{
		try
		{
			if (_visited.Count > 0 && string.Equals(_visited[0], id, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			_visited.RemoveAll((string v) => string.Equals(v, id, StringComparison.OrdinalIgnoreCase));
			_visited.Insert(0, id);
			if (_visited.Count > 4)
			{
				_visited.RemoveRange(4, _visited.Count - 4);
			}
			_visitedSnapshot = string.Join(",", _visited);
			if (_visitedWriter == null)
			{
				_visitedWriter = new OrderedSaveWorker(VisitedFile, () => _visitedSnapshot);
				OrderedSaveWorker writer = _visitedWriter;
				PersistenceHelper.RegisterExitFlush("visited tabs", delegate
				{
					writer.Flush(2000);
				});
			}
			_visitedWriter.Request();
		}
		catch
		{
		}
	}

	private static IEnumerable<string> CanonicalOrder()
	{
		foreach (IBuddyTool t in ToolRegistry.Instance.EnabledTools)
		{
			yield return t.Id;
		}
	}

	public bool Prewarm(string toolId)
	{
		if (!EntitlementGate.IsEntitled)
		{
			return false;
		}
		IBuddyTool tool = ToolRegistry.Instance.GetTool(toolId);
		if (tool == null || !tool.IsEnabled || _viewCache.ContainsKey(tool.Id))
		{
			return false;
		}
		try
		{
			if (!tool.IsInitialized)
			{
				tool.Initialize();
			}
			UIElement uIElement = tool.CreateView();
			if (uIElement == null)
			{
				return false;
			}
			uIElement.Measure(new Size(728.0, 242.0));
			uIElement.Arrange(new Rect(0.0, 0.0, 728.0, 242.0));
			_viewCache[tool.Id] = uIElement;
			return true;
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("ToolRouter: warm-up of '" + toolId + "' failed: " + ex.GetType().Name);
			return false;
		}
	}

	public void StartIdleWarmup(Dispatcher ui, Func<bool> notchIsIdle)
	{
		List<string> ids = new List<string>();
		try
		{
			string text = PersistenceHelper.SafeReadAllText(VisitedFile);
			if (!string.IsNullOrEmpty(text))
			{
				string[] array = text.Split(',');
				foreach (string text2 in array)
				{
					string text3 = text2.Trim();
					if (text3.Length > 0 && !ids.Contains(text3) && ids.Count < 4)
					{
						ids.Add(text3);
						_visited.Add(text3);
					}
				}
			}
		}
		catch
		{
		}
		if (!ids.Contains("home"))
		{
			ids.Insert(0, "home");
		}
		foreach (IBuddyTool enabledTool in ToolRegistry.Instance.EnabledTools)
		{
			if (!ids.Contains(enabledTool.Id))
			{
				ids.Add(enabledTool.Id);
			}
		}
		int next = 0;
		StopIdleWarmup();
		DispatcherTimer timer = (_warmupTimer = new DispatcherTimer(DispatcherPriority.SystemIdle, ui)
		{
			Interval = TimeSpan.FromSeconds(6.0)
		});
		timer.Tick += delegate
		{
			timer.Interval = TimeSpan.FromMilliseconds(700.0);
			if (notchIsIdle())
			{
				while (next < ids.Count)
				{
					string text4 = ids[next++];
					long timestamp = Stopwatch.GetTimestamp();
					if (Prewarm(text4))
					{
						long num = (Stopwatch.GetTimestamp() - timestamp) * 1000 / Stopwatch.Frequency;
						TransactionLogger.Info("Warm-up: tab '" + text4 + "' built in " + num + " ms");
						return;
					}
				}
				StopIdleWarmup();
			}
		};
		timer.Start();
	}

	public void StopIdleWarmup()
	{
		if (_warmupTimer != null)
		{
			_warmupTimer.Stop();
			_warmupTimer = null;
		}
	}

	[DllImport("psapi.dll")]
	private static extern bool EmptyWorkingSet(IntPtr process);

	internal static bool ShouldTrimIdleMemory(bool idle, long idleMs, long sinceTrimMs, long workingSet)
	{
		if (idle && idleMs >= 30000 && sinceTrimMs >= 300000)
		{
			return workingSet >= 134217728;
		}
		return false;
	}

	public void StartIdleMemoryMaintenance(Dispatcher ui, Func<bool> notchIsIdle)
	{
		StopIdleMemoryMaintenance();
		Stopwatch watch = Stopwatch.StartNew();
		long lastTrim = -300000L;
		_idleMemoryTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle, ui)
		{
			Interval = TimeSpan.FromSeconds(30.0)
		};
		_idleMemoryTimer.Tick += delegate
		{
			if (Notch == null || !notchIsIdle())
			{
				return;
			}
			try
			{
				using Process process = Process.GetCurrentProcess();
				if (ShouldTrimIdleMemory(idle: true, Notch.MsInPhase, watch.ElapsedMilliseconds - lastTrim, process.WorkingSet64))
				{
					lastTrim = watch.ElapsedMilliseconds;
					ClipboardService.ReleaseThumbnailCache();
					EmptyWorkingSet(process.Handle);
				}
			}
			catch
			{
			}
		};
		_idleMemoryTimer.Start();
	}

	public void StopIdleMemoryMaintenance()
	{
		if (_idleMemoryTimer != null)
		{
			_idleMemoryTimer.Stop();
			_idleMemoryTimer = null;
		}
	}

	public static void AfterNextFrame(Action work)
	{
		Dispatcher ui = Dispatcher.CurrentDispatcher;
		bool done = false;
		EventHandler h = null;
		DispatcherTimer guard = null;
		Action run = delegate
		{
			if (done)
			{
				return;
			}
			done = true;
			if (guard != null)
			{
				guard.Stop();
			}
			CompositionTarget.Rendering -= h;
			try
			{
				work();
			}
			catch (Exception ex)
			{
				TransactionLogger.Error("ToolRouter: deferred work failed: " + ex.Message, ex);
			}
		};
		h = delegate
		{
			CompositionTarget.Rendering -= h;
			ui.BeginInvoke(run, DispatcherPriority.Background);
		};
		CompositionTarget.Rendering += h;
		guard = new DispatcherTimer(DispatcherPriority.Loaded, ui)
		{
			Interval = TimeSpan.FromMilliseconds(250.0)
		};
		guard.Tick += delegate
		{
			guard.Stop();
			CompositionTarget.Rendering -= h;
			run();
		};
		guard.Start();
	}

	public static void Activate(IBuddyTool tool)
	{
		Instance.InvalidateActivation();
		if (tool == null || !EntitlementGate.IsEntitled)
		{
			return;
		}
		long timestamp = Stopwatch.GetTimestamp();
		try
		{
			tool.OnActivated();
		}
		finally
		{
			double num = (double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
			ActivationMs.TryGetValue(tool.Id, out var value);
			if (num > value)
			{
				ActivationMs[tool.Id] = num;
			}
			if (num >= 8.0)
			{
				try
				{
					TransactionLogger.Warn("Slow tab activation: " + tool.Id + " took " + num.ToString("0.0") + " ms");
				}
				catch
				{
				}
			}
		}
	}

	private void BuildDeferred(IBuddyTool tool, long generation)
	{
		if (!EntitlementGate.IsEntitled || generation != _activationGeneration || _activeTool != tool || Notch == null || (Notch.Phase != NotchPhase.Open && Notch.Phase != NotchPhase.Opening))
		{
			return;
		}
		UIElement value;
		try
		{
			if (!_viewCache.TryGetValue(tool.Id, out value) || value == null)
			{
				if (!tool.IsInitialized)
				{
					tool.Initialize();
				}
				value = tool.CreateView() ?? CreateEmptyFallback(tool.DisplayName);
				_viewCache[tool.Id] = value;
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Error($"ToolRouter: Error creating view for '{tool.Id}': {ex.Message}", ex);
			value = CreateErrorFallback(tool, ex.Message);
			_viewCache[tool.Id] = value;
		}
		if (generation != _activationGeneration || _activeTool != tool || Notch == null || (Notch.Phase != NotchPhase.Open && Notch.Phase != NotchPhase.Opening))
		{
			return;
		}
		_currentView = value;
		if (this.Navigated != null)
		{
			try
			{
				this.Navigated(this, new ToolNavigationEventArgs(tool, value, 680.0, 328.0)
				{
					IsSwap = true
				});
			}
			catch (Exception ex2)
			{
				TransactionLogger.Error("ToolRouter: Error dispatching Navigated event: " + ex2.Message, ex2);
			}
		}
		if (Notch != null && Notch.Phase != NotchPhase.Open)
		{
			return;
		}
		AfterNextFrame(delegate
		{
			if (!CanActivate(tool, generation))
			{
				return;
			}
			try
			{
				Activate(tool);
			}
			catch (Exception ex3)
			{
				TransactionLogger.Error($"ToolRouter: Error activating '{tool.Id}': {ex3.Message}", ex3);
			}
		});
	}

	public void ClearViewCache(string toolId = null)
	{
		if (string.IsNullOrEmpty(toolId))
		{
			_viewCache.Clear();
		}
		else
		{
			_viewCache.Remove(toolId);
		}
	}

	public static void DetachFromParent(UIElement element)
	{
		if (element == null)
		{
			return;
		}
		try
		{
			element.BeginAnimation(UIElement.OpacityProperty, null);
			DependencyObject parent = VisualTreeHelper.GetParent(element);
			int num = 0;
			while (parent != null && num++ < 10)
			{
				if (parent is Panel panel)
				{
					if (panel.Children.Contains(element))
					{
						panel.Children.Remove(element);
					}
					break;
				}
				if (parent is ContentControl contentControl && contentControl.Content == element)
				{
					contentControl.Content = null;
					break;
				}
				if (parent is ContentPresenter contentPresenter && contentPresenter.Content == element)
				{
					contentPresenter.Content = null;
					break;
				}
				if (parent is Decorator decorator && decorator.Child == element)
				{
					decorator.Child = null;
					break;
				}
				if (parent is Border border && border.Child == element)
				{
					border.Child = null;
					break;
				}
				if (parent is Viewbox viewbox && viewbox.Child == element)
				{
					viewbox.Child = null;
					break;
				}
				if (parent is Popup popup && popup.Child == element)
				{
					popup.Child = null;
					break;
				}
				parent = VisualTreeHelper.GetParent(parent);
			}
			DependencyObject parent2 = LogicalTreeHelper.GetParent(element);
			if (parent2 != null)
			{
				if (parent2 is Panel panel2 && panel2.Children.Contains(element))
				{
					panel2.Children.Remove(element);
				}
				else if (parent2 is ContentControl contentControl2 && contentControl2.Content == element)
				{
					contentControl2.Content = null;
				}
				else if (parent2 is ContentPresenter contentPresenter2 && contentPresenter2.Content == element)
				{
					contentPresenter2.Content = null;
				}
				else if (parent2 is Decorator decorator2 && decorator2.Child == element)
				{
					decorator2.Child = null;
				}
				else if (parent2 is Border border2 && border2.Child == element)
				{
					border2.Child = null;
				}
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("ToolRouter: DetachFromParent non-fatal error: " + ex.Message);
		}
	}

	private UIElement CreateEmptyFallback(string toolName)
	{
		Border border = new Border();
		border.Background = BuddyTokens.ToolSurfaceBrush;
		border.BorderBrush = BuddyTokens.BorderSubtleBrush;
		border.BorderThickness = new Thickness(1.0);
		border.CornerRadius = new CornerRadius(8.0);
		border.Padding = new Thickness(16.0);
		Border border2 = border;
		TextBlock textBlock = new TextBlock();
		textBlock.Text = $"No active view available for {toolName}.";
		textBlock.FontSize = 11.5;
		textBlock.FontFamily = BuddyTypography.FontText;
		textBlock.Foreground = BuddyTokens.TextMutedBrush;
		textBlock.HorizontalAlignment = HorizontalAlignment.Center;
		TextBlock child = textBlock;
		border2.Child = child;
		return border2;
	}

	private UIElement CreateErrorFallback(IBuddyTool tool, string errorMessage)
	{
		Border border = new Border();
		border.Background = Brushes.Transparent;
		border.BorderThickness = new Thickness(0.0);
		border.Padding = new Thickness(20.0);
		border.HorizontalAlignment = HorizontalAlignment.Center;
		border.VerticalAlignment = VerticalAlignment.Center;
		Border border2 = border;
		StackPanel stackPanel = new StackPanel();
		stackPanel.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Orientation = Orientation.Horizontal;
		stackPanel3.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel3.Margin = new Thickness(0.0, 0.0, 0.0, 4.0);
		StackPanel stackPanel4 = stackPanel3;
		stackPanel4.Children.Add(BuddyStatusDot.Create(BuddyTokens.DangerRedBrush));
		TextBlock textBlock = new TextBlock();
		textBlock.Text = $"Unable to load {tool.DisplayName}";
		textBlock.FontSize = 13.5;
		textBlock.FontFamily = BuddyTypography.FontText;
		textBlock.FontWeight = AynType.Medium;
		textBlock.Foreground = BuddyTokens.TextPrimaryBrush;
		textBlock.Margin = new Thickness(6.0, 0.0, 0.0, 0.0);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		TextBlock element = textBlock;
		stackPanel4.Children.Add(element);
		stackPanel2.Children.Add(stackPanel4);
		TextBlock textBlock2 = new TextBlock();
		textBlock2.Text = errorMessage ?? "An unexpected initialization fault occurred.";
		textBlock2.FontSize = 11.5;
		textBlock2.FontFamily = BuddyTypography.FontText;
		textBlock2.Foreground = BuddyTokens.TextMutedBrush;
		textBlock2.HorizontalAlignment = HorizontalAlignment.Center;
		textBlock2.TextAlignment = TextAlignment.Center;
		textBlock2.MaxWidth = 320.0;
		textBlock2.TextWrapping = TextWrapping.Wrap;
		textBlock2.Margin = new Thickness(0.0, 0.0, 0.0, 12.0);
		TextBlock element2 = textBlock2;
		stackPanel2.Children.Add(element2);
		Button button = BuddyButton.Create("Retry");
		button.Click += delegate
		{
			RouteTo(tool.Id, forceRecreate: true);
		};
		button.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel2.Children.Add(button);
		border2.Child = stackPanel2;
		return border2;
	}
}
