using System;
using System.IO;
using System.Web.Script.Serialization;

namespace WindowsBuddy.Services;

public sealed class EyesPreferences
{
	private static readonly Lazy<EyesPreferences> instance = new Lazy<EyesPreferences>(() => new EyesPreferences());

	private readonly string path = Path.Combine(AppPaths.DataDir, "settings", "eyes.json");

	private EyesOptions value;

	internal bool? QaEnabled;

	public static EyesPreferences Instance => instance.Value;

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
				Set(delegate(EyesOptions o)
				{
					o.Enabled = value;
				});
			}
		}
	}

	public string Skin
	{
		get
		{
			return EyeSkins.Normalize(value.Skin);
		}
		set
		{
			string v = EyeSkins.Normalize(value);
			if (v != Skin)
			{
				Set(delegate(EyesOptions o)
				{
					o.Skin = v;
				});
			}
		}
	}

	public event EventHandler Changed;

	private EyesPreferences()
	{
		EyesOptions eyesOptions = null;
		try
		{
			eyesOptions = PersistenceHelper.ReadWithBackup(path, (string text) => new JavaScriptSerializer().Deserialize<EyesOptions>(text));
		}
		catch
		{
			eyesOptions = null;
		}
		value = eyesOptions ?? new EyesOptions();
		value.Skin = EyeSkins.Normalize(value.Skin);
	}

	private void Set(Action<EyesOptions> change)
	{
		change(value);
		if (!QaEnabled.HasValue)
		{
			PersistenceHelper.SafeWriteJson(path, value);
		}
		this.Changed?.Invoke(this, EventArgs.Empty);
	}

	internal void SetQaEnabled(bool? enabled)
	{
		QaEnabled = enabled;
		this.Changed?.Invoke(this, EventArgs.Empty);
	}
}
