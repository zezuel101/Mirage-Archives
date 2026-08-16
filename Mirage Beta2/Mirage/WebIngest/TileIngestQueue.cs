using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Mirage.VirtualTexture;

namespace Mirage.WebIngest
{
	/// <summary>
	/// The ingest state machine (WebIngest §7). Ingest is driven from the <b>required-tile walk</b>, never from a
	/// loader miss — that coupling is the trap the doc calls out: a not-yet-baked tile faults, `DrainInFlight`
	/// files it under `knownMissing`, and the periodic reset then re-requests it mid-bake, producing re-error →
	/// re-blacklist thrash and duplicate ingests. Here the local loader is simply never asked for a tile that
	/// isn't on disk, so `knownMissing` stays reserved for genuinely corrupt LOCAL tiles, as designed.
	///
	/// <b>Deviation from the doc, deliberately: there is no <c>residentOnDisk</c> HashSet.</b> §7 keeps one to
	/// dodge a per-frame <c>File.Exists</c> syscall per tile — a real cost when tiles were loose DDS files. They
	/// aren't any more: the archive merges its <c>.idx</c> files into an in-RAM map at load, so
	/// <c>ITileLayerSource.Exists</c> is already the O(1) dictionary lookup the HashSet was there to provide, and
	/// the web tier's index is updated in the same breath as its blob. A second copy of that set could only drift
	/// from it. (Same shape as §7.1's "one cache, not two" — a GeoStream assumption that doesn't transfer.)
	///
	/// <b>There is also no persistent pending queue</b>, and that is a simplification, not an omission. §7 wants
	/// the queue bounded and ordered "nearest / most-visible first, or we bake tiles the camera already passed" —
	/// but the required set is rebuilt from scratch every frame, so a key that doesn't start this frame is simply
	/// re-offered next frame if it is still wanted, and silently forgotten if it isn't. Staleness is designed out
	/// rather than evicted: the queue cannot contain a tile the camera has passed, because it contains nothing at
	/// all between frames. The caller offers keys in priority order and the cap does the rest.
	/// </summary>
	// Token: 0x0200002F RID: 47
	public sealed class TileIngestQueue
	{
		// Token: 0x17000028 RID: 40
		// (get) Token: 0x060000F5 RID: 245 RVA: 0x00009200 File Offset: 0x00007400
		public int InProgress
		{
			get
			{
				return this.inProgress.Count;
			}
		}

		/// <summary>Room to start another bake. Callers must loop on THIS rather than on
		/// <see cref="M:Mirage.WebIngest.TileIngestQueue.TryRequest(System.Int32,System.Int32,System.Int32,System.Int32,System.Int32)" />'s return value: a false from TryRequest means "cap full" OR "this key is
		/// blocked", and stopping the frame's offers on the second kind lets one permanently-uncoverable tile at
		/// the head of the queue starve everything behind it — forever, since the queue is rebuilt in the same
		/// order every frame.</summary>
		// Token: 0x17000029 RID: 41
		// (get) Token: 0x060000F6 RID: 246 RVA: 0x0000920D File Offset: 0x0000740D
		public bool HasCapacity
		{
			get
			{
				return this.inProgress.Count < this.maxConcurrent;
			}
		}

		// Token: 0x1700002A RID: 42
		// (get) Token: 0x060000F7 RID: 247 RVA: 0x00009222 File Offset: 0x00007422
		public int NoCoverageCount
		{
			get
			{
				return this.noCoverage.Count;
			}
		}

		// Token: 0x1700002B RID: 43
		// (get) Token: 0x060000F8 RID: 248 RVA: 0x0000922F File Offset: 0x0000742F
		public int FailedCount
		{
			get
			{
				return this.failedAt.Count;
			}
		}

		// Token: 0x1700002C RID: 44
		// (get) Token: 0x060000F9 RID: 249 RVA: 0x0000923C File Offset: 0x0000743C
		// (set) Token: 0x060000FA RID: 250 RVA: 0x00009244 File Offset: 0x00007444
		public int CommittedCount { get; private set; }

		// Token: 0x060000FB RID: 251 RVA: 0x00009250 File Offset: 0x00007450
		public TileIngestQueue(ITileBaker baker, int maxConcurrent = 2)
		{
			if (baker == null)
			{
				throw new ArgumentNullException("baker");
			}
			this.baker = baker;
			this.maxConcurrent = Math.Max(1, maxConcurrent);
		}

		/// <summary>True if this key is already baking, known uncoverable, or in its retry backoff — i.e. the
		/// caller must NOT queue a local load for it, but also must not request an ingest.</summary>
		// Token: 0x060000FC RID: 252 RVA: 0x000092BF File Offset: 0x000074BF
		public bool IsBlocked(ulong key, int frame)
		{
			return this.inProgress.Contains(key) || this.noCoverage.Contains(key) || this.InBackoff(key, frame);
		}

		// Token: 0x060000FD RID: 253 RVA: 0x000092E8 File Offset: 0x000074E8
		private bool InBackoff(ulong key, int frame)
		{
			int f;
			return this.failedAt.TryGetValue(key, out f) && frame - f < 600;
		}

