using System;
using System.Globalization;

namespace WindowsBuddy.Tools.Calendar;

public static class HolidayCountries
{
	public static readonly string[][] All = new string[104][]
	{
		new string[3] { "AF", "Afghanistan", "en.af" },
		new string[3] { "DZ", "Algeria", "en.dz" },
		new string[3] { "AR", "Argentina", "en.ar" },
		new string[3] { "AU", "Australia", "en.australian" },
		new string[3] { "AT", "Austria", "en.austrian" },
		new string[3] { "BH", "Bahrain", "en.bh" },
		new string[3] { "BD", "Bangladesh", "en.bd" },
		new string[3] { "BE", "Belgium", "en.be" },
		new string[3] { "BT", "Bhutan", "en.bt" },
		new string[3] { "BO", "Bolivia", "en.bo" },
		new string[3] { "BR", "Brazil", "en.brazilian" },
		new string[3] { "BN", "Brunei", "en.bn" },
		new string[3] { "BG", "Bulgaria", "en.bulgarian" },
		new string[3] { "KH", "Cambodia", "en.kh" },
		new string[3] { "CA", "Canada", "en.canadian" },
		new string[3] { "CL", "Chile", "en.cl" },
		new string[3] { "CN", "China", "en.china" },
		new string[3] { "CO", "Colombia", "en.co" },
		new string[3] { "CR", "Costa Rica", "en.cr" },
		new string[3] { "HR", "Croatia", "en.croatian" },
		new string[3] { "CU", "Cuba", "en.cu" },
		new string[3] { "CY", "Cyprus", "en.cy" },
		new string[3] { "CZ", "Czechia", "en.czech" },
		new string[3] { "DK", "Denmark", "en.danish" },
		new string[3] { "DO", "Dominican Republic", "en.do" },
		new string[3] { "EC", "Ecuador", "en.ec" },
		new string[3] { "EG", "Egypt", "en.eg" },
		new string[3] { "EE", "Estonia", "en.ee" },
		new string[3] { "FJ", "Fiji", "en.fj" },
		new string[3] { "FI", "Finland", "en.finnish" },
		new string[3] { "FR", "France", "en.french" },
		new string[3] { "DE", "Germany", "en.german" },
		new string[3] { "GH", "Ghana", "en.gh" },
		new string[3] { "GR", "Greece", "en.greek" },
		new string[3] { "GT", "Guatemala", "en.gt" },
		new string[3] { "HK", "Hong Kong", "en.hong_kong" },
		new string[3] { "HU", "Hungary", "en.hungarian" },
		new string[3] { "IS", "Iceland", "en.is" },
		new string[3] { "IN", "India", "en.indian" },
		new string[3] { "ID", "Indonesia", "en.indonesian" },
		new string[3] { "IR", "Iran", "en.ir" },
		new string[3] { "IQ", "Iraq", "en.iq" },
		new string[3] { "IE", "Ireland", "en.irish" },
		new string[3] { "IT", "Italy", "en.italian" },
		new string[3] { "JM", "Jamaica", "en.jm" },
		new string[3] { "JP", "Japan", "en.japanese" },
		new string[3] { "JO", "Jordan", "en.jo" },
		new string[3] { "KZ", "Kazakhstan", "en.kz" },
		new string[3] { "KE", "Kenya", "en.ke" },
		new string[3] { "KW", "Kuwait", "en.kw" },
		new string[3] { "LA", "Laos", "en.la" },
		new string[3] { "LV", "Latvia", "en.latvian" },
		new string[3] { "LB", "Lebanon", "en.lb" },
		new string[3] { "LT", "Lithuania", "en.lithuanian" },
		new string[3] { "LU", "Luxembourg", "en.lu" },
		new string[3] { "MY", "Malaysia", "en.malaysia" },
		new string[3] { "MV", "Maldives", "en.mv" },
		new string[3] { "MT", "Malta", "en.mt" },
		new string[3] { "MX", "Mexico", "en.mexican" },
		new string[3] { "MN", "Mongolia", "en.mn" },
		new string[3] { "MA", "Morocco", "en.ma" },
		new string[3] { "MM", "Myanmar", "en.mm" },
		new string[3] { "NP", "Nepal", "en.np" },
		new string[3] { "NL", "Netherlands", "en.dutch" },
		new string[3] { "NZ", "New Zealand", "en.new_zealand" },
		new string[3] { "NG", "Nigeria", "en.ng" },
		new string[3] { "NO", "Norway", "en.norwegian" },
		new string[3] { "OM", "Oman", "en.om" },
		new string[3] { "PK", "Pakistan", "en.pk" },
		new string[3] { "PA", "Panama", "en.pa" },
		new string[3] { "PY", "Paraguay", "en.py" },
		new string[3] { "PE", "Peru", "en.pe" },
		new string[3] { "PH", "Philippines", "en.philippines" },
		new string[3] { "PL", "Poland", "en.polish" },
		new string[3] { "PT", "Portugal", "en.portuguese" },
		new string[3] { "PR", "Puerto Rico", "en.pr" },
		new string[3] { "QA", "Qatar", "en.qa" },
		new string[3] { "RO", "Romania", "en.romanian" },
		new string[3] { "RU", "Russia", "en.russian" },
		new string[3] { "SA", "Saudi Arabia", "en.saudiarabian" },
		new string[3] { "SG", "Singapore", "en.singapore" },
		new string[3] { "SK", "Slovakia", "en.slovak" },
		new string[3] { "SI", "Slovenia", "en.slovenian" },
		new string[3] { "ZA", "South Africa", "en.sa" },
		new string[3] { "KR", "South Korea", "en.south_korea" },
		new string[3] { "ES", "Spain", "en.spain" },
		new string[3] { "LK", "Sri Lanka", "en.lk" },
		new string[3] { "SE", "Sweden", "en.swedish" },
		new string[3] { "CH", "Switzerland", "en.ch" },
		new string[3] { "TW", "Taiwan", "en.taiwan" },
		new string[3] { "TZ", "Tanzania", "en.tz" },
		new string[3] { "TH", "Thailand", "en.th" },
		new string[3] { "TT", "Trinidad & Tobago", "en.tt" },
		new string[3] { "TN", "Tunisia", "en.tn" },
		new string[3] { "TR", "Turkey", "en.turkish" },
		new string[3] { "UG", "Uganda", "en.ug" },
		new string[3] { "UA", "Ukraine", "en.ukrainian" },
		new string[3] { "AE", "United Arab Emirates", "en.ae" },
		new string[3] { "GB", "United Kingdom", "en.uk" },
		new string[3] { "US", "United States", "en.usa" },
		new string[3] { "UY", "Uruguay", "en.uy" },
		new string[3] { "UZ", "Uzbekistan", "en.uz" },
		new string[3] { "VE", "Venezuela", "en.ve" },
		new string[3] { "VN", "Vietnam", "en.vietnamese" }
	};

	public static string[] Find(string code)
	{
		if (string.IsNullOrEmpty(code))
		{
			return null;
		}
		string[][] all = All;
		foreach (string[] array in all)
		{
			if (string.Equals(array[0], code, StringComparison.OrdinalIgnoreCase))
			{
				return array;
			}
		}
		return null;
	}

	public static string NameOf(string code)
	{
		string[] array = Find(code);
		if (array == null)
		{
			return code;
		}
		return array[1];
	}

	public static string FeedUrl(string code)
	{
		string[] array = Find(code);
		if (array == null)
		{
			return null;
		}
		return "https://calendar.google.com/calendar/ical/" + array[2] + "%23holiday%40group.v.calendar.google.com/public/basic.ics";
	}

	public static string DefaultCode()
	{
		try
		{
			string twoLetterISORegionName = RegionInfo.CurrentRegion.TwoLetterISORegionName;
			if (Find(twoLetterISORegionName) != null)
			{
				return twoLetterISORegionName.ToUpperInvariant();
			}
		}
		catch
		{
		}
		return "IN";
	}
}
