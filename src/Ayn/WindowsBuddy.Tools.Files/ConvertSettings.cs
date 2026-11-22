using System;
using System.IO;
using System.Web.Script.Serialization;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Files;

public sealed class ConvertSettings
{
	private static readonly string StatePath = Path.Combine(AppPaths.RoamingAppData, AppPaths.FolderName, "converter_state.json");

	public OutFormat Format { get; set; }

	public SizeMode Mode { get; set; }

	public int Quality { get; set; }

	public double TargetValue { get; set; }

	public bool TargetInMB { get; set; }

	public int ResizeLongEdge { get; set; }

	public int CustomLongEdge { get; set; }

	public SaveTarget SaveTo { get; set; }

	public string CustomFolder { get; set; }

	[ScriptIgnore]
	public bool IsLossless
	{
		get
		{
			return Format is OutFormat.Png or OutFormat.Ico or OutFormat.Bmp or OutFormat.Gif or OutFormat.Tiff;
		}
	}

	[ScriptIgnore]
	public bool UsesTarget
	{
		get
		{
			if (Mode == SizeMode.Target)
			{
				return !IsLossless;
			}
			return false;
		}
	}

	[ScriptIgnore]
	public long TargetBytes
	{
		get
		{
			double num = Math.Max(0.001, TargetValue);
			return (long)Math.Round(num * (TargetInMB ? 1048576.0 : 1024.0));
		}
	}

	public ConvertSettings()
	{
		Format = OutFormat.Jpg;
		Mode = SizeMode.Quality;
		Quality = 80;
		TargetValue = 500.0;
		TargetInMB = false;
		ResizeLongEdge = 0;
		CustomLongEdge = 1600;
		SaveTo = SaveTarget.SameFolder;
		CustomFolder = null;
	}

	public ConvertSettings Clone()
	{
		return (ConvertSettings)MemberwiseClone();
	}

	public static ConvertSettings Load()
	{
		try
		{
			if (File.Exists(StatePath))
			{
				ConvertSettings convertSettings = new JavaScriptSerializer().Deserialize<ConvertSettings>(PersistenceHelper.ReadShared(StatePath));
				if (convertSettings != null)
				{
					convertSettings.Quality = Math.Max(1, Math.Min(100, convertSettings.Quality));
					if (convertSettings.TargetValue <= 0.0)
					{
						convertSettings.TargetValue = 500.0;
					}
					if (convertSettings.ResizeLongEdge < 0)
					{
						convertSettings.ResizeLongEdge = 0;
					}
					if (convertSettings.CustomLongEdge < 16)
					{
						convertSettings.CustomLongEdge = 1600;
					}
					if (convertSettings.SaveTo == SaveTarget.Custom && (string.IsNullOrEmpty(convertSettings.CustomFolder) || !Directory.Exists(convertSettings.CustomFolder)))
					{
						convertSettings.SaveTo = SaveTarget.SameFolder;
					}
					return convertSettings;
				}
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Converter: state load failed: " + ex.Message);
		}
		return new ConvertSettings();
	}

	public void Save()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(StatePath));
			PersistenceHelper.SafeWriteAllText(StatePath, new JavaScriptSerializer().Serialize(this));
		}
		catch (Exception error)
		{
			PersistenceHelper.ReportSaveFailure(StatePath, error);
		}
	}
}
