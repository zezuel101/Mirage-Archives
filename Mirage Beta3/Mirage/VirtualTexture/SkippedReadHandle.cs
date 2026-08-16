using System;
using UnityEngine;

namespace Mirage.VirtualTexture
{
	/// <summary>Stands in for a layer that does not reach a tile's level, so nothing was
	/// loaded.</summary>
	// Token: 0x0200004C RID: 76
	public sealed class SkippedReadHandle : TileReadHandle
	{
		// Token: 0x060001DA RID: 474 RVA: 0x0000DE11 File Offset: 0x0000C011
		private SkippedReadHandle()
		{
		}

		// Token: 0x17000056 RID: 86
		// (get) Token: 0x060001DB RID: 475 RVA: 0x0000DE1B File Offset: 0x0000C01B
		public override bool IsComplete
		{
			get
			{
				return true;
			}
		}

		// Token: 0x17000057 RID: 87
		// (get) Token: 0x060001DC RID: 476 RVA: 0x0000DE1E File Offset: 0x0000C01E
		public override bool IsFaulted
		{
			get
			{
				return false;
			}
		}

		// Token: 0x060001DD RID: 477 RVA: 0x0000DE21 File Offset: 0x0000C021
		public override Texture2D GetTexture()
		{
			return null;
		}

		// Token: 0x060001DE RID: 478 RVA: 0x0000DE24 File Offset: 0x0000C024
		public override void Dispose()
		{
		}

		// Token: 0x04000176 RID: 374
		public static readonly SkippedReadHandle Instance = new SkippedReadHandle();
	}
}
