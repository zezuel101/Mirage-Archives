using System;
using KSPTextureLoader;
using UnityEngine;

namespace Mirage.VirtualTexture
{
	// Token: 0x02000040 RID: 64
	public sealed class LooseFileTileSource : ITileLayerSource, IDisposable
	{
		// Token: 0x1700004B RID: 75
		// (get) Token: 0x06000196 RID: 406 RVA: 0x0000C382 File Offset: 0x0000A582
		public bool Linear { get; }

		// Token: 0x06000197 RID: 407 RVA: 0x0000C38A File Offset: 0x0000A58A
		public LooseFileTileSource(string rootPath, bool linear)
		{
			this.rootPath = rootPath;
			this.Linear = linear;
		}

		// Token: 0x06000198 RID: 408 RVA: 0x0000C3A2 File Offset: 0x0000A5A2
		public bool Exists(int face, int level, int tx, int ty)
		{
			return TextureLoader.TextureExists(MirageTileMath.TilePath(this.rootPath, face, level, tx, ty));
		}

		// Token: 0x06000199 RID: 409 RVA: 0x0000C3BC File Offset: 0x0000A5BC
		public TileReadHandle BeginLoad(int face, int level, int tx, int ty)
		{
			string path = MirageTileMath.TilePath(this.rootPath, face, level, tx, ty);
			TextureLoadOptions textureLoadOptions;
			textureLoadOptions..ctor();
			textureLoadOptions.Linear = new bool?(this.Linear);
			textureLoadOptions.Unreadable = true;
			TextureLoadOptions opts = textureLoadOptions;
			return new LooseFileTileSource.LooseReadHandle(TextureLoader.LoadTexture<Texture2D>(path, opts));
		}

		// Token: 0x0600019A RID: 410 RVA: 0x0000C410 File Offset: 0x0000A610
		public void Dispose()
		{
		}

		// Token: 0x04000159 RID: 345
		private readonly string rootPath;

		// Token: 0x020000B6 RID: 182
		private sealed class LooseReadHandle : TileReadHandle
		{
			// Token: 0x060004D1 RID: 1233 RVA: 0x000210FA File Offset: 0x0001F2FA
			public LooseReadHandle(TextureHandle<Texture2D> handle)
			{
				this.handle = handle;
			}

			// Token: 0x1700010B RID: 267
			// (get) Token: 0x060004D2 RID: 1234 RVA: 0x0002110A File Offset: 0x0001F30A
			public override bool IsComplete
			{
				get
				{
					return this.handle.IsComplete;
				}
			}

			// Token: 0x1700010C RID: 268
			// (get) Token: 0x060004D3 RID: 1235 RVA: 0x00021117 File Offset: 0x0001F317
			public override bool IsFaulted
			{
				get
				{
					return this.handle.IsError;
				}
			}

			// Token: 0x060004D4 RID: 1236 RVA: 0x00021124 File Offset: 0x0001F324
			public override Texture2D GetTexture()
			{
				return this.handle.GetTexture();
			}

			// Token: 0x060004D5 RID: 1237 RVA: 0x00021131 File Offset: 0x0001F331
			public override void Dispose()
			{
				this.handle.Dispose();
			}

			// Token: 0x040004C1 RID: 1217
			private readonly TextureHandle<Texture2D> handle;
		}
	}
}
