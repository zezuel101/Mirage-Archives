using System;

namespace Mirage.VirtualTexture
{
	/// <summary>Immutable descent state, passed by ref to avoid per-level copies.</summary>
	// Token: 0x02000059 RID: 89
	internal readonly struct DescentContext
	{
		/// <summary>Immutable descent state, passed by ref to avoid per-level copies.</summary>
		// Token: 0x0600029B RID: 667 RVA: 0x000149A7 File Offset: 0x00012BA7
		public DescentContext(VTLevelContext ctx, int maxLevel, int tileSize, int borderPx, float projScale)
		{
			this.Ctx = ctx;
			this.MaxLevel = maxLevel;
			this.TileSize = tileSize;
			this.BorderPx = borderPx;
			this.ProjScale = projScale;
		}

		// Token: 0x0400025C RID: 604
		public readonly VTLevelContext Ctx;

		// Token: 0x0400025D RID: 605
		public readonly int MaxLevel;

		// Token: 0x0400025E RID: 606
		public readonly int TileSize;

		// Token: 0x0400025F RID: 607
		public readonly int BorderPx;

		// Token: 0x04000260 RID: 608
		public readonly float ProjScale;
	}
}
