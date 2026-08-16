using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Mirage.WebIngest;
using Unity.Profiling;
using UnityEngine;

namespace Mirage.VirtualTexture
{
	/// <summary>
	/// CPU-driven virtual texture streaming manager.
	///
	/// Each frame the host calls <see cref="M:Mirage.VirtualTexture.TileStreamingManager.Update(System.Int32)" />. For every registered body the manager:
	///   1. Asks the body for its visible leaf quads (<see cref="M:Mirage.VirtualTexture.IMirageBody.EnumerateVisibleLeafQuads(System.Collections.Generic.List{Mirage.VirtualTexture.LeafQuad})" />).
	///   2. Walks each leaf + coarser ancestors down to <see cref="F:Mirage.VirtualTexture.TileStreamingManager.CoarseMaxLevel" /> for the required tiles.
	///   3. Refreshes LRU for already-resident tiles.
	///   4. Queues non-resident tiles and starts async loads for ALL present layers of each (a "group").
	///   5. Ticks in-flight groups; a tile is uploaded (all layers into the shared slot) only when every
	///      present layer has landed — the lockstep invariant. Capped by <see cref="F:Mirage.VirtualTexture.TileStreamingManager.MaxUploadsPerFrame" />.
	///   6. Flushes the one page table once per frame.
	///
	/// Coarse levels (0..<see cref="F:Mirage.VirtualTexture.TileStreamingManager.CoarseMaxLevel" />) are pinned at bootstrap so the shader always has a
	/// fallback. Tile coords (tx, ty) are in CORRECTED face-UV space (see <see cref="M:Mirage.VirtualTexture.TileStreamingManager.GetCorrectedTileCoord(System.Int32,System.Single,System.Single,System.Int32,System.Int32@,System.Int32@)" />).
	/// </summary>
	// Token: 0x0200004E RID: 78
	public static class TileStreamingManager
	{
		// Token: 0x0600020F RID: 527 RVA: 0x00010998 File Offset: 0x0000EB98
		private static float[] BuildHorizonD(bool cosNotSin)
		{
			float[] a = new float[25];
			for (int i = 0; i <= 24; i++)
			{
				double d = Math.Sqrt(2.0) * 3.141592653589793 / Math.Pow(2.0, (double)(i + 2)) + 0.009999999776482582;
				a[i] = (float)(cosNotSin ? Math.Cos(d) : Math.Sin(d));
			}
			return a;
		}

		// Token: 0x06000210 RID: 528 RVA: 0x00010A18 File Offset: 0x0000EC18
		public static void RegisterBody(string sphereName, IMirageBody body)
		{
			bool flag = TileStreamingManager.s_Bodies.ContainsKey(sphereName);
			if (!flag)
			{
				TileStreamingManager.s_Bodies[sphereName] = new TileStreamingManager.BodyStreamState
				{
					body = body,
					sphereName = sphereName,
					cfg = body.Config
				};
				MirageDebug.Log("TileStreamingManager: registered '" + sphereName + "'");
			}
		}

		// Token: 0x06000211 RID: 529 RVA: 0x00010A78 File Offset: 0x0000EC78
		public static void UnregisterBody(string sphereName)
		{
			TileStreamingManager.BodyStreamState state;
			bool flag = !TileStreamingManager.s_Bodies.TryGetValue(sphereName, out state);
			if (!flag)
			{
				foreach (TileStreamingManager.InFlightGroup g in state.inFlight)
				{
					g.DisposeHandles();
				}
				while (state.completed.Count > 0)
				{
					state.completed.Dequeue().DisposeHandles();
				}
				TileIngestQueue ingest = state.ingest;
				if (ingest != null)
				{
					ingest.Shutdown();
				}
				bool flag2 = state.budget != null && state.webTiers != null;
				if (flag2)
				{
					state.budget.MaybeCompact(state.webTiers, false);
					foreach (WebTileArchive tier in state.webTiers)
					{
						if (tier != null)
						{
							tier.Flush();
						}
					}
				}
				TileStreamingManager.s_Bodies.Remove(sphereName);
				MirageDebug.Log("TileStreamingManager: unregistered '" + sphereName + "'");
			}
		}

		// Token: 0x06000212 RID: 530 RVA: 0x00010BA4 File Offset: 0x0000EDA4
		public static List<TileStreamingManager.BodyDebugInfo> GetAllBodyDebugInfo()
		{
			List<TileStreamingManager.BodyDebugInfo> result = new List<TileStreamingManager.BodyDebugInfo>(TileStreamingManager.s_Bodies.Count);
			foreach (KeyValuePair<string, TileStreamingManager.BodyStreamState> kvp in TileStreamingManager.s_Bodies)
			{
				TileStreamingManager.BodyStreamState state = kvp.Value;
				TileCache cache = state.body.Cache;
				result.Add(new TileStreamingManager.BodyDebugInfo
				{
					sphereName = state.sphereName,
					slots = ((cache != null) ? cache.OccupiedSlots : 0),
					total = ((cache != null) ? cache.TotalSlots : 0),
					levelCounts = ((cache != null) ? cache.GetLevelCounts() : null),
					queue = state.queue.Count,
					flight = state.inFlight.Count,
					loading = state.loading.Count,
					completed = state.completed.Count,
					missing = state.knownMissing.Count,
					tilesRequested = state.tilesRequestedLastFrame,
					tilesLoaded = state.tilesLoadedLastFrame,
					desync = ((cache != null) ? cache.CountPageTableDesync() : 0),
					badIndirection = state.indirectionViolations,
					blocks = ((cache != null) ? cache.OccupiedBlocks : 0),
					totalBlocks = ((cache != null && cache.HasFineTier) ? cache.TotalBlocks : 0),
					dirLevel = ((cache != null) ? cache.DirectoryLevel : 0),
					maxLevel = ((cache != null) ? cache.maxLevel : 0),
					hasIngest = (state.ingest != null),
					ingestPending = state.ingestQueue.Count,
					ingestActive = ((state.ingest != null) ? state.ingest.InProgress : 0),
					ingestBaked = state.tilesIngestedTotal,
					ingestNoCoverage = ((state.ingest != null) ? state.ingest.NoCoverageCount : 0),
					ingestFailed = ((state.ingest != null) ? state.ingest.FailedCount : 0),
					webPhysicalBytes = ((state.budget != null) ? state.budget.PhysicalBytes(state.webTiers) : 0L),
					webLiveBytes = ((state.budget != null) ? state.budget.LiveBytes(state.webTiers) : 0L),
					webCapBytes = ((state.budget != null) ? state.budget.CapBytes : 0L),
					webEvicted = ((state.budget != null) ? state.budget.TotalEvicted : 0),
					webCapTooSmall = (state.budget != null && state.budget.CapTooSmall)
				});
			}
			return result;
		}

