using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Mirage.WebIngest
{
	/// <summary>
	/// Main-thread transport for ESA WorldCover COG range reads, bridged to the bake worker as a
	/// <see cref="T:System.Threading.Tasks.Task`1" />. Mirrors <see cref="T:Mirage.WebIngest.GmrtFetcher" /> — a <c>DontDestroyOnLoad</c> coroutine pump
	/// so a scene change can't strand an in-flight bake — with two differences that WorldCover needs:
	///
	/// <list type="bullet">
	/// <item>It issues HTTP <c>Range</c> requests: a COG is read windowed, never whole (a 3° tile is 36000² px).</item>
	/// <item>It <b>caches</b> ranges. Every bake re-opens the 3° COG covering its tile and re-reads the internal
	/// tiles it needs, and adjacent cube tiles share both the 512 KB header and those internal tiles — so without
	/// a cache the same bytes are refetched endlessly. An exact-range LRU dedups almost all of it.</item>
	/// </list>
	///
	/// A null result means the object does not exist (404 — an ocean-only 3° cell has no file) or any transient
	/// error; <see cref="T:Mirage.WebIngest.WorldCoverSource" /> treats null as "no data here" and the mask falls back to the
	/// sea-level term. Signature matches <see cref="T:Mirage.WebIngest.WorldCoverSource.RangeFetch" />.
	/// </summary>
	// Token: 0x02000032 RID: 50
	public static class WorldCoverFetcher
	{
		/// <summary>Create the pump. Must be called once from the main thread (GameObject construction is
		/// main-thread-only), so it cannot be done lazily from <see cref="M:Mirage.WebIngest.WorldCoverFetcher.FetchAsync(System.String,System.Int64,System.Int64,System.Threading.CancellationToken)" /> on the bake worker.</summary>
		// Token: 0x0600012E RID: 302 RVA: 0x0000A2F8 File Offset: 0x000084F8
		public static void Install()
		{
			bool flag = WorldCoverFetcher.s_Host != null;
			if (!flag)
			{
				GameObject go = new GameObject("MirageWorldCoverFetcher");
				Object.DontDestroyOnLoad(go);
				WorldCoverFetcher.s_Host = go.AddComponent<WorldCoverFetcher.PumpHost>();
			}
		}

		/// <summary>Fetch <c>[from..toInclusive]</c> of a COG, callable from any thread. Resolves to the bytes,
		/// or null on 404 / any error.</summary>
		// Token: 0x0600012F RID: 303 RVA: 0x0000A334 File Offset: 0x00008534
		public static Task<byte[]> FetchAsync(string url, long from, long to, CancellationToken ct)
		{
			TaskCompletionSource<byte[]> tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
			bool isCancellationRequested = ct.IsCancellationRequested;
			Task<byte[]> task;
			if (isCancellationRequested)
			{
				tcs.TrySetCanceled(ct);
				task = tcs.Task;
			}
			else
			{
				string key = string.Concat(new string[]
				{
					url,
					"\n",
					from.ToString(),
					"-",
					to.ToString()
				});
				byte[] hit;
				bool flag = WorldCoverFetcher.TryGetCached(key, out hit);
				if (flag)
				{
					tcs.TrySetResult(hit);
					task = tcs.Task;
				}
				else
				{
					WorldCoverFetcher.s_Queue.Enqueue(new WorldCoverFetcher.Request
					{
						url = url,
						from = from,
						to = to,
						key = key,
						tcs = tcs,
						ct = ct
					});
					task = tcs.Task;
				}
			}
			return task;
		}

		// Token: 0x06000130 RID: 304 RVA: 0x0000A408 File Offset: 0x00008608
		private static void Pump()
		{
			for (;;)
			{
				WorldCoverFetcher.Request r;
				bool flag = WorldCoverFetcher.s_Active < 4 && WorldCoverFetcher.s_Queue.TryDequeue(out r);
				if (!flag)
				{
					break;
				}
				bool isCancellationRequested = r.ct.IsCancellationRequested;
				if (isCancellationRequested)
				{
					r.tcs.TrySetCanceled(r.ct);
				}
				else
				{
					byte[] hit;
					bool flag2 = WorldCoverFetcher.TryGetCached(r.key, out hit);
					if (flag2)
					{
						r.tcs.TrySetResult(hit);
					}
					else
					{
						WorldCoverFetcher.s_Active++;
						WorldCoverFetcher.s_Host.StartCoroutine(WorldCoverFetcher.Download(r));
					}
				}
			}
		}

		// Token: 0x06000131 RID: 305 RVA: 0x0000A49C File Offset: 0x0000869C
		private static IEnumerator Download(WorldCoverFetcher.Request r)
		{
			WorldCoverFetcher.<Download>d__16 <Download>d__ = new WorldCoverFetcher.<Download>d__16(0);
			<Download>d__.r = r;
			return <Download>d__;
		}

		// Token: 0x06000132 RID: 306 RVA: 0x0000A4AC File Offset: 0x000086AC
		private static bool TryGetCached(string key, out byte[] bytes)
		{
			object obj = WorldCoverFetcher.s_CacheLock;
			lock (obj)
			{
				LinkedListNode<WorldCoverFetcher.Entry> node;
				bool flag2 = WorldCoverFetcher.s_Cache.TryGetValue(key, out node);
				if (flag2)
				{
					WorldCoverFetcher.s_Lru.Remove(node);
					WorldCoverFetcher.s_Lru.AddLast(node);
					bytes = node.Value.bytes;
					return true;
				}
			}
			bytes = null;
			return false;
		}

		// Token: 0x06000133 RID: 307 RVA: 0x0000A530 File Offset: 0x00008730
		private static void Cache(string key, byte[] bytes)
		{
			object obj = WorldCoverFetcher.s_CacheLock;
			lock (obj)
			{
				bool flag2 = WorldCoverFetcher.s_Cache.ContainsKey(key);
				if (!flag2)
				{
					LinkedListNode<WorldCoverFetcher.Entry> node = WorldCoverFetcher.s_Lru.AddLast(new WorldCoverFetcher.Entry
					{
						key = key,
						bytes = bytes
					});
					WorldCoverFetcher.s_Cache[key] = node;
					WorldCoverFetcher.s_CacheBytes += (long)bytes.Length;
					while (WorldCoverFetcher.s_CacheBytes > 100663296L && WorldCoverFetcher.s_Lru.First != null)
					{
						LinkedListNode<WorldCoverFetcher.Entry> first = WorldCoverFetcher.s_Lru.First;
						WorldCoverFetcher.s_Lru.RemoveFirst();
						WorldCoverFetcher.s_Cache.Remove(first.Value.key);
						WorldCoverFetcher.s_CacheBytes -= (long)first.Value.bytes.Length;
					}
				}
			}
		}

		// Token: 0x04000102 RID: 258
		private const int MaxConcurrent = 4;

		// Token: 0x04000103 RID: 259
		private const int TimeoutSeconds = 60;

		// Token: 0x04000104 RID: 260
		private const long MaxCacheBytes = 100663296L;

		// Token: 0x04000105 RID: 261
		private static readonly ConcurrentQueue<WorldCoverFetcher.Request> s_Queue = new ConcurrentQueue<WorldCoverFetcher.Request>();

		// Token: 0x04000106 RID: 262
		private static WorldCoverFetcher.PumpHost s_Host;

		// Token: 0x04000107 RID: 263
		private static int s_Active;

		// Token: 0x04000108 RID: 264
		private static readonly object s_CacheLock = new object();

		// Token: 0x04000109 RID: 265
		private static readonly Dictionary<string, LinkedListNode<WorldCoverFetcher.Entry>> s_Cache = new Dictionary<string, LinkedListNode<WorldCoverFetcher.Entry>>();

		// Token: 0x0400010A RID: 266
		private static readonly LinkedList<WorldCoverFetcher.Entry> s_Lru = new LinkedList<WorldCoverFetcher.Entry>();

		// Token: 0x0400010B RID: 267
		private static long s_CacheBytes;

		// Token: 0x020000AA RID: 170
		private sealed class Request
		{
			// Token: 0x04000468 RID: 1128
			public string url;

			// Token: 0x04000469 RID: 1129
			public long from;

			// Token: 0x0400046A RID: 1130
			public long to;

			// Token: 0x0400046B RID: 1131
			public string key;

			// Token: 0x0400046C RID: 1132
			public TaskCompletionSource<byte[]> tcs;

			// Token: 0x0400046D RID: 1133
			public CancellationToken ct;
		}

		// Token: 0x020000AB RID: 171
		private sealed class Entry
		{
			// Token: 0x0400046E RID: 1134
			public string key;

			// Token: 0x0400046F RID: 1135
			public byte[] bytes;
		}

		// Token: 0x020000AC RID: 172
		private sealed class PumpHost : MonoBehaviour
		{
			// Token: 0x060004AE RID: 1198 RVA: 0x00020242 File Offset: 0x0001E442
			private void Update()
			{
				WorldCoverFetcher.Pump();
			}
		}
	}
}
