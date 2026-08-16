using System;

namespace Mirage.VirtualTexture
{
	/// <summary>Payload layer. Mirrors <c>VTLayer</c> in the runtime (kept separate so
	/// this file stays Unity-free).</summary>
	// Token: 0x02000041 RID: 65
	public enum ArchiveLayer : byte
	{
		// Token: 0x0400015C RID: 348
		Color,
		// Token: 0x0400015D RID: 349
		Height,
		// Token: 0x0400015E RID: 350
		Normal
	}
}
