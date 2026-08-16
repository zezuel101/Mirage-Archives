using System;
using System.Collections.Generic;
using Mirage.WebIngest;
using UnityEngine;

namespace Mirage.VirtualTexture
{
	/// <summary>Per-body VT streamer: require, load, upload, flush, once per frame.</summary>
	/// <summary>Metrics logging, indirection self-check, and Alt+F12 debug snapshot.</summary>
	/// <summary>Bake-as-you-fly: route unbaked tiles to web ingest and commit results.</summary>
	/// <summary>Tile loading, polling, and upload pipeline.</summary>
	/// <summary>Seed building, ancestor chains, and screen-space descent.</summary>
	// Token: 0x02000057 RID: 87
	internal sealed class BodyStreamer
	{
		/// <summary>Per-body VT streamer: require, load, upload, flush, once per frame.</summary>
		// Token: 0x0600026F RID: 623 RVA: 0x00012224 File Offset: 0x00010424
		public BodyStreamer(string sphereName, IMirageBody body)
		{
		}

		// Token: 0x06000270 RID: 624 RVA: 0x00012304 File Offset: 0x00010504
		public void Update(int frame)
		{
			TileCache cache = this.body.Cache;
			bool flag = cache == null;
			if (!flag)
			{
				this.CollectRequired();
				this.BuildQueues(cache, frame);
				this.SortQueues();
				this.ServiceIngest(frame);
				using (StreamProfiling.StartLoads.Measure())
				{
					this.StartLoads(cache);
				}
				using (StreamProfiling.Drain.Measure())
				{
					this.tilesLoaded = this.DrainInFlight(cache, frame);
				}
				using (StreamProfiling.ApplyPageTable.Measure())
				{
					cache.ApplyPageTable();
				}
				this.CheckIndirection(cache, frame);
				this.LogMetrics(cache);
			}
		}

		/// <summary>Release all owned resources; the body is going away.</summary>
		// Token: 0x06000271 RID: 625 RVA: 0x000123F8 File Offset: 0x000105F8
		public void Shutdown()
		{
			foreach (BodyStreamer.InFlightGroup group in this.inFlight)
			{
				group.DisposeHandles();
			}
			while (this.completed.Count > 0)
			{
				this.completed.Dequeue().DisposeHandles();
			}
			this.ShutdownIngest();
		}

		/// <summary>Rebuild <see cref="F:Mirage.VirtualTexture.BodyStreamer.required" /> from visible leaf quads.</summary>
		// Token: 0x06000272 RID: 626 RVA: 0x0001247C File Offset: 0x0001067C
		private void CollectRequired()
		{
			this.leafQuads.Clear();
			using (StreamProfiling.Leaves.Measure())
			{
				this.body.EnumerateVisibleLeafQuads(this.leafQuads);
			}
			VTLevelContext levelContext;
			bool hasLevelContext;
			using (StreamProfiling.LevelContext.Measure())
			{
				hasLevelContext = this.body.TryGetLevelContext(out levelContext);
			}
			int maxLevel = this.body.StreamingMaxLevel;
			bool rebuildSeeds = this.seedsLeafVersion != this.body.LeafSetVersion || this.seedsMaxLevel != maxLevel;
			using (StreamProfiling.Collect.Measure())
			{
				this.CollectRequiredTiles(maxLevel, rebuildSeeds, hasLevelContext, levelContext);
			}
			this.seedsLeafVersion = this.body.LeafSetVersion;
			this.seedsMaxLevel = maxLevel;
			this.tilesRequested = this.required.Count;
		}

		/// <summary>Refresh LRUs and queue non-resident tiles.</summary>
		// Token: 0x06000273 RID: 627 RVA: 0x0001259C File Offset: 0x0001079C
		private void BuildQueues(TileCache cache, int frame)
		{
			this.ExpireKnownMissing();
			this.queue.Clear();
			this.ingestQueue.Clear();
			bool fineTier = cache.HasFineTier;
			int dirLevel = cache.DirectoryLevel;
			bool touchBudget = this.ShouldTouchDiskBudget(frame);
			using (StreamProfiling.RequiredPass.Measure())
			{
				foreach (KeyValuePair<long, int> req in this.required)
				{
					bool resident = cache.TryMarkTileUsed(req.Key, frame);
					bool flag = resident && !fineTier && !touchBudget;
					if (!flag)
					{
						int face;
						int level;
						int tx;
						int ty;
						TileCache.UnpackKey(req.Key, out face, out level, out tx, out ty);
						bool flag2 = touchBudget;
						if (flag2)
						{
							using (StreamProfiling.BudgetTouch.Auto())
							{
								this.budget.Touch(BodyStreamer.ArchiveKey(face, level, tx, ty), frame);
							}
						}
						bool flag3 = fineTier && level > dirLevel;
						if (flag3)
						{
							using (StreamProfiling.TouchBlock.Auto())
							{
								int shift = level - dirLevel;
								cache.TouchBlock(face, tx >> shift, ty >> shift, frame);
							}
						}
						bool flag4 = resident || this.loading.Contains(req.Key);
						if (!flag4)
						{
							BodyStreamer.PendingTile pendingTile = new BodyStreamer.PendingTile(req.Key, face, level, tx, ty, req.Value);
							this.Enqueue(cache, pendingTile, frame);
						}
					}
				}
			}
		}

