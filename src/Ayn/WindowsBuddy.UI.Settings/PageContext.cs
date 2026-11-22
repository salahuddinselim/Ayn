using System;
using System.Collections.Generic;
using WindowsBuddy.Core;

namespace WindowsBuddy.UI.Settings;

internal sealed class PageContext
{
	public readonly SettingsWindow Window;

	private readonly List<Action> _leave = new List<Action>();

	private readonly List<Action> _return = new List<Action>();

	private readonly List<Action> _close = new List<Action>();

	private bool _hidden;

	private bool _disposed;

	internal bool CanUpdateUi
	{
		get
		{
			if (!_disposed)
			{
				return !_hidden;
			}
			return false;
		}
	}

	public PageContext(SettingsWindow window)
	{
		Window = window;
	}

	public void OnLeave(Action a)
	{
		if (a != null)
		{
			_leave.Add(a);
		}
	}

	public void OnReturn(Action a)
	{
		if (a != null)
		{
			_return.Add(a);
		}
	}

	public void OnClose(Action a)
	{
		if (a != null)
		{
			_close.Add(a);
		}
	}

	public void Navigate(string key)
	{
		Window.Navigate(key);
	}

	public void Reload()
	{
		Window.ReloadPage();
	}

	private static void Run(List<Action> list)
	{
		Action[] array = list.ToArray();
		foreach (Action action in array)
		{
			try
			{
				action();
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Settings page cleanup: " + ex.Message);
			}
		}
	}

	internal void Hide()
	{
		if (!_hidden)
		{
			_hidden = true;
			Run(_leave);
		}
	}

	internal void Show()
	{
		if (_hidden)
		{
			_hidden = false;
			Run(_return);
		}
	}

	internal void RunCleanup()
	{
		_disposed = true;
		if (!_hidden)
		{
			Run(_leave);
		}
		_hidden = true;
		Run(_close);
		_leave.Clear();
		_return.Clear();
		_close.Clear();
	}
}
