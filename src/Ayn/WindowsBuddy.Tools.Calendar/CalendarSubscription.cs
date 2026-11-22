using System;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace WindowsBuddy.Tools.Calendar;

public sealed class CalendarSubscription
{
	public string Id { get; set; }

	public string Name { get; set; }

	public string UrlProtected { get; set; }

	public CalColor Color { get; set; }

	public bool Enabled { get; set; }

	public string ETag { get; set; }

	public string LastModified { get; set; }

	public long LastSyncUtc { get; set; }

	public string LastError { get; set; }

	[ScriptIgnore]
	public string Key => "sub_" + Id;

	[ScriptIgnore]
	public string Url
	{
		get
		{
			if (string.IsNullOrEmpty(UrlProtected))
			{
				return null;
			}
			try
			{
				return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(UrlProtected), null, DataProtectionScope.CurrentUser));
			}
			catch
			{
				return null;
			}
		}
		set
		{
			try
			{
				UrlProtected = (string.IsNullOrEmpty(value) ? null : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser)));
			}
			catch
			{
				UrlProtected = null;
			}
		}
	}

	public CalendarSubscription()
	{
		Id = Guid.NewGuid().ToString("N");
		Enabled = true;
		Color = CalColor.Blue;
	}
}
