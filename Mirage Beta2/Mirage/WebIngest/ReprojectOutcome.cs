using System;

namespace Mirage.WebIngest
{
	/// <summary>Outcome of trying to bake one cube tile from mercator sources.</summary>
	// Token: 0x02000014 RID: 20
	public enum ReprojectOutcome
	{
		/// <summary>Every output texel resolved to real source imagery.</summary>
		// Token: 0x0400007C RID: 124
		Complete,
		/// <summary>At least one output texel had no source. The tile is NOT baked — §4: "the baker must never
		/// write an empty/placeholder tile" (blank tiles cached as real imagery was one of GeoStream's real
		/// bugs). The VT indirection falls back to a coarser resident ancestor by itself, which is the correct
		/// result and needs no extra machinery.</summary>
		// Token: 0x0400007D RID: 125
		Incomplete
	}
}
