using System;
using System.Collections.Generic;

namespace Mirage.WebIngest
{
	/// <summary>
	/// Everything one bake rented, returned together when the bake ends.
	///
	/// This type exists because the dangerous half of pooling is not the rent, it is the return. A buffer
	/// returned twice is handed to two concurrent bakes and corrupts both silently; a buffer never returned is
	/// just a slower leak than not pooling at all. Both are easy to write when rents and returns are scattered
	/// across four methods and every early-out (`return IngestOutcome.NoCoverage`, a decoder throwing, a
	/// cancellation) is a path that has to remember.
	///
	/// So: rent through here, and <see cref="M:Mirage.WebIngest.BakeBuffers.Dispose" /> in a `finally` at the ONE place a bake ends. Every
	/// early-out is then correct by construction rather than by review — which matters because
	/// <see cref="M:Mirage.WebIngest.CubeTileBaker.BakeAsync(System.Int32,System.Int32,System.Int32,System.Int32,System.Threading.CancellationToken)" /> has a dozen of them.
	///
	/// Lifetime note: <see cref="T:Mirage.WebIngest.MercatorGather" /> KEEPS the arrays it is given (Dictionary&lt;long, float[]&gt;),
	/// and the DEM gather outlives the reprojection because BakeNormal samples it afterwards. So a source tile's
	/// buffer cannot be returned when the decode loop ends — only when the whole bake does. That is exactly the
	/// scope this type has.
	///
	/// Not thread-safe, deliberately: one instance belongs to one bake. Bakes run concurrently with each other,
	/// but nothing inside a single bake rents from two threads at once (the parallel decode loops write into
	/// slots of an array rented up front on the bake's own thread).
	/// </summary>
	// Token: 0x02000007 RID: 7
	internal sealed class BakeBuffers : IDisposable
	{
		// Token: 0x06000032 RID: 50 RVA: 0x00002854 File Offset: 0x00000A54
		public float[] RentFloat(int length)
		{
			float[] a = BufferPool.RentFloat(length);
			this.floats.Add(a);
			return a;
		}

		// Token: 0x06000033 RID: 51 RVA: 0x0000287C File Offset: 0x00000A7C
		public byte[] RentByte(int length)
		{
			byte[] a = BufferPool.RentByte(length);
			this.bytes.Add(a);
			return a;
		}

		// Token: 0x06000034 RID: 52 RVA: 0x000028A4 File Offset: 0x00000AA4
		public void Dispose()
		{
			for (int i = 0; i < this.floats.Count; i++)
			{
				BufferPool.Return(this.floats[i]);
			}
			for (int j = 0; j < this.bytes.Count; j++)
			{
				BufferPool.Return(this.bytes[j]);
			}
			this.floats.Clear();
			this.bytes.Clear();
		}

		// Token: 0x04000024 RID: 36
		private readonly List<float[]> floats = new List<float[]>();

		// Token: 0x04000025 RID: 37
		private readonly List<byte[]> bytes = new List<byte[]>();
	}
}
