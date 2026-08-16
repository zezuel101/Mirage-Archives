using System;

namespace Mirage.VirtualTexture
{
	/// <summary>One visible leaf quad as the streaming layer sees it.</summary>
	// Token: 0x02000049 RID: 73
	public readonly struct LeafQuad
	{
		/// <summary>One visible leaf quad as the streaming layer sees it.</summary>
		// Token: 0x060001D1 RID: 465 RVA: 0x0000DDE9 File Offset: 0x0000BFE9
		public LeafQuad(int face, double uvSwX, double uvSwY, int subdivision)
		{
			this.Face = face;
			this.UvSwX = uvSwX;
			this.UvSwY = uvSwY;
			this.Subdivision = subdivision;
		}

		// Token: 0x04000172 RID: 370
		public readonly int Face;

		// Token: 0x04000173 RID: 371
		public readonly double UvSwX;

		// Token: 0x04000174 RID: 372
		public readonly double UvSwY;

		// Token: 0x04000175 RID: 373
		public readonly int Subdivision;
	}
}