		/// <summary>Route a tile to the disk loader or the ingest queue.</summary>
		// Token: 0x06000274 RID: 628 RVA: 0x000127B8 File Offset: 0x000109B8
		private void Enqueue(TileCache cache, in BodyStreamer.PendingTile pending, int frame)
		{
			bool onDisk = true;
			bool flag = this.ingest != null;
			if (flag)
			{
				using (StreamProfiling.QueueOnDisk.Auto())
				{
					onDisk = cache.AllLayersHave(pending.face, pending.level, pending.tx, pending.ty);
				}
			}
			bool flag2 = !onDisk;
			if (flag2)
			{
				ulong archiveKey = BodyStreamer.ArchiveKey(pending.face, pending.level, pending.tx, pending.ty);
				bool flag3 = !this.ingest.IsBlocked(archiveKey, frame);
				if (flag3)
				{
					this.ingestQueue.Add(pending);
				}
			}
			else
			{
				bool flag4 = !this.knownMissing.Contains(pending.key);
				if (flag4)
				{
					this.queue.Add(pending);
				}
			}
		}

		// Token: 0x06000275 RID: 629 RVA: 0x000128A4 File Offset: 0x00010AA4
		private void SortQueues()
		{
			using (StreamProfiling.SortQueues.Measure())
			{
				this.queue.Sort(BodyStreamer.s_PendingOrder);
				this.ingestQueue.Sort(BodyStreamer.s_PendingOrder);
			}
		}

		// Token: 0x06000276 RID: 630 RVA: 0x00012904 File Offset: 0x00010B04
		private void ExpireKnownMissing()
		{
			int num = this.framesSinceMissingReset + 1;
			this.framesSinceMissingReset = num;
			bool flag = num < 600;
			if (!flag)
			{
				this.framesSinceMissingReset = 0;
				this.knownMissing.Clear();
			}
		}

		// Token: 0x06000277 RID: 631 RVA: 0x00012943 File Offset: 0x00010B43
		private bool ShouldTouchDiskBudget(int frame)
		{
			bool result;
			if (this.budget != null)
			{
				string text = this.sphereName;
				result = ((frame + ((text != null) ? text.Length : 0)) % 30 == 0);
			}
			else
			{
				result = false;
			}
			return result;
		}

		// Token: 0x06000278 RID: 632 RVA: 0x0001296C File Offset: 0x00010B6C
		private void CheckIndirection(TileCache cache, int frame)
		{
			bool flag = !TileStreamingManager.ValidateEveryFrame || this.indirectionReported;
			if (!flag)
			{
				this.validationReport.Clear();
				bool flag2 = cache.ValidateIndirection(this.validationReport, 8, false) == 0;
				if (!flag2)
				{
					this.validationReport.Clear();
					this.indirectionViolations = cache.ValidateIndirection(this.validationReport, 16, true);
					this.indirectionReported = true;
					MirageDebug.LogError(string.Format("[VT Validate] {0} frame {1}: {2} indirection ", this.sphereName, frame, this.indirectionViolations) + string.Format("violation(s) — slots={0}/{1} ", cache.OccupiedSlots, cache.TotalSlots) + string.Format("blocks={0}/{1}\n  ", cache.OccupiedBlocks, cache.TotalBlocks) + string.Join("\n  ", this.validationReport));
				}
			}
		}

		// Token: 0x06000279 RID: 633 RVA: 0x00012A60 File Offset: 0x00010C60
		private void LogMetrics(TileCache cache)
		{
			int num = this.framesSinceLastLog + 1;
			this.framesSinceLastLog = num;
			bool flag = num < 6000;
			if (!flag)
			{
				this.framesSinceLastLog = 0;
				using (StreamProfiling.Metrics.Measure())
				{
					MirageDebug.Log(string.Concat(new string[]
					{
						string.Format("[VT Stream] {0}  req={1}  visited={2}  ", this.sphereName, this.tilesRequested, this.descendVisited),
						string.Format("leaves={0}  loaded={1}  ", this.leafQuads.Count, this.tilesLoaded),
						string.Format("slots={0}/{1}  queue={2}  ", cache.OccupiedSlots, cache.TotalSlots, this.queue.Count),
						string.Format("flight={0}  loading={1}  ", this.inFlight.Count, this.loading.Count),
						string.Format("completed={0}  missing={1}  ", this.completed.Count, this.knownMissing.Count),
						string.Format("desync={0}", cache.CountPageTableDesync()),
						this.IngestMetrics(),
						FrameProfile.Report()
					}));
					FrameProfile.Reset();
				}
			}
		}

		// Token: 0x0600027A RID: 634 RVA: 0x00012BF0 File Offset: 0x00010DF0
		public BodyDebugInfo Snapshot()
		{
			TileCache cache = this.body.Cache;
			return new BodyDebugInfo
			{
				sphereName = this.sphereName,
				slots = ((cache != null) ? cache.OccupiedSlots : 0),
				total = ((cache != null) ? cache.TotalSlots : 0),
				levelCounts = ((cache != null) ? cache.GetLevelCounts() : null),
				queue = this.queue.Count,
				flight = this.inFlight.Count,
				loading = this.loading.Count,
				completed = this.completed.Count,
				missing = this.knownMissing.Count,
				tilesRequested = this.tilesRequested,
				tilesLoaded = this.tilesLoaded,
				desync = ((cache != null) ? cache.CountPageTableDesync() : 0),
				badIndirection = this.indirectionViolations,
				blocks = ((cache != null) ? cache.OccupiedBlocks : 0),
				totalBlocks = ((cache != null && cache.HasFineTier) ? cache.TotalBlocks : 0),
				dirLevel = ((cache != null) ? cache.DirectoryLevel : 0),
				maxLevel = ((cache != null) ? cache.maxLevel : 0),
				hasIngest = (this.ingest != null),
				ingestPending = this.ingestQueue.Count,
				ingestActive = ((this.ingest != null) ? this.ingest.InProgress : 0),
				ingestBaked = this.tilesIngestedTotal,
				ingestNoCoverage = ((this.ingest != null) ? this.ingest.NoCoverageCount : 0),
				ingestFailed = ((this.ingest != null) ? this.ingest.FailedCount : 0),
				webPhysicalBytes = ((this.budget != null) ? this.budget.PhysicalBytes(this.webTiers) : 0L),
				webLiveBytes = ((this.budget != null) ? this.budget.LiveBytes(this.webTiers) : 0L),
				webCapBytes = ((this.budget != null) ? this.budget.CapBytes : 0L),
				webEvicted = ((this.budget != null) ? this.budget.TotalEvicted : 0),
				webCapTooSmall = (this.budget != null && this.budget.CapTooSmall)
			};
		}

