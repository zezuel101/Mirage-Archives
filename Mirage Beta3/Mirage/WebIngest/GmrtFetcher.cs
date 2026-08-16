using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Mirage.WebIngest
{
	/// <summary>
	/// Main-thread transport for GMRT GridServer BOUNDING-BOX requests, bridged to the bake worker as a
	/// <see cref="T:System.Threading.Tasks.Task`1" />. <see cref="T:Mirage.WebIngest.WebTileFetcher" /> and <see cref="T:Mirage.WebIngest.MainThreadFetch" /> are z/x/y
	/// keyed; GMRT is a bbox GET, so it gets its own tiny fetcher rather than being forced through that model.
	///
	/// Deliberately minimal — no cache, no dedup, no round-robin. GMRT is fetched only for COASTAL tiles (low
	/// volume) and each grid is used once per bake, so the durable artifact that needs caching is the BAKED tile
	/// in the web archive, not the raw grid. The concurrency cap is small because a GridServer grid is generated
	/// on the server (~2–3 s) and there is no point queuing dozens against it.
	///
	/// Like <see cref="T:Mirage.WebIngest.WebTileFetcher" />, hosted on a DontDestroyOnLoad object so a scene change cannot kill an
	/// in-flight coroutine and strand the awaiting bake. A null result (not an exception) signals failure; the
	/// baker treats that as transient and retries, so the seabed falls back to the coarse tile meanwhile.
	/// </summary>
	// Token: 0x0200002D RID: 45
	public static class GmrtFetcher
	{
		/// <summary>Create the pump. Must be called once from the main thread (GameObject construction is
		/// main-thread-only), so it cannot be done lazily from <see cref="M:Mirage.WebIngest.GmrtFetcher.FetchAsync(System.String,System.Threading.CancellationToken)" /> on the bake worker.</summary>
		// Token: 0x0600010D RID: 269 RVA: 0x00009354 File Offset: 0x00007554
		public static void Install()
		{
			bool flag = GmrtFetcher.s_Host != null;
			if (!flag)
			{
				GameObject go = new GameObject("MirageGmrtFetcher");
				Object.DontDestroyOnLoad(go);
				GmrtFetcher.s_Host = go.AddComponent<GmrtFetcher.PumpHost>();
			}
		}

		/// <summary>Fetch the raw ArcASCII bytes for a GridServer URL, callable from any thread. Resolves to null
		/// on any failure (network, HTTP error, or a GridServer text error body served with a 200) — the caller
		/// distinguishes that from success, never from a hang.</summary>
		// Token: 0x0600010E RID: 270 RVA: 0x00009390 File Offset: 0x00007590
		public static Task<byte[]> FetchAsync(string url, CancellationToken ct)
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
				GmrtFetcher.s_Queue.Enqueue(new GmrtFetcher.Request
				{
					url = url,
					tcs = tcs,
					ct = ct
				});
				task = tcs.Task;
			}
			return task;
		}

		// Token: 0x0600010F RID: 271 RVA: 0x000093F4 File Offset: 0x000075F4
		private static void Pump()
		{
			for (;;)
			{
				GmrtFetcher.Request r;
				bool flag = GmrtFetcher.s_Active < 4 && GmrtFetcher.s_Queue.TryDequeue(out r);
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
					GmrtFetcher.s_Active++;
					GmrtFetcher.s_Host.StartCoroutine(GmrtFetcher.Download(r));
				}
			}
		}

		// Token: 0x06000110 RID: 272 RVA: 0x00009462 File Offset: 0x00007662
		private static IEnumerator Download(GmrtFetcher.Request r)
		{
			GmrtFetcher.<Download>d__10 <Download>d__ = new GmrtFetcher.<Download>d__10(0);
			<Download>d__.r = r;
			return <Download>d__;
		}

		// Token: 0x040000D6 RID: 214
		private const int MaxConcurrent = 4;

		// Token: 0x040000D7 RID: 215
		private const int TimeoutSeconds = 60;

		// Token: 0x040000D8 RID: 216
		private static readonly ConcurrentQueue<GmrtFetcher.Request> s_Queue = new ConcurrentQueue<GmrtFetcher.Request>();

		// Token: 0x040000D9 RID: 217
		private static GmrtFetcher.PumpHost s_Host;

		// Token: 0x040000DA RID: 218
		private static int s_Active;

		// Token: 0x020000B6 RID: 182
		private sealed class Request
		{
			// Token: 0x040004C4 RID: 1220
			public string url;

			// Token: 0x040004C5 RID: 1221
			public TaskCompletionSource<byte[]> tcs;

			// Token: 0x040004C6 RID: 1222
			public CancellationToken ct;
		}

		// Token: 0x020000B7 RID: 183
		private sealed class PumpHost : MonoBehaviour
		{
			// Token: 0x06000477 RID: 1143 RVA: 0x000205DC File Offset: 0x0001E7DC
			private void Update()
			{
				GmrtFetcher.Pump();
			}
		}
	}
}
