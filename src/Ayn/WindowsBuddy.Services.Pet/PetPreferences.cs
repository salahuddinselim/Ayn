using System;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;

namespace WindowsBuddy.Services.Pet;

public sealed class PetPreferences
{
	private static readonly Lazy<PetPreferences> instance = new Lazy<PetPreferences>(() => new PetPreferences());

	private readonly string path = Path.Combine(AppPaths.DataDir, "settings", "pet.json");

	private PetOptions value;

	internal bool? QaEnabled;

	private readonly OrderedSaveWorker writer;

	private string snapshot;

	private volatile bool saveRequested;

	public static PetPreferences Instance => instance.Value;

	public PetOptions LegacyOptions { get; private set; }

	public double Affection => Math.Max(0.0, Math.Min(1.0, LegacyOptions.Affection));

	public bool Enabled
	{
		get
		{
			return QaEnabled ?? value.Enabled;
		}
		set
		{
			bool hasValue = QaEnabled.HasValue;
			QaEnabled = null;
			if (hasValue || value != this.value.Enabled)
			{
				Set(delegate(PetOptions o)
				{
					o.Enabled = value;
				});
			}
		}
	}

	public bool IntroSeen => LegacyOptions.IntroV1;

	public string Birthday
	{
		get
		{
			string birthday = LegacyOptions.Birthday;
			if (birthday == null || birthday.Length != 5 || birthday[2] != '-')
			{
				return null;
			}
			return birthday;
		}
	}

	public bool Greetings
	{
		get
		{
			return value.Greetings;
		}
		set
		{
			if (value != Greetings)
			{
				Set(delegate(PetOptions o)
				{
					o.Greetings = value;
				});
			}
		}
	}

	public bool FollowCursor
	{
		get
		{
			return value.FollowCursor;
		}
		set
		{
			if (value != FollowCursor)
			{
				Set(delegate(PetOptions o)
				{
					o.FollowCursor = value;
				});
			}
		}
	}

	public bool Suggestions
	{
		get
		{
			return value.Suggestions;
		}
		set
		{
			if (value != Suggestions)
			{
				Set(delegate(PetOptions o)
				{
					o.Suggestions = value;
				});
			}
		}
	}

	public bool ShowName
	{
		get
		{
			return value.ShowName;
		}
		set
		{
			if (value != ShowName)
			{
				Set(delegate(PetOptions o)
				{
					o.ShowName = value;
				});
			}
		}
	}

	public bool Walks
	{
		get
		{
			return value.Walks;
		}
		set
		{
			if (value != Walks)
			{
				Set(delegate(PetOptions o)
				{
					o.Walks = value;
				});
			}
		}
	}

	public bool Talks
	{
		get
		{
			return value.Talks;
		}
		set
		{
			if (value != Talks)
			{
				Set(delegate(PetOptions o)
				{
					o.Talks = value;
				});
			}
		}
	}

	public string ActivityLevel
	{
		get
		{
			if (!(value.ActivityLevel == "Calm") && !(value.ActivityLevel == "Playful"))
			{
				return "Normal";
			}
			return value.ActivityLevel;
		}
		set
		{
			if (value != ActivityLevel)
			{
				Set(delegate(PetOptions o)
				{
					o.ActivityLevel = value;
				});
			}
		}
	}

	public bool DanceToMusic
	{
		get
		{
			return value.DanceToMusic;
		}
		set
		{
			if (value != DanceToMusic)
			{
				Set(delegate(PetOptions o)
				{
					o.DanceToMusic = value;
				});
			}
		}
	}

	public bool Hinglish
	{
		get
		{
			return value.Hinglish;
		}
		set
		{
			if (value != Hinglish)
			{
				Set(delegate(PetOptions o)
				{
					o.Hinglish = value;
				});
			}
		}
	}

	public bool CheckIns
	{
		get
		{
			return value.CheckIns;
		}
		set
		{
			if (value != CheckIns)
			{
				Set(delegate(PetOptions o)
				{
					o.CheckIns = value;
				});
			}
		}
	}

	public bool QuietHours
	{
		get
		{
			return value.QuietHours;
		}
		set
		{
			if (value != QuietHours)
			{
				Set(delegate(PetOptions o)
				{
					o.QuietHours = value;
				});
			}
		}
	}

	public int QuietStart
	{
		get
		{
			return value.QuietStart;
		}
		set
		{
			Set(delegate(PetOptions o)
			{
				o.QuietStart = Math.Max(0, Math.Min(23, value));
			});
		}
	}

	public int QuietEnd
	{
		get
		{
			return value.QuietEnd;
		}
		set
		{
			Set(delegate(PetOptions o)
			{
				o.QuietEnd = Math.Max(0, Math.Min(23, value));
			});
		}
	}

	public event EventHandler Changed;

	private PetPreferences()
	{
		LegacyOptions = Read<PetOptions>(path) ?? new PetOptions();
		value = new PetOptions
		{
			Enabled = LegacyOptions.Enabled
		};
		writer = new OrderedSaveWorker(path, () => Volatile.Read(ref snapshot));
		PersistenceHelper.RegisterExitFlush("pet-preferences", delegate
		{
			if (saveRequested)
			{
				writer.Flush(5000);
			}
		});
	}

	public void FlushSaves(int timeoutMs = 5000)
	{
		if (saveRequested)
		{
			writer.Flush(timeoutMs);
		}
	}

	private void WriteLater()
	{
		try
		{
			Volatile.Write(ref snapshot, new JavaScriptSerializer().Serialize(LegacyOptions));
			saveRequested = true;
			writer.Request();
		}
		catch (Exception error)
		{
			PersistenceHelper.ReportSaveFailure(path, error);
		}
	}

	internal static T Read<T>(string path) where T : class
	{
		try
		{
			return PersistenceHelper.ReadWithBackup(path, (string text) => new JavaScriptSerializer().Deserialize<T>(text));
		}
		catch
		{
			return null;
		}
	}

	public void SaveAffection(double amount, DateTime now, bool force = false)
	{
		if (!QaEnabled.HasValue && Enabled && (force || !(now - LegacyOptions.AffectionSaved < TimeSpan.FromMinutes(1.0))))
		{
			LegacyOptions.Affection = Math.Max(0.0, Math.Min(1.0, amount));
			LegacyOptions.AffectionSaved = now;
			WriteLater();
		}
	}

	private void Set(Action<PetOptions> change)
	{
		change(value);
		LegacyOptions.Enabled = value.Enabled;
		if (!QaEnabled.HasValue)
		{
			WriteLater();
		}
		this.Changed?.Invoke(this, EventArgs.Empty);
	}

	public void CompleteFirstMeet()
	{
		if (!QaEnabled.HasValue && !LegacyOptions.IntroV1)
		{
			LegacyOptions.IntroV1 = true;
			WriteLater();
		}
	}

	internal void SetQaEnabled(bool? enabled)
	{
		QaEnabled = enabled;
		this.Changed?.Invoke(this, EventArgs.Empty);
	}
}
