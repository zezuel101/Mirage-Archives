using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Mirage.WebIngest
{
	/// <summary>
	/// Recycles the big short-lived arrays a bake churns through. This is not micro-optimisation — it is the
	/// only lever available on the frame spikes.
	///
	/// Measured: KSP's Mono uses the BOEHM collector, which is NON-GENERATIONAL. `gc[0:13 1:13 2:13]` in the
	/// streamer log is the tell — gen0 == gen1 == gen2 means every single collection is a full, stop-the-world
	/// scan of the whole ~1.4 GB heap. There is no such thing as a cheap gen-0 collection here. So pause LENGTH
	/// is fixed by KSP's heap and is not ours to influence; only pause FREQUENCY is, and frequency is set purely
	/// by how fast we allocate. Intervals with 0 collections showed no phase above 2.3 ms; intervals with 10-13
	/// showed 17-21 ms spikes landing on whatever phase happened to be running (a Dictionary walk "took" 20 ms).
	///
	/// A bake allocated ~37 MB — ~35 DEM tiles at float[65536], ~35 color tiles at byte[196608] + float[196608],
	/// plus the per-tile outputs — and at 4 concurrent bakes and ~4 tiles/s that is ~150 MB/s, i.e. ~0.67 full
	/// collections per second. Recycling the buffers attacks the rate directly.
	///
	/// <b>Contract: a rented buffer has ONE owner and must be returned exactly once.</b> Returning a buffer
	/// twice hands the same array to two concurrent bakes, which corrupts both with no error anywhere — the
	/// failure mode this codebase has already paid for once (see NormalFromHeight's per-worker samplers). Rent
	/// through <see cref="T:Mirage.WebIngest.BakeBuffers" /> rather than calling this directly; it owns the returns.
	///
	/// Contents are NOT cleared on rent. Every consumer here fully overwrites what it takes, and zeroing 27 MB
	/// per bake would give back much of what pooling buys.
	///
	/// Unity-free, like the rest of WebIngest, so tools/TestBench links it.
	/// </summary>
	// Token: 0x02000006 RID: 6
	public static class BufferPool
	{
		/// <summary>Bytes currently held. Exposed so a test can assert the pool is bounded rather than trust it.</summary>
		// Token: 0x17000011 RID: 17
		// (get) Token: 0x06000031 RID: 49 RVA: 0x000025E6 File Offset: 0x000007E6
		public static long PooledBytes
		{
			get
			{
				return Interlocked.Read(ref BufferPool.s_PooledBytes);
			}
		}

		// Token: 0x06000032 RID: 50 RVA: 0x000025F4 File Offset: 0x000007F4
		private static bool TryReserve(long bytes)
		{
			bool flag = Interlocked.Read(ref BufferPool.s_PooledBytes) + bytes > 335544320L;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				Interlocked.Add(ref BufferPool.s_PooledBytes, bytes);
				result = true;
			}
			return result;
		}

		// Token: 0x06000033 RID: 51 RVA: 0x00002630 File Offset: 0x00000830
		public static float[] RentFloat(int length)
		{
			ConcurrentBag<float[]> bag = BufferPool.s_Floats.GetOrAdd(length, (int _) => new ConcurrentBag<float[]>());
			float[] a;
			bool flag = !bag.TryTake(out a);
			float[] result;
			if (flag)
			{
				result = new float[length];
			}
			else
			{
				Interlocked.Add(ref BufferPool.s_PooledBytes, -(long)length * 4L);
				result = a;
			}
			return result;
		}

		// Token: 0x06000034 RID: 52 RVA: 0x00002698 File Offset: 0x00000898
		public static void Return(float[] array)
		{
			bool flag = array == null || !BufferPool.TryReserve((long)array.Length * 4L);
			if (!flag)
			{
				BufferPool.s_Floats.GetOrAdd(array.Length, (int _) => new ConcurrentBag<float[]>()).Add(array);
			}
		}

		// Token: 0x06000035 RID: 53 RVA: 0x000026F4 File Offset: 0x000008F4
		public static byte[] RentByte(int length)
		{
			ConcurrentBag<byte[]> bag = BufferPool.s_Bytes.GetOrAdd(length, (int _) => new ConcurrentBag<byte[]>());
			byte[] a;
			bool flag = !bag.TryTake(out a);
			byte[] result;
			if (flag)
			{
				result = new byte[length];
			}
			else
			{
				Interlocked.Add(ref BufferPool.s_PooledBytes, -(long)length);
				result = a;
			}
			return result;
		}

		// Token: 0x06000036 RID: 54 RVA: 0x00002758 File Offset: 0x00000958
		public static void Return(byte[] array)
		{
			bool flag = array == null || !BufferPool.TryReserve((long)array.Length);
			if (!flag)
			{
				BufferPool.s_Bytes.GetOrAdd(array.Length, (int _) => new ConcurrentBag<byte[]>()).Add(array);
			}
		}

		/// <summary>Drop everything held. For the packer's tests, so one test's pool cannot mask another's
		/// allocation behaviour.</summary>
		// Token: 0x06000037 RID: 55 RVA: 0x000027B1 File Offset: 0x000009B1
		public static void Clear()
		{
			BufferPool.s_Floats.Clear();
			BufferPool.s_Bytes.Clear();
			Interlocked.Exchange(ref BufferPool.s_PooledBytes, 0L);
		}

		// Token: 0x04000018 RID: 24
		private static readonly ConcurrentDictionary<int, ConcurrentBag<float[]>> s_Floats = new ConcurrentDictionary<int, ConcurrentBag<float[]>>();

		// Token: 0x04000019 RID: 25
		private static readonly ConcurrentDictionary<int, ConcurrentBag<byte[]>> s_Bytes = new ConcurrentDictionary<int, ConcurrentBag<byte[]>>();

		// Token: 0x0400001A RID: 26
		private const long MaxPooledBytes = 335544320L;

		// Token: 0x0400001B RID: 27
		private static long s_PooledBytes;
	}
}
