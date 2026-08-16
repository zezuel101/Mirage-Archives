using System;
using UnityEngine;

namespace Mirage.VirtualTexture
{
	/// <summary>A pending single-tile load. Poll, GetTexture once complete, always Dispose.</summary>
	// Token: 0x0200004B RID: 75
	public abstract class TileReadHandle
	{
		// Token: 0x17000054 RID: 84
		// (get) Token: 0x060001D5 RID: 469
		public abstract bool IsComplete { get; }

		// Token: 0x17000055 RID: 85
		// (get) Token: 0x060001D6 RID: 470
		public abstract bool IsFaulted { get; }

		// Token: 0x060001D7 RID: 471
		public abstract Texture2D GetTexture();

		/// <summary>Reclaims the texture unconditionally — even if GetTexture was never called.</summary>
		// Token: 0x060001D8 RID: 472
		public abstract void Dispose();
	}
}