		// Token: 0x0600027B RID: 635 RVA: 0x00012E6C File Offset: 0x0001106C
		private string IngestMetrics()
		{
			return (this.ingest == null) ? "" : string.Concat(new string[]
			{
				string.Format("  ingest[want={0} active={1} ", this.ingestQueue.Count, this.ingest.InProgress),
				string.Format("baked={0} nocov={1} ", this.tilesIngestedTotal, this.ingest.NoCoverageCount),
				string.Format("failed={0}]", this.ingest.FailedCount),
				this.WebTierDiagnostic(),
				BakeProfile.Report()
			});
		}

		// Token: 0x0600027C RID: 636 RVA: 0x00012F1C File Offset: 0x0001111C
		private string WebTierDiagnostic()
		{
			bool flag = this.webTiers == null;
			string result;
			if (flag)
			{
				result = "";
			}
			else
			{
				int wantButCached = 0;
				for (int i = 0; i < this.ingestQueue.Count; i++)
				{
					BodyStreamer.PendingTile p = this.ingestQueue[i];
					bool flag2 = BodyStreamer.AllTiersContain(this.webTiers, BodyStreamer.ArchiveKey(p.face, p.level, p.tx, p.ty));
					if (flag2)
					{
						wantButCached++;
					}
				}
				string[] counts = new string[this.webTiers.Length];
				for (int t = 0; t < this.webTiers.Length; t++)
				{
					counts[t] = ((this.webTiers[t] == null) ? "-" : this.webTiers[t].Count.ToString());
				}
				result = string.Format("  web[chn={0} wantButCached={1}]", string.Join("/", counts), wantButCached);
			}
			return result;
		}

		// Token: 0x0600027D RID: 637 RVA: 0x00013024 File Offset: 0x00011224
		private static bool AllTiersContain(WebTileArchive[] tiers, ulong archiveKey)
		{
			int installed = 0;
			for (int t = 0; t < tiers.Length; t++)
			{
				bool flag = tiers[t] == null;
				if (!flag)
				{
					installed++;
					bool flag2 = !tiers[t].Contains(archiveKey);
					if (flag2)
					{
						return false;
					}
				}
			}
			return installed > 0;
		}

		// Token: 0x0600027E RID: 638 RVA: 0x0001307C File Offset: 0x0001127C
		public void EnableIngest(ITileBaker baker, long diskCapBytes, int maxConcurrent)
		{
			TileIngestQueue tileIngestQueue = this.ingest;
			if (tileIngestQueue != null)
			{
				tileIngestQueue.Shutdown();
			}
			this.ingest = new TileIngestQueue(baker, maxConcurrent);
			this.webTiers = new WebTileArchive[]
			{
				this.config.GetWebArchive(VTLayer.Color),
				this.config.GetWebArchive(VTLayer.Height),
				this.config.GetWebArchive(VTLayer.Normal)
			};
			this.budget = new WebDiskBudget(diskCapBytes);
			this.budget.Seed(this.webTiers);
			MirageDebug.Log(string.Concat(new string[]
			{
				"TileStreamingManager: web ingest enabled for '",
				this.sphereName,
				"' ",
				string.Format("(max {0} concurrent, disk cap {1} MB, ", maxConcurrent, diskCapBytes / 1048576L),
				string.Format("{0} tile(s) already baked).", this.budget.TrackedKeys)
			}));
		}

		/// <summary>Offer the ingest queue to bakers and commit results.</summary>
		// Token: 0x0600027F RID: 639 RVA: 0x00013168 File Offset: 0x00011368
		private void ServiceIngest(int frame)
		{
			bool flag = this.ingest == null;
			if (!flag)
			{
				using (StreamProfiling.Ingest.Measure())
				{
					int i = 0;
					while (i < this.ingestQueue.Count && this.ingest.HasCapacity)
					{
						BodyStreamer.PendingTile p = this.ingestQueue[i];
						this.ingest.TryRequest(p.face, p.level, p.tx, p.ty, frame);
						i++;
					}
					this.tilesIngestedTotal += this.ingest.Drain((BakedTile t) => this.CommitBakedTile(t, frame), frame, 2);
					using (StreamProfiling.EnforceBudget.Auto())
					{
						WebDiskBudget webDiskBudget = this.budget;
						if (webDiskBudget != null)
						{
							webDiskBudget.Enforce(this.webTiers, frame);
						}
					}
					FrameProfile.NoteIngest(this.ingest.InProgress, WebTileFetcher.ActiveDownloads, WebTileFetcher.QueuedCount);
				}
			}
		}

		// Token: 0x06000280 RID: 640 RVA: 0x000132C4 File Offset: 0x000114C4
		private void ShutdownIngest()
		{
			TileIngestQueue tileIngestQueue = this.ingest;
			if (tileIngestQueue != null)
			{
				tileIngestQueue.Shutdown();
			}
			bool flag = this.budget == null || this.webTiers == null;
			if (!flag)
			{
				this.budget.MaybeCompact(this.webTiers, false);
				foreach (WebTileArchive tier in this.webTiers)
				{
					if (tier != null)
					{
						tier.Flush();
					}
				}
			}
		}

