using System;

namespace Mirage.WebIngest
{
	/// <summary>Decoded RGB24 image: tightly packed, 3 bytes per pixel, row-major, top-left origin.</summary>
	// Token: 0x0200000A RID: 10
	public readonly struct DecodedRgbTile
	{
		// Token: 0x0600004C RID: 76 RVA: 0x00002DA0 File Offset: 0x00000FA0
		public DecodedRgbTile(byte[] rgb, int width, int height)
		{
			this.Rgb = rgb;
			this.Width = width;
			this.Height = height;
		}

		// Token: 0x04000037 RID: 55
		public readonly byte[] Rgb;

		// Token: 0x04000038 RID: 56
		public readonly int Width;

		// Token: 0x04000039 RID: 57
		public readonly int Height;
	}
}
