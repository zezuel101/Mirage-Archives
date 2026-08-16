using System;
using KSPTextureLoader;
using Unity.Collections;
using UnityEngine;

namespace Mirage.VirtualTexture
{
	/// <summary>Uncompressed archive tile, seek-read and uploaded by KSPTextureLoader.</summary>
	// Token: 0x02000034 RID: 52
	internal sealed class ArchiveReadHandle : TileReadHandle
	{
		// Token: 0x06000142 RID: 322 RVA: 0x0000A7AA File Offset: 0x000089AA
		public ArchiveReadHandle(TextureLoadTask<Texture2D> task)
		{
			this.task = task;
		}

		// Token: 0x1700003D RID: 61
		// (get) Token: 0x06000143 RID: 323 RVA: 0x0000A7BA File Offset: 0x000089BA
		public override bool IsComplete
		{
			get
			{
				return this.task == null || this.task.IsComplete;
			}
		}

		// Token: 0x1700003E RID: 62
		// (get) Token: 0x06000144 RID: 324 RVA: 0x0000A7D2 File Offset: 0x000089D2
		public override bool IsFaulted
		{
			get
			{
				return this.task == null;
			}
		}

		// Token: 0x06000145 RID: 325 RVA: 0x0000A7E0 File Offset: 0x000089E0
		public override Texture2D GetTexture()
		{
			bool flag = this.task == null;
			if (flag)
			{
				throw new InvalidOperationException("ArchiveReadHandle: tile not present in archive.");
			}
			this.result = this.task.GetTexture();
			return this.result;
		}

		// Token: 0x06000146 RID: 326 RVA: 0x0000A824 File Offset: 0x00008A24
		public override void Dispose()
		{
			bool flag = this.task == null;
			if (!flag)
			{
				bool isComplete = this.task.IsComplete;
				if (isComplete)
				{
					ArchiveLoadReaper.DestroyCompleted(this.task, ref this.result);
				}
				else
				{
					ArchiveLoadReaper.Park(this.task, default(NativeArray<byte>));
				}
			}
		}

		// Token: 0x04000108 RID: 264
		public static readonly ArchiveReadHandle Missing = new ArchiveReadHandle(null);

		// Token: 0x04000109 RID: 265
		private readonly TextureLoadTask<Texture2D> task;

		// Token: 0x0400010A RID: 266
		private Texture2D result;
	}
}
