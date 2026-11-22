using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace WindowsBuddy.Tools.Analytics;

public sealed class ServiceAccount
{
	public string ClientEmail;

	public string TokenUri;

	public RSAParameters Key;

	public static ServiceAccount Parse(string json)
	{
		object o = J.Parse(json);
		string text = J.Str(J.Get(o, "client_email"));
		string text2 = J.Str(J.Get(o, "private_key"));
		if (J.Str(J.Get(o, "type")) != "service_account" || string.IsNullOrEmpty(text) || string.IsNullOrEmpty(text2))
		{
			throw new AnalyticsException(AnalyticsErrorKind.WrongKey, "That isn't a Google service-account key file");
		}
		ServiceAccount serviceAccount = new ServiceAccount();
		serviceAccount.ClientEmail = text;
		serviceAccount.TokenUri = J.Str(J.Get(o, "token_uri")) ?? "https://oauth2.googleapis.com/token";
		serviceAccount.Key = Pkcs8.FromPem(text2);
		return serviceAccount;
	}

	public string CreateAssertion(string scope, DateTime utcNow)
	{
		long num = (long)(utcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
		string text = GoogleAuth.B64Url(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"));
		string text2 = GoogleAuth.B64Url(Encoding.UTF8.GetBytes(J.Write(new Dictionary<string, object>
		{
			{ "iss", ClientEmail },
			{ "scope", scope },
			{ "aud", TokenUri },
			{ "iat", num },
			{
				"exp",
				num + 3600
			}
		})));
		string text3 = text + "." + text2;
		using RSACng rSACng = new RSACng();
		rSACng.ImportParameters(Key);
		byte[] b = rSACng.SignData(Encoding.ASCII.GetBytes(text3), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		return text3 + "." + GoogleAuth.B64Url(b);
	}
}