		// Token: 0x06000281 RID: 641 RVA: 0x00013337 File Offset: 0x00011537
		private static ulong ArchiveKey(int face, int level, int tx, int ty)
		{
			return MirageArchiveFormat.PackKey(face, level, tx, ty);
		}

		/// <summary>Commit a baked tile to web archives. False = incomplete, retry.</summary>
		// Token: 0x06000282 RID: 642 RVA: 0x00013344 File Offset: 0x00011544
		private bool CommitBakedTile(BakedTile tile, int frame)
		{
			bool ok;
			using (StreamProfiling.Commit.Measure())
			{
				ok = this.AppendToWebTiers(tile);
			}
			bool flag = !ok;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				WebDiskBudget webDiskBudget = this.budget;
				if (webDiskBudget != null)
				{
					webDiskBudget.OnBaked(tile.key, frame);
				}
				result = true;
			}
			return result;
		}

		// Token: 0x06000283 RID: 643 RVA: 0x000133B0 File Offset: 0x000115B0
		private bool AppendToWebTiers(BakedTile tile)
		{
			IReadOnlyList<TileLayerAtlases.Layer> layers = this.body.Cache.Layers;
			bool result;
			try
			{
				for (int i = 0; i < layers.Count; i++)
				{
					VTLayer layer = layers[i].id;
					bool flag = !this.body.Cache.LayerCoversLevel(i, tile.level);
					if (!flag)
					{
						byte[] stored = tile.stored[(int)layer];
						bool flag2 = stored == null;
						if (flag2)
						{
							MirageDebug.LogError(string.Format("TileIngest: baker returned no {0} payload for L{1} ", layer, tile.level) + string.Format("f{0} {1},{2}, but the body has a {3} layer ", new object[]
							{
								tile.face,
								tile.tx,
								tile.ty,
								layer
							}) + "— refusing to commit a partial group.");
							return false;
						}
						WebTileArchive web = this.config.GetWebArchive(layer);
						bool flag3 = web == null;
						if (flag3)
						{
							MirageDebug.LogError(string.Format("TileIngest: no {0} web tier for '{1}' — cannot commit.", layer, this.sphereName));
							return false;
						}
						web.Append(tile.key, stored, tile.format[(int)layer], tile.codec[(int)layer], tile.crc[(int)layer]);
					}
				}
				result = true;
			}
			catch (Exception e)
			{
				MirageDebug.LogError(string.Format("TileIngest: commit failed for L{0} f{1} {2},{3}: ", new object[]
				{
					tile.level,
					tile.face,
					tile.tx,
					tile.ty
				}) + e.Message);
				result = false;
			}
			return result;
		}

		/// <summary>Begin async reads within the concurrency budget.</summary>
		// Token: 0x06000284 RID: 644 RVA: 0x00013598 File Offset: 0x00011798
		private void StartLoads(TileCache cache)
		{
			IReadOnlyList<TileLayerAtlases.Layer> layers = cache.Layers;
			int starts = Mathf.Min(16 - this.inFlight.Count - this.completed.Count, 4);
			int i = 0;
			while (i < this.queue.Count && starts > 0)
			{
				BodyStreamer.PendingTile p = this.queue[i];
				bool flag = this.loading.Contains(p.key);
				if (!flag)
				{
					BodyStreamer.InFlightGroup group = BodyStreamer.RentGroup(layers.Count);
					group.Reset(p.key, p.face, p.level, p.tx, p.ty);
					using (StreamProfiling.BeginLoad.Auto())
					{
						for (int li = 0; li < layers.Count; li++)
						{
							group.handles[li] = (cache.LayerCoversLevel(li, p.level) ? layers[li].source.BeginLoad(p.face, p.level, p.tx, p.ty) : SkippedReadHandle.Instance);
						}
					}
					this.loading.Add(p.key);
					this.inFlight.Add(group);
					starts--;
				}
				i++;
			}
		}

		/// <summary>Tick in-flight groups and upload landed ones. Returns blits spent.</summary>
		// Token: 0x06000285 RID: 645 RVA: 0x00013710 File Offset: 0x00011910
		private int DrainInFlight(TileCache cache, int frame)
		{
			this.PollInFlight(cache);
			return this.UploadCompleted(cache, frame);
		}

		// Token: 0x06000286 RID: 646 RVA: 0x00013734 File Offset: 0x00011934
		private void PollInFlight(TileCache cache)
		{
			using (StreamProfiling.PollHandles.Auto())
			{
				for (int i = this.inFlight.Count - 1; i >= 0; i--)
				{
					BodyStreamer.InFlightGroup group = this.inFlight[i];
					string faultedLayers;
					group.remaining = BodyStreamer.PollHandles(group, cache, out faultedLayers);
					bool failed = group.failed;
					if (failed)
					{
						MirageDebug.LogError(string.Format("TileStreamingManager: tile load failed for L{0} ", group.level) + string.Format("face{0} {1},{2} — layer(s): {3}", new object[]
						{
							group.face,
							group.tx,
							group.ty,
							faultedLayers
						}));
						group.DisposeHandles();
						this.loading.Remove(group.key);
						this.knownMissing.Add(group.key);
						this.inFlight.RemoveAt(i);
						BodyStreamer.ReleaseGroup(group);
					}
					else
					{
						bool flag = group.remaining == 0;
						if (flag)
						{
							this.completed.Enqueue(group);
							this.inFlight.RemoveAt(i);
						}
					}
				}
			}
		}

