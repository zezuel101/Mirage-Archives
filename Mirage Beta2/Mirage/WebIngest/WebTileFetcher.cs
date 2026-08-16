using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mirage.WebIngest
{
	/// <summary>
	/// Fetches raw Web-Mercator imagery tiles as BYTES. WebIngest P0.
	///
	/// Ported from GeoStream's GeoStreamTiles with its hard-won operational behaviour intact — bounded
	/// concurrency, retry with backoff, in-flight dedup, round-robin fairness across callers, and content
	/// validation — but with three deliberate departures:
	///
	///  1. <b>DownloadHandlerBuffer, not DownloadHandlerTexture</b> (§3). We own the decode (§6), so we never
	///     want Unity decoding to a Texture2D. This alone deletes GeoStream's whole 30-frame
	///     GetNativeTexturePtr() wait: that dance existed only because DownloadHandlerTexture produced a GPU
	///     texture that could be reported ready before its GPU resource existed. No texture, no race. (That fix
	///     was originally lifted FROM Mirage; it stops applying the moment we stop asking Unity for a texture.)
	///  2. <b>No mercator disk cache.</b> GeoStream cached JPEGs on disk forever because for GeoStream the
	///     mercator tile WAS the final artifact. Mirage's durable artifact is the baked BC7 cube tile in the web
	///     archive; a mercator JPEG is scaffolding needed once, to bake once. Caching it permanently would double
	///     disk for intermediates. Adjacent cube tiles DO share mercator sources, so a bounded in-memory byte LRU
	///     captures the reuse that actually exists, without a second on-disk tier to cap, evict and compact.
	///  3. <b>Bytes are never turned into a Texture2D here</b>, so nothing in this file touches the GPU and the
	///     only main-thread work is UnityWebRequest bookkeeping.
	///
	/// Lifetime: hosted on a DontDestroyOnLoad object. GeoStream learned this the hard way — Unity kills a
	/// MonoBehaviour's coroutines when its GameObject dies, so a scene-scoped host silently stranded in-flight
	/// downloads and permanently leaked concurrency slots until downloading stopped altogether with no errors
	/// logged. A persistent host removes the failure mode instead of mitigating it.
	/// </summary>
	// Token: 0x02000031 RID: 49
	public static class WebTileFetcher
	{
		// Token: 0x17000032 RID: 50
		// (get) Token: 0x06000112 RID: 274 RVA: 0x00009BB8 File Offset: 0x00007DB8
		public static int ActiveDownloads
		{
			get
			{
				return WebTileFetcher.s_ActiveDownloads;
			}
		}

		// Token: 0x17000033 RID: 51
		// (get) Token: 0x06000113 RID: 275 RVA: 0x00009BC0 File Offset: 0x00007DC0
		public static int QueuedCount
		{
			get
			{
				int i = 0;
				foreach (Queue<WebTileFetcher.QueuedTile> q in WebTileFetcher.s_QueuesByGroup.Values)
				{
					i += q.Count;
				}
				return i;
			}
		}

		// Token: 0x17000034 RID: 52
		// (get) Token: 0x06000114 RID: 276 RVA: 0x00009C24 File Offset: 0x00007E24
		public static int CachedTiles
		{
			get
			{
				return WebTileFetcher.s_Cache.Count;
			}
		}

		// Token: 0x17000035 RID: 53
		// (get) Token: 0x06000115 RID: 277 RVA: 0x00009C30 File Offset: 0x00007E30
		public static long CachedBytes
		{
			get
			{
				return WebTileFetcher.s_CacheBytes;
			}
		}

		// Token: 0x17000036 RID: 54
		// (get) Token: 0x06000116 RID: 278 RVA: 0x00009C37 File Offset: 0x00007E37
		// (set) Token: 0x06000117 RID: 279 RVA: 0x00009C3E File Offset: 0x00007E3E
		public static int TotalFetched { get; private set; }

		// Token: 0x17000037 RID: 55
		// (get) Token: 0x06000118 RID: 280 RVA: 0x00009C46 File Offset: 0x00007E46
		// (set) Token: 0x06000119 RID: 281 RVA: 0x00009C4D File Offset: 0x00007E4D
		public static int TotalFailed { get; private set; }

		// Token: 0x17000038 RID: 56
		// (get) Token: 0x0600011A RID: 282 RVA: 0x00009C55 File Offset: 0x00007E55
		// (set) Token: 0x0600011B RID: 283 RVA: 0x00009C5C File Offset: 0x00007E5C
		public static int TotalRejected { get; private set; }

		// Token: 0x17000039 RID: 57
		// (get) Token: 0x0600011C RID: 284 RVA: 0x00009C64 File Offset: 0x00007E64
		// (set) Token: 0x0600011D RID: 285 RVA: 0x00009C6B File Offset: 0x00007E6B
		public static int TotalNoCoverage { get; private set; }

		// Token: 0x1700003A RID: 58
		// (get) Token: 0x0600011E RID: 286 RVA: 0x00009C73 File Offset: 0x00007E73
		public static int NoCoverageMemoized
		{
			get
			{
				return WebTileFetcher.s_NoCoverage.Count;
			}
		}

		// Token: 0x0600011F RID: 287 RVA: 0x00009C7F File Offset: 0x00007E7F
		public static string TileKey(ImageryProvider provider, int z, int x, int y)
		{
			return string.Format("{0}:{1}/{2}/{3}", new object[]
			{
				provider.Name,
				z,
				x,
				y
			});
		}

		// Token: 0x1700003B RID: 59
		// (get) Token: 0x06000120 RID: 288 RVA: 0x00009CB8 File Offset: 0x00007EB8
		private static MonoBehaviour Host
		{
			get
			{
				bool flag = WebTileFetcher.s_Host == null;
				if (flag)
				{
					GameObject go = new GameObject("MirageWebTileFetcher");
					Object.DontDestroyOnLoad(go);
					WebTileFetcher.s_Host = go.AddComponent<WebTileFetcher.FetcherHost>();
				}
				return WebTileFetcher.s_Host;
			}
		}

		/// <summary>
		/// Request one mercator tile's raw bytes. <paramref name="onComplete" /> fires exactly once per request
		/// with the outcome — synchronously if the answer is already known (cached, or known to have no coverage).
		/// <paramref name="group" /> gives round-robin fairness across callers.
		///
		/// Unlike GeoStream's equivalent, this fires on EVERY terminal outcome including permanent failure.
		/// That is required, not cosmetic: the ingest state machine (§7) removes the key from
		/// <c>ingestInProgress</c> on completion, so a callback that silently never fires would strand the key
		/// forever and that cube tile would never be retried — a permanent hole with no error logged.
		/// </summary>
		// Token: 0x06000121 RID: 289 RVA: 0x00009D00 File Offset: 0x00007F00
		public static void RequestTile(ImageryProvider provider, string group, int z, int x, int y, Action<TileFetchResult> onComplete)
		{
			if (provider == null)
			{
				provider = ImageryProvider.Default;
			}
			string key = WebTileFetcher.TileKey(provider, z, x, y);
			WebTileFetcher.CacheEntry hit;
			bool flag = WebTileFetcher.s_Cache.TryGetValue(key, out hit);
			if (flag)
			{
				hit.lastUsed = Time.realtimeSinceStartup;
				if (onComplete != null)
				{
					onComplete(new TileFetchResult(TileFetchOutcome.Success, hit.bytes, hit.info));
				}
			}
			else
			{
				bool flag2 = WebTileFetcher.s_NoCoverage.Contains(key);
				if (flag2)
				{
					if (onComplete != null)
					{
						onComplete(new TileFetchResult(TileFetchOutcome.NoCoverage, null, default(JpegInfo)));
					}
				}
				else
				{
					bool flag3 = onComplete != null;
					if (flag3)
					{
						List<Action<TileFetchResult>> list;
						bool flag4 = !WebTileFetcher.s_PendingCallbacks.TryGetValue(key, out list);
						if (flag4)
						{
							list = new List<Action<TileFetchResult>>();
							WebTileFetcher.s_PendingCallbacks[key] = list;
						}
						list.Add(onComplete);
					}
					bool flag5 = WebTileFetcher.s_InProgress.Contains(key);
					if (!flag5)
					{
						WebTileFetcher.s_InProgress.Add(key);
						Queue<WebTileFetcher.QueuedTile> q;
						bool flag6 = !WebTileFetcher.s_QueuesByGroup.TryGetValue(group, out q);
						if (flag6)
						{
							q = new Queue<WebTileFetcher.QueuedTile>();
							WebTileFetcher.s_QueuesByGroup[group] = q;
							WebTileFetcher.s_GroupOrder.Add(group);
						}
						q.Enqueue(new WebTileFetcher.QueuedTile
						{
							z = z,
							x = x,
							y = y,
							key = key,
							group = group,
							retriesLeft = 4,
							provider = provider
						});
						WebTileFetcher.SchedulePump();
					}
				}
			}
		}

		/// <summary>Already-cached bytes, without triggering a fetch. Lets the gather (§4) check what it has
		/// before deciding whether the whole cube tile is bakeable yet.</summary>
		// Token: 0x06000122 RID: 290 RVA: 0x00009E88 File Offset: 0x00008088
		public static bool TryGetCached(ImageryProvider provider, int z, int x, int y, out byte[] bytes)
		{
			WebTileFetcher.CacheEntry e;
			bool flag = WebTileFetcher.s_Cache.TryGetValue(WebTileFetcher.TileKey(provider, z, x, y), out e);
			bool result;
			if (flag)
			{
				e.lastUsed = Time.realtimeSinceStartup;
				bytes = e.bytes;
				result = true;
			}
			else
			{
				bytes = null;
				result = false;
			}
			return result;
		}

		// Token: 0x06000123 RID: 291 RVA: 0x00009ED4 File Offset: 0x000080D4
		private static void SchedulePump()
		{
			bool flag = WebTileFetcher.s_PumpPending;
			if (!flag)
			{
				WebTileFetcher.s_PumpPending = true;
				WebTileFetcher.Host.StartCoroutine(WebTileFetcher.PumpQueue());
			}
		}

		// Token: 0x06000124 RID: 292 RVA: 0x00009F03 File Offset: 0x00008103
		private static IEnumerator PumpQueue()
		{
			return new WebTileFetcher.<PumpQueue>d__52(0);
		}

		// Token: 0x06000125 RID: 293 RVA: 0x00009F0C File Offset: 0x0000810C
		private static bool TryDequeueRoundRobin(out WebTileFetcher.QueuedTile item)
		{
			for (int i = 0; i < WebTileFetcher.s_GroupOrder.Count; i++)
			{
				int idx = (WebTileFetcher.s_NextGroupIndex + i) % WebTileFetcher.s_GroupOrder.Count;
				Queue<WebTileFetcher.QueuedTile> q = WebTileFetcher.s_QueuesByGroup[WebTileFetcher.s_GroupOrder[idx]];
				bool flag = q.Count > 0;
				if (flag)
				{
					WebTileFetcher.s_NextGroupIndex = (idx + 1) % WebTileFetcher.s_GroupOrder.Count;
					item = q.Dequeue();
					return true;
				}
			}
			item = default(WebTileFetcher.QueuedTile);
			return false;
		}

		// Token: 0x06000126 RID: 294 RVA: 0x00009FA0 File Offset: 0x000081A0
		private static IEnumerator DownloadOne(WebTileFetcher.QueuedTile item)
		{
			WebTileFetcher.<DownloadOne>d__54 <DownloadOne>d__ = new WebTileFetcher.<DownloadOne>d__54(0);
			<DownloadOne>d__.item = item;
			return <DownloadOne>d__;
		}

		/// <summary>Fire and clear every callback waiting on a key. One try/catch per callback so a throwing
		/// consumer can't strand the others or the concurrency slot.</summary>
		// Token: 0x06000127 RID: 295 RVA: 0x00009FB0 File Offset: 0x000081B0
		private static void FireCallbacks(string key, TileFetchResult result)
		{
			List<Action<TileFetchResult>> callbacks;
			bool flag = !WebTileFetcher.s_PendingCallbacks.TryGetValue(key, out callbacks);
			if (!flag)
			{
				WebTileFetcher.s_PendingCallbacks.Remove(key);
				foreach (Action<TileFetchResult> cb in callbacks)
				{
					try
					{
						if (cb != null)
						{
							cb(result);
						}
					}
					catch (Exception ex)
					{
						MirageDebug.LogError("WebIngest: tile callback threw for " + key + ": " + ex.Message);
					}
				}
			}
		}

		// Token: 0x06000128 RID: 296 RVA: 0x0000A060 File Offset: 0x00008260
		private static void RecordFrameKind(JpegFrameKind kind)
		{
			int i;
			WebTileFetcher.ObservedFrameKinds.TryGetValue(kind, out i);
			WebTileFetcher.ObservedFrameKinds[kind] = i + 1;
		}

		// Token: 0x06000129 RID: 297 RVA: 0x0000A08C File Offset: 0x0000828C
		private static void StoreInCache(string key, byte[] bytes, JpegInfo info)
		{
			bool flag = WebTileFetcher.s_Cache.ContainsKey(key);
			if (!flag)
			{
				WebTileFetcher.s_Cache[key] = new WebTileFetcher.CacheEntry
				{
					bytes = bytes,
					info = info,
					lastUsed = Time.realtimeSinceStartup
				};
				WebTileFetcher.s_CacheBytes += (long)bytes.Length;
				bool flag2 = WebTileFetcher.s_CacheBytes > 201326592L;
				if (flag2)
				{
					WebTileFetcher.EvictToTarget();
				}
			}
		}

		// Token: 0x0600012A RID: 298 RVA: 0x0000A0FC File Offset: 0x000082FC
		private static void EvictToTarget()
		{
			List<KeyValuePair<string, WebTileFetcher.CacheEntry>> byAge = new List<KeyValuePair<string, WebTileFetcher.CacheEntry>>(WebTileFetcher.s_Cache);
			byAge.Sort((KeyValuePair<string, WebTileFetcher.CacheEntry> a, KeyValuePair<string, WebTileFetcher.CacheEntry> b) => a.Value.lastUsed.CompareTo(b.Value.lastUsed));
			int evicted = 0;
			foreach (KeyValuePair<string, WebTileFetcher.CacheEntry> kv in byAge)
			{
				bool flag = WebTileFetcher.s_CacheBytes <= 134217728L;
				if (flag)
				{
					break;
				}
				WebTileFetcher.s_CacheBytes -= (long)kv.Value.bytes.Length;
				WebTileFetcher.s_Cache.Remove(kv.Key);
				evicted++;
			}
			MirageDebug.Log(string.Format("WebIngest: mercator byte cache trimmed — {0} evicted, {1} resident ", evicted, WebTileFetcher.s_Cache.Count) + string.Format("({0} MB).", WebTileFetcher.s_CacheBytes / 1048576L));
		}

		/// <summary>Drop in-flight bookkeeping. Unlike GeoStream this is NOT needed at scene boundaries (the host
		/// is persistent, so no coroutine is ever killed mid-flight) — it exists for teardown and tests.</summary>
		// Token: 0x0600012B RID: 299 RVA: 0x0000A208 File Offset: 0x00008408
		public static void Reset()
		{
			WebTileFetcher.s_InProgress.Clear();
			WebTileFetcher.s_PendingCallbacks.Clear();
			foreach (Queue<WebTileFetcher.QueuedTile> q in WebTileFetcher.s_QueuesByGroup.Values)
			{
				q.Clear();
			}
			WebTileFetcher.s_ActiveDownloads = 0;
			WebTileFetcher.s_PumpPending = false;
		}

		// Token: 0x0600012C RID: 300 RVA: 0x0000A284 File Offset: 0x00008484
		public static void ClearCache()
		{
			WebTileFetcher.s_Cache.Clear();
			WebTileFetcher.s_CacheBytes = 0L;
			WebTileFetcher.s_NoCoverage.Clear();
		}

		// Token: 0x040000ED RID: 237
		private const int MaxConcurrentDownloads = 16;

		// Token: 0x040000EE RID: 238
		private const int MaxRetries = 4;

		// Token: 0x040000EF RID: 239
		private const float RetryDelaySeconds = 1.5f;

		/// <summary>Bounded in-memory cache of fetched JPEG bytes, keyed by provider:z/x/y. Sized in BYTES, not
		/// entries: tiles vary ~5-60 KB and an entry cap can't bound memory. GeoStream's texture cache had no
		/// eviction at all and climbed ~1.7 GB over one 170-second flight.</summary>
		// Token: 0x040000F0 RID: 240
		private const long MaxCacheBytes = 201326592L;

		// Token: 0x040000F1 RID: 241
		private const long CacheTargetBytes = 134217728L;

		// Token: 0x040000F2 RID: 242
		private static readonly Dictionary<string, WebTileFetcher.CacheEntry> s_Cache = new Dictionary<string, WebTileFetcher.CacheEntry>();

		// Token: 0x040000F3 RID: 243
		private static long s_CacheBytes;

		// Token: 0x040000F4 RID: 244
		private static readonly HashSet<string> s_InProgress = new HashSet<string>();

		/// <summary>Tiles the provider told us don't exist. Memoized because coverage gaps are large and
		/// permanent — measured, s2cloudless has no z14 over open ocean at all — so re-asking would burn a
		/// request per ocean tile per session for an answer that cannot change.</summary>
		// Token: 0x040000F5 RID: 245
		private static readonly HashSet<string> s_NoCoverage = new HashSet<string>();

		// Token: 0x040000F6 RID: 246
		private static readonly Dictionary<string, List<Action<TileFetchResult>>> s_PendingCallbacks = new Dictionary<string, List<Action<TileFetchResult>>>();

		// Token: 0x040000F7 RID: 247
		private static readonly Dictionary<string, Queue<WebTileFetcher.QueuedTile>> s_QueuesByGroup = new Dictionary<string, Queue<WebTileFetcher.QueuedTile>>();

		// Token: 0x040000F8 RID: 248
		private static readonly List<string> s_GroupOrder = new List<string>();

		// Token: 0x040000F9 RID: 249
		private static int s_NextGroupIndex;

		// Token: 0x040000FA RID: 250
		private static int s_ActiveDownloads;

		// Token: 0x040000FB RID: 251
		private static bool s_PumpPending;

		/// <summary>Frame kinds actually observed from providers this session — the empirical answer to §11
		/// decision 3, rather than an assumption baked into the decoder's scope.</summary>
		// Token: 0x04000100 RID: 256
		public static readonly Dictionary<JpegFrameKind, int> ObservedFrameKinds = new Dictionary<JpegFrameKind, int>();

		// Token: 0x04000101 RID: 257
		private static MonoBehaviour s_Host;

		// Token: 0x020000A4 RID: 164
		private sealed class CacheEntry
		{
			// Token: 0x0400044C RID: 1100
			public byte[] bytes;

			// Token: 0x0400044D RID: 1101
			public JpegInfo info;

			// Token: 0x0400044E RID: 1102
			public float lastUsed;
		}

		// Token: 0x020000A5 RID: 165
		private struct QueuedTile
		{
			// Token: 0x0400044F RID: 1103
			public int z;

			// Token: 0x04000450 RID: 1104
			public int x;

			// Token: 0x04000451 RID: 1105
			public int y;

			// Token: 0x04000452 RID: 1106
			public int retriesLeft;

			// Token: 0x04000453 RID: 1107
			public string key;

			// Token: 0x04000454 RID: 1108
			public string group;

			// Token: 0x04000455 RID: 1109
			public ImageryProvider provider;
		}

		// Token: 0x020000A6 RID: 166
		private sealed class FetcherHost : MonoBehaviour
		{
		}
	}
}
