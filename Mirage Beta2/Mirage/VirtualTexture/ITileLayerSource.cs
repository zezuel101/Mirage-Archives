using System;

namespace Mirage.VirtualTexture
{
	/// <summary>
	/// A per-layer source of tiles for one payload atlas (color / height / normal). This is the seam that
	/// lets the streamer and the coarse bootstrap read from EITHER the legacy loose <c>.dds</c> pyramid
	/// (<see cref="T:Mirage.VirtualTexture.LooseFileTileSource" />) or the new binary archive (<see cref="T:Mirage.VirtualTexture.ArchiveTileLayerSource" />)
	/// without either caller knowing which. It replaces the three direct
	/// <c>MirageTileMath.TilePath</c> + <c>TextureLoader.LoadTexture</c> / <c>TextureExists</c> call sites.
	///
	/// Tile coordinates are always in CORRECTED UV space (the streamer applies the per-face rotation before
	/// calling), matching the on-disk / in-archive layout.
	/// </summary>
	// Token: 0x0200003E RID: 62
	public interface ITileLayerSource : IDisposable
	{
		/// <summary>Colour space the atlas for this layer must use (sRGB vs linear).</summary>
		// Token: 0x17000048 RID: 72
		// (get) Token: 0x0600018E RID: 398
		bool Linear { get; }

		/// <summary>O(1) "does this tile exist in this source?" — the design's <c>residentOnDisk</c> check.
		/// Used by the coarse bootstrap; the streaming path skips it (a miss surfaces as a faulted load).</summary>
		// Token: 0x0600018F RID: 399
		bool Exists(int face, int level, int tx, int ty);

		/// <summary>Begin an async load of one tile. Never blocks. A tile that does not exist returns a handle
		/// that is immediately complete + faulted (so it routes to knownMissing, same as a loose load error).</summary>
		// Token: 0x06000190 RID: 400
		TileReadHandle BeginLoad(int face, int level, int tx, int ty);
	}
}
