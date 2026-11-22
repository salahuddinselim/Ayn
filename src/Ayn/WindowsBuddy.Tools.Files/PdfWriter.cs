using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WindowsBuddy.Tools.Files;

internal static class PdfWriter
{
	public static byte[] Build(IList<ConvertEngine.Encoded> pages)
	{
		MemoryStream ms = new MemoryStream();
		try
		{
			List<long> offsets = new List<long>();
			Action<string> w = delegate(string text)
			{
				byte[] bytes = Encoding.ASCII.GetBytes(text);
				ms.Write(bytes, 0, bytes.Length);
			};
			w("%PDF-1.4\n");
			ms.Write(new byte[6] { 37, 226, 227, 207, 211, 10 }, 0, 6);
			int count = pages.Count;
			Action<int> action = delegate(int id)
			{
				while (offsets.Count < id)
				{
					offsets.Add(0L);
				}
				offsets[id - 1] = ms.Position;
				w(id + " 0 obj\n");
			};
			action(1);
			w("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < count; i++)
			{
				stringBuilder.Append(3 + i * 3).Append(" 0 R ");
			}
			action(2);
			w("<< /Type /Pages /Kids [" + stringBuilder.ToString().TrimEnd() + "] /Count " + count + " >>\nendobj\n");
			for (int j = 0; j < count; j++)
			{
				ConvertEngine.Encoded encoded = pages[j];
				int num = 3 + j * 3;
				int num2 = num + 1;
				int num3 = num + 2;
				action(num);
				w("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + encoded.W + " " + encoded.H + "] /Resources << /XObject << /Im0 " + num3 + " 0 R >> >> /Contents " + num2 + " 0 R >>\nendobj\n");
				string text2 = "q " + encoded.W + " 0 0 " + encoded.H + " 0 0 cm /Im0 Do Q";
				action(num2);
				w("<< /Length " + text2.Length + " >>\nstream\n" + text2 + "\nendstream\nendobj\n");
				action(num3);
				w("<< /Type /XObject /Subtype /Image /Width " + encoded.W + " /Height " + encoded.H + " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length " + encoded.Bytes.Length + " >>\nstream\n");
				ms.Write(encoded.Bytes, 0, encoded.Bytes.Length);
				w("\nendstream\nendobj\n");
			}
			long position = ms.Position;
			StringBuilder stringBuilder2 = new StringBuilder();
			stringBuilder2.Append("xref\n0 ").Append(offsets.Count + 1).Append("\n0000000000 65535 f \n");
			foreach (long item in offsets)
			{
				stringBuilder2.Append(item.ToString("D10")).Append(" 00000 n \n");
			}
			stringBuilder2.Append("trailer\n<< /Size ").Append(offsets.Count + 1).Append(" /Root 1 0 R >>\nstartxref\n")
				.Append(position)
				.Append("\n%%EOF\n");
			w(stringBuilder2.ToString());
			return ms.ToArray();
		}
		finally
		{
			if (ms != null)
			{
				((IDisposable)ms).Dispose();
			}
		}
	}
}
