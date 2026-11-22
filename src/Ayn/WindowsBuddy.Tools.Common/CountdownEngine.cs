using System;
using System.Windows.Threading;

namespace WindowsBuddy.Tools.Common;

public class CountdownEngine
{
	private DispatcherTimer _timer;

	private int _totalSeconds;

	private int _remainingSeconds;

	private bool _isRunning;

	private bool _isPaused;

	public int TotalSeconds
	{
		get
		{
			return _totalSeconds;
		}
		set
		{
			_totalSeconds = ((value > 0) ? value : 60);
			if (!_isRunning && !_isPaused)
			{
				_remainingSeconds = _totalSeconds;
			}
		}
	}

	public int RemainingSeconds
	{
		get
		{
			return _remainingSeconds;
		}
		set
		{
			_remainingSeconds = Math.Max(0, value);
		}
	}

	public bool IsRunning
	{
		get
		{
			return _isRunning;
		}
		set
		{
			_isRunning = value;
		}
	}

	public bool IsPaused
	{
		get
		{
			return _isPaused;
		}
		set
		{
			_isPaused = value;
		}
	}

	public double ProgressFraction
	{
		get
		{
			if (_totalSeconds <= 0)
			{
				return 0.0;
			}
			return 1.0 - (double)_remainingSeconds / (double)_totalSeconds;
		}
	}

	public string FormattedTime
	{
		get
		{
			int num = _remainingSeconds / 60;
			int num2 = _remainingSeconds % 60;
			return $"{num:D2}:{num2:D2}";
		}
	}

	public event EventHandler Tick;

	public event EventHandler Completed;

	public CountdownEngine(int defaultTotalSeconds = 300)
	{
		_totalSeconds = defaultTotalSeconds;
		_remainingSeconds = defaultTotalSeconds;
		_timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		DispatcherTimer timer = _timer;
		EventHandler value = delegate
		{
			OnInternalTick();
		};
		timer.Tick += value;
	}

	public void Start()
	{
		_isRunning = true;
		_isPaused = false;
		if (_remainingSeconds <= 0)
		{
			_remainingSeconds = _totalSeconds;
		}
		if (_timer != null && !_timer.IsEnabled)
		{
			_timer.Start();
		}
	}

	public void Pause()
	{
		_isPaused = true;
		_isRunning = false;
		if (_timer != null && _timer.IsEnabled)
		{
			_timer.Stop();
		}
	}

	public void Resume()
	{
		if (_remainingSeconds > 0)
		{
			Start();
		}
	}

	public void Reset(int? newTotalSeconds = null)
	{
		_isRunning = false;
		_isPaused = false;
		if (_timer != null && _timer.IsEnabled)
		{
			_timer.Stop();
		}
		if (newTotalSeconds.HasValue && newTotalSeconds.Value > 0)
		{
			_totalSeconds = newTotalSeconds.Value;
		}
		_remainingSeconds = _totalSeconds;
	}

	public void Shutdown()
	{
		if (_timer != null)
		{
			_timer.Stop();
			_timer = null;
		}
		_isRunning = false;
		_isPaused = false;
	}

	public void RestoreState(int total, int remaining, bool running, bool paused, long savedAtUtcTicks)
	{
		_totalSeconds = ((total > 0) ? total : 300);
		_remainingSeconds = ((remaining >= 0) ? remaining : _totalSeconds);
		_isPaused = paused;
		_isRunning = false;
		if (running && savedAtUtcTicks > 0)
		{
			long num = (DateTime.UtcNow.Ticks - savedAtUtcTicks) / 10000000;
			if (num >= _remainingSeconds)
			{
				_remainingSeconds = 0;
				_isRunning = false;
				_isPaused = false;
			}
			else
			{
				_remainingSeconds -= (int)num;
				Start();
			}
		}
		else if (_isPaused)
		{
			_isRunning = false;
		}
	}

	private void OnInternalTick()
	{
		if (!_isRunning)
		{
			return;
		}
		if (_remainingSeconds > 0)
		{
			_remainingSeconds--;
			if (this.Tick != null)
			{
				try
				{
					this.Tick(this, EventArgs.Empty);
				}
				catch
				{
				}
			}
			if (_remainingSeconds != 0)
			{
				return;
			}
			_isRunning = false;
			_isPaused = false;
			if (_timer != null)
			{
				_timer.Stop();
			}
			if (this.Completed != null)
			{
				try
				{
					this.Completed(this, EventArgs.Empty);
				}
				catch
				{
				}
			}
		}
		else
		{
			_isRunning = false;
			_isPaused = false;
			if (_timer != null)
			{
				_timer.Stop();
			}
		}
	}
}
