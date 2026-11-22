using System;
using System.Numerics;
using System.Security.Cryptography;

namespace WindowsBuddy.Services;

public static class Ed25519
{
	private struct Point
	{
		public readonly BigInteger X;

		public readonly BigInteger Y;

		public readonly bool IsIdentity;

		public static readonly Point Identity = new Point(isIdentity: true);

		public Point(BigInteger x, BigInteger y)
		{
			X = x;
			Y = y;
			IsIdentity = false;
		}

		private Point(bool isIdentity)
		{
			X = BigInteger.Zero;
			Y = BigInteger.One;
			IsIdentity = isIdentity;
		}
	}

	private static readonly BigInteger P = (BigInteger.One << 255) - 19;

	private static readonly BigInteger L = BigInteger.Parse("7237005577332262213973186563042994240857116359379907606001950938285454250989");

	private static readonly BigInteger D = Mod(-121665 * ModInverse(121666, P), P);

	private static readonly BigInteger By = Mod(4 * ModInverse(5, P), P);

	private static readonly BigInteger Bx = RecoverX(By, 0);

	private static BigInteger Mod(BigInteger x, BigInteger m)
	{
		BigInteger bigInteger = x % m;
		if (!(bigInteger < 0L))
		{
			return bigInteger;
		}
		return bigInteger + m;
	}

	private static BigInteger ModInverse(BigInteger a, BigInteger m)
	{
		return BigInteger.ModPow(a, m - 2, m);
	}

	private static BigInteger RecoverX(BigInteger y, int sign)
	{
		BigInteger bigInteger = Mod(y * y, P);
		BigInteger bigInteger2 = Mod(bigInteger - 1, P);
		BigInteger a = Mod(D * bigInteger + 1, P);
		BigInteger bigInteger3 = Mod(bigInteger2 * ModInverse(a, P), P);
		BigInteger bigInteger4 = BigInteger.ModPow(bigInteger3, (P + 3) / 8, P);
		if (Mod(bigInteger4 * bigInteger4 - bigInteger3, P) != 0L)
		{
			BigInteger bigInteger5 = BigInteger.ModPow(2, (P - 1) / 4, P);
			bigInteger4 = Mod(bigInteger4 * bigInteger5, P);
		}
		if (((bigInteger4 % 2 != 0L) ? 1 : 0) != sign)
		{
			bigInteger4 = Mod(-bigInteger4, P);
		}
		return bigInteger4;
	}

	private static Point PointAdd(Point p1, Point p2)
	{
		if (p1.IsIdentity)
		{
			return p2;
		}
		if (p2.IsIdentity)
		{
			return p1;
		}
		BigInteger x = p1.X;
		BigInteger y = p1.Y;
		BigInteger x2 = p2.X;
		BigInteger y2 = p2.Y;
		BigInteger bigInteger = Mod(D * Mod(x * x2, P) * Mod(y * y2, P), P);
		BigInteger x3 = Mod(Mod(x * y2 + y * x2, P) * ModInverse(1 + bigInteger, P), P);
		BigInteger y3 = Mod(Mod(y * y2 + x * x2, P) * ModInverse(1 - bigInteger, P), P);
		return new Point(x3, y3);
	}

	private static Point ScalarMul(Point p, BigInteger s)
	{
		Point point = Point.Identity;
		Point point2 = p;
		while (s > 0L)
		{
			if (!s.IsEven)
			{
				point = PointAdd(point, point2);
			}
			point2 = PointAdd(point2, point2);
			s >>= 1;
		}
		return point;
	}

	private static byte[] PointEncode(Point p)
	{
		byte[] array = p.Y.ToByteArray();
		byte[] array2 = new byte[32];
		Array.Copy(array, array2, Math.Min(array.Length, 32));
		if (!p.X.IsEven)
		{
			array2[31] |= 128;
		}
		return array2;
	}

	private static Point? PointDecode(byte[] bytes)
	{
		if (bytes == null || bytes.Length != 32)
		{
			return null;
		}
		byte[] array = (byte[])bytes.Clone();
		int sign = (((array[31] & 0x80u) != 0) ? 1 : 0);
		array[31] &= 127;
		byte[] array2 = new byte[33];
		Array.Copy(array, array2, 32);
		BigInteger bigInteger = new BigInteger(array2);
		if (bigInteger >= P)
		{
			return null;
		}
		BigInteger x = RecoverX(bigInteger, sign);
		return new Point(x, bigInteger);
	}

