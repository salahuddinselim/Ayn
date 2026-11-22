using System.Runtime.InteropServices;

namespace WindowsBuddy.Services.CoreAudio;

[Guid("C02216F6-0388-4E45-9285-18B42C14A568")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAudioMeterInformation
{
	[PreserveSig]
	int GetPeakValue(out float pfPeak);

	[PreserveSig]
	int GetMeteringChannelCount(out uint pnChannelCount);

	[PreserveSig]
	int GetChannelsPeakValues(uint u32ChannelCount, [In][Out] float[] afPeakValues);

	[PreserveSig]
	int QueryHardwareSupport(out uint pdwHardwareSupportMask);
}
