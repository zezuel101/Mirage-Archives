using System;

namespace Mirage.VirtualTexture
{
	/// <summary>Numeric texture-format codes. These are exactly Unity's
	/// <c>TextureFormat</c> enum values, stored raw so this file needn't reference
	/// UnityEngine; the runtime casts straight to <c>TextureFormat</c> /
	/// <c>ExtendedTextureFormat</c>. Only the formats Mirage tiles actually use are
	/// named here.</summary>
	// Token: 0x02000043 RID: 67
	public static class ArchiveTextureFormat
	{
		// Token: 0x04000165 RID: 357
		public const int RGBA32 = 4;

		// Token: 0x04000166 RID: 358
		public const int R16 = 9;

		// Token: 0x04000167 RID: 359
		public const int DXT1 = 10;

		// Token: 0x04000168 RID: 360
		public const int DXT5 = 12;

		// Token: 0x04000169 RID: 361
		public const int BC6H = 24;

		// Token: 0x0400016A RID: 362
		public const int BC7 = 25;

		// Token: 0x0400016B RID: 363
		public const int BC4 = 26;

		// Token: 0x0400016C RID: 364
		public const int BC5 = 27;
	}
}