		// Token: 0x06000213 RID: 531 RVA: 0x00010EA8 File Offset: 0x0000F0A8
		public static void Update(int frame)
		{
			FrameProfile.AddFrameTime((double)Time.unscaledDeltaTime * 1000.0);
			foreach (KeyValuePair<string, TileStreamingManager.BodyStreamState> kvp in TileStreamingManager.s_Bodies)
			{
				TileStreamingManager.UpdateBody(kvp.Value, frame);
			}
		}

		// Token: 0x06000214 RID: 532 RVA: 0x00010F1C File Offset: 0x0000F11C
		private static void UpdateBody(TileStreamingManager.BodyStreamState state, int frame)
		{
			TileCache cache = state.body.Cache;
			VirtualTextureConfig cfg = state.cfg;
			bool flag = cache == null;
			if (!flag)
			{
				Stopwatch sw = FrameProfile.Start();
				TileStreamingManager.s_LeafQuadBuffer.Clear();
				using (TileStreamingManager.s_EnumerateLeavesMarker.Auto())
				{
					state.body.EnumerateVisibleLeafQuads(TileStreamingManager.s_LeafQuadBuffer);
				}
				FrameProfile.AddLeaves(sw.ElapsedTicks);
				TileStreamingManager.s_RequiredScratch.Clear();
				sw.Restart();
				VTLevelContext levelCtx;
				bool hasCtx;
				using (TileStreamingManager.s_LevelContextMarker.Auto())
				{
					hasCtx = state.body.TryGetLevelContext(out levelCtx);
				}
				FrameProfile.AddLevelCtx(sw.ElapsedTicks);
				sw.Restart();
				using (TileStreamingManager.s_CollectRequiredMarker.Auto())
				{
					TileStreamingManager.CollectRequiredTiles(TileStreamingManager.s_LeafQuadBuffer, state.body.StreamingMaxLevel, TileStreamingManager.s_RequiredScratch, hasCtx, levelCtx, cfg.tileSize, cfg.borderPx);
				}
				FrameProfile.AddCollect(sw.ElapsedTicks);
				state.tilesRequestedLastFrame = TileStreamingManager.s_RequiredScratch.Count;
				TileStreamingManager.BodyStreamState state2 = state;
				int num = state2.framesSinceMissingReset + 1;
				state2.framesSinceMissingReset = num;
				bool flag2 = num >= 600;
				if (flag2)
				{
					state.framesSinceMissingReset = 0;
					state.knownMissing.Clear();
				}
				bool fineTier = cache.HasFineTier;
				int dirLevel = cache.DirectoryLevel;
				sw.Restart();
				using (TileStreamingManager.s_RefreshLruMarker.Auto())
				{
					foreach (long key in TileStreamingManager.s_RequiredScratch.Keys)
					{
						cache.MarkTileUsed(key, frame);
						bool flag3 = !fineTier && state.budget == null;
						if (!flag3)
						{
							int face;
							int level;
							int tx;
							int ty;
							TileCache.UnpackKey(key, out face, out level, out tx, out ty);
							bool flag4 = state.budget != null;
							if (flag4)
							{
								state.budget.Touch(TileStreamingManager.ArchiveKey(face, level, tx, ty), frame);
							}
							bool flag5 = fineTier && level > dirLevel;
							if (flag5)
							{
								int i = level - dirLevel;
								cache.TouchBlock(face, tx >> i, ty >> i, frame);
							}
						}
					}
				}
				FrameProfile.AddLru(sw.ElapsedTicks);
				state.queue.Clear();
				state.ingestQueue.Clear();
				sw.Restart();
				using (TileStreamingManager.s_BuildQueuesMarker.Auto())
				{
					foreach (KeyValuePair<long, int> req in TileStreamingManager.s_RequiredScratch)
					{
						long key2 = req.Key;
						bool flag6 = cache.IsTileResident(key2) || state.loading.Contains(key2);
						if (!flag6)
						{
							int face2;
							int level2;
							int tx2;
							int ty2;
							TileCache.UnpackKey(key2, out face2, out level2, out tx2, out ty2);
							TileStreamingManager.PendingTile pending = new TileStreamingManager.PendingTile
							{
								key = key2,
								face = face2,
								level = level2,
								tx = tx2,
								ty = ty2,
								priority = level2,
								nearness = req.Value
							};
							bool flag7 = state.ingest != null && !TileStreamingManager.AllLayersOnDisk(cache, face2, level2, tx2, ty2);
							if (flag7)
							{
								bool flag8 = !state.ingest.IsBlocked(TileStreamingManager.ArchiveKey(face2, level2, tx2, ty2), frame);
								if (flag8)
								{
									state.ingestQueue.Add(pending);
								}
							}
							else
							{
								bool flag9 = state.knownMissing.Contains(key2);
								if (!flag9)
								{
									state.queue.Add(pending);
								}
							}
						}
					}
				}
				using (TileStreamingManager.s_SortQueuesMarker.Auto())
				{
					List<TileStreamingManager.PendingTile> queue = state.queue;
					Comparison<TileStreamingManager.PendingTile> comparison;
					if ((comparison = TileStreamingManager.<>O.<0>__CompareByLevelThenNearness) == null)
					{
						comparison = (TileStreamingManager.<>O.<0>__CompareByLevelThenNearness = new Comparison<TileStreamingManager.PendingTile>(TileStreamingManager.CompareByLevelThenNearness));
					}
					queue.Sort(comparison);
					List<TileStreamingManager.PendingTile> ingestQueue = state.ingestQueue;
					Comparison<TileStreamingManager.PendingTile> comparison2;
					if ((comparison2 = TileStreamingManager.<>O.<0>__CompareByLevelThenNearness) == null)
					{
						comparison2 = (TileStreamingManager.<>O.<0>__CompareByLevelThenNearness = new Comparison<TileStreamingManager.PendingTile>(TileStreamingManager.CompareByLevelThenNearness));
					}
					ingestQueue.Sort(comparison2);
				}
				FrameProfile.AddQueues(sw.ElapsedTicks);
				bool flag10 = state.ingest != null;
				if (flag10)
				{
					using (TileStreamingManager.s_IngestMarker.Auto())
					{
						sw.Restart();
						int j = 0;
						while (j < state.ingestQueue.Count && state.ingest.HasCapacity)
						{
							TileStreamingManager.PendingTile p = state.ingestQueue[j];
							state.ingest.TryRequest(p.face, p.level, p.tx, p.ty, frame);
							j++;
						}
						state.tilesIngestedTotal += state.ingest.Drain(delegate(BakedTile t)
						{
							Stopwatch swC = FrameProfile.Start();
							bool ok = TileStreamingManager.CommitBakedTile(state, t);
							FrameProfile.AddCommit(swC.ElapsedTicks);
							bool flag14 = !ok;
							bool result;
							if (flag14)
							{
								result = false;
							}
							else
							{
								WebDiskBudget budget2 = state.budget;
								if (budget2 != null)
								{
									budget2.OnBaked(t.key, frame);
								}
								result = true;
							}
							return result;
						}, frame, 2);
						using (TileStreamingManager.s_EnforceBudgetMarker.Auto())
						{
							WebDiskBudget budget = state.budget;
							if (budget != null)
							{
								budget.Enforce(state.webTiers, frame);
							}
						}
						FrameProfile.AddIngest(sw.ElapsedTicks);
						FrameProfile.NoteIngest(state.ingest.InProgress, WebTileFetcher.ActiveDownloads, WebTileFetcher.QueuedCount);
					}
				}
				sw.Restart();
				using (TileStreamingManager.s_StartLoadsMarker.Auto())
				{
					TileStreamingManager.StartLoads(state, cache);
				}
				FrameProfile.AddStartLoads(sw.ElapsedTicks);
				int uploaded = 0;
				sw.Restart();
				using (TileStreamingManager.s_DrainInFlightMarker.Auto())
				{
					TileStreamingManager.DrainInFlight(state, cache, frame, ref uploaded);
				}
				FrameProfile.AddDrain(sw.ElapsedTicks);
				sw.Restart();
				using (TileStreamingManager.s_ApplyPageTableMarker.Auto())
				{
					cache.ApplyPageTable();
				}
				FrameProfile.AddApplyPage(sw.ElapsedTicks);
				state.tilesLoadedLastFrame = uploaded;
				bool flag11 = TileStreamingManager.ValidateEveryFrame && !state.indirectionReported;
				if (flag11)
				{
					TileStreamingManager.s_ValidationReport.Clear();
					int bad = cache.ValidateIndirection(TileStreamingManager.s_ValidationReport, 8, false);
					bool flag12 = bad > 0;
					if (flag12)
					{
						TileStreamingManager.s_ValidationReport.Clear();
						bad = cache.ValidateIndirection(TileStreamingManager.s_ValidationReport, 16, true);
						state.indirectionViolations = bad;
						state.indirectionReported = true;
						MirageDebug.LogError(string.Format("[VT Validate] {0} frame {1}: {2} indirection violation(s) — ", state.sphereName, frame, bad) + string.Format("slots={0}/{1} blocks={2}/{3}\n  ", new object[]
						{
							cache.OccupiedSlots,
							cache.TotalSlots,
							cache.OccupiedBlocks,
							cache.TotalBlocks
						}) + string.Join("\n  ", TileStreamingManager.s_ValidationReport));
					}
				}
				state.framesSinceLastLog++;
				bool flag13 = state.framesSinceLastLog >= 600;
				if (flag13)
				{
					Stopwatch swMetrics = FrameProfile.Start();
					using (TileStreamingManager.s_MetricsMarker.Auto())
					{
						state.framesSinceLastLog = 0;
						MirageDebug.Log(string.Concat(new string[]
						{
							string.Format("[VT Stream] {0}  req={1}  loaded={2}  ", state.sphereName, state.tilesRequestedLastFrame, state.tilesLoadedLastFrame),
							string.Format("slots={0}/{1}  queue={2}  flight={3}  ", new object[]
							{
								cache.OccupiedSlots,
								cache.TotalSlots,
								state.queue.Count,
								state.inFlight.Count
							}),
							string.Format("loading={0}  completed={1}  missing={2}  ", state.loading.Count, state.completed.Count, state.knownMissing.Count),
							string.Format("desync={0}", cache.CountPageTableDesync()),
							(state.ingest != null) ? string.Concat(new string[]
							{
								string.Format("  ingest[want={0} active={1} ", state.ingestQueue.Count, state.ingest.InProgress),
								string.Format("baked={0} nocov={1} ", state.tilesIngestedTotal, state.ingest.NoCoverageCount),
								string.Format("failed={0}]", state.ingest.FailedCount),
								TileStreamingManager.WebTierDiagnostic(state),
								BakeProfile.Report()
							}) : "",
							FrameProfile.Report()
						}));
						FrameProfile.Reset();
					}
					FrameProfile.AddMetrics(swMetrics.ElapsedTicks);
				}
			}
		}

