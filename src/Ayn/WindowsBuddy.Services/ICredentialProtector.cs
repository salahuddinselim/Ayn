namespace WindowsBuddy.Services;

internal interface ICredentialProtector
{
	byte[] Protect(byte[] plain);

	byte[] Unprotect(byte[] blob);
}
