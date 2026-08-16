using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirage.VirtualTexture
{
	/// <summary>
	/// The parallel payload atlases — color, height, normal, emissive — that share one slot map. Every
	/// layer uses the same slot index, so a tile is only usable once all of them accept it. Owns atlas
	/// allocation, per-tile validation, the copy into a slot, and material binding.
	/// </summary>
	// Token: 0x02000054 RID: 84
	public sealed class TileLayerAtlases : IDisposable
	{
		/// <summary>
		/// The parallel payload atlases — color, height, normal, emissive — that share one slot map. Every
		/// layer uses the same slot index, so a tile is only usable once all of them accept it. Owns atlas
		/// allocation, per-tile validation, the copy into a slot, and material binding.
		/// </summary>
		// Token: 0x06000254 RID: 596 RVA: 0x000118D8 File Offset: 0x0000FAD8
		public TileLayerAtlases(int atlasSize, int slotSize)
		{
		}

		// Token: 0x17000066 RID: 102
		// (get) Token: 0x06000255 RID: 597 RVA: 0x000118FA File Offset: 0x0000FAFA
		public int Count
		{
			get
			{
				return this.layers.Count;
			}
		}

		// Token: 0x17000067 RID: 103
		public TileLayerAtlases.Layer this[int index]
		{
			get
			{
				return this.layers[index];
			}
		}

		// Token: 0x17000068 RID: 104
		// (get) Token: 0x06000257 RID: 599 RVA: 0x00011915 File Offset: 0x0000FB15
		public IReadOnlyList<TileLayerAtlases.Layer> All
		{
			get
			{
				return this.layers;
			}
		}

		/// <summary>Register a payload layer with the tile source it reads from.
		/// <paramref name="maxLevel" /> is the deepest level it holds tiles for. Call before
		/// bootstrapping.</summary>
		// Token: 0x06000258 RID: 600 RVA: 0x00011920 File Offset: 0x0000FB20
		public void Add(VTLayer id, string uniformPrefix, ITileLayerSource source, int maxLevel)
		{
			this.layers.Add(new TileLayerAtlases.Layer
			{
				id = id,
				uniformPrefix = uniformPrefix,
				atlasPropertyId = Shader.PropertyToID(uniformPrefix + "TileAtlas"),
				source = source,
				linear = source.Linear,
				maxLevel = maxLevel
			});
		}

		/// <summary>Does this layer reach the given level at all?</summary>
		// Token: 0x06000259 RID: 601 RVA: 0x0001197E File Offset: 0x0000FB7E
		public bool CoversLevel(int layerIndex, int level)
		{
			return level <= this.layers[layerIndex].maxLevel;
		}

		/// <summary>Does every layer that reaches this level have the tile on disk? Coarse tiles are
		/// pinned in lockstep, so one missing layer disqualifies the tile entirely.</summary>
		// Token: 0x0600025A RID: 602 RVA: 0x00011998 File Offset: 0x0000FB98
		public bool AllHave(int face, int level, int tx, int ty)
		{
			for (int i = 0; i < this.layers.Count; i++)
			{
				bool flag = this.CoversLevel(i, level) && !this.layers[i].source.Exists(face, level, tx, ty);
				if (flag)
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Are all the payloads usable — present, and matching the atlas in size and format? Allocates
		/// each layer's atlas on its first tile. False means the tile should be rejected outright.
		/// </summary>
		// Token: 0x0600025B RID: 603 RVA: 0x000119F8 File Offset: 0x0000FBF8
		public bool Accept(Texture2D[] tilesByLayer, int face, int level, int tx, int ty)
		{
			for (int i = 0; i < this.layers.Count; i++)
			{
				bool flag = !this.CoversLevel(i, level);
				if (!flag)
				{
					bool flag2 = tilesByLayer[i] == null || !this.EnsureAtlasAllocated(this.layers[i], tilesByLayer[i], face, level, tx, ty) || !this.ValidateTile(this.layers[i], tilesByLayer[i], face, level, tx, ty);
					if (flag2)
					{
						return false;
					}
				}
			}
			return true;
		}

		/// <summary>
		/// Force every layer's GPU resource before any copy — never sync/copy/sync/copy.
		/// </summary>
		// Token: 0x0600025C RID: 604 RVA: 0x00011A8C File Offset: 0x0000FC8C
		public void ForceGpuResources(Texture2D[] tilesByLayer)
		{
			for (int i = 0; i < this.layers.Count; i++)
			{
				Texture2D texture2D = tilesByLayer[i];
				if (texture2D != null)
				{
					texture2D.GetNativeTexturePtr();
				}
			}
		}

		/// <summary>Copy every loaded payload into the shared slot at (slotX, slotY). Returns the
		/// number of copies made; a layer that does not reach this level has nothing to copy.</summary>
		// Token: 0x0600025D RID: 605 RVA: 0x00011AC4 File Offset: 0x0000FCC4
		public int CopyToSlot(Texture2D[] tilesByLayer, int slotX, int slotY)
		{
			int copies = 0;
			for (int i = 0; i < this.layers.Count; i++)
			{
				bool flag = tilesByLayer[i] == null;
				if (!flag)
				{
					copies++;
					Graphics.CopyTexture(tilesByLayer[i], 0, 0, 0, 0, this.slotSize, this.slotSize, this.layers[i].atlas, 0, 0, slotX * this.slotSize, slotY * this.slotSize);
				}
			}
			return copies;
		}

		/// <summary>Bind every allocated atlas onto a material, plus which optional layers
		/// exist.</summary>
		// Token: 0x0600025E RID: 606 RVA: 0x00011B48 File Offset: 0x0000FD48
		public void BindTo(Material mat)
		{
			bool hasNormal = false;
			bool hasEmissive = false;
			int emissiveMaxLevel = 0;
			foreach (TileLayerAtlases.Layer layer in this.layers)
			{
				bool flag = layer.atlas != null;
				if (flag)
				{
					mat.SetTexture(layer.atlasPropertyId, layer.atlas);
				}
				bool flag2 = layer.id == VTLayer.Normal;
				if (flag2)
				{
					hasNormal = true;
				}
				bool flag3 = layer.id == VTLayer.Emissive;
				if (flag3)
				{
					hasEmissive = true;
					emissiveMaxLevel = layer.maxLevel;
				}
			}
			mat.SetFloat(TileLayerAtlases.s_HasNormalId, hasNormal ? 1f : 0f);
			mat.SetFloat(TileLayerAtlases.s_HasEmissiveId, hasEmissive ? 1f : 0f);
			mat.SetFloat(TileLayerAtlases.s_EmissiveMaxLevelId, (float)emissiveMaxLevel);
		}

		// Token: 0x0600025F RID: 607 RVA: 0x00011C3C File Offset: 0x0000FE3C
		public void Dispose()
		{
			foreach (TileLayerAtlases.Layer layer in this.layers)
			{
				bool flag = layer.atlas != null;
				if (flag)
				{
					Object.Destroy(layer.atlas);
					layer.atlas = null;
				}
				ITileLayerSource source = layer.source;
				if (source != null)
				{
					source.Dispose();
				}
			}
			this.layers.Clear();
		}

		/// <summary>Describes a tile for an error message. Failure paths only — it allocates.</summary>
		// Token: 0x06000260 RID: 608 RVA: 0x00011CD0 File Offset: 0x0000FED0
		private static string Describe(TileLayerAtlases.Layer layer, int face, int level, int tx, int ty)
		{
			return string.Format("streaming {0} L{1} f{2} {3},{4}", new object[]
			{
				layer.id,
				level,
				face,
				tx,
				ty
			});
		}

		// Token: 0x06000261 RID: 609 RVA: 0x00011D20 File Offset: 0x0000FF20
		private static int CompressedBlockSize(TextureFormat fmt)
		{
			int result;
			if (fmt != 10 && fmt != 12 && fmt - 24 > 3)
			{
				result = 1;
			}
			else
			{
				result = 4;
			}
			return result;
		}

		// Token: 0x06000262 RID: 610 RVA: 0x00011D54 File Offset: 0x0000FF54
		private bool EnsureAtlasAllocated(TileLayerAtlases.Layer layer, Texture2D firstTile, int face, int level, int tx, int ty)
		{
			bool flag = layer.atlas != null;
			bool result;
			if (flag)
			{
				result = true;
			}
			else
			{
				int blockSize = TileLayerAtlases.CompressedBlockSize(firstTile.format);
				bool flag2 = blockSize > 1 && this.slotSize % blockSize != 0;
				if (flag2)
				{
					MirageDebug.LogError(string.Concat(new string[]
					{
						string.Format("TileCache: {0} block size {1} doesn't divide slot size ", firstTile.format, blockSize),
						string.Format("{0} — use an aligned tile/border or an uncompressed format. ", this.slotSize),
						"(",
						TileLayerAtlases.Describe(layer, face, level, tx, ty),
						")"
					}));
					result = false;
				}
				else
				{
					layer.atlas = new Texture2D(this.atlasSize, this.atlasSize, firstTile.format, false, layer.linear)
					{
						name = "VTAtlas_" + layer.id.ToString(),
						wrapMode = 1,
						filterMode = 1
					};
					result = true;
				}
			}
			return result;
		}

		// Token: 0x06000263 RID: 611 RVA: 0x00011E64 File Offset: 0x00010064
		private bool ValidateTile(TileLayerAtlases.Layer layer, Texture2D tile, int face, int level, int tx, int ty)
		{
			bool flag = tile.width != this.slotSize || tile.height != this.slotSize;
			bool result;
			if (flag)
			{
				MirageDebug.LogError("TileCache: tile " + TileLayerAtlases.Describe(layer, face, level, tx, ty) + " is " + string.Format("{0}x{1}, expected {2}x{3}", new object[]
				{
					tile.width,
					tile.height,
					this.slotSize,
					this.slotSize
				}));
				result = false;
			}
			else
			{
				bool flag2 = tile.format != layer.atlas.format;
				if (flag2)
				{
					MirageDebug.LogError(string.Format("TileCache: tile {0} format {1} ", TileLayerAtlases.Describe(layer, face, level, tx, ty), tile.format) + string.Format("≠ atlas {0}", layer.atlas.format));
					result = false;
				}
				else
				{
					result = true;
				}
			}
			return result;
		}

		// Token: 0x04000207 RID: 519
		private static readonly int s_HasNormalId = Shader.PropertyToID("_HasNormalVT");

		// Token: 0x04000208 RID: 520
		private static readonly int s_HasEmissiveId = Shader.PropertyToID("_HasEmissiveVT");

		// Token: 0x04000209 RID: 521
		private static readonly int s_EmissiveMaxLevelId = Shader.PropertyToID("_EmissiveMaxLevel");

		// Token: 0x0400020A RID: 522
		private readonly List<TileLayerAtlases.Layer> layers = new List<TileLayerAtlases.Layer>();

		// Token: 0x0400020B RID: 523
		private readonly int atlasSize = atlasSize;

		// Token: 0x0400020C RID: 524
		private readonly int slotSize = slotSize;

		/// <summary>A payload atlas and its load parameters. The atlas is allocated on the first tile,
		/// so it matches that tile's format.</summary>
		// Token: 0x020000D3 RID: 211
		public sealed class Layer
		{
			// Token: 0x0400058D RID: 1421
			public VTLayer id;

			// Token: 0x0400058E RID: 1422
			public string uniformPrefix;

			// Token: 0x0400058F RID: 1423
			public int atlasPropertyId;

			// Token: 0x04000590 RID: 1424
			public ITileLayerSource source;

			// Token: 0x04000591 RID: 1425
			public bool linear;

			// Token: 0x04000592 RID: 1426
			public int maxLevel;

			// Token: 0x04000593 RID: 1427
			public Texture2D atlas;
		}
	}
}
