using System;
using System.IO;

namespace WindowsBuddy.BuddyCapsule.Renderer;

internal sealed class PetFrameInflater
{
	private sealed class Tree
	{
		private readonly int[] counts = new int[16];

		private readonly int[] symbols = new int[288];

		private readonly int[] offsets = new int[16];

		public void Build(int[] lengths, int start, int size)
		{
			Array.Clear(counts, 0, counts.Length);
			for (int i = 0; i < size; i++)
			{
				counts[lengths[start + i]]++;
			}
			offsets[1] = 0;
			for (int j = 1; j < 15; j++)
			{
				offsets[j + 1] = offsets[j] + counts[j];
			}
			for (int k = 0; k < size; k++)
			{
				int num = lengths[start + k];
				if (num != 0)
				{
					symbols[offsets[num]++] = k;
				}
			}
		}

		public int Read(PetFrameInflater input)
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			for (int i = 1; i <= 15; i++)
			{
				num |= input.Bits(1);
				int num4 = counts[i];
				if (num >= num2 && num - num2 < num4)
				{
					return symbols[num3 + num - num2];
				}
				num3 += num4;
				num2 = num2 + num4 << 1;
				num <<= 1;
			}
			throw new InvalidDataException("Invalid pet frame Huffman code");
		}
	}

	private readonly Tree literals = new Tree();

	private readonly Tree distances = new Tree();

	private readonly Tree codeLengths = new Tree();

	private readonly int[] lengths = new int[320];

	private readonly int[] codes = new int[19];

	private static readonly int[] order = new int[19]
	{
		16, 17, 18, 0, 8, 7, 9, 6, 10, 5,
		11, 4, 12, 3, 13, 2, 14, 1, 15
	};

	private static readonly int[] lengthBase = new int[29]
	{
		3, 4, 5, 6, 7, 8, 9, 10, 11, 13,
		15, 17, 19, 23, 27, 31, 35, 43, 51, 59,
		67, 83, 99, 115, 131, 163, 195, 227, 258
	};

	private static readonly int[] lengthExtra = new int[29]
	{
		0, 0, 0, 0, 0, 0, 0, 0, 1, 1,
		1, 1, 2, 2, 2, 2, 3, 3, 3, 3,
		4, 4, 4, 4, 5, 5, 5, 5, 0
	};

	private static readonly int[] distanceBase = new int[30]
	{
		1, 2, 3, 4, 5, 7, 9, 13, 17, 25,
		33, 49, 65, 97, 129, 193, 257, 385, 513, 769,
		1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577
	};

	private static readonly int[] distanceExtra = new int[30]
	{
		0, 0, 0, 0, 1, 1, 2, 2, 3, 3,
		4, 4, 5, 5, 6, 6, 7, 7, 8, 8,
		9, 9, 10, 10, 11, 11, 12, 12, 13, 13
	};

	private byte[] source;

	private int position;

	private int end;

	private int available;

	private uint pending;

	private int Bits(int count)
	{
		while (available < count)
		{
			if (position >= end)
			{
				throw new EndOfStreamException("Pet frame bits");
			}
			pending |= (uint)(source[position++] << available);
			available += 8;
		}
		int result = (int)pending & ((1 << count) - 1);
		pending >>= count;
		available -= count;
		return result;
	}

	private void Fixed()
	{
		for (int i = 0; i < 288; i++)
		{
			lengths[i] = ((i < 144) ? 8 : ((i < 256) ? 9 : ((i < 280) ? 7 : 8)));
		}
		for (int j = 288; j < 320; j++)
		{
			lengths[j] = 5;
		}
		literals.Build(lengths, 0, 288);
		distances.Build(lengths, 288, 32);
	}

	private void Dynamic()
	{
		int num = Bits(5) + 257;
		int num2 = Bits(5) + 1;
		int num3 = Bits(4) + 4;
		if (num > 286)
		{
			throw new InvalidDataException("Pet frame literal table");
		}
		Array.Clear(codes, 0, codes.Length);
		for (int i = 0; i < num3; i++)
		{
			codes[order[i]] = Bits(3);
		}
		codeLengths.Build(codes, 0, 19);
		int num4 = num + num2;
		int num5 = 0;
		while (num5 < num4)
		{
			int num6 = codeLengths.Read(this);
			if (num6 < 16)
			{
				lengths[num5++] = num6;
				continue;
			}
			int num7 = 0;
			int num8;
			switch (num6)
			{
			case 16:
				if (num5 == 0)
				{
					throw new InvalidDataException("Pet frame repeat");
				}
				num7 = lengths[num5 - 1];
				num8 = Bits(2) + 3;
				break;
			case 17:
				num8 = Bits(3) + 3;
				break;
			case 18:
				num8 = Bits(7) + 11;
				break;
			default:
				throw new InvalidDataException("Pet frame code table");
			}
			if (num5 + num8 > num4)
			{
				throw new InvalidDataException("Pet frame repeated lengths");
			}
			while (num8-- > 0)
			{
				lengths[num5++] = num7;
			}
		}
		literals.Build(lengths, 0, num);
		distances.Build(lengths, num, num2);
	}

	public void Decode(byte[] compressed, int size, byte[] output, int expected)
	{
		source = compressed;
		position = (available = 0);
		pending = 0u;
		end = size;
		int num = 0;
		bool flag;
		do
		{
			flag = Bits(1) != 0;
			switch (Bits(2))
			{
			case 0:
			{
				pending = 0u;
				available = 0;
				int num2 = Bits(16);
				int num3 = Bits(16);
				if ((num2 ^ num3) != 65535 || num + num2 > expected)
				{
					throw new InvalidDataException("Pet stored block");
				}
				while (num2-- > 0)
				{
					output[num++] = (byte)Bits(8);
				}
				continue;
			}
			case 1:
				Fixed();
				break;
			case 2:
				Dynamic();
				break;
			default:
				throw new InvalidDataException("Pet frame block");
			}
			while (true)
			{
				int num4 = literals.Read(this);
				if (num4 == 256)
				{
					break;
				}
				if (num4 < 256)
				{
					if (num >= expected)
					{
						throw new InvalidDataException("Pet frame overflow");
					}
					output[num++] = (byte)num4;
					continue;
				}
				num4 -= 257;
				if (num4 < 0 || num4 >= lengthBase.Length)
				{
					throw new InvalidDataException("Pet frame length");
				}
				int num5 = lengthBase[num4] + Bits(lengthExtra[num4]);
				int num6 = distances.Read(this);
				if (num6 >= distanceBase.Length)
				{
					throw new InvalidDataException("Pet frame distance code");
				}
				int num7 = distanceBase[num6] + Bits(distanceExtra[num6]);
				if (num7 > num || num + num5 > expected)
				{
					throw new InvalidDataException("Pet frame back reference");
				}
				while (num5-- > 0)
				{
					output[num] = output[num - num7];
					num++;
				}
			}
		}
		while (!flag);
		if (num != expected)
		{
			throw new InvalidDataException("Incomplete pet frame");
		}
		source = null;
	}
}