	private static BigInteger DecodeScalar(byte[] bytes)
	{
		byte[] array = new byte[bytes.Length + 1];
		Array.Copy(bytes, array, bytes.Length);
		return new BigInteger(array);
	}

	public static byte[] PublicKeyFromSeed(byte[] seed)
	{
		if (seed == null || seed.Length != 32)
		{
			throw new ArgumentException("Seed must be 32 bytes");
		}
		using SHA512 sHA = SHA512.Create();
		byte[] array = sHA.ComputeHash(seed);
		array[0] &= 248;
		array[31] &= 127;
		array[31] |= 64;
		byte[] array2 = new byte[32];
		Array.Copy(array, array2, 32);
		BigInteger s = DecodeScalar(array2);
		Point p = ScalarMul(new Point(Bx, By), s);
		return PointEncode(p);
	}

	public static byte[] Sign(byte[] message, byte[] privateKey)
	{
		if (privateKey == null || (privateKey.Length != 32 && privateKey.Length != 64))
		{
			throw new ArgumentException("Private key must be 32 (seed) or 64 bytes");
		}
		byte[] array = new byte[32];
		Array.Copy(privateKey, array, 32);
		using SHA512 sHA = SHA512.Create();
		byte[] array2 = sHA.ComputeHash(array);
		array2[0] &= 248;
		array2[31] &= 127;
		array2[31] |= 64;
		byte[] array3 = new byte[32];
		Array.Copy(array2, 0, array3, 0, 32);
		BigInteger bigInteger = DecodeScalar(array3);
		Point p = ScalarMul(new Point(Bx, By), bigInteger);
		byte[] sourceArray = PointEncode(p);
		byte[] array4 = new byte[32];
		Array.Copy(array2, 32, array4, 0, 32);
		byte[] array5 = new byte[32 + message.Length];
		Array.Copy(array4, 0, array5, 0, 32);
		Array.Copy(message, 0, array5, 32, message.Length);
		byte[] bytes = sHA.ComputeHash(array5);
		BigInteger bigInteger2 = Mod(DecodeScalar(bytes), L);
		Point p2 = ScalarMul(new Point(Bx, By), bigInteger2);
		byte[] sourceArray2 = PointEncode(p2);
		byte[] array6 = new byte[64 + message.Length];
		Array.Copy(sourceArray2, 0, array6, 0, 32);
		Array.Copy(sourceArray, 0, array6, 32, 32);
		Array.Copy(message, 0, array6, 64, message.Length);
		byte[] bytes2 = sHA.ComputeHash(array6);
		BigInteger bigInteger3 = Mod(DecodeScalar(bytes2), L);
		byte[] array7 = Mod(bigInteger2 + bigInteger3 * bigInteger, L).ToByteArray();
		byte[] array8 = new byte[32];
		Array.Copy(array7, array8, Math.Min(array7.Length, 32));
		byte[] array9 = new byte[64];
		Array.Copy(sourceArray2, 0, array9, 0, 32);
		Array.Copy(array8, 0, array9, 32, 32);
		return array9;
	}

	public static bool Verify(byte[] signature, byte[] message, byte[] publicKey)
	{
		if (signature == null || signature.Length != 64)
		{
			return false;
		}
		if (publicKey == null || publicKey.Length != 32)
		{
			return false;
		}
		if (message == null)
		{
			message = new byte[0];
		}
		try
		{
			byte[] array = new byte[32];
			byte[] array2 = new byte[32];
			Array.Copy(signature, 0, array, 0, 32);
			Array.Copy(signature, 32, array2, 0, 32);
			BigInteger bigInteger = DecodeScalar(array2);
			if (bigInteger >= L)
			{
				return false;
			}
			Point? point = PointDecode(array);
			if (!point.HasValue)
			{
				return false;
			}
			Point? point2 = PointDecode(publicKey);
			if (!point2.HasValue)
			{
				return false;
			}
			using SHA512 sHA = SHA512.Create();
			byte[] array3 = new byte[64 + message.Length];
			Array.Copy(array, 0, array3, 0, 32);
			Array.Copy(publicKey, 0, array3, 32, 32);
			Array.Copy(message, 0, array3, 64, message.Length);
			byte[] bytes = sHA.ComputeHash(array3);
			BigInteger s = Mod(DecodeScalar(bytes), L);
			Point point3 = ScalarMul(new Point(Bx, By), bigInteger);
			Point p = ScalarMul(point2.Value, s);
			Point point4 = PointAdd(point.Value, p);
			return point3.X == point4.X && point3.Y == point4.Y;
		}
		catch
		{
			return false;
		}
	}
}
