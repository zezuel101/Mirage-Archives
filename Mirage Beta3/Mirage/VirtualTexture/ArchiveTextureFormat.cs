using System;

namespace Mirage.VirtualTexture
{
	/// <summary>Numeric texture-format codes, stored raw in the container.</summary>
	// Token: 0x0200003B RID: 59
	public static class ArchiveTextureFormat
	{
		// Token: 0x04000124 RID: 292
		public const int RGBA32 = 4;

		// Token: 0x04000125 RID: 293
		public const int R16 = 9;

		// Token: 0x04000126 RID: 294
		public const int DXT1 = 10;

		// Token: 0x04000127 RID: 295
		public const int DXT5 = 12;

		// Token: 0x04000128 RID: 296
		public const int BC6H = 24;

		// Token: 0x04000129 RID: 297
		public const int BC7 = 25;

		// Token: 0x0400012A RID: 298
		public const int BC4 = 26;

		// Token: 0x0400012B RID: 299
		public const int BC5 = 27;
	}
}
