using System;
using System.Collections.Generic;
using Mirage.VirtualTexture;

namespace Mirage.WebIngest
{
	/// <summary>
	/// Bounds what bake-as-you-fly costs the user's disk (WebIngest §8: "a flown-over cube pyramid grows without
	/// limit — their 64k Earth was ~10 GB").
	///
	/// <b>Eviction is all-layers-or-nothing, and that is the whole design.</b> The cache is lockstep: a tile is
	/// resident only when every present layer landed (§4.4). So dropping a key from the colour tier but leaving
	/// it in height does not free a third of the tile — it creates a key that can never load again, faults its
	/// group forever, and whose height bytes are now unreachable garbage. Every evict here therefore hits every
	/// tier, and the key either exists everywhere or nowhere.
	///
	/// <b>Physical and live bytes are different numbers and both matter.</b> Eviction only tombstones; the bytes
	/// come back at <see cref="M:Mirage.VirtualTexture.WebTileArchive.Compact" />. So the cap is *triggered* on physical (what the drive
	/// actually gives up — a live-bytes trigger would let the file grow while the budget reported itself
	/// healthy), but eviction *converges* on live, because live is the only thing eviction can move. Measuring
	/// eviction's progress against physical instead makes the loop believe it is achieving nothing, evict every
	/// last tile, and still declare failure. Compaction is what turns the freed live bytes back into disk.
	///
	/// <b>Recently-used tiles are never evicted</b> (<see cref="F:Mirage.WebIngest.WebDiskBudget.MinAgeFrames" />). Without that floor, a working
	/// set larger than the cap evicts a tile the walk still wants, re-ingests it (a dozen HTTPS fetches, a
	/// reproject, a BC7 encode), evicts it again — a thrash that is far worse than simply being over cap. Hitting
	/// the floor means the cap is too small for the view, which is reported rather than papered over.
	///
	/// Unity-free so the policy can be tested offline against a real archive rather than only by filling a disk.
	/// </summary>
	// Token: 0x02000030 RID: 48
	public sealed class WebDiskBudget
	{
		// Token: 0x1700002D RID: 45
		// (get) Token: 0x06000101 RID: 257 RVA: 0x00009518 File Offset: 0x00007718
		public long CapBytes { get; }

		// Token: 0x1700002E RID: 46
		// (get) Token: 0x06000102 RID: 258 RVA: 0x00009520 File Offset: 0x00007720
		public int TrackedKeys
		{
			get
			{
				return this.lastUsed.Count;
			}
		}

		// Token: 0x1700002F RID: 47
		// (get) Token: 0x06000103 RID: 259 RVA: 0x0000952D File Offset: 0x0000772D
		// (set) Token: 0x06000104 RID: 260 RVA: 0x00009535 File Offset: 0x00007735
		public int TotalEvicted { get; private set; }

		// Token: 0x17000030 RID: 48
		// (get) Token: 0x06000105 RID: 261 RVA: 0x0000953E File Offset: 0x0000773E
		// (set) Token: 0x06000106 RID: 262 RVA: 0x00009546 File Offset: 0x00007746
		public int TotalCompactions { get; private set; }

		// Token: 0x17000031 RID: 49
		// (get) Token: 0x06000107 RID: 263 RVA: 0x0000954F File Offset: 0x0000774F
		// (set) Token: 0x06000108 RID: 264 RVA: 0x00009557 File Offset: 0x00007757
		public bool CapTooSmall { get; private set; }

		// Token: 0x06000109 RID: 265 RVA: 0x00009560 File Offset: 0x00007760
		public WebDiskBudget(long capBytes)
		{
			this.CapBytes = ((capBytes > 0L) ? capBytes : 4294967296L);
		}

		/// <summary>Seed from what previous sessions baked. They all start maximally stale (frame 0), so they are
		/// the first to go — and a key you actually revisit gets touched by the walk and protected before the cap
		/// ever bites. The alternative (trusting an unknown key) would pin a whole previous session's pyramid.</summary>
		// Token: 0x0600010A RID: 266 RVA: 0x000095B0 File Offset: 0x000077B0
		public void Seed(IReadOnlyList<WebTileArchive> tiers)
		{
			foreach (WebTileArchive t in tiers)
			{
				bool flag = t == null;
				if (!flag)
				{
					foreach (ulong i in t.Keys())
					{
						bool flag2 = !this.lastUsed.ContainsKey(i);
						if (flag2)
						{
							this.lastUsed[i] = 0;
						}
					}
				}
			}
		}

		/// <summary>Mark a baked key as still wanted. Called from the required-tile walk for every required key,
		/// so it must stay a single dictionary probe — untracked keys (canonical ones, the overwhelming majority)
		/// fall straight through.</summary>
		// Token: 0x0600010B RID: 267 RVA: 0x00009664 File Offset: 0x00007864
		public void Touch(ulong key, int frame)
		{
			bool flag = this.lastUsed.ContainsKey(key);
			if (flag)
			{
				this.lastUsed[key] = frame;
			}
		}