		/// <summary>Poll one group's handles. Returns pending count, names faulted layers.</summary>
		// Token: 0x06000287 RID: 647 RVA: 0x000138A0 File Offset: 0x00011AA0
		private static int PollHandles(BodyStreamer.InFlightGroup group, TileCache cache, out string faultedLayers)
		{
			faultedLayers = null;
			int stillPending = 0;
			for (int li = 0; li < group.handles.Length; li++)
			{
				bool flag = (group.doneMask & 1 << li) != 0;
				if (!flag)
				{
					TileReadHandle handle = group.handles[li];
					bool isFaulted = handle.IsFaulted;
					if (isFaulted)
					{
						group.failed = true;
						faultedLayers = ((faultedLayers == null) ? cache.Layers[li].id.ToString() : (faultedLayers + "+" + cache.Layers[li].id.ToString()));
					}
					else
					{
						bool isComplete = handle.IsComplete;
						if (isComplete)
						{
							group.doneMask |= 1 << li;
						}
						else
						{
							stillPending++;
							bool flag2 = !group.failed;
							if (flag2)
							{
								break;
							}
						}
					}
				}
			}
			return stillPending;
		}

		/// <summary>Upload completed groups up to the per-frame blit budget.</summary>
		// Token: 0x06000288 RID: 648 RVA: 0x0001399C File Offset: 0x00011B9C
		private int UploadCompleted(TileCache cache, int frame)
		{
			IReadOnlyList<TileLayerAtlases.Layer> layers = cache.Layers;
			bool flag = this.uploadScratch == null || this.uploadScratch.Length < layers.Count;
			if (flag)
			{
				this.uploadScratch = new Texture2D[layers.Count];
			}
			int uploaded = 0;
			while (uploaded < 12 && this.completed.Count > 0)
			{
				bool flag2 = !cache.HasUploadCapacity(frame);
				if (flag2)
				{
					break;
				}
				BodyStreamer.InFlightGroup group = this.completed.Dequeue();
				this.loading.Remove(group.key);
				bool flag3 = !BodyStreamer.TryTakeTextures(group, layers, this.uploadScratch);
				if (flag3)
				{
					this.knownMissing.Add(group.key);
					group.DisposeHandles();
					BodyStreamer.ReleaseGroup(group);
				}
				else
				{
					int blits;
					TileCache.TileUploadResult result;
					using (StreamProfiling.Upload.Measure())
					{
						result = cache.TryUploadTile(group.face, group.level, group.tx, group.ty, this.uploadScratch, frame, out blits);
					}
					using (StreamProfiling.DisposeHandles.Measure())
					{
						group.DisposeHandles();
					}
					switch (result)
					{
					case TileCache.TileUploadResult.Uploaded:
						uploaded += blits;
						break;
					case TileCache.TileUploadResult.Rejected:
						this.knownMissing.Add(group.key);
						break;
					}
					BodyStreamer.ReleaseGroup(group);
				}
			}
			return uploaded;
		}

		/// <summary>Materialize every layer's texture. False = unusable, caller must dispose.</summary>
		// Token: 0x06000289 RID: 649 RVA: 0x00013B48 File Offset: 0x00011D48
		private static bool TryTakeTextures(BodyStreamer.InFlightGroup group, IReadOnlyList<TileLayerAtlases.Layer> layers, Texture2D[] tiles)
		{
			bool result;
			using (StreamProfiling.GetTexture.Measure())
			{
				for (int li = 0; li < layers.Count; li++)
				{
					try
					{
						tiles[li] = group.handles[li].GetTexture();
					}
					catch (Exception e)
					{
						MirageDebug.LogError(string.Format("TileStreamingManager: GetTexture failed for {0} ", layers[li].id) + string.Format("L{0} face{1} {2},{3}: {4}", new object[]
						{
							group.level,
							group.face,
							group.tx,
							group.ty,
							e.Message
						}));
						return false;
					}
				}
				result = true;
			}
			return result;
		}

		// Token: 0x0600028A RID: 650 RVA: 0x00013C44 File Offset: 0x00011E44
		private static BodyStreamer.InFlightGroup RentGroup(int layerCount)
		{
			BodyStreamer.InFlightGroup group = (BodyStreamer.s_GroupPool.Count > 0) ? BodyStreamer.s_GroupPool.Pop() : new BodyStreamer.InFlightGroup();
			bool flag = group.handles == null || group.handles.Length != layerCount;
			if (flag)
			{
				group.handles = new TileReadHandle[layerCount];
			}
			return group;
		}

		// Token: 0x0600028B RID: 651 RVA: 0x00013CA0 File Offset: 0x00011EA0
		private static void ReleaseGroup(BodyStreamer.InFlightGroup group)
		{
			bool flag = group == null;
			if (!flag)
			{
				for (int i = 0; i < group.handles.Length; i++)
				{
					group.handles[i] = null;
				}
				BodyStreamer.s_GroupPool.Push(group);
			}
		}

		/// <summary>Fill <see cref="F:Mirage.VirtualTexture.BodyStreamer.required" /> from visible quads: seeds, ancestors, then descent.</summary>
		// Token: 0x0600028C RID: 652 RVA: 0x00013CE4 File Offset: 0x00011EE4
		private void CollectRequiredTiles(int maxLevel, bool rebuildSeeds, bool hasLevelContext, in VTLevelContext ctx)
		{
			this.required.Clear();
			using (StreamProfiling.Seeds.Auto())
			{
				if (rebuildSeeds)
				{
					using (StreamProfiling.SeedDedupe.Auto())
					{
						this.BuildSeeds(maxLevel);
					}
					using (StreamProfiling.AncestorWalk.Auto())
					{
						this.BuildAncestorChains();
					}
				}
				using (StreamProfiling.AncestorReplay.Auto())
				{
					for (int i = 0; i < this.ancestors.Count; i++)
					{
						this.required[this.ancestors[i].key] = this.ancestors[i].nearness;
					}
				}
			}
			bool flag = !hasLevelContext;
			if (!flag)
			{
				float pixelThreshold = (float)this.config.tileSize / MirageSettings.Oversample;
				DescentContext descent = new DescentContext(ctx, maxLevel, this.config.tileSize, this.config.borderPx, ctx.PixelsPerUnitTangent / pixelThreshold);
				using (StreamProfiling.Descend.Auto())
				{
					this.descendVisited = 0;
					this.CullSeeds(descent);
					this.WalkSeeds(descent);
				}
			}
		}

