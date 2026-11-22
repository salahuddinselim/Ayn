using System;
using System.Collections.Generic;

namespace WindowsBuddy.Tools.AICoding;

public sealed class ActivityDays
{
	private readonly HashSet<string> _realSessions = new HashSet<string>(StringComparer.Ordinal);

	private readonly Dictionary<string, HashSet<DateTime>> _realDays = new Dictionary<string, HashSet<DateTime>>(StringComparer.Ordinal);

	private readonly Dictionary<string, HashSet<DateTime>> _metaDays = new Dictionary<string, HashSet<DateTime>>(StringComparer.Ordinal);

	public static bool IsReal(bool promptOrResponse, string uuid, string messageId)
	{
		if (promptOrResponse)
		{
			if (string.IsNullOrEmpty(uuid))
			{
				return !string.IsNullOrEmpty(messageId);
			}
			return true;
		}
		return false;
	}

	public static string SessionOf(string sessionId, string fileSession)
	{
		if (string.IsNullOrEmpty(sessionId))
		{
			return fileSession;
		}
		return sessionId;
	}

	public void Add(string agent, string session, DateTime localDay, bool real)
	{
		if (real)
		{
			Set(_realDays, agent).Add(localDay.Date);
			if (!string.IsNullOrEmpty(session))
			{
				_realSessions.Add(agent + "|" + session);
			}
		}
		else if (!string.IsNullOrEmpty(session))
		{
			Set(_metaDays, agent + "|" + session).Add(localDay.Date);
		}
	}

	public HashSet<DateTime> Days(string agent)
	{
		HashSet<DateTime> hashSet = new HashSet<DateTime>();
		foreach (KeyValuePair<string, HashSet<DateTime>> realDay in _realDays)
		{
			if (agent == null || realDay.Key == agent)
			{
				hashSet.UnionWith(realDay.Value);
			}
		}
		foreach (KeyValuePair<string, HashSet<DateTime>> metaDay in _metaDays)
		{
			if ((agent == null || metaDay.Key.StartsWith(agent + "|", StringComparison.Ordinal)) && _realSessions.Contains(metaDay.Key))
			{
				hashSet.UnionWith(metaDay.Value);
			}
		}
		return hashSet;
	}

	public static int Streak(ICollection<DateTime> days, DateTime today)
	{
		DateTime item = (days.Contains(today) ? today : today.AddDays(-1.0));
		int num = 0;
		while (days.Contains(item))
		{
			num++;
			item = item.AddDays(-1.0);
		}
		return num;
	}

	private static HashSet<DateTime> Set(Dictionary<string, HashSet<DateTime>> map, string key)
	{
		if (!map.TryGetValue(key, out var value))
		{
			value = (map[key] = new HashSet<DateTime>());
		}
		return value;
	}
}
