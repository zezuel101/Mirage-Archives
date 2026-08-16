using System;
using System.Collections.Generic;
using Mirage.VirtualTexture;
using Unity.Profiling;
using UnityEngine;

namespace Mirage.Runtime
{
	/// <summary>Caches visible leaf quads of one PQS sphere, rebuilding only when stale.</summary>
	// Token: 0x02000070 RID: 112
	internal sealed class PqsLeafCache : IDisposable
	{
		// Token: 0x06000352 RID: 850 RVA: 0x00019790 File Offset: 0x00017990
		public PqsLeafCache(PQS pqs)
		{
			this.pqs = pqs;
			this.visibilityHandler = new PQ.QuadDelegate(this.OnQuadVisibilityChanged);
		}

		/// <summary>Incremented on every rebuild.</summary>
		// Token: 0x17000086 RID: 134
		// (get) Token: 0x06000353 RID: 851 RVA: 0x00019808 File Offset: 0x00017A08
		// (set) Token: 0x06000354 RID: 852 RVA: 0x00019810 File Offset: 0x00017A10
		public int Version { get; private set; }

		/// <summary>Append every currently-visible leaf to <paramref name="output" />.</summary>
		// Token: 0x06000355 RID: 853 RVA: 0x0001981C File Offset: 0x00017A1C
		public void EnumerateInto(List<LeafQuad> output)
		{
			PQS pqs = this.pqs;
			PQ[] roots = (pqs != null) ? pqs.quads : null;
			bool flag = roots == null;
			if (!flag)
			{
				bool flag2 = this.NeedsRebuild();
				if (flag2)
				{
					this.Rebuild(roots);
				}
				using (PqsLeafCache.s_LeafCopyMarker.Auto())
				{
					output.AddRange(this.cachedLeaves);
				}
			}
		}

		// Token: 0x06000356 RID: 854 RVA: 0x00019898 File Offset: 0x00017A98
		public void Dispose()
		{
			foreach (PQ quad in this.subscribedQuads)
			{
				bool flag = quad == null;
				if (!flag)
				{
					PQ pq = quad;
					pq.onVisible = (PQ.QuadDelegate)Delegate.Remove(pq.onVisible, this.visibilityHandler);
					PQ pq2 = quad;
					pq2.onInvisible = (PQ.QuadDelegate)Delegate.Remove(pq2.onInvisible, this.visibilityHandler);
				}
			}
			this.subscribedQuads.Clear();
		}

		// Token: 0x06000357 RID: 855 RVA: 0x00019938 File Offset: 0x00017B38
		private bool NeedsRebuild()
		{
			return this.leavesDirty || this.pqs.pqID != this.lastQuadCreateId || PqsLeafCache.LiveQuadCount != this.lastQuadLiveCount || Time.frameCount - this.lastWalkFrame >= 30;
		}

		// Token: 0x17000087 RID: 135
		// (get) Token: 0x06000358 RID: 856 RVA: 0x00019978 File Offset: 0x00017B78
		private static int LiveQuadCount
		{
			get
			{
				return (PQSCache.Instance != null) ? PQSCache.Instance.cachePQAssignedCount : 0;
			}
		}

		/// <summary>Walk the whole tree and replace the cached leaf set.</summary>
		// Token: 0x06000359 RID: 857 RVA: 0x00019994 File Offset: 0x00017B94
		private void Rebuild(PQ[] roots)
		{
			using (PqsLeafCache.s_PqsWalkMarker.Auto())
			{
				this.cachedLeaves.Clear();
				this.walkMaxQuadId = this.maxSubscribedQuadId;
				foreach (PQ root in roots)
				{
					bool flag = root != null;
					if (flag)
					{
						this.CollectVisibleLeaves(root, this.cachedLeaves);
					}
				}
				this.maxSubscribedQuadId = this.walkMaxQuadId;
			}
			this.leavesDirty = false;
			this.lastQuadCreateId = this.pqs.pqID;
			this.lastQuadLiveCount = PqsLeafCache.LiveQuadCount;
			this.lastWalkFrame = Time.frameCount;
			int version = this.Version;
			this.Version = version + 1;
		}

		// Token: 0x0600035A RID: 858 RVA: 0x00019A6C File Offset: 0x00017C6C
		private void CollectVisibleLeaves(PQ quad, List<LeafQuad> output)
		{
			int id = quad.id;
			bool flag = id > this.walkMaxQuadId;
			if (flag)
			{
				this.walkMaxQuadId = id;
			}
			bool flag2 = id > this.maxSubscribedQuadId;
			if (flag2)
			{
				this.Subscribe(quad);
			}
			bool isVisible = quad.isVisible;
			if (isVisible)
			{
				output.Add(new LeafQuad(quad.plane, (double)quad.uvSW.x, (double)quad.uvSW.y, quad.subdivision));
			}
			else
			{
				bool flag3 = !quad.isSubdivided;
				if (!flag3)
				{
					PQ[] subs = quad.subNodes;
					bool flag4 = subs == null;
					if (!flag4)
					{
						foreach (PQ sub in subs)
						{
							bool flag5 = sub != null;
							if (flag5)
							{
								this.CollectVisibleLeaves(sub, output);
							}
						}
					}
				}
			}
		}

		// Token: 0x0600035B RID: 859 RVA: 0x00019B44 File Offset: 0x00017D44
		private void Subscribe(PQ quad)
		{
			bool flag = !this.subscribedQuads.Add(quad);
			if (!flag)
			{
				quad.onVisible = (PQ.QuadDelegate)Delegate.Combine(quad.onVisible, this.visibilityHandler);
				quad.onInvisible = (PQ.QuadDelegate)Delegate.Combine(quad.onInvisible, this.visibilityHandler);
			}
		}

		// Token: 0x0600035C RID: 860 RVA: 0x00019B9F File Offset: 0x00017D9F
		private void OnQuadVisibilityChanged(PQ _)
		{
			this.leavesDirty = true;
		}

		// Token: 0x0400032D RID: 813
		private const int MaxFramesBetweenRebuilds = 30;

		// Token: 0x0400032E RID: 814
		private static readonly ProfilerMarker s_PqsWalkMarker = new ProfilerMarker("Mirage.VT.Leaves.PqsWalk");

		// Token: 0x0400032F RID: 815
		private static readonly ProfilerMarker s_LeafCopyMarker = new ProfilerMarker("Mirage.VT.Leaves.PqsCopyCached");

		// Token: 0x04000330 RID: 816
		private readonly PQS pqs;

		// Token: 0x04000331 RID: 817
		private readonly List<LeafQuad> cachedLeaves = new List<LeafQuad>(1024);

		// Token: 0x04000332 RID: 818
		private readonly HashSet<PQ> subscribedQuads = new HashSet<PQ>();

		// Token: 0x04000333 RID: 819
		private readonly PQ.QuadDelegate visibilityHandler;

		// Token: 0x04000334 RID: 820
		private bool leavesDirty = true;

		// Token: 0x04000335 RID: 821
		private int lastQuadCreateId = int.MinValue;

		// Token: 0x04000336 RID: 822
		private int lastQuadLiveCount = int.MinValue;

		// Token: 0x04000337 RID: 823
		private int lastWalkFrame = int.MinValue;

		// Token: 0x04000338 RID: 824
		private int maxSubscribedQuadId = -1;

		// Token: 0x04000339 RID: 825
		private int walkMaxQuadId;
	}
}