		/// <summary>
		/// The one number that separates "the cache is empty" from "the cache is there and we can't see it":
		/// of the tiles we are about to BAKE this frame, how many does the baked tier ALREADY hold?
		///
		/// By construction that must be zero — a key on disk is routed to the load path by
		/// <see cref="M:Mirage.VirtualTexture.TileStreamingManager.AllLayersOnDisk(Mirage.VirtualTexture.TileCache,System.Int32,System.Int32,System.Int32,System.Int32)" /> and never reaches the ingest queue. Any other number means that gate is
		/// lying, and it fails silently: within a session the residency check masks it, so it only surfaces after
		/// a restart as a full re-bake of a region already cached — at the bake rate, indistinguishable from a
		/// cache that was never written. It asks the web tiers DIRECTLY rather than through
		/// <c>source.Exists</c>, so a source whose web tier was never attached shows up as a disagreement between
		/// the two rather than as a matching pair of wrong answers.
		///
		/// Costs a walk of the ingest queue, so it runs only at the metrics interval.
		/// </summary>
		// Token: 0x06000215 RID: 533 RVA: 0x00011B30 File Offset: 0x0000FD30
		private static string WebTierDiagnostic(TileStreamingManager.BodyStreamState state)
		{
			bool flag = state.webTiers == null;
			string result;
			if (flag)
			{
				result = "";
			}
			else
			{
				int wantButCached = 0;
				for (int i = 0; i < state.ingestQueue.Count; i++)
				{
					TileStreamingManager.PendingTile p = state.ingestQueue[i];
					ulong ak = TileStreamingManager.ArchiveKey(p.face, p.level, p.tx, p.ty);
					int present = 0;
					bool all = true;
					for (int t = 0; t < state.webTiers.Length; t++)
					{
						bool flag2 = state.webTiers[t] == null;
						if (!flag2)
						{
							present++;
							bool flag3 = !state.webTiers[t].Contains(ak);
							if (flag3)
							{
								all = false;
								break;
							}
						}
					}
					bool flag4 = all && present > 0;
					if (flag4)
					{
						wantButCached++;
					}
				}
				string[] counts = new string[state.webTiers.Length];
				for (int t2 = 0; t2 < state.webTiers.Length; t2++)
				{
					counts[t2] = ((state.webTiers[t2] == null) ? "-" : state.webTiers[t2].Count.ToString());
				}
				result = string.Format("  web[chn={0} wantButCached={1}]", string.Join("/", counts), wantButCached);
			}
			return result;
		}

