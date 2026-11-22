using System;
using System.Linq;
using System.Security.Cryptography;

namespace WindowsBuddy.Tools.Analytics;

internal static class Pkcs8
{
	public static RSAParameters FromPem(string pem)
	{
		try
		{
			string s = string.Concat(from l in pem.Replace("\\n", "\n").Split('\n')
				select l.Trim() into l
				where l.Length > 0 && !l.StartsWith("-----")
				select l);
			byte[] b = Convert.FromBase64String(s);
			int p = 0;
			Expect(b, ref p, 48);
			Len(b, ref p);
			Skip(b, ref p);
			Skip(b, ref p);
			Expect(b, ref p, 4);
			Len(b, ref p);
			Expect(b, ref p, 48);
			Len(b, ref p);
			Skip(b, ref p);
			byte[] array = Int(b, ref p);
			byte[] exponent = Int(b, ref p);
			byte[] v = Int(b, ref p);
			byte[] v2 = Int(b, ref p);
			byte[] v3 = Int(b, ref p);
			byte[] v4 = Int(b, ref p);
			byte[] v5 = Int(b, ref p);
			byte[] v6 = Int(b, ref p);
			int len = (array.Length + 1) / 2;
			RSAParameters result = default(RSAParameters);
			result.Modulus = array;
			result.Exponent = exponent;
			result.D = Pad(v, array.Length);
			result.P = Pad(v2, len);
			result.Q = Pad(v3, len);
			result.DP = Pad(v4, len);
			result.DQ = Pad(v5, len);
			result.InverseQ = Pad(v6, len);
			return result;
		}
		catch (AnalyticsException)
		{
			throw;
		}
		catch
		{
			throw new AnalyticsException(AnalyticsErrorKind.WrongKey, "The service-account private key couldn't be read");
		}
	}

	private static void Expect(byte[] b, ref int p, byte tag)
	{
		if (b[p++] != tag)
		{
			throw new FormatException();
		}
	}

	private static int Len(byte[] b, ref int p)
	{
		int num = b[p++];
		if (num < 128)
		{
			return num;
		}
		int num2 = num & 0x7F;
		int num3 = 0;
		for (int i = 0; i < num2; i++)
		{
			num3 = (num3 << 8) | b[p++];
		}
		return num3;
	}

	private static void Skip(byte[] b, ref int p)
	{
		p++;
		int num = Len(b, ref p);
		p += num;
	}

	private static byte[] Int(byte[] b, ref int p)
	{
		Expect(b, ref p, 2);
		int num = Len(b, ref p);
		int num2 = p;
		p += num;
		while (num > 1 && b[num2] == 0)
		{
			num2++;
			num--;
		}
		byte[] array = new byte[num];
		Buffer.BlockCopy(b, num2, array, 0, num);
		return array;
	}

	private static byte[] Pad(byte[] v, int len)
	{
		if (v.Length >= len)
		{
			return v;
		}
		byte[] array = new byte[len];
		Buffer.BlockCopy(v, 0, array, len - v.Length, v.Length);
		return array;
	}
}
