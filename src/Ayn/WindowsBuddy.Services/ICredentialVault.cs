namespace WindowsBuddy.Services;

internal interface ICredentialVault
{
	bool Enabled { get; }

	bool TryRead(string target, out byte[] blob);

	void Write(string target, byte[] blob);

	void Delete(string target);
}
