using System.Runtime.InteropServices;

namespace WindowsBuddy.Services.CoreAudio;

[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
public interface IPropertyStore
{
	int GetCount(out uint cProps);

	int GetAt(uint iProp, out PROPERTYKEY pkey);

	[PreserveSig]
	int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);

	int SetValue(ref PROPERTYKEY key, ref PROPVARIANT propvar);

	int Commit();
}