		/// <summary>
		/// Offer a key for ingest. Returns true if a bake was started. Offers should arrive in priority order
		/// (coarsest / nearest first) — once the concurrency cap is reached this returns false for the rest of
		/// the frame, so whatever is offered first wins the slots.
		/// </summary>
		// Token: 0x060000FE RID: 254 RVA: 0x00009314 File Offset: 0x00007514
		public bool TryRequest(int face, int level, int tx, int ty, int frame)
		{
			TileIngestQueue.<>c__DisplayClass23_0 CS$<>8__locals1 = new TileIngestQueue.<>c__DisplayClass23_0();
			CS$<>8__locals1.<>4__this = this;
			CS$<>8__locals1.face = face;
			CS$<>8__locals1.level = level;
			CS$<>8__locals1.tx = tx;
			CS$<>8__locals1.ty = ty;
			CS$<>8__locals1.key = MirageArchiveFormat.PackKey(CS$<>8__locals1.face, CS$<>8__locals1.level, CS$<>8__locals1.tx, CS$<>8__locals1.ty);
			bool flag = this.IsBlocked(CS$<>8__locals1.key, frame) || this.inProgress.Count >= this.maxConcurrent;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				this.failedAt.Remove(CS$<>8__locals1.key);
				this.inProgress.Add(CS$<>8__locals1.key);
				Task.Run(delegate()
				{
					TileIngestQueue.<>c__DisplayClass23_0.<<TryRequest>b__0>d <<TryRequest>b__0>d = new TileIngestQueue.<>c__DisplayClass23_0.<<TryRequest>b__0>d();
					<<TryRequest>b__0>d.<>t__builder = AsyncTaskMethodBuilder.Create();
					<<TryRequest>b__0>d.<>4__this = CS$<>8__locals1;
					<<TryRequest>b__0>d.<>1__state = -1;
					<<TryRequest>b__0>d.<>t__builder.Start<TileIngestQueue.<>c__DisplayClass23_0.<<TryRequest>b__0>d>(ref <<TryRequest>b__0>d);
					return <<TryRequest>b__0>d.<>t__builder.Task;
				}, this.cts.Token);
				result = true;
			}
			return result;
		}

		/// <summary>
		/// Main-thread drain: hand each finished bake to <paramref name="commit" /> (which appends to the web
		/// archives) and release its in-progress slot. <paramref name="commit" /> returns false if the write
		/// failed, which is treated as a transient failure so the tile is retried rather than lost.
		///
		/// Committing on the main thread is deliberate. The web blob is the one writable handle in the system
		/// and exactly one may exist per file (see <c>VirtualTextureConfig.GetWebArchive</c>); serialising the
		/// appends here means the archive needs no lock of its own, and the index update that follows the blob
		/// write cannot interleave with another bake's.
		/// </summary>
		// Token: 0x060000FF RID: 255 RVA: 0x000093E8 File Offset: 0x000075E8
		public int Drain(Func<BakedTile, bool> commit, int frame, int maxPerFrame = 2)
		{
			int i = 0;
			for (;;)
			{
				BakedTile t;
				bool flag = i < maxPerFrame && this.completed.TryDequeue(out t);
				if (!flag)
				{
					break;
				}
				this.inProgress.Remove(t.key);
				IngestOutcome outcome = t.outcome;
				IngestOutcome ingestOutcome = outcome;
				if (ingestOutcome != IngestOutcome.Baked)
				{
					if (ingestOutcome != IngestOutcome.NoCoverage)
					{
						this.failedAt[t.key] = frame;
					}
					else
					{
						this.noCoverage.Add(t.key);
					}
				}
				else
				{
					bool flag2 = commit(t);
					if (flag2)
					{
						int committedCount = this.CommittedCount;
						this.CommittedCount = committedCount + 1;
						this.failedAt.Remove(t.key);
						i++;
					}
					else
					{
						this.failedAt[t.key] = frame;
					}
				}
			}
			return i;
		}

		/// <summary>Cancel every in-flight bake. Their results are dropped — the queue is dead after this.</summary>
		// Token: 0x06000100 RID: 256 RVA: 0x000094C4 File Offset: 0x000076C4
		public void Shutdown()
		{
			try
			{
				this.cts.Cancel();
			}
			catch (Exception)
			{
			}
			this.inProgress.Clear();
			BakedTile bakedTile;
			while (this.completed.TryDequeue(out bakedTile))
			{
			}
		}

		// Token: 0x040000D8 RID: 216
		private readonly ITileBaker baker;

		// Token: 0x040000D9 RID: 217
		private readonly int maxConcurrent;

		// Token: 0x040000DA RID: 218
		private readonly CancellationTokenSource cts = new CancellationTokenSource();

		// Token: 0x040000DB RID: 219
		private readonly HashSet<ulong> inProgress = new HashSet<ulong>();

		// Token: 0x040000DC RID: 220
		private readonly ConcurrentQueue<BakedTile> completed = new ConcurrentQueue<BakedTile>();

		// Token: 0x040000DD RID: 221
		private readonly HashSet<ulong> noCoverage = new HashSet<ulong>();

		// Token: 0x040000DE RID: 222
		private readonly Dictionary<ulong, int> failedAt = new Dictionary<ulong, int>();

		/// <summary>~10 s at 60 fps, matching TileStreamingManager.MissingRetryInterval.</summary>
		// Token: 0x040000DF RID: 223
		public const int RetryFrames = 600;
	}
}
