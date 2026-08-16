using System;
using Mirage.WebIngest;

namespace Mirage.VirtualTexture
{
	/// <summary>Addressing math shared by CPU sampling paths.</summary>
	// Token: 0x02000045 RID: 69
	public static class MirageTileMath
	{
		/// <summary>Integer square root — the row stride for a square vertex grid.</summary>
		// Token: 0x060001B9 RID: 441 RVA: 0x0000D7A4 File Offset: 0x0000B9A4
		public static int GridSide(int vertCount)
		{
			bool flag = vertCount <= 0;
			int result;
			if (flag)
			{
				result = 1;
			}
			else
			{
				int s = vertCount;
				for (int next = (s + 1) / 2; next < s; next = (s + vertCount / s) / 2)
				{
					s = next;
				}
				bool flag2 = (s + 1) * (s + 1) <= vertCount;
				if (flag2)
				{
					s++;
				}
				result = ((s < 2) ? 2 : s);
			}
			return result;
		}

		/// <summary>Map a raw face UV into the corrected UV space tiles are stored in.</summary>
		// Token: 0x060001BA RID: 442 RVA: 0x0000D804 File Offset: 0x0000BA04
		public static void CorrectFaceUV(int face, double u, double v, out double cu, out double cv)
		{
			MirageCubeMath.CorrectFaceUV(face, u, v, out cu, out cv);
		}
	}
}
