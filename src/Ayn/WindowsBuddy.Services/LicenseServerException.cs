using System;

namespace WindowsBuddy.Services;

internal sealed class LicenseServerException : Exception
{
	public int StatusCode { get; private set; }

	public string Code { get; private set; }

	public LicenseServerException(int statusCode, string code = null)
		: base("License server returned " + statusCode + ".")
	{
		StatusCode = statusCode;
		Code = code;
	}
}