		/// <summary>
		/// For each visible leaf we queue the leaf tile AND every coarser ancestor down to
		/// <see cref="F:Mirage.VirtualTexture.TileStreamingManager.CoarseMaxLevel" />+1 so the shader's per-pixel resolve has a gradient of loaded tiles
		/// rather than a cliff to the pinned floor. Coarse ancestors are shared across neighbouring leaves.
		///
		/// On top of that, when the body can give us a <see cref="T:Mirage.VirtualTexture.VTLevelContext" />, we DESCEND below the quad's
		/// own subdivision by screen-space size (see <see cref="M:Mirage.VirtualTexture.TileStreamingManager.Descend(Mirage.VirtualTexture.VTLevelContext@,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Single,System.Collections.Generic.Dictionary{System.Int64,System.Int32},UnityEngine.Vector3,System.Single)" />). Without that, the finest level any
		/// quad can pull is its own subdivision — and Mirage keeps subdivision deliberately low, so the terrain
		/// drops to coarse tiles a short way from the craft no matter how high maxLevel is set.
		/// </summary>
		// Token: 0x06000216 RID: 534 RVA: 0x00011CA0 File Offset: 0x0000FEA0
		private static void CollectRequiredTiles(List<LeafQuad> leafQuads, int maxLevel, Dictionary<long, int> required, bool hasCtx, in VTLevelContext ctx, int tileSize, int borderPx)
		{
			TileStreamingManager.s_SeedScratch.Clear();
			for (int i = 0; i < leafQuads.Count; i++)
			{
				LeafQuad quad = leafQuads[i];
				int face = quad.Face;
				int leafLevel = Mathf.Min(quad.Subdivision, maxLevel);
				for (int level = leafLevel; level > 2; level--)
				{
					int atx;
					int aty;
					TileStreamingManager.GetCorrectedTileCoord(face, (float)quad.UvSwX, (float)quad.UvSwY, level, out atx, out aty);
					long akey = TileCache.PackKey(face, level, atx, aty);
					int nearest;
					bool flag = !required.TryGetValue(akey, out nearest) || quad.Subdivision > nearest;
					if (flag)
					{
						required[akey] = quad.Subdivision;
					}
				}
				bool flag2 = !hasCtx;
				if (!flag2)
				{
					int seed = Mathf.Clamp(leafLevel, 2, maxLevel);
					int stx;
					int sty;
					TileStreamingManager.GetCorrectedTileCoord(face, (float)quad.UvSwX, (float)quad.UvSwY, seed, out stx, out sty);
					long skey = TileCache.PackKey(face, seed, stx, sty);
					int seen;
					bool flag3 = !TileStreamingManager.s_SeedScratch.TryGetValue(skey, out seen) || quad.Subdivision > seen;
					if (flag3)
					{
						TileStreamingManager.s_SeedScratch[skey] = quad.Subdivision;
					}
				}
			}
			float pixelThreshold = (float)tileSize / Mathf.Max(MirageSettings.Oversample, 0.25f);
			foreach (KeyValuePair<long, int> s in TileStreamingManager.s_SeedScratch)
			{
				int f;
				int j;
				int x;
				int y;
				TileCache.UnpackKey(s.Key, out f, out j, out x, out y);
				Vector3 c0;
				float e0;
				Vector3 vector;
				TileStreamingManager.TileSphere(ctx, f, j, x, y, tileSize, borderPx, out c0, out e0, out vector);
				TileStreamingManager.Descend(ctx, f, j, x, y, s.Value, maxLevel, tileSize, borderPx, pixelThreshold, required, c0, e0);
			}
		}

