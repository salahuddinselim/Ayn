using System;

namespace WindowsBuddy.Services.Lifecycle;

public sealed class AdmissionDeferredException : InvalidOperationException
{
	public AdmissionDeferredException()
		: base("Owner cannot safely resume while writes are pending; admission reopens once they settle.")
	{
	}
}
