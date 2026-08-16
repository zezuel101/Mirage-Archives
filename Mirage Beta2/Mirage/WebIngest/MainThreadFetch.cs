using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Mirage.VirtualTexture;
using UnityEngine;

namespace Mirage.WebIngest
{
	/// <summary>
	/// Bridges <see cref="T:Mirage.WebIngest.CubeTileBaker" />'s worker-thread fetch seam to <see cref="T:Mirage.WebIngest.WebTileFetcher" />, which is
	/// main-thread-only (coroutines, <c>UnityWebRequest</c>, <c>Time.realtimeSinceStartup</c>). Calling the
	/// fetcher from the bake worker would be a race at best and an engine tear-down at worst.
	///
	/// The shape is deliberately thin: a request goes on a queue, an <c>Update</c> pump drains it on the main
	/// thread and hands the fetcher a callback that completes the <see cref="T:System.Threading.Tasks.TaskCompletionSource`1" />,
	/// and the awaiting worker resumes on a thread-pool thread. Nothing here does I/O or caching — all of that
	/// (dedup, retry, round-robin, the byte-capped LRU) already lives in the fetcher, and duplicating any of it
	/// here would give two caches that disagree.
	///
	/// <b>Every request must complete.</b> The fetcher fires its callback on every terminal outcome including
	/// permanent failure, and this must preserve that: a <c>TaskCompletionSource</c> that never resolves would
	/// hang the bake worker forever, which in turn strands the key in <c>ingestInProgress</c> and means that
	/// cube tile is never retried — a permanent hole with nothing logged (§7). Hence the try/catch around the
	/// dispatch and the cancellation registration below.
	/// </summary>
	// Token: 0x0200001F RID: 31
	public static class MainThreadFetch
	{
		// Token: 0x17000022 RID: 34
		// (get) Token: 0x060000B4 RID: 180 RVA: 0x000072B2 File Offset: 0x000054B2
		public static int Pending
		{
			get
			{
				return MainThreadFetch.s_Queue.Count;
			}
		}

		// Token: 0x060000B5 RID: 181 RVA: 0x000072C0 File Offset: 0x000054C0
		private static void EnsureHost()
		{
			bool flag = MainThreadFetch.s_Host != null;
			if (!flag)
			{
				GameObject go = new GameObject("MirageMainThreadFetch");
				Object.DontDestroyOnLoad(go);
				MainThreadFetch.s_Host = go.AddComponent<MainThreadFetch.PumpHost>();
			}
		}

		/// <summary>Awaitable fetch, callable from any thread. <paramref name="group" /> is the fetcher's
		/// round-robin fairness key — pass the body name so one body's descent can't starve another's.</summary>
		// Token: 0x060000B6 RID: 182 RVA: 0x000072FC File Offset: 0x000054FC
		public static Task<TileFetchResult> FetchAsync(ImageryProvider provider, string group, int z, int x, int y, CancellationToken ct)
		{
			TaskCompletionSource<TileFetchResult> tcs = new TaskCompletionSource<TileFetchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
			bool isCancellationRequested = ct.IsCancellationRequested;
			Task<TileFetchResult> task;
			if (isCancellationRequested)
			{
				tcs.TrySetCanceled(ct);
				task = tcs.Task;
			}
			else
			{
				MainThreadFetch.s_Queue.Enqueue(new MainThreadFetch.Request
				{
					provider = provider,
					group = group,
					z = z,
					x = x,
					y = y,
					tcs = tcs,
					ct = ct
				});
				task = tcs.Task;
			}
			return task;
		}

		/// <summary>Create the pump. Must be called once from the main thread — <c>GameObject</c> construction is
		/// itself main-thread-only, so it cannot be done lazily from <see cref="M:Mirage.WebIngest.MainThreadFetch.FetchAsync(Mirage.WebIngest.ImageryProvider,System.String,System.Int32,System.Int32,System.Int32,System.Threading.CancellationToken)" />.</summary>
		// Token: 0x060000B7 RID: 183 RVA: 0x0000737D File Offset: 0x0000557D
		public static void Install()
		{
			MainThreadFetch.EnsureHost();
		}

		// Token: 0x060000B8 RID: 184 RVA: 0x00007388 File Offset: 0x00005588
		private static void Pump()
		{
			int i = 0;
			for (;;)
			{
				MainThreadFetch.Request r;
				bool flag = i < 32 && MainThreadFetch.s_Queue.TryDequeue(out r);
				if (!flag)
				{
					break;
				}
				i++;
				bool isCancellationRequested = r.ct.IsCancellationRequested;
				if (isCancellationRequested)
				{
					r.tcs.TrySetCanceled(r.ct);
				}
				else
				{
					try
					{
						WebTileFetcher.RequestTile(r.provider, r.group, r.z, r.x, r.y, delegate(TileFetchResult result)
						{
							r.tcs.TrySetResult(result);
						});
					}
					catch (Exception e)
					{
						MirageDebug.LogError(string.Format("MainThreadFetch: dispatch failed for {0}/{1}/{2}: {3}", new object[]
						{
							r.z,
							r.x,
							r.y,
							e.Message
						}));
						r.tcs.TrySetResult(new TileFetchResult(TileFetchOutcome.Failed, null, default(JpegInfo)));
					}
				}
			}
		}

		// Token: 0x040000A6 RID: 166
		private static readonly ConcurrentQueue<MainThreadFetch.Request> s_Queue = new ConcurrentQueue<MainThreadFetch.Request>();

		// Token: 0x040000A7 RID: 167
		private static MainThreadFetch.PumpHost s_Host;

		/// <summary>Requests dispatched per frame. The fetcher has its own concurrency cap, so this only bounds
		/// how fast the queue is handed over — but a burst still costs main-thread time to dispatch, and the
		/// ingest queue's own cap means a deep backlog here is unexpected anyway.</summary>
		// Token: 0x040000A8 RID: 168
		private const int MaxDispatchPerFrame = 32;

		// Token: 0x0200009B RID: 155
		private sealed class Request
		{
			// Token: 0x0400042A RID: 1066
			public ImageryProvider provider;

			// Token: 0x0400042B RID: 1067
			public string group;

			// Token: 0x0400042C RID: 1068
			public int z;

			// Token: 0x0400042D RID: 1069
			public int x;

			// Token: 0x0400042E RID: 1070
			public int y;

			// Token: 0x0400042F RID: 1071
			public TaskCompletionSource<TileFetchResult> tcs;

			// Token: 0x04000430 RID: 1072
			public CancellationToken ct;
		}

		// Token: 0x0200009C RID: 156
		private sealed class PumpHost : MonoBehaviour
		{
			// Token: 0x06000488 RID: 1160 RVA: 0x0001F914 File Offset: 0x0001DB14
			private void Update()
			{
				Stopwatch sw = FrameProfile.Start();
				MainThreadFetch.Pump();
				FrameProfile.AddPump(sw.ElapsedTicks);
			}
		}
	}
}