		/// <summary>Dedupe leaf quads into seed tiles, keeping the deepest subdivision.</summary>
		// Token: 0x0600028D RID: 653 RVA: 0x00013EB0 File Offset: 0x000120B0
		private void BuildSeeds(int maxLevel)
		{
			this.seedScratch.Clear();
			for (int i = 0; i < this.leafQuads.Count; i++)
			{
				LeafQuad quad = this.leafQuads[i];
				int seedLevel = Mathf.Min(quad.Subdivision, maxLevel);
				int stx;
				int sty;
				TileGeometry.GetCorrectedTileCoord(quad.Face, (float)quad.UvSwX, (float)quad.UvSwY, seedLevel, out stx, out sty);
				long key = TileCache.PackKey(quad.Face, seedLevel, stx, sty);
				int seen;
				bool flag = !this.seedScratch.TryGetValue(key, out seen) || quad.Subdivision > seen;
				if (flag)
				{
					this.seedScratch[key] = quad.Subdivision;
				}
			}
			this.seeds.Reset(this.seedScratch.Count);
			foreach (KeyValuePair<long, int> seed in this.seedScratch)
			{
				int face;
				int level;
				int tx;
				int ty;
				TileCache.UnpackKey(seed.Key, out face, out level, out tx, out ty);
				Vector3 local;
				float extentScale;
				TileGeometry.TileLocal(face, level, tx, ty, this.config.tileSize, this.config.borderPx, out local, out extentScale);
				BodyStreamer.SeedSet seedSet = this.seeds;
				int count = seedSet.count;
				seedSet.count = count + 1;
				int w = count;
				this.seeds.cull[w].localDir = local;
				this.seeds.cull[w].extentScale = extentScale;
				this.seeds.cull[w].level = level;
				this.seeds.walk[w].face = face;
				this.seeds.walk[w].level = level;
				this.seeds.walk[w].tx = tx;
				this.seeds.walk[w].ty = ty;
				this.seeds.walk[w].nearness = seed.Value;
			}
		}

		/// <summary>Walk each seed's ancestor chain down to PinnedMaxLevel+1 into <see cref="F:Mirage.VirtualTexture.BodyStreamer.ancestors" />.</summary>
		// Token: 0x0600028E RID: 654 RVA: 0x00014104 File Offset: 0x00012304
		private void BuildAncestorChains()
		{
			this.ancestorScratch.Clear();
			for (int i = 0; i < this.seeds.count; i++)
			{
				int face = this.seeds.walk[i].face;
				int atx = this.seeds.walk[i].tx;
				int aty = this.seeds.walk[i].ty;
				int nearness = this.seeds.walk[i].nearness;
				for (int level = this.seeds.walk[i].level; level > 0; level--)
				{
					long key = TileCache.PackKey(face, level, atx, aty);
					int nearest;
					bool flag = this.ancestorScratch.TryGetValue(key, out nearest) && nearest >= nearness;
					if (flag)
					{
						break;
					}
					this.ancestorScratch[key] = nearness;
					atx >>= 1;
					aty >>= 1;
				}
			}
			this.ancestors.Clear();
			bool flag2 = this.ancestors.Capacity < this.ancestorScratch.Count;
			if (flag2)
			{
				this.ancestors.Capacity = this.ancestorScratch.Count;
			}
			foreach (KeyValuePair<long, int> ancestor in this.ancestorScratch)
			{
				this.ancestors.Add(new BodyStreamer.RequiredEntry(ancestor.Key, ancestor.Value));
			}
		}

		// Token: 0x0600028F RID: 655 RVA: 0x000142B8 File Offset: 0x000124B8
		private void CullSeeds(in DescentContext d)
		{
			using (StreamProfiling.SeedCull.Auto())
			{
				bool flag = this.visibleSeeds.Length < this.seeds.count;
				if (flag)
				{
					this.visibleSeeds = new BodyStreamer.VisibleSeed[Mathf.NextPowerOfTwo(this.seeds.count)];
				}
				this.visibleSeedCount = 0;
				for (int i = 0; i < this.seeds.count; i++)
				{
					Vector3 center;
					float extent;
					Vector3 dirWorld;
					TileGeometry.PlaceTile(d.Ctx, this.seeds.cull[i].localDir, this.seeds.cull[i].extentScale, out center, out extent, out dirWorld);
					bool visible = TileGeometry.Visible(d.Ctx, center, dirWorld, extent * 1.5f, this.seeds.cull[i].level);
					bool flag2 = !visible;
					if (!flag2)
					{
						BodyStreamer.VisibleSeed[] array = this.visibleSeeds;
						int num = this.visibleSeedCount;
						this.visibleSeedCount = num + 1;
						array[num] = new BodyStreamer.VisibleSeed(i, center, extent);
					}
				}
			}
		}

		/// <summary>Run the screen-space descent under every seed that survived the cull.</summary>
		// Token: 0x06000290 RID: 656 RVA: 0x00014408 File Offset: 0x00012608
		private void WalkSeeds(in DescentContext d)
		{
			using (StreamProfiling.DescendWalk.Auto())
			{
				for (int i = 0; i < this.visibleSeedCount; i++)
				{
					int si = this.visibleSeeds[i].index;
					this.Descend(d, this.seeds.walk[si].face, this.seeds.walk[si].level, this.seeds.walk[si].tx, this.seeds.walk[si].ty, this.seeds.walk[si].nearness, this.visibleSeeds[i].center, this.visibleSeeds[i].extent);
				}
			}
		}

