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
	// Token: 0x0200002E RID: 46
	public static class WorldCoverFetcher
	{
		/// <summary>Create the pump. Must be called once from the main thread (GameObject construction is
		/// main-thread-only), so it cannot be done lazily from <see cref="M:Mirage.WebIngest.WorldCoverFetcher.FetchAsync(System.String,System.Int64,System.Int64,System.Threading.CancellationToken)" /> on the bake worker.</summary>
		// Token: 0x06000112 RID: 274 RVA: 0x00009480 File Offset: 0x00007680
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
		// Token: 0x06000113 RID: 275 RVA: 0x000094BC File Offset: 0x000076BC
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

		// Token: 0x06000114 RID: 276 RVA: 0x00009590 File Offset: 0x00007790
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

		// Token: 0x06000115 RID: 277 RVA: 0x00009624 File Offset: 0x00007824
		private static IEnumerator Download(WorldCoverFetcher.Request r)
		{
			WorldCoverFetcher.<Download>d__16 <Download>d__ = new WorldCoverFetcher.<Download>d__16(0);
			<Download>d__.r = r;
			return <Download>d__;
		}

		// Token: 0x06000116 RID: 278 RVA: 0x00009634 File Offset: 0x00007834
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

		// Token: 0x06000117 RID: 279 RVA: 0x000096B8 File Offset: 0x000078B8
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

		// Token: 0x040000DB RID: 219
		private const int MaxConcurrent = 4;

		// Token: 0x040000DC RID: 220
		private const int TimeoutSeconds = 60;

		// Token: 0x040000DD RID: 221
		private const long MaxCacheBytes = 100663296L;

		// Token: 0x040000DE RID: 222
		private static readonly ConcurrentQueue<WorldCoverFetcher.Request> s_Queue = new ConcurrentQueue<WorldCoverFetcher.Request>();

		// Token: 0x040000DF RID: 223
		private static WorldCoverFetcher.PumpHost s_Host;

		// Token: 0x040000E0 RID: 224
		private static int s_Active;

		// Token: 0x040000E1 RID: 225
		private static readonly object s_CacheLock = new object();

		// Token: 0x040000E2 RID: 226
		private static readonly Dictionary<string, LinkedListNode<WorldCoverFetcher.Entry>> s_Cache = new Dictionary<string, LinkedListNode<WorldCoverFetcher.Entry>>();

		// Token: 0x040000E3 RID: 227
		private static readonly LinkedList<WorldCoverFetcher.Entry> s_Lru = new LinkedList<WorldCoverFetcher.Entry>();

		// Token: 0x040000E4 RID: 228
		private static long s_CacheBytes;

		// Token: 0x020000B9 RID: 185
		private sealed class Request
		{
			// Token: 0x040004CD RID: 1229
			public string url;

			// Token: 0x040004CE RID: 1230
			public long from;

			// Token: 0x040004CF RID: 1231
			public long to;

			// Token: 0x040004D0 RID: 1232
			public string key;

			// Token: 0x040004D1 RID: 1233
			public TaskCompletionSource<byte[]> tcs;

			// Token: 0x040004D2 RID: 1234
			public CancellationToken ct;
		}

		// Token: 0x020000BA RID: 186
		private sealed class Entry
		{
			// Token: 0x040004D3 RID: 1235
			public string key;

			// Token: 0x040004D4 RID: 1236
			public byte[] bytes;
		}

		// Token: 0x020000BB RID: 187
		private sealed class PumpHost : MonoBehaviour
		{
			// Token: 0x06000482 RID: 1154 RVA: 0x000208BE File Offset: 0x0001EABE
			private void Update()
			{
				WorldCoverFetcher.Pump();
			}
		}
	}
}