		/// <summary>Register a freshly committed tile.</summary>
		// Token: 0x0600010C RID: 268 RVA: 0x00009690 File Offset: 0x00007890
		public void OnBaked(ulong key, int frame)
		{
			this.lastUsed[key] = frame;
		}

		// Token: 0x0600010D RID: 269 RVA: 0x000096A0 File Offset: 0x000078A0
		public long PhysicalBytes(IReadOnlyList<WebTileArchive> tiers)
		{
			long i = 0L;
			foreach (WebTileArchive t in tiers)
			{
				bool flag = t != null;
				if (flag)
				{
					i += t.PhysicalBytes;
				}
			}
			return i;
		}

		// Token: 0x0600010E RID: 270 RVA: 0x00009700 File Offset: 0x00007900
		public long LiveBytes(IReadOnlyList<WebTileArchive> tiers)
		{
			long i = 0L;
			foreach (WebTileArchive t in tiers)
			{
				bool flag = t != null;
				if (flag)
				{
					i += t.LiveBytes;
				}
			}
			return i;
		}

		/// <summary>
		/// Evict least-recently-used keys until the tiers fit the cap. Returns the number of keys evicted (each
		/// removed from EVERY tier).
		///
		/// Called every frame, but the expensive sweep (copy + sort of the whole tracked set) is gated hard.
		/// Eviction only TOMBSTONES — it moves LIVE bytes, never physical, which stays fat until a compaction. So
		/// the natural "physical &gt; cap" trigger stayed true frame after frame and re-sorted the entire tracked
		/// set forever, long after there was anything left to evict (measured: 14 ms/frame standing under
		/// TileStreamingManager.Update, plus ~a MB of List garbage per frame). Two guards below spare that work.
		/// </summary>
		// Token: 0x0600010F RID: 271 RVA: 0x00009760 File Offset: 0x00007960
		public int Enforce(IReadOnlyList<WebTileArchive> tiers, int frame)
		{
			long physical = this.PhysicalBytes(tiers);
			bool flag = physical <= this.CapBytes;
			int result;
			if (flag)
			{
				this.CapTooSmall = false;
				result = 0;
			}
			else
			{
				long live = this.LiveBytes(tiers);
				int evicted = 0;
				bool flag2 = live > this.CapBytes;
				if (flag2)
				{
					bool flag3 = frame >= this.lastEvictionFrame + 60;
					if (flag3)
					{
						this.lastEvictionFrame = frame;
						evicted = this.EvictOldest(tiers, frame, live);
					}
				}
				else
				{
					this.CapTooSmall = false;
				}
				bool flag4 = (double)physical > (double)this.CapBytes * 1.25;
				if (flag4)
				{
					MirageDebug.LogWarning(string.Format("WebDiskBudget: the baked blob is {0} MB of mostly tombstones ", physical / 1048576L) + string.Format("against a {0} MB cap. Compacting now — this will stall.", this.CapBytes / 1048576L));
					this.MaybeCompact(tiers, true);
				}
				result = evicted;
			}
			return result;
		}

		/// <summary>The eviction sweep proper: sort the tracked set oldest-first and tombstone until LIVE fits or
		/// the working set is all that remains. Split out of <see cref="M:Mirage.WebIngest.WebDiskBudget.Enforce(System.Collections.Generic.IReadOnlyList{Mirage.VirtualTexture.WebTileArchive},System.Int32)" /> so the per-frame guards there
		/// stay readable, and gated by them so this — the O(n log n) part — runs rarely.</summary>
		// Token: 0x06000110 RID: 272 RVA: 0x00009854 File Offset: 0x00007A54
		private int EvictOldest(IReadOnlyList<WebTileArchive> tiers, int frame, long live)
		{
			this.victimScratch.Clear();
			foreach (KeyValuePair<ulong, int> kv in this.lastUsed)
			{
				this.victimScratch.Add(kv);
			}
			this.victimScratch.Sort((KeyValuePair<ulong, int> a, KeyValuePair<ulong, int> b) => a.Value.CompareTo(b.Value));
			int evicted = 0;
			long projectedLive = live;
			foreach (KeyValuePair<ulong, int> v in this.victimScratch)
			{
				bool flag = projectedLive <= this.CapBytes;
				if (flag)
				{
					break;
				}
				bool flag2 = frame - v.Value < 600;
				if (flag2)
				{
					break;
				}
				long reclaim = 0L;
				bool removed = false;
				foreach (WebTileArchive t in tiers)
				{
					bool flag3 = t == null;
					if (!flag3)
					{
						IndexEntry e;
						bool flag4 = t.TryResolve(v.Key, out e);
						if (flag4)
						{
							reclaim += (long)((ulong)(e.length + 24U));
						}
						bool flag5 = t.Evict(v.Key);
						if (flag5)
						{
							removed = true;
						}
					}
				}
				this.lastUsed.Remove(v.Key);
				bool flag6 = !removed;
				if (!flag6)
				{
					evicted++;
					projectedLive -= reclaim;
				}
			}
			this.CapTooSmall = (projectedLive > this.CapBytes);
			bool capTooSmall = this.CapTooSmall;
			if (capTooSmall)
			{
				MirageDebug.LogWarning(string.Format("WebDiskBudget: the live baked set ({0} MB) exceeds the cap ", projectedLive / 1048576L) + string.Format("({0} MB) and every remaining tile is in the active working set. ", this.CapBytes / 1048576L) + "Raise webDiskCapMB — evicting these would only force an immediate re-bake.");
			}
			this.TotalEvicted += evicted;
			return evicted;
		}

