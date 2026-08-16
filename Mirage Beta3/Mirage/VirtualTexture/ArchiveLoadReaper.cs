using System;
using System.Collections.Generic;
using KSPTextureLoader;
using Unity.Collections;
using UnityEngine;

namespace Mirage.VirtualTexture
{
	/// <summary>Holds in-flight archive loads dropped mid-read until they complete and can be freed.</summary>
	// Token: 0x02000036 RID: 54
	internal static class ArchiveLoadReaper
	{
		// Token: 0x06000151 RID: 337 RVA: 0x0000ABB8 File Offset: 0x00008DB8
		public static void Park(TextureLoadTask<Texture2D> task, NativeArray<byte> data)
		{
			bool flag = task == null;
			if (flag)
			{
				bool isCreated = data.IsCreated;
				if (isCreated)
				{
					data.Dispose();
				}
			}
			else
			{
				ArchiveLoadReaper.s_Orphans.Add(new ArchiveLoadReaper.Orphan(task, data));
			}
		}

		/// <summary>Reclaim every abandoned load that has since completed. Main thread only.</summary>
		// Token: 0x06000152 RID: 338 RVA: 0x0000ABF8 File Offset: 0x00008DF8
		public static void Reap()
		{
			for (int i = ArchiveLoadReaper.s_Orphans.Count - 1; i >= 0; i--)
			{
				ArchiveLoadReaper.Orphan orphan = ArchiveLoadReaper.s_Orphans[i];
				bool flag = !orphan.task.IsComplete;
				if (!flag)
				{
					Texture2D result = null;
					ArchiveLoadReaper.DestroyCompleted(orphan.task, ref result);
					bool isCreated = orphan.data.IsCreated;
					if (isCreated)
					{
						orphan.data.Dispose();
					}
					ArchiveLoadReaper.s_Orphans.RemoveAt(i);
				}
			}
		}

		// Token: 0x06000153 RID: 339 RVA: 0x0000AC8C File Offset: 0x00008E8C
		public static void DestroyCompleted(TextureLoadTask<Texture2D> task, ref Texture2D result)
		{
			bool flag = result == null;
			if (flag)
			{
				try
				{
					result = task.GetTexture();
				}
				catch
				{
					return;
				}
			}
			bool flag2 = result == null;
			if (!flag2)
			{
				Object.Destroy(result);
				result = null;
			}
		}

		// Token: 0x04000111 RID: 273
		private static readonly List<ArchiveLoadReaper.Orphan> s_Orphans = new List<ArchiveLoadReaper.Orphan>();

		// Token: 0x020000C9 RID: 201
		private readonly struct Orphan
		{
			// Token: 0x060004AB RID: 1195 RVA: 0x00021D8C File Offset: 0x0001FF8C
			public Orphan(TextureLoadTask<Texture2D> task, NativeArray<byte> data)
			{
				this.task = task;
				this.data = data;
			}

			// Token: 0x04000562 RID: 1378
			public readonly TextureLoadTask<Texture2D> task;

			// Token: 0x04000563 RID: 1379
			public readonly NativeArray<byte> data;
		}
	}
}
