using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mirage.WebIngest
{
	/// <summary>
	/// One shared, bounded, LOW-PRIORITY pool for every CPU-heavy loop a bake runs (decode, reprojection, normal
	/// derivation, BC7). It exists to fix a measured problem: with the decode/reproject/normal loops all using
	/// the default <see cref="T:System.Threading.Tasks.Parallel" /> scheduler — <see cref="P:System.Environment.ProcessorCount" /> threads at NORMAL
	/// priority on the shared ThreadPool — and <c>webIngestConcurrency</c> bakes running at once, ingest asked
	/// for up to <c>concurrency × cores</c> compute tasks at the SAME priority as KSP's main and render threads.
	/// The OS had no reason to favour the game, so frames hitched hard. Dropping <c>webIngestConcurrency</c> to 1
	/// removed the hitching but also serialised the NETWORK fetches, halving ingest throughput — the two were
	/// conflated in one knob.
	///
	/// This separates them. All bake compute runs here, so:
	///   • <b>Bounded.</b> The pool has a FIXED thread count (<see cref="P:Mirage.WebIngest.BakeScheduler.WorkerCount" />), so total ingest
	///     parallelism is capped no matter how many bakes run at once — the bound COMPOSES, which a per-loop
	///     <c>MaxDegreeOfParallelism</c> does not (four bakes each capped at N still oversubscribe to 4N).
	///   • <b>Deferential.</b> Threads run at <see cref="F:System.Threading.ThreadPriority.BelowNormal" />, so on the cores it does
	///     use the OS preempts ingest whenever a KSP thread is runnable. Reserving two cores
	///     (<c>ProcessorCount − 2</c>) additionally keeps the main + render threads from ever having to fight for
	///     one at all.
	/// With CPU protected here, <c>webIngestConcurrency</c> is free to rise again purely for network overlap
	/// (bakes awaiting fetches cost no CPU), which is what it should have controlled all along.
	///
	/// <b>Deadlock safety.</b> The classic failure of a fixed-thread scheduler is a task that re-enters it and
	/// waits for a thread while every thread is itself waiting. That cannot happen here by construction: bakes
	/// are started on the normal ThreadPool (<c>Task.Run</c>), not on this pool, and each bake calls its parallel
	/// loops SEQUENTIALLY — so a pool thread only ever runs a leaf loop body and never re-enters
	/// <see cref="T:System.Threading.Tasks.Parallel" /> on this scheduler.
	///
	/// Unity-free like the rest of WebIngest, so tools/ArchivePacker links it and its offline bakes get the same
	/// bounded, low-priority behaviour the plugin does.
	/// </summary>
	// Token: 0x02000009 RID: 9
	public static class BakeScheduler
	{
		// Token: 0x17000012 RID: 18
		// (get) Token: 0x06000049 RID: 73 RVA: 0x00002D41 File Offset: 0x00000F41
		public static int WorkerCount { get; } = Math.Max(1, Environment.ProcessorCount - 2);

		/// <summary>Pass to every bake <see cref="M:System.Threading.Tasks.Parallel.For(System.Int32,System.Int32,System.Threading.Tasks.ParallelOptions,System.Action{System.Int32})" /> (and its
		/// localInit overload). Binds the loop to the shared low-priority pool and caps a single loop's fan-out at
		/// the pool size — the pool's fixed thread count is what actually bounds concurrency ACROSS loops.</summary>
		// Token: 0x17000013 RID: 19
		// (get) Token: 0x0600004A RID: 74 RVA: 0x00002D48 File Offset: 0x00000F48
		public static ParallelOptions Options { get; } = new ParallelOptions
		{
			MaxDegreeOfParallelism = BakeScheduler.WorkerCount,
			TaskScheduler = BakeScheduler.s_Scheduler
		};

		// Token: 0x04000035 RID: 53
		private static readonly BakeScheduler.LowPriorityScheduler s_Scheduler = new BakeScheduler.LowPriorityScheduler(BakeScheduler.WorkerCount);

		/// <summary>
		/// A <see cref="T:System.Threading.Tasks.TaskScheduler" /> over a fixed set of dedicated, BelowNormal, background threads.
		///
		/// Dedicated threads rather than the ThreadPool for two reasons: priority (the ThreadPool runs at Normal
		/// and resets priority between work items, so there is nowhere to set BelowNormal that sticks), and
		/// steadiness (the ThreadPool injects threads slowly via hill-climbing, which would stall a burst of
		/// bakes waiting for the pool to grow). Background threads so they never keep the process alive at exit.
		/// </summary>
		// Token: 0x0200007B RID: 123
		private sealed class LowPriorityScheduler : TaskScheduler
		{
			// Token: 0x06000424 RID: 1060 RVA: 0x0001BB4C File Offset: 0x00019D4C
			public LowPriorityScheduler(int count)
			{
				this.threads = new Thread[count];
				for (int i = 0; i < count; i++)
				{
					this.threads[i] = new Thread(new ThreadStart(this.Work))
					{
						IsBackground = true,
						Priority = ThreadPriority.BelowNormal,
						Name = string.Format("MirageBake{0}", i)
					};
					this.threads[i].Start();
				}
			}

			// Token: 0x170000FD RID: 253
			// (get) Token: 0x06000425 RID: 1061 RVA: 0x0001BBD8 File Offset: 0x00019DD8
			public override int MaximumConcurrencyLevel
			{
				get
				{
					return this.threads.Length;
				}
			}

			// Token: 0x06000426 RID: 1062 RVA: 0x0001BBE4 File Offset: 0x00019DE4
			private void Work()
			{
				foreach (Task t in this.queue.GetConsumingEnumerable())
				{
					base.TryExecuteTask(t);
				}
			}

			// Token: 0x06000427 RID: 1063 RVA: 0x0001BC3C File Offset: 0x00019E3C
			protected override void QueueTask(Task task)
			{
				this.queue.Add(task);
			}

			// Token: 0x06000428 RID: 1064 RVA: 0x0001BC4B File Offset: 0x00019E4B
			protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
			{
				return false;
			}

			// Token: 0x06000429 RID: 1065 RVA: 0x0001BC4E File Offset: 0x00019E4E
			protected override IEnumerable<Task> GetScheduledTasks()
			{
				return this.queue.ToArray();
			}

			// Token: 0x040002EC RID: 748
			private readonly BlockingCollection<Task> queue = new BlockingCollection<Task>();

			// Token: 0x040002ED RID: 749
			private readonly Thread[] threads;
		}
	}
}
