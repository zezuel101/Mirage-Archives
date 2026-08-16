using System;

namespace Mirage.VirtualTexture
{
	/// <summary>Payload layer. Mirrors <c>VTLayer</c> in the runtime; kept separate to stay Unity-free.</summary>
	// Token: 0x02000039 RID: 57
	public enum ArchiveLayer : byte
	{
		// Token: 0x0400011A RID: 282
		Color,
		// Token: 0x0400011B RID: 283
		Height,
		// Token: 0x0400011C RID: 284
		Normal,
		// Token: 0x0400011D RID: 285
		Emissive
	}
}
