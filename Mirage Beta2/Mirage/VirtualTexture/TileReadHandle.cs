using System;
using UnityEngine;

namespace Mirage.VirtualTexture
{
	/// <summary>
	/// A pending single-tile load, unifying KSPTextureLoader's two async result types
	/// (<c>TextureHandle&lt;Texture2D&gt;</c> for loose files, <c>TextureLoadTask&lt;Texture2D&gt;</c> for the
	/// archive's offset reads). The consumer polls <see cref="P:Mirage.VirtualTexture.TileReadHandle.IsComplete" /> / <see cref="P:Mirage.VirtualTexture.TileReadHandle.IsFaulted" />, calls
	/// <see cref="M:Mirage.VirtualTexture.TileReadHandle.GetTexture" /> once complete (may throw on failure), then always <see cref="M:Mirage.VirtualTexture.TileReadHandle.Dispose" />s.
	/// </summary>
	// Token: 0x0200003F RID: 63
	public abstract class TileReadHandle
	{
		/// <summary>The load has finished (successfully or not).</summary>
		// Token: 0x17000049 RID: 73
		// (get) Token: 0x06000191 RID: 401
		public abstract bool IsComplete { get; }

		/// <summary>Known-failed. May be true before completion (a fast-fail path); when it is not known early
		/// (the archive task has no error flag), failure surfaces only as a throw from <see cref="M:Mirage.VirtualTexture.TileReadHandle.GetTexture" />.</summary>
		// Token: 0x1700004A RID: 74
		// (get) Token: 0x06000192 RID: 402
		public abstract bool IsFaulted { get; }

		/// <summary>The loaded texture. Call only once <see cref="P:Mirage.VirtualTexture.TileReadHandle.IsComplete" />. Throws on load failure.</summary>
		// Token: 0x06000193 RID: 403
		public abstract Texture2D GetTexture();

		/// <summary>Release the handle. For owned (archive) textures this destroys the texture, so call it only
		/// after the tile has been copied into the atlas.</summary>
		// Token: 0x06000194 RID: 404
		public abstract void Dispose();
	}
}