		/// <summary>
		/// Subdivide while this tile resolves to more than <paramref name="pixelThreshold" /> pixels on screen —
		/// i.e. while one tile's texels are being stretched over more than one screen pixel each (or over more
		/// than 1/oversample of one; the threshold is tileSize / MirageSettings.Oversample).
		///
		/// This terminates on its own and that is the whole reason it is safe: projected size halves per level,
		/// so the walk stops as soon as texel density matches pixel density. The tile count it produces tracks
		/// SCREEN COVERAGE (~a couple of dozen tiles for a 1080p view at 256²), not pyramid depth — which is why
		/// a big near quad can be allowed to pull L12 without the 4^(L−S) explosion that requesting the whole
		/// quad's footprint at a fixed level would cause. <see cref="F:Mirage.VirtualTexture.TileStreamingManager.MaxRequiredTiles" /> is a backstop against a
		/// degenerate camera, not the mechanism. Note the working set scales with the SQUARE of oversample —
		/// screen coverage in tiles — which is why that setting is clamped.
		/// </summary>
		// Token: 0x06000217 RID: 535 RVA: 0x00011E84 File Offset: 0x00010084
		private static void Descend(in VTLevelContext ctx, int face, int level, int tx, int ty, int nearness, int maxLevel, int tileSize, int borderPx, float pixelThreshold, Dictionary<long, int> required, Vector3 centre, float extent)
		{
			bool flag = level >= maxLevel || required.Count >= 8192;
			if (!flag)
			{
				bool flag2 = TileStreamingManager.Projected(ctx, centre, extent) <= pixelThreshold;
				if (!flag2)
				{
					int child = level + 1;
					for (int dy = 0; dy < 2; dy++)
					{
						for (int dx = 0; dx < 2; dx++)
						{
							int cx = tx * 2 + dx;
							int cy = ty * 2 + dy;
							Vector3 cc;
							float ce;
							Vector3 cdir;
							TileStreamingManager.TileSphere(ctx, face, child, cx, cy, tileSize, borderPx, out cc, out ce, out cdir);
							bool flag3 = !TileStreamingManager.Visible(ctx, cc, cdir, ce, child);
							if (!flag3)
							{
								long key = TileCache.PackKey(face, child, cx, cy);
								int nearest;
								bool flag4 = !required.TryGetValue(key, out nearest) || nearness > nearest;
								if (flag4)
								{
									required[key] = nearness;
								}
								TileStreamingManager.Descend(ctx, face, child, cx, cy, nearness, maxLevel, tileSize, borderPx, pixelThreshold, required, cc, ce);
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// Is this tile worth streaming — i.e. can the camera see any of it? Frustum first (it removes the bulk:
		/// everything behind the camera), then horizon (the planet occluding its own far side, which no frustum
		/// plane expresses).
		///
		/// <paramref name="dirWorld" /> is the unit planet-centre→tile direction, taken straight from
		/// <see cref="M:Mirage.VirtualTexture.TileStreamingManager.TileSphere(Mirage.VirtualTexture.VTLevelContext@,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,UnityEngine.Vector3@,System.Single@,UnityEngine.Vector3@)" /> so no normalize is needed here. The horizon test is done in COSINE space:
		/// the tile is visible iff angTile ≤ angHorizon + D(level), and since cos is monotonic on [0,π] that is
		/// dot(dirWorld, CamDir) ≥ cos(angHorizon + D). cos(angTile) is the dot itself (no acos), and
		/// cos(angHorizon+D) expands from the frame-precomputed CosHorizon/SinHorizon and the per-level cos/sin
		/// of D — so the whole cull is a dot plus two multiplies, no per-tile transcendental at all.
		/// </summary>
		// Token: 0x06000218 RID: 536 RVA: 0x00011F9C File Offset: 0x0001019C
		private static bool Visible(in VTLevelContext ctx, Vector3 centre, Vector3 dirWorld, float extent, int level)
		{
			float r = extent * 0.75f;
			Plane[] planes = ctx.FrustumPlanes;
			for (int i = 0; i < planes.Length; i++)
			{
				bool flag = planes[i].GetDistanceToPoint(centre) < -r;
				if (flag)
				{
					return false;
				}
			}
			bool cameraInsideSphere = ctx.CameraInsideSphere;
			if (cameraInsideSphere)
			{
				return true;
			}
			int li = (level < TileStreamingManager.s_HorizonCosD.Length) ? level : (TileStreamingManager.s_HorizonCosD.Length - 1);
			float cosThreshold = ctx.CosHorizon * TileStreamingManager.s_HorizonCosD[li] - ctx.SinHorizon * TileStreamingManager.s_HorizonSinD[li];
			return Vector3.Dot(dirWorld, ctx.CamDir) >= cosThreshold;
		}

		/// <summary>Tile centre (world), world-space edge length, and the unit planet-centre→centre direction
		/// (reused by <see cref="M:Mirage.VirtualTexture.TileStreamingManager.Visible(Mirage.VirtualTexture.VTLevelContext@,UnityEngine.Vector3,UnityEngine.Vector3,System.Single,System.Int32)" />'s horizon test, so it never re-normalizes).
		///
		/// A cube face spans a quarter great circle, so the AVERAGE level-L tile spans (πR/2)/2^L — but the cube
		/// map is gnomonic, so the actual size varies by 2x across a face (centre biggest, corners smallest).
		/// Using the average for every tile made the descent think seam tiles were ~1.6x larger than they are
		/// and subdivide roughly a level too far, which is a ~2.8x required-tile spike near a face border
		/// (measured: 205 tiles mid-face vs 574 at a seam, 5 km up). <see cref="M:Mirage.WebIngest.MirageCubeMath.FaceExtentScale(System.Double,System.Double)" />
		/// supplies the real size; it is normalised to 1.0 at the face centre, so mid-face behaviour — and hence
		/// what <c>oversample = 1</c> means — is unchanged, and only the false variation goes away.</summary>
		// Token: 0x06000219 RID: 537 RVA: 0x00012050 File Offset: 0x00010250
		private static void TileSphere(in VTLevelContext ctx, int face, int level, int tx, int ty, int tileSize, int borderPx, out Vector3 centre, out float extent, out Vector3 dirWorld)
		{
			double c = (double)borderPx + (double)tileSize * 0.5;
			double lat;
			double lon;
			MirageCubeMath.TileTexelToLatLon(face, level, tx, ty, c, c, tileSize, borderPx, out lat, out lon);
			double dx;
			double dy;
			double dz;
			MirageCubeMath.LatLonToDirection(lat, lon, out dx, out dy, out dz);
			Vector3 local;
			local..ctor((float)dx, (float)dy, (float)dz);
			dirWorld = ctx.PlanetRotation * local;
			centre = ctx.PlanetOrigin + dirWorld * ctx.PlanetRadius;
			int grid = 1 << level;
			double scale = MirageCubeMath.FaceExtentScale(((double)tx + 0.5) / (double)grid, ((double)ty + 0.5) / (double)grid);
			extent = (float)(1.5707963267948966 * (double)ctx.PlanetRadius / (double)grid * scale);
		}

		/// <summary>
		/// This tile's on-screen size in pixels.
		///
		/// Distance is to the tile's NEAREST point, approximated by pulling half the tile's extent off the
		/// centre distance. Using the centre alone would under-estimate a large tile the camera is sitting on
		/// top of — its centre can be far away while the ground underfoot is part of it — and the walk would
		/// prune exactly where detail is needed most. Erring toward "too big" costs a wasted level of descent;
		/// erring the other way is the bug we are fixing.
		/// </summary>
		// Token: 0x0600021A RID: 538 RVA: 0x00012128 File Offset: 0x00010328
		private static float Projected(in VTLevelContext ctx, Vector3 centre, float extent)
		{
			float d = Mathf.Max((centre - ctx.CameraPos).magnitude - extent * 0.5f, 1f);
			return extent / d * ctx.PixelsPerUnitTangent;
		}

		/// <summary>
		/// Turn on web ingest for a registered body. Separate from <see cref="M:Mirage.VirtualTexture.TileStreamingManager.RegisterBody(System.String,Mirage.VirtualTexture.IMirageBody)" /> because a baker
		/// needs things the cache doesn't know (an imagery provider, the body's radius and height mapping), and
		/// because a body with no web tier must be able to skip all of this.
		/// </summary>
		// Token: 0x0600021B RID: 539 RVA: 0x0001216C File Offset: 0x0001036C
		public static void EnableIngest(string sphereName, ITileBaker baker, long diskCapBytes, int maxConcurrent = 2)
		{
			TileStreamingManager.BodyStreamState state;
			bool flag = !TileStreamingManager.s_Bodies.TryGetValue(sphereName, out state);
			if (flag)
			{
				MirageDebug.LogError("EnableIngest: '" + sphereName + "' is not registered.");
			}
			else
			{
				TileIngestQueue ingest = state.ingest;
				if (ingest != null)
				{
					ingest.Shutdown();
				}
				state.ingest = new TileIngestQueue(baker, maxConcurrent);
				state.webTiers = new WebTileArchive[]
				{
					state.cfg.GetWebArchive(VTLayer.Color),
					state.cfg.GetWebArchive(VTLayer.Height),
					state.cfg.GetWebArchive(VTLayer.Normal)
				};
				state.budget = new WebDiskBudget(diskCapBytes);
				state.budget.Seed(state.webTiers);
				MirageDebug.Log(string.Format("TileStreamingManager: web ingest enabled for '{0}' (max {1} concurrent, ", sphereName, maxConcurrent) + string.Format("disk cap {0} MB, {1} tile(s) already baked).", diskCapBytes / 1048576L, state.budget.TrackedKeys));
			}
		}

		/// <summary>
		/// Re-key a tile from the streamer's packing to the archive's.
		///
		/// There are two key encodings in this system and they are NOT interchangeable:
		/// <c>TileCache.PackKey</c> is <c>face&lt;&lt;40 | level&lt;&lt;32 | ty&lt;&lt;16 | tx</c> (cheap, and all
		/// the atlas needs), while <c>MirageArchiveFormat.PackKey</c> is Morton-ordered
		/// (<c>face&lt;&lt;60 | level&lt;&lt;51 | interleave(x,y)</c>) so that on-disk locality follows spatial
		/// locality. Everything in WebIngest — the archive, the ingest queue, the disk budget — speaks the
		/// archive's.
		///
		/// Both are 64-bit, so <c>(ulong)tileCacheKey</c> compiles perfectly and is silently wrong. It was:
		/// the first in-game run had `IsBlocked` checking a TileCache key against a set of archive keys, so it
		/// never matched, and dead tiles were re-offered forever. This function exists so the conversion is
		/// named rather than cast.
		/// </summary>
		/// <summary>Coarser level first; ties broken by nearness (deepest requesting leaf first).</summary>
		// Token: 0x0600021C RID: 540 RVA: 0x00012264 File Offset: 0x00010464
		private static int CompareByLevelThenNearness(TileStreamingManager.PendingTile a, TileStreamingManager.PendingTile b)
		{
			int byLevel = a.priority.CompareTo(b.priority);
			return (byLevel != 0) ? byLevel : b.nearness.CompareTo(a.nearness);
		}

		// Token: 0x0600021D RID: 541 RVA: 0x000122A1 File Offset: 0x000104A1
		private static ulong ArchiveKey(int face, int level, int tx, int ty)
		{
			return MirageArchiveFormat.PackKey(face, level, tx, ty);
		}

		/// <summary>
		/// Every present layer has this key on disk. The lockstep cache uploads a tile only when EVERY layer
		/// landed (§4.4), so a group is loadable only if all of them resolve — a partially-baked key must read
		/// as "not on disk" and be re-ingested, not handed to the loader to fault on.
		/// </summary>
		// Token: 0x0600021E RID: 542 RVA: 0x000122AC File Offset: 0x000104AC
		private static bool AllLayersOnDisk(TileCache cache, int face, int level, int tx, int ty)
		{
			IReadOnlyList<TileCache.LayerState> layers = cache.Layers;
			for (int i = 0; i < layers.Count; i++)
			{
				bool flag = !layers[i].source.Exists(face, level, tx, ty);
				if (flag)
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Append a baked tile to the body's web archives — main thread, so the appends serialise and the one
		/// writable handle per blob needs no lock (see <c>VirtualTextureConfig.GetWebArchive</c>).
		///
		/// All layers or none. A key present in colour's index but absent from height's is worse than an absent
		/// key: the group would fault forever under lockstep. It is not transactional across three blobs, so a
		/// mid-way failure does leave a partial key — that self-heals, because <see cref="M:Mirage.VirtualTexture.TileStreamingManager.AllLayersOnDisk(Mirage.VirtualTexture.TileCache,System.Int32,System.Int32,System.Int32,System.Int32)" />
		/// then reports false and the tile is simply re-ingested and re-appended, orphaning the old payload as
		/// garbage for compaction to reclaim (§6).
		/// </summary>
		// Token: 0x0600021F RID: 543 RVA: 0x00012300 File Offset: 0x00010500
		private static bool CommitBakedTile(TileStreamingManager.BodyStreamState state, BakedTile t)
		{
			IReadOnlyList<TileCache.LayerState> layers = state.body.Cache.Layers;
			bool result;
			try
			{
				for (int i = 0; i < layers.Count; i++)
				{
					VTLayer layer = layers[i].id;
					byte[] stored = t.stored[(int)layer];
					bool flag = stored == null;
					if (flag)
					{
						MirageDebug.LogError(string.Format("TileIngest: baker returned no {0} payload for L{1} f{2} ", layer, t.level, t.face) + string.Format("{0},{1}, but the body has a {2} layer — refusing to commit a ", t.tx, t.ty, layer) + "partial group.");
						return false;
					}
					WebTileArchive web = state.cfg.GetWebArchive(layer);
					bool flag2 = web == null;
					if (flag2)
					{
						MirageDebug.LogError(string.Format("TileIngest: no {0} web tier for '{1}' — cannot commit.", layer, state.sphereName));
						return false;
					}
					web.Append(t.key, stored, t.format[(int)layer], t.codec[(int)layer], t.crc[(int)layer]);
				}
				result = true;
			}
			catch (Exception e)
			{
				MirageDebug.LogError(string.Format("TileIngest: commit failed for L{0} f{1} {2},{3}: {4}", new object[]
				{
					t.level,
					t.face,
					t.tx,
					t.ty,
					e.Message
				}));
				result = false;
			}
			return result;
		}

		// Token: 0x06000220 RID: 544 RVA: 0x000124AC File Offset: 0x000106AC
		private static void StartLoads(TileStreamingManager.BodyStreamState state, TileCache cache)
		{
			IReadOnlyList<TileCache.LayerState> layers = cache.Layers;
			int slots = Mathf.Min(16 - state.inFlight.Count, 4);
			int i = 0;
			while (i < state.queue.Count && slots > 0)
			{
				TileStreamingManager.PendingTile p = state.queue[i];
				bool flag = state.loading.Contains(p.key);
				if (!flag)
				{
					TileStreamingManager.InFlightGroup group = new TileStreamingManager.InFlightGroup
					{
						key = p.key,
						face = p.face,
						level = p.level,
						tx = p.tx,
						ty = p.ty,
						handles = new TileReadHandle[layers.Count],
						remaining = layers.Count,
						failed = false
					};
					for (int li = 0; li < layers.Count; li++)
					{
						group.handles[li] = layers[li].source.BeginLoad(p.face, p.level, p.tx, p.ty);
					}
					state.loading.Add(p.key);
					state.inFlight.Add(group);
					slots--;
				}
				i++;
			}
		}

		// Token: 0x06000221 RID: 545 RVA: 0x00012610 File Offset: 0x00010810
		private static void DrainInFlight(TileStreamingManager.BodyStreamState state, TileCache cache, int frame, ref int uploaded)
		{
			for (int i = state.inFlight.Count - 1; i >= 0; i--)
			{
				TileStreamingManager.InFlightGroup group = state.inFlight[i];
				int stillPending = 0;
				string faultedLayers = null;
				for (int li = 0; li < group.handles.Length; li++)
				{
					TileReadHandle h = group.handles[li];
					bool isFaulted = h.IsFaulted;
					if (isFaulted)
					{
						group.failed = true;
						faultedLayers = ((faultedLayers == null) ? cache.Layers[li].id.ToString() : (faultedLayers + "+" + cache.Layers[li].id.ToString()));
					}
					else
					{
						bool flag = !h.IsComplete;
						if (flag)
						{
							stillPending++;
						}
					}
				}
				group.remaining = stillPending;
				bool failed = group.failed;
				if (failed)
				{
					MirageDebug.LogError(string.Format("TileStreamingManager: tile load failed for L{0} face{1} ", group.level, group.face) + string.Format("{0},{1} — layer(s): {2}", group.tx, group.ty, faultedLayers));
					group.DisposeHandles();
					state.loading.Remove(group.key);
					state.knownMissing.Add(group.key);
					state.inFlight.RemoveAt(i);
				}
				else
				{
					bool flag2 = group.remaining == 0;
					if (flag2)
					{
						state.completed.Enqueue(group);
						state.inFlight.RemoveAt(i);
					}
				}
			}
			IReadOnlyList<TileCache.LayerState> layers = cache.Layers;
			Texture2D[] tiles = new Texture2D[layers.Count];
			while (uploaded < 12 && state.completed.Count > 0)
			{
				TileStreamingManager.InFlightGroup group2 = state.completed.Dequeue();
				state.loading.Remove(group2.key);
				bool ok = true;
				Stopwatch swGet = FrameProfile.Start();
				for (int li2 = 0; li2 < layers.Count; li2++)
				{
					try
					{
						tiles[li2] = group2.handles[li2].GetTexture();
					}
					catch (Exception e)
					{
						MirageDebug.LogError(string.Format("TileStreamingManager: GetTexture failed for {0} L{1} face{2} {3},{4}: {5}", new object[]
						{
							layers[li2].id,
							group2.level,
							group2.face,
							group2.tx,
							group2.ty,
							e.Message
						}));
						ok = false;
						break;
					}
				}
				FrameProfile.AddGetTex(swGet.ElapsedTicks);
				bool flag3 = !ok;
				if (flag3)
				{
					state.knownMissing.Add(group2.key);
					group2.DisposeHandles();
				}
				else
				{
					Stopwatch swUp = FrameProfile.Start();
					TileCache.TileUploadResult result = cache.TryUploadTile(group2.face, group2.level, group2.tx, group2.ty, tiles, frame);
					FrameProfile.AddUpload(swUp.ElapsedTicks);
					Stopwatch swDisp = FrameProfile.Start();
					group2.DisposeHandles();
					FrameProfile.AddDispose(swDisp.ElapsedTicks);
					switch (result)
					{
					case TileCache.TileUploadResult.Uploaded:
						uploaded += layers.Count;
						break;
					case TileCache.TileUploadResult.Rejected:
						state.knownMissing.Add(group2.key);
						break;
					}
				}
			}
		}

		/// <summary>
		/// Convert a quad's raw face-UV origin (uvSW.x/y) to the tile coordinate in corrected UV space —
		/// the shader samples the page table in corrected UVs, so tile keys must match. Rotations mirror
		/// MirageVT.cginc:CorrectFaceUV: Xp 90°CCW (v,1-u); Xn 90°CW (1-v,u); faces 2-4 180° (1-u,1-v); Zn identity.
		/// </summary>
		// Token: 0x06000222 RID: 546 RVA: 0x000129C8 File Offset: 0x00010BC8
		public static void GetCorrectedTileCoord(int face, float uvSwX, float uvSwY, int tileLevel, out int tx, out int ty)
		{
			int g = 1 << tileLevel;
			int rawX = Mathf.Clamp(Mathf.FloorToInt(uvSwX * (float)g), 0, g - 1);
			int rawY = Mathf.Clamp(Mathf.FloorToInt(uvSwY * (float)g), 0, g - 1);
			switch (face)
			{
			case 0:
				tx = rawY;
				ty = g - 1 - rawX;
				break;
			case 1:
				tx = g - 1 - rawY;
				ty = rawX;
				break;
			case 2:
			case 3:
			case 4:
				tx = g - 1 - rawX;
				ty = g - 1 - rawY;
				break;
			default:
				tx = rawX;
				ty = rawY;
				break;
			}
		}

		/// <summary>Levels 0..CoarseMaxLevel are always bootstrapped + pinned; only finer levels stream.</summary>
		// Token: 0x040001CE RID: 462
		public const int CoarseMaxLevel = 2;

		// Token: 0x040001CF RID: 463
		private const int MaxConcurrentLoads = 16;

		// Token: 0x040001D0 RID: 464
		private const int MaxUploadsPerFrame = 12;

		// Token: 0x040001D1 RID: 465
		private const int MaxLoadStartsPerFrame = 4;

		// Token: 0x040001D2 RID: 466
		private const int MetricsLogInterval = 600;

		// Token: 0x040001D3 RID: 467
		private const int MissingRetryInterval = 600;

		// Token: 0x040001D4 RID: 468
		private const int MaxRequiredTiles = 8192;

		// Token: 0x040001D5 RID: 469
		private const float HorizonReliefMargin = 0.01f;

		/// <summary>
		/// Run the cheap indirection self-check every frame, and log the first breakage with a full report.
		/// Off by default (it is a debugging aid, not a runtime guard) — turn it on with
		/// <c>Mirage { validateIndirection = true }</c>.
		///
		/// Latched deliberately: page-table corruption persists once it happens, so logging every frame after
		/// would bury the one line that identifies the cause. The first report is the one worth reading — it is
		/// produced by the same frame's eviction, before any later churn muddies the state.
		/// </summary>
		// Token: 0x040001D6 RID: 470
		public static bool ValidateEveryFrame;

		// Token: 0x040001D7 RID: 471
		private static readonly Dictionary<long, int> s_RequiredScratch = new Dictionary<long, int>();

		// Token: 0x040001D8 RID: 472
		private static readonly List<LeafQuad> s_LeafQuadBuffer = new List<LeafQuad>();

		// Token: 0x040001D9 RID: 473
		private static readonly Dictionary<long, int> s_SeedScratch = new Dictionary<long, int>();

		// Token: 0x040001DA RID: 474
		private static readonly List<string> s_ValidationReport = new List<string>();

		// Token: 0x040001DB RID: 475
		private const int MaxHorizonLevel = 24;

		// Token: 0x040001DC RID: 476
		private static readonly float[] s_HorizonCosD = TileStreamingManager.BuildHorizonD(true);

		// Token: 0x040001DD RID: 477
		private static readonly float[] s_HorizonSinD = TileStreamingManager.BuildHorizonD(false);

		// Token: 0x040001DE RID: 478
		private static readonly Dictionary<string, TileStreamingManager.BodyStreamState> s_Bodies = new Dictionary<string, TileStreamingManager.BodyStreamState>();

		// Token: 0x040001DF RID: 479
		private static readonly ProfilerMarker s_EnumerateLeavesMarker = new ProfilerMarker("Mirage.VT.EnumerateLeaves");

		// Token: 0x040001E0 RID: 480
		private static readonly ProfilerMarker s_LevelContextMarker = new ProfilerMarker("Mirage.VT.LevelContext");

		// Token: 0x040001E1 RID: 481
		private static readonly ProfilerMarker s_CollectRequiredMarker = new ProfilerMarker("Mirage.VT.CollectRequired");

		// Token: 0x040001E2 RID: 482
		private static readonly ProfilerMarker s_RefreshLruMarker = new ProfilerMarker("Mirage.VT.RefreshLRU");

		// Token: 0x040001E3 RID: 483
		private static readonly ProfilerMarker s_BuildQueuesMarker = new ProfilerMarker("Mirage.VT.BuildQueues");

		// Token: 0x040001E4 RID: 484
		private static readonly ProfilerMarker s_SortQueuesMarker = new ProfilerMarker("Mirage.VT.SortQueues");

		// Token: 0x040001E5 RID: 485
		private static readonly ProfilerMarker s_StartLoadsMarker = new ProfilerMarker("Mirage.VT.StartLoads");

		// Token: 0x040001E6 RID: 486
		private static readonly ProfilerMarker s_DrainInFlightMarker = new ProfilerMarker("Mirage.VT.DrainInFlight");

		// Token: 0x040001E7 RID: 487
		private static readonly ProfilerMarker s_ApplyPageTableMarker = new ProfilerMarker("Mirage.VT.ApplyPageTable");

		// Token: 0x040001E8 RID: 488
		private static readonly ProfilerMarker s_IngestMarker = new ProfilerMarker("Mirage.VT.Ingest");

		// Token: 0x040001E9 RID: 489
		private static readonly ProfilerMarker s_EnforceBudgetMarker = new ProfilerMarker("Mirage.VT.EnforceBudget");

		// Token: 0x040001EA RID: 490
		private static readonly ProfilerMarker s_MetricsMarker = new ProfilerMarker("Mirage.VT.Metrics");

		// Token: 0x020000BB RID: 187
		private class BodyStreamState
		{
			// Token: 0x040004E2 RID: 1250
			public IMirageBody body;

			// Token: 0x040004E3 RID: 1251
			public string sphereName;

			// Token: 0x040004E4 RID: 1252
			public VirtualTextureConfig cfg;

			// Token: 0x040004E5 RID: 1253
			public HashSet<long> loading = new HashSet<long>();

			// Token: 0x040004E6 RID: 1254
			public HashSet<long> knownMissing = new HashSet<long>();

			// Token: 0x040004E7 RID: 1255
			public int framesSinceMissingReset;

			// Token: 0x040004E8 RID: 1256
			public List<TileStreamingManager.InFlightGroup> inFlight = new List<TileStreamingManager.InFlightGroup>();

			// Token: 0x040004E9 RID: 1257
			public List<TileStreamingManager.PendingTile> queue = new List<TileStreamingManager.PendingTile>();

			// Token: 0x040004EA RID: 1258
			public Queue<TileStreamingManager.InFlightGroup> completed = new Queue<TileStreamingManager.InFlightGroup>();

			// Token: 0x040004EB RID: 1259
			public TileIngestQueue ingest;

			// Token: 0x040004EC RID: 1260
			public List<TileStreamingManager.PendingTile> ingestQueue = new List<TileStreamingManager.PendingTile>();

			// Token: 0x040004ED RID: 1261
			public int tilesIngestedTotal;

			// Token: 0x040004EE RID: 1262
			public WebDiskBudget budget;

			// Token: 0x040004EF RID: 1263
			public WebTileArchive[] webTiers;

			// Token: 0x040004F0 RID: 1264
			public int tilesRequestedLastFrame;

			// Token: 0x040004F1 RID: 1265
			public int tilesLoadedLastFrame;

			// Token: 0x040004F2 RID: 1266
			public int framesSinceLastLog;

			// Token: 0x040004F3 RID: 1267
			public int indirectionViolations;

			// Token: 0x040004F4 RID: 1268
			public bool indirectionReported;
		}

		// Token: 0x020000BC RID: 188
		private class InFlightGroup
		{
			// Token: 0x060004DE RID: 1246 RVA: 0x000215FC File Offset: 0x0001F7FC
			public void DisposeHandles()
			{
				bool flag = this.handles == null;
				if (!flag)
				{
					for (int i = 0; i < this.handles.Length; i++)
					{
						this.handles[i].Dispose();
					}
				}
			}

			// Token: 0x040004F5 RID: 1269
			public long key;

			// Token: 0x040004F6 RID: 1270
			public int face;

			// Token: 0x040004F7 RID: 1271
			public int level;

			// Token: 0x040004F8 RID: 1272
			public int tx;

			// Token: 0x040004F9 RID: 1273
			public int ty;

			// Token: 0x040004FA RID: 1274
			public TileReadHandle[] handles;

			// Token: 0x040004FB RID: 1275
			public int remaining;

			// Token: 0x040004FC RID: 1276
			public bool failed;
		}

		// Token: 0x020000BD RID: 189
		private struct PendingTile
		{
			// Token: 0x040004FD RID: 1277
			public long key;

			// Token: 0x040004FE RID: 1278
			public int face;

			// Token: 0x040004FF RID: 1279
			public int level;

			// Token: 0x04000500 RID: 1280
			public int tx;

			// Token: 0x04000501 RID: 1281
			public int ty;

			// Token: 0x04000502 RID: 1282
			public int priority;

			/// <summary>Deepest leaf subdivision that asked for this tile — a nearness proxy. PQS subdivides by
			/// camera distance, so a tile pulled in by a level-12 leaf is under the craft while the same-level
			/// tile pulled in by a level-8 leaf is out near the horizon. Higher = closer.</summary>
			// Token: 0x04000503 RID: 1283
			public int nearness;
		}

		// Token: 0x020000BE RID: 190
		public struct BodyDebugInfo
		{
			// Token: 0x04000504 RID: 1284
			public string sphereName;

			// Token: 0x04000505 RID: 1285
			public int slots;

			// Token: 0x04000506 RID: 1286
			public int total;

			// Token: 0x04000507 RID: 1287
			public int[] levelCounts;

			// Token: 0x04000508 RID: 1288
			public int queue;

			// Token: 0x04000509 RID: 1289
			public int flight;

			// Token: 0x0400050A RID: 1290
			public int loading;

			// Token: 0x0400050B RID: 1291
			public int completed;

			// Token: 0x0400050C RID: 1292
			public int missing;

			// Token: 0x0400050D RID: 1293
			public int tilesRequested;

			// Token: 0x0400050E RID: 1294
			public int tilesLoaded;

			// Token: 0x0400050F RID: 1295
			public int desync;

			// Token: 0x04000510 RID: 1296
			public int badIndirection;

			// Token: 0x04000511 RID: 1297
			public int blocks;

			// Token: 0x04000512 RID: 1298
			public int totalBlocks;

			// Token: 0x04000513 RID: 1299
			public int dirLevel;

			// Token: 0x04000514 RID: 1300
			public int maxLevel;

			// Token: 0x04000515 RID: 1301
			public bool hasIngest;

			// Token: 0x04000516 RID: 1302
			public int ingestPending;

			// Token: 0x04000517 RID: 1303
			public int ingestActive;

			// Token: 0x04000518 RID: 1304
			public int ingestBaked;

			// Token: 0x04000519 RID: 1305
			public int ingestNoCoverage;

			// Token: 0x0400051A RID: 1306
			public int ingestFailed;

			// Token: 0x0400051B RID: 1307
			public long webPhysicalBytes;

			// Token: 0x0400051C RID: 1308
			public long webLiveBytes;

			// Token: 0x0400051D RID: 1309
			public long webCapBytes;

			// Token: 0x0400051E RID: 1310
			public int webEvicted;

			// Token: 0x0400051F RID: 1311
			public bool webCapTooSmall;
		}

		// Token: 0x020000BF RID: 191
		[CompilerGenerated]
		private static class <>O
		{
			// Token: 0x04000520 RID: 1312
			public static Comparison<TileStreamingManager.PendingTile> <0>__CompareByLevelThenNearness;
		}
	}
}
