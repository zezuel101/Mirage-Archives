using System;

namespace Mirage.VirtualTexture
{
	/// <summary>
	/// The atlas slot pool: which slot holds what, when each was last wanted, and the free-list plus
	/// LRU policy that hands them out. Only the policy lives here — the key-to-slot index every tier of
	/// the indirection resolves through stays on <see cref="T:Mirage.VirtualTexture.TileCache" />.
	/// </summary>
	// Token: 0x02000055 RID: 85
	internal sealed class TileSlotAllocator
	{
		// Token: 0x06000265 RID: 613 RVA: 0x00011FA4 File Offset: 0x000101A4
		public TileSlotAllocator(int slotCount)
		{
			this.owner = new long[slotCount];
			this.lastFrame = new int[slotCount];
			for (int i = 0; i < slotCount; i++)
			{
				this.owner[i] = long.MinValue;
			}
		}

		// Token: 0x17000069 RID: 105
		// (get) Token: 0x06000266 RID: 614 RVA: 0x00011FF9 File Offset: 0x000101F9
		public int Count
		{
			get
			{
				return this.owner.Length;
			}
		}

		// Token: 0x06000267 RID: 615 RVA: 0x00012003 File Offset: 0x00010203
		public long OwnerOf(int slot)
		{
			return this.owner[slot];
		}

		/// <summary>True when an owner value is a real tile key rather than Free or Pinned.</summary>
		// Token: 0x06000268 RID: 616 RVA: 0x0001200D File Offset: 0x0001020D
		public static bool HoldsTile(long slotOwner)
		{
			return slotOwner != long.MinValue && slotOwner != -9223372036854775807L;
		}

		/// <summary>Take any free slot without evicting. -1 when the atlas is full.</summary>
		// Token: 0x06000269 RID: 617 RVA: 0x00012030 File Offset: 0x00010230
		public int TakeFree()
		{
			for (int i = 0; i < this.owner.Length; i++)
			{
				bool flag = this.owner[i] == long.MinValue;
				if (flag)
				{
					return i;
				}
			}
			return -1;
		}

		/// <summary>
		/// Take a free slot, or evict the least recently used non-pinned one and report the key it held
		/// in <paramref name="evictedKey" />. -1 when every non-pinned slot is still needed this frame.
		/// </summary>
		// Token: 0x0600026A RID: 618 RVA: 0x00012078 File Offset: 0x00010278
		public int TakeOrEvict(int frame, out long evictedKey)
		{
			evictedKey = long.MinValue;
			int slot = this.TakeFree();
			bool flag = slot >= 0;
			int result;
			if (flag)
			{
				result = slot;
			}
			else
			{
				int oldest = int.MaxValue;
				int lru = -1;
				for (int i = 0; i < this.owner.Length; i++)
				{
					bool flag2 = !TileSlotAllocator.HoldsTile(this.owner[i]) || this.lastFrame[i] == frame;
					if (!flag2)
					{
						bool flag3 = this.lastFrame[i] < oldest;
						if (flag3)
						{
							oldest = this.lastFrame[i];
							lru = i;
						}
					}
				}
				bool flag4 = lru < 0;
				if (flag4)
				{
					result = -1;
				}
				else
				{
					evictedKey = this.owner[lru];
					this.owner[lru] = long.MinValue;
					this.lastFrame[lru] = 0;
					result = lru;
				}
			}
			return result;
		}

		/// <summary>
		/// Can a tile be placed this frame — is a slot free, or non-pinned and not already wanted?
		/// </summary>
		// Token: 0x0600026B RID: 619 RVA: 0x00012154 File Offset: 0x00010354
		public bool HasCapacity(int frame)
		{
			bool flag = this.noCapacityFrame == frame;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				for (int i = 0; i < this.owner.Length; i++)
				{
					bool flag2 = this.owner[i] == long.MinValue;
					if (flag2)
					{
						return true;
					}
					bool flag3 = this.owner[i] != -9223372036854775807L && this.lastFrame[i] != frame;
					if (flag3)
					{
						return true;
					}
				}
				this.noCapacityFrame = frame;
				result = false;
			}
			return result;
		}

		// Token: 0x0600026C RID: 620 RVA: 0x000121E3 File Offset: 0x000103E3
		public void Assign(int slot, long key, int frame)
		{
			this.owner[slot] = key;
			this.lastFrame[slot] = frame;
		}

		/// <summary>Claim a slot for a bootstrapped tile, so it reads as perpetually fresh.</summary>
		// Token: 0x0600026D RID: 621 RVA: 0x000121F8 File Offset: 0x000103F8
		public void Pin(int slot)
		{
			this.owner[slot] = -9223372036854775807L;
			this.lastFrame[slot] = int.MaxValue;
		}

		/// <summary>Refresh a slot's LRU stamp so it survives while the tile is needed.</summary>
		// Token: 0x0600026E RID: 622 RVA: 0x00012219 File Offset: 0x00010419
		public void Touch(int slot, int frame)
		{
			this.lastFrame[slot] = frame;
		}

		/// <summary>Owner of a slot holding nothing.</summary>
		// Token: 0x0400020D RID: 525
		public const long Free = -9223372036854775808L;

		/// <summary>Owner of a bootstrapped slot, which never ages out.</summary>
		// Token: 0x0400020E RID: 526
		public const long Pinned = -9223372036854775807L;

		// Token: 0x0400020F RID: 527
		private readonly long[] owner;

		// Token: 0x04000210 RID: 528
		private readonly int[] lastFrame;

		// Token: 0x04000211 RID: 529
		private int noCapacityFrame = -1;
	}
}