		/// <summary>Subdivide while the tile covers more than threshold pixels on screen.</summary>
		// Token: 0x06000291 RID: 657 RVA: 0x00014510 File Offset: 0x00012710
		private void Descend(in DescentContext d, int face, int level, int tx, int ty, int nearness, Vector3 center, float extent)
		{
			bool flag = level >= d.MaxLevel || this.required.Count >= 4096;
			if (!flag)
			{
				bool flag2 = !TileGeometry.UnderResolved(d, center, extent);
				if (!flag2)
				{
					int child = level + 1;
					for (int dy = 0; dy < 2; dy++)
					{
						for (int dx = 0; dx < 2; dx++)
						{
							int cx = tx * 2 + dx;
							int cy = ty * 2 + dy;
							this.descendVisited++;
							Vector3 childCenter;
							float childExtent;
							Vector3 childDir;
							TileGeometry.TileSphere(d, face, child, cx, cy, out childCenter, out childExtent, out childDir);
							bool flag3 = !TileGeometry.Visible(d.Ctx, childCenter, childDir, childExtent, child);
							if (!flag3)
							{
								long key = TileCache.PackKey(face, child, cx, cy);
								int nearest;
								bool flag4 = !this.required.TryGetValue(key, out nearest) || nearness > nearest;
								if (flag4)
								{
									this.required[key] = nearness;
								}
								this.Descend(d, face, child, cx, cy, nearness, childCenter, childExtent);
							}
						}
					}
				}
			}
		}

		// Token: 0x0400022E RID: 558
		private const int MetricsLogInterval = 6000;

		// Token: 0x0400022F RID: 559
		private const int MissingRetryInterval = 600;

		// Token: 0x04000230 RID: 560
		private const int BudgetTouchInterval = 30;

		// Token: 0x04000231 RID: 561
		private readonly string sphereName = sphereName;

		// Token: 0x04000232 RID: 562
		private readonly IMirageBody body = body;

		// Token: 0x04000233 RID: 563
		private readonly VirtualTextureConfig config = body.Config;

		// Token: 0x04000234 RID: 564
		private readonly Dictionary<long, int> required = new Dictionary<long, int>();

		// Token: 0x04000235 RID: 565
		private readonly List<LeafQuad> leafQuads = new List<LeafQuad>();

		// Token: 0x04000236 RID: 566
		private readonly List<BodyStreamer.PendingTile> queue = new List<BodyStreamer.PendingTile>();

		// Token: 0x04000237 RID: 567
		private readonly List<BodyStreamer.PendingTile> ingestQueue = new List<BodyStreamer.PendingTile>();

		// Token: 0x04000238 RID: 568
		private readonly HashSet<long> loading = new HashSet<long>();

		// Token: 0x04000239 RID: 569
		private readonly HashSet<long> knownMissing = new HashSet<long>();

		// Token: 0x0400023A RID: 570
		private int framesSinceMissingReset;

		// Token: 0x0400023B RID: 571
		private int framesSinceLastLog;

		// Token: 0x0400023C RID: 572
		private int tilesRequested;

		// Token: 0x0400023D RID: 573
		private int tilesLoaded;

		// Token: 0x0400023E RID: 574
		private int descendVisited;

		// Token: 0x0400023F RID: 575
		private static readonly IComparer<BodyStreamer.PendingTile> s_PendingOrder = new BodyStreamer.PendingTileComparer();

		// Token: 0x04000240 RID: 576
		private int indirectionViolations;

		// Token: 0x04000241 RID: 577
		private bool indirectionReported;

		// Token: 0x04000242 RID: 578
		private readonly List<string> validationReport = new List<string>();

		// Token: 0x04000243 RID: 579
		private TileIngestQueue ingest;

		// Token: 0x04000244 RID: 580
		private WebDiskBudget budget;

		// Token: 0x04000245 RID: 581
		private WebTileArchive[] webTiers;

		// Token: 0x04000246 RID: 582
		private int tilesIngestedTotal;

		// Token: 0x04000247 RID: 583
		private const int MaxConcurrentLoads = 16;

		// Token: 0x04000248 RID: 584
		private const int MaxUploadsPerFrame = 12;

		// Token: 0x04000249 RID: 585
		private const int MaxLoadStartsPerFrame = 4;

		// Token: 0x0400024A RID: 586
		private readonly List<BodyStreamer.InFlightGroup> inFlight = new List<BodyStreamer.InFlightGroup>();

		// Token: 0x0400024B RID: 587
		private readonly Queue<BodyStreamer.InFlightGroup> completed = new Queue<BodyStreamer.InFlightGroup>();

		// Token: 0x0400024C RID: 588
		private Texture2D[] uploadScratch;

		// Token: 0x0400024D RID: 589
		private static readonly Stack<BodyStreamer.InFlightGroup> s_GroupPool = new Stack<BodyStreamer.InFlightGroup>();

		// Token: 0x0400024E RID: 590
		private const int MaxRequiredTiles = 4096;

		// Token: 0x0400024F RID: 591
		private const float SeedCullRadiusScale = 1.5f;

		// Token: 0x04000250 RID: 592
		private readonly BodyStreamer.SeedSet seeds = new BodyStreamer.SeedSet();

		// Token: 0x04000251 RID: 593
		private int seedsLeafVersion = int.MinValue;

		// Token: 0x04000252 RID: 594
		private int seedsMaxLevel = -1;

		// Token: 0x04000253 RID: 595
		private readonly List<BodyStreamer.RequiredEntry> ancestors = new List<BodyStreamer.RequiredEntry>();

		// Token: 0x04000254 RID: 596
		private BodyStreamer.VisibleSeed[] visibleSeeds = new BodyStreamer.VisibleSeed[1024];

