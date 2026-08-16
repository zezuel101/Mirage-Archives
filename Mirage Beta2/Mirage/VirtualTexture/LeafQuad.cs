using System;

namespace Mirage.VirtualTexture
{
	/// <summary>
	/// One visible leaf quad as seen by the VT streaming layer. Carries the raw
	/// (PRE-CorrectFaceUV) south-west corner so the streaming manager can apply
	/// the per-face rotation when computing tile coordinates.
	/// </summary>
	// Token: 0x0200003D RID: 61
	public readonly struct LeafQuad
	{
		// Token: 0x0600018D RID: 397 RVA: 0x0000C359 File Offset: 0x0000A559
		public LeafQuad(int face, double uvSwX, double uvSwY, int subdivision)
		{
			this.Face = face;
			this.UvSwX = uvSwX;
			this.UvSwY = uvSwY;
			this.Subdivision = subdivision;
		}

		/// <summary>Cube-face index 0..5 — matches the ordering in <see cref="T:Mirage.VirtualTexture.TileCache" />'s page-table layout (Xp, Xn, Yp, Yn, Zp, Zn).</summary>
		// Token: 0x04000155 RID: 341
		public readonly int Face;

		/// <summary>Raw face-UV south-west corner X (PRE CorrectFaceUV). Range [0, 1].</summary>
		// Token: 0x04000156 RID: 342
		public readonly double UvSwX;

		/// <summary>Raw face-UV south-west corner Y (PRE CorrectFaceUV). Range [0, 1].</summary>
		// Token: 0x04000157 RID: 343
		public readonly double UvSwY;

		/// <summary>PQS subdivision level for this quad. Mirage clamps to <see cref="F:Mirage.VirtualTexture.VirtualTextureConfig.webMaxLevel" />.</summary>
		// Token: 0x04000158 RID: 344
		public readonly int Subdivision;
	}
}
