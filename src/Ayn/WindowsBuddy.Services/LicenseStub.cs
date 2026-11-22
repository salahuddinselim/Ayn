using System;
using System.Threading.Tasks;

namespace WindowsBuddy.Services;

public static class LicenseStub
{
	public const int AynDays = 3;

	public static LicenseStatus Status => LicenseService.Instance.Status;

	public static int AynDaysLeft => LicenseService.Instance.AynDaysLeft;

	public static string StatusLine => LicenseService.Instance.StatusLine;

	public static bool AynStarted => LicenseService.Instance.AynStarted;

	public static bool AwaitingFirstServerContact => LicenseService.Instance.AwaitingFirstServerContact;

	public static AccessState Access => LicenseService.Instance.Access;

	public static bool HasStoredKey => LicenseService.Instance.HasStoredKey;

	public static string MaskedKey => LicenseService.Instance.MaskedKey;

	public static int? OfflineGraceDaysLeft => LicenseService.Instance.OfflineGraceDaysLeft;

	public static string PersistenceWarning => LicenseService.Instance.PersistenceWarning;

	public static SeatReleaseState PendingSeatRelease => LicenseService.Instance.PendingSeatReleaseState;

	public static event EventHandler Changed
	{
		add
		{
			LicenseService.Instance.LicenseChanged += value;
		}
		remove
		{
			LicenseService.Instance.LicenseChanged -= value;
		}
	}

	public static void CheckAynNow()
	{
		LicenseService.Instance.CheckAynNow();
	}

	public static string CleanKey(string key)
	{
		return LicenseKeyInput.Normalize(key);
	}

	public static void StartAyn()
	{
		LicenseService.Instance.StartAyn();
	}

	public static string Normalize(string key)
	{
		return LicenseKeyInput.Normalize(key) ?? "";
	}

	public static bool Activate(string key, out string message)
	{
		try
		{
			ActivationResult result = Task.Run(() => LicenseService.Instance.ActivateAsync(key)).GetAwaiter().GetResult();
			message = result.Message;
			return result.Success;
		}
		catch (Exception ex)
		{
			message = "Activation error: " + ex.Message;
			return false;
		}
	}

	public static async Task<ActivationResult> ActivateAsync(string key)
	{
		return await LicenseService.Instance.ActivateAsync(key);
	}

	public static void Deactivate()
	{
		try
		{
			Task.Run(() => LicenseService.Instance.DeactivateCurrentDeviceAsync()).GetAwaiter().GetResult();
		}
		catch
		{
		}
	}

	public static async Task<bool> DeactivateAsync()
	{
		return await LicenseService.Instance.DeactivateCurrentDeviceAsync();
	}

	public static async Task<DeactivationResult> DeactivateDetailedAsync()
	{
		return await LicenseService.Instance.DeactivateDeviceAsync();
	}

	public static async Task<SeatReleaseState> RetrySeatReleaseAsync()
	{
		return await LicenseService.Instance.RetryPendingReleasesAsync(manual: true);
	}
}