		// Token: 0x04000255 RID: 597
		private int visibleSeedCount;

		// Token: 0x04000256 RID: 598
		private readonly Dictionary<long, int> seedScratch = new Dictionary<long, int>();

		// Token: 0x04000257 RID: 599
		private readonly Dictionary<long, int> ancestorScratch = new Dictionary<long, int>();

		// Token: 0x020000D4 RID: 212
		private readonly struct PendingTile
		{
			// Token: 0x060004C5 RID: 1221 RVA: 0x00022339 File Offset: 0x00020539
			public PendingTile(long key, int face, int level, int tx, int ty, int nearness)
			{
				this.key = key;
				this.face = face;
				this.level = level;
				this.tx = tx;
				this.ty = ty;
				this.nearness = nearness;
			}

			// Token: 0x04000594 RID: 1428
			public readonly long key;

			// Token: 0x04000595 RID: 1429
			public readonly int face;

			// Token: 0x04000596 RID: 1430
			public readonly int level;

			// Token: 0x04000597 RID: 1431
			public readonly int tx;

			// Token: 0x04000598 RID: 1432
			public readonly int ty;

			// Token: 0x04000599 RID: 1433
			public readonly int nearness;
		}

		/// <summary>Coarser level first, then nearest first.</summary>
		// Token: 0x020000D5 RID: 213
		private sealed class PendingTileComparer : IComparer<BodyStreamer.PendingTile>
		{
			// Token: 0x060004C6 RID: 1222 RVA: 0x00022368 File Offset: 0x00020568
			public int Compare(BodyStreamer.PendingTile a, BodyStreamer.PendingTile b)
			{
				int byLevel = a.level.CompareTo(b.level);
				return (byLevel != 0) ? byLevel : b.nearness.CompareTo(a.nearness);
			}
		}

		// Token: 0x020000D6 RID: 214
		private sealed class InFlightGroup
		{
			// Token: 0x060004C8 RID: 1224 RVA: 0x000223B4 File Offset: 0x000205B4
			public void Reset(long key, int face, int level, int tx, int ty)
			{
				this.key = key;
				this.face = face;
				this.level = level;
				this.tx = tx;
				this.ty = ty;
				this.failed = false;
				this.remaining = this.handles.Length;
				this.doneMask = 0;
			}

			// Token: 0x060004C9 RID: 1225 RVA: 0x00022404 File Offset: 0x00020604
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

			// Token: 0x0400059A RID: 1434
			public long key;

			// Token: 0x0400059B RID: 1435
			public int face;

			// Token: 0x0400059C RID: 1436
			public int level;

			// Token: 0x0400059D RID: 1437
			public int tx;

			// Token: 0x0400059E RID: 1438
			public int ty;

			// Token: 0x0400059F RID: 1439
			public TileReadHandle[] handles;

			// Token: 0x040005A0 RID: 1440
			public int remaining;

			// Token: 0x040005A1 RID: 1441
			public bool failed;

			// Token: 0x040005A2 RID: 1442
			public int doneMask;
		}

		// Token: 0x020000D7 RID: 215
		private readonly struct RequiredEntry
		{
			// Token: 0x060004CB RID: 1227 RVA: 0x0002244F File Offset: 0x0002064F
			public RequiredEntry(long key, int nearness)
			{
				this.key = key;
				this.nearness = nearness;
			}

			// Token: 0x040005A3 RID: 1443
			public readonly long key;

			// Token: 0x040005A4 RID: 1444
			public readonly int nearness;
		}

		// Token: 0x020000D8 RID: 216
		private readonly struct VisibleSeed
		{
			// Token: 0x060004CC RID: 1228 RVA: 0x0002245F File Offset: 0x0002065F
			public VisibleSeed(int index, Vector3 center, float extent)
			{
				this.index = index;
				this.center = center;
				this.extent = extent;
			}

			// Token: 0x040005A5 RID: 1445
			public readonly int index;

			// Token: 0x040005A6 RID: 1446
			public readonly Vector3 center;

			// Token: 0x040005A7 RID: 1447
			public readonly float extent;
		}

		// Token: 0x020000D9 RID: 217
		private sealed class SeedSet
		{
			// Token: 0x060004CD RID: 1229 RVA: 0x00022478 File Offset: 0x00020678
			public void Reset(int capacity)
			{
				bool flag = this.cull.Length < capacity;
				if (flag)
				{
					this.cull = new BodyStreamer.SeedSet.Cull[Mathf.NextPowerOfTwo(capacity)];
					this.walk = new BodyStreamer.SeedSet.Walk[this.cull.Length];
				}
				this.count = 0;
			}

			// Token: 0x040005A8 RID: 1448
			public BodyStreamer.SeedSet.Cull[] cull = new BodyStreamer.SeedSet.Cull[1024];

			// Token: 0x040005A9 RID: 1449
			public BodyStreamer.SeedSet.Walk[] walk = new BodyStreamer.SeedSet.Walk[1024];

			// Token: 0x040005AA RID: 1450
			public int count;

			// Token: 0x020000EE RID: 238
			public struct Cull
			{
				// Token: 0x040005E8 RID: 1512
				public Vector3 localDir;

				// Token: 0x040005E9 RID: 1513
				public float extentScale;

				// Token: 0x040005EA RID: 1514
				public int level;
			}

			// Token: 0x020000EF RID: 239
			public struct Walk
			{
				// Token: 0x040005EB RID: 1515
				public int face;

				// Token: 0x040005EC RID: 1516
				public int level;

				// Token: 0x040005ED RID: 1517
				public int tx;

				// Token: 0x040005EE RID: 1518
				public int ty;

				// Token: 0x040005EF RID: 1519
				public int nearness;
			}
		}
	}
}
