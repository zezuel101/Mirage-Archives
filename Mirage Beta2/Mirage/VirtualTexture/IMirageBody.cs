using System;
using System.Collections.Generic;

namespace Mirage.VirtualTexture
{
	/// <summary>
	/// Host-side adapter that Mirage's streaming manager consumes for a single planet
	/// (or anything else that maps onto the cube-face VT layout).
	///
	/// The implementor — typically a Kopernicus / PQS-aware body wrapper in the host
	/// mod — owns the one unified tile cache (color/height/normal as lockstep layers)
	/// and knows how to enumerate the currently visible leaf quads on its sphere.
	/// Mirage drives the streaming, eviction, and page-table flushes; the host just
	/// exposes data through this interface.
	///
	/// Register a body with <see cref="M:Mirage.VirtualTexture.TileStreamingManager.RegisterBody(System.String,Mirage.VirtualTexture.IMirageBody)" />; unregister
	/// on body unload so the streaming loop stops looking at stale references.
	/// </summary>
	// Token: 0x0200003B RID: 59
	public interface IMirageBody
	{
		/// <summary>
		/// Per-body VT config (atlasSize, tileSize, webMaxLevel, tile paths). The streaming
		/// manager reads <see cref="F:Mirage.VirtualTexture.VirtualTextureConfig.webMaxLevel" /> to clamp the
		/// requested tile depth and reads the per-layer paths when queuing async loads.
		/// </summary>
		// Token: 0x17000045 RID: 69
		// (get) Token: 0x06000187 RID: 391
		VirtualTextureConfig Config { get; }

		/// <summary>
		/// The unified VT cache: one shared slot map + one indirection (page table) driving up to three
		/// parallel payload atlases (color/height/normal). Null if the body has no VT layers.
		/// </summary>
		// Token: 0x17000046 RID: 70
		// (get) Token: 0x06000188 RID: 392
		TileCache Cache { get; }

		/// <summary>
		/// Deepest level THIS body streams. Per body, not per config: the surface and scaled representations of
		/// a planet share one <see cref="T:Mirage.VirtualTexture.VirtualTextureConfig" /> but not one cap — scaled stops at
		/// <c>canonicalMaxLevel</c> unless <see cref="P:Mirage.MirageSettings.ScaledWebStreaming" /> says otherwise
		/// (<see cref="P:Mirage.VirtualTexture.VirtualTextureConfig.ScaledStreamingMaxLevel" />), while the surface follows
		/// <see cref="P:Mirage.VirtualTexture.VirtualTextureConfig.StreamingMaxLevel" />.
		/// </summary>
		// Token: 0x17000047 RID: 71
		// (get) Token: 0x06000189 RID: 393
		int StreamingMaxLevel { get; }

		/// <summary>
		/// Push every currently-visible leaf quad on this body's sphere into
		/// <paramref name="output" />. Called once per frame per registered body.
		/// Mirage clears the list before invocation; the host just appends.
		/// Skip quads that aren't on this sphere, are culled, or are subdivided
		/// (i.e. not leaves) — the streaming manager only needs leaves.
		/// </summary>
		// Token: 0x0600018A RID: 394
		void EnumerateVisibleLeafQuads(List<LeafQuad> output);

		/// <summary>
		/// Everything needed to project a tile's world extent to screen pixels, so the streamer can choose a VT
		/// level from what the eye actually resolves. Return false when this body has no camera to project
		/// against this frame; the streamer then falls back to the quad's subdivision.
		/// </summary>
		// Token: 0x0600018B RID: 395
		bool TryGetLevelContext(out VTLevelContext ctx);
	}
}
