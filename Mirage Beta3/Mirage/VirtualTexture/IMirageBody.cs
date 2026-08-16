using System;
using System.Collections.Generic;

namespace Mirage.VirtualTexture
{
	/// <summary>One body the streamer works on: its tile caches and visible leaf quads.</summary>
	// Token: 0x02000047 RID: 71
	public interface IMirageBody
	{
		// Token: 0x1700004F RID: 79
		// (get) Token: 0x060001CA RID: 458
		VirtualTextureConfig Config { get; }

		/// <summary>Unified cache driving up to three payload atlases, or null if no layers.</summary>
		// Token: 0x17000050 RID: 80
		// (get) Token: 0x060001CB RID: 459
		TileCache Cache { get; }

		/// <summary>Deepest level this body streams (per body, not per config — surface and scaled differ).</summary>
		// Token: 0x17000051 RID: 81
		// (get) Token: 0x060001CC RID: 460
		int StreamingMaxLevel { get; }

		/// <summary>Append visible leaf quads to output (cleared by Mirage). Only non-subdivided leaves.</summary>
		// Token: 0x060001CD RID: 461
		void EnumerateVisibleLeafQuads(List<LeafQuad> output);

		/// <summary>Bumped when the leaf set is recomputed; only inequality is meaningful.</summary>
		// Token: 0x17000052 RID: 82
		// (get) Token: 0x060001CE RID: 462
		int LeafSetVersion { get; }

		/// <summary>Projection context for this frame, or false when no camera is available.</summary>
		// Token: 0x060001CF RID: 463
		bool TryGetLevelContext(out VTLevelContext ctx);
	}
}