		/// <summary>
		/// Reclaim tombstoned bytes if the blob is mostly holes. Rewrites entire files, so the caller must pick a
		/// moment where a stall is acceptable — a scene change or body unload — never mid-flight.
		/// </summary>
		// Token: 0x06000111 RID: 273 RVA: 0x00009A90 File Offset: 0x00007C90
		public bool MaybeCompact(IReadOnlyList<WebTileArchive> tiers, bool force = false)
		{
			long physical = this.PhysicalBytes(tiers);
			long live = this.LiveBytes(tiers);
			bool flag = !force && (double)physical < (double)live * 1.5;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				bool flag2 = physical <= live;
				if (flag2)
				{
					result = false;
				}
				else
				{
					foreach (WebTileArchive t in tiers)
					{
						bool flag3 = t == null;
						if (!flag3)
						{
							try
							{
								t.Compact();
							}
							catch (Exception e)
							{
								MirageDebug.LogError(string.Format("WebDiskBudget: compacting the {0} tier failed: {1}", t.Layer, e.Message));
							}
						}
					}
					int totalCompactions = this.TotalCompactions;
					this.TotalCompactions = totalCompactions + 1;
					MirageDebug.Log(string.Format("WebDiskBudget: compacted {0} MB -> {1} MB.", physical / 1048576L, this.LiveBytes(tiers) / 1048576L));
					result = true;
				}
			}
			return result;
		}

		/// <summary>Default cap across all layers of one body. Sol's canonical Earth archive is already tens of
		/// GB, so a few GB of baked detail is proportionate; configurable per body via <c>webDiskCapMB</c>.</summary>
		// Token: 0x040000E1 RID: 225
		public const long DefaultCapBytes = 4294967296L;

		/// <summary>~10 s at 60 fps. A tile touched this recently is part of the working set; evicting it would
		/// only buy a re-ingest.</summary>
		// Token: 0x040000E2 RID: 226
		public const int MinAgeFrames = 600;

		/// <summary>Compact once garbage exceeds a third of the blob. Compaction rewrites the entire file, so it
		/// must be rare — but leaving it too long means the physical size (what the cap sees) is mostly holes,
		/// and eviction starts dropping live tiles to make room for garbage.</summary>
		// Token: 0x040000E3 RID: 227
		public const double CompactWhenPhysicalExceedsLiveBy = 1.5;

		/// <summary>Physical size at which compaction stops waiting for a safe point and happens mid-flight,
		/// stall and all. Eviction cannot shrink the file, so without this the blob has no bound at all within
		/// a session — see <see cref="M:Mirage.WebIngest.WebDiskBudget.Enforce(System.Collections.Generic.IReadOnlyList{Mirage.VirtualTexture.WebTileArchive},System.Int32)" />.</summary>
		// Token: 0x040000E4 RID: 228
		public const double HardCeilingFactor = 1.25;

		/// <summary>Sweep the tracked set at most this often (frames) when genuinely over the LIVE cap. Nothing
		/// becomes newly evictable faster than <see cref="F:Mirage.WebIngest.WebDiskBudget.MinAgeFrames" /> (600), so a per-frame sweep could only
		/// recompute an identical answer — this bounds the sort cost in the persistent-over-cap case.</summary>
		// Token: 0x040000E5 RID: 229
		public const int EvictionInterval = 60;

		// Token: 0x040000E6 RID: 230
		private readonly Dictionary<ulong, int> lastUsed = new Dictionary<ulong, int>();

		// Token: 0x040000E7 RID: 231
		private readonly List<KeyValuePair<ulong, int>> victimScratch = new List<KeyValuePair<ulong, int>>();

		// Token: 0x040000E8 RID: 232
		private int lastEvictionFrame = int.MinValue;
	}
}
