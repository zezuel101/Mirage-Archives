using System;

namespace Mirage.WebIngest
{
	/// <summary>Decoded RGB24 image: tightly packed, 3 bytes per pixel, row-major, top-left origin.</summary>
	// Token: 0x0200001D RID: 29
	public readonly struct DecodedRgbTile
	{
		// Token: 0x0600009F RID: 159 RVA: 0x00005E16 File Offset: 0x00004016
		public DecodedRgbTile(byte[] rgb, int width, int height)
		{
			this.Rgb = rgb;
			this.Width = width;
			this.Height = height;
		}

		// Token: 0x0400008C RID: 140
		public readonly byte[] Rgb;

		// Token: 0x0400008D RID: 141
		public readonly int Width;

		// Token: 0x0400008E RID: 142
		public readonly int Height;
	}
}
