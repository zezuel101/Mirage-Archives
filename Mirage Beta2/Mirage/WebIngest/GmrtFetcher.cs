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
	// Token: 0x02000017 RID: 23
	public static class GmrtFetcher
	{
		/// <summary>Create the pump. Must be called once from the main thread (GameObject construction is
		/// main-thread-only), so it cannot be done lazily from <see cref="M:Mirage.WebIngest.GmrtFetcher.FetchAsync(System.String,System.Threading.CancellationToken)" /> on the bake worker.</summary>
		// Token: 0x06000094 RID: 148 RVA: 0x0000658C File Offset: 0x0000478C
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
		// Token: 0x06000095 RID: 149 RVA: 0x000065C8 File Offset: 0x000047C8
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

		// Token: 0x06000096 RID: 150 RVA: 0x0000662C File Offset: 0x0000482C
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

		// Token: 0x06000097 RID: 151 RVA: 0x0000669A File Offset: 0x0000489A
		private static IEnumerator Download(GmrtFetcher.Request r)
		{
			GmrtFetcher.<Download>d__10 <Download>d__ = new GmrtFetcher.<Download>d__10(0);
			<Download>d__.r = r;
			return <Download>d__;
		}

		// Token: 0x04000081 RID: 129
		private const int MaxConcurrent = 4;

		// Token: 0x04000082 RID: 130
		private const int TimeoutSeconds = 60;

		// Token: 0x04000083 RID: 131
		private static readonly ConcurrentQueue<GmrtFetcher.Request> s_Queue = new ConcurrentQueue<GmrtFetcher.Request>();

		// Token: 0x04000084 RID: 132
		private static GmrtFetcher.PumpHost s_Host;

		// Token: 0x04000085 RID: 133
		private static int s_Active;

		// Token: 0x02000098 RID: 152
		private sealed class Request
		{
			// Token: 0x04000421 RID: 1057
			public string url;

			// Token: 0x04000422 RID: 1058
			public TaskCompletionSource<byte[]> tcs;

			// Token: 0x04000423 RID: 1059
			public CancellationToken ct;
		}

		// Token: 0x02000099 RID: 153
		private sealed class PumpHost : MonoBehaviour
		{
			// Token: 0x0600047E RID: 1150 RVA: 0x0001F639 File Offset: 0x0001D839
			private void Update()
			{
				GmrtFetcher.Pump();
			}
		}
	}
}
