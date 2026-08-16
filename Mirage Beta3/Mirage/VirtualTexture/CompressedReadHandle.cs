using System;
using System.IO;
using System.Threading.Tasks;
using KSPTextureLoader;
using Unity.Collections;
using UnityEngine;

namespace Mirage.VirtualTexture
{
	/// <summary>Compressed archive tile: decode off-thread (phase 1), then native upload (phase 2).</summary>
	// Token: 0x02000035 RID: 53
	internal sealed class CompressedReadHandle : TileReadHandle
	{
		// Token: 0x06000148 RID: 328 RVA: 0x0000A888 File Offset: 0x00008A88
		public CompressedReadHandle(Texture2DConfig config, string blobPath, long payloadOffset, int storedLen, TileCodec codec, int rawLen)
		{
			this.config = config;
			this.decodeTask = Task.Run<byte[]>(() => CompressedReadHandle.ReadAndDecode(blobPath, payloadOffset, storedLen, codec, rawLen));
		}

		// Token: 0x1700003F RID: 63
		// (get) Token: 0x06000149 RID: 329 RVA: 0x0000A8E8 File Offset: 0x00008AE8
		public override bool IsComplete
		{
			get
			{
				bool flag = this.loadTask == null && !this.TryStartUpload();
				bool isComplete;
				if (flag)
				{
					isComplete = this.faulted;
				}
				else
				{
					isComplete = this.loadTask.IsComplete;
				}
				return isComplete;
			}
		}

		// Token: 0x17000040 RID: 64
		// (get) Token: 0x0600014A RID: 330 RVA: 0x0000A926 File Offset: 0x00008B26
		public override bool IsFaulted
		{
			get
			{
				return this.faulted || (this.loadTask == null && this.decodeTask.IsFaulted);
			}
		}

		// Token: 0x0600014B RID: 331 RVA: 0x0000A94C File Offset: 0x00008B4C
		public override Texture2D GetTexture()
		{
			bool flag = this.loadTask == null;
			if (flag)
			{
				this.StartUpload(this.AwaitDecode());
			}
			this.result = this.loadTask.GetTexture();
			return this.result;
		}

		// Token: 0x0600014C RID: 332 RVA: 0x0000A990 File Offset: 0x00008B90
		public override void Dispose()
		{
			bool flag = this.loadTask == null;
			if (flag)
			{
				bool isCreated = this.data.IsCreated;
				if (isCreated)
				{
					this.data.Dispose();
				}
			}
			else
			{
				bool flag2 = !this.loadTask.IsComplete;
				if (flag2)
				{
					ArchiveLoadReaper.Park(this.loadTask, this.data);
					this.data = default(NativeArray<byte>);
				}
				else
				{
					bool isCreated2 = this.data.IsCreated;
					if (isCreated2)
					{
						this.data.Dispose();
					}
					ArchiveLoadReaper.DestroyCompleted(this.loadTask, ref this.result);
				}
			}
		}

		// Token: 0x0600014D RID: 333 RVA: 0x0000AA2C File Offset: 0x00008C2C
		private bool TryStartUpload()
		{
			bool flag = !this.decodeTask.IsCompleted;
			bool flag2;
			if (flag)
			{
				flag2 = false;
			}
			else
			{
				bool isFaulted = this.decodeTask.IsFaulted;
				if (isFaulted)
				{
					this.faulted = true;
					flag2 = false;
				}
				else
				{
					this.StartUpload(this.decodeTask.Result);
					flag2 = true;
				}
			}
			return flag2;
		}

		// Token: 0x0600014E RID: 334 RVA: 0x0000AA82 File Offset: 0x00008C82
		private void StartUpload(byte[] raw)
		{
			this.data = new NativeArray<byte>(raw, 4);
			this.loadTask = TextureLoader.LoadOwnedTexture2D<byte>(this.config, this.data);
		}

		// Token: 0x0600014F RID: 335 RVA: 0x0000AAAC File Offset: 0x00008CAC
		private byte[] AwaitDecode()
		{
			byte[] array;
			try
			{
				array = this.decodeTask.Result;
			}
			catch (AggregateException ae)
			{
				this.faulted = true;
				Exception inner = ae.GetBaseException();
				throw new InvalidOperationException("archive: tile decode failed: " + inner.GetType().Name + ": " + inner.Message, inner);
			}
			return array;
		}

		// Token: 0x06000150 RID: 336 RVA: 0x0000AB14 File Offset: 0x00008D14
		private static byte[] ReadAndDecode(string blobPath, long payloadOffset, int storedLen, TileCodec codec, int rawLen)
		{
			byte[] stored = new byte[storedLen];
			using (FileStream fs = new FileStream(blobPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536))
			{
				fs.Position = payloadOffset;
				int i;
				for (int read = 0; read < storedLen; read += i)
				{
					i = fs.Read(stored, read, storedLen - read);
					bool flag = i <= 0;
					if (flag)
					{
						throw new EndOfStreamException("archive: short read for compressed tile");
					}
				}
			}
			byte[] raw = new byte[rawLen];
			MirageArchiveFormat.DecodeTilePayload(codec, stored, storedLen, raw, rawLen);
			return raw;
		}

		// Token: 0x0400010B RID: 267
		private readonly Texture2DConfig config;

		// Token: 0x0400010C RID: 268
		private readonly Task<byte[]> decodeTask;

		// Token: 0x0400010D RID: 269
		private TextureLoadTask<Texture2D> loadTask;

		// Token: 0x0400010E RID: 270
		private NativeArray<byte> data;

		// Token: 0x0400010F RID: 271
		private Texture2D result;

		// Token: 0x04000110 RID: 272
		private bool faulted;
	}
}
