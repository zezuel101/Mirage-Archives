using System;
using System.IO;

namespace Mirage.VirtualTexture
{
	/// <summary>Container constants, tile keys, alignment, CRC32, and per-tile codecs.</summary>
	// Token: 0x0200003C RID: 60
	public static class MirageArchiveFormat
	{
		// Token: 0x06000163 RID: 355 RVA: 0x0000B298 File Offset: 0x00009498
		public static ulong PackKey(int face, int level, int x, int y)
		{
			bool flag = face > 5;
			if (flag)
			{
				throw new ArgumentOutOfRangeException("face");
			}
			bool flag2 = level > 511;
			if (flag2)
			{
				throw new ArgumentOutOfRangeException("level");
			}
			bool flag3 = x >= 131072 || y >= 131072;
			if (flag3)
			{
				throw new ArgumentOutOfRangeException(string.Format("tile coord out of range: {0},{1}", x, y));
			}
			ulong interleaved = MirageArchiveFormat.Part1By1((uint)x) | MirageArchiveFormat.Part1By1((uint)y) << 1;
			return (ulong)((long)face << 60 | (long)level << 51 | (long)interleaved);
		}

		// Token: 0x06000164 RID: 356 RVA: 0x0000B32C File Offset: 0x0000952C
		public static void UnpackKey(ulong key, out int face, out int level, out int x, out int y)
		{
			face = MirageArchiveFormat.KeyFace(key);
			level = MirageArchiveFormat.KeyLevel(key);
			ulong interleaved = key & 17179869183UL;
			x = (int)MirageArchiveFormat.Compact1By1(interleaved);
			y = (int)MirageArchiveFormat.Compact1By1(interleaved >> 1);
		}

		// Token: 0x06000165 RID: 357 RVA: 0x0000B369 File Offset: 0x00009569
		public static int KeyFace(ulong key)
		{
			return (int)(key >> 60 & 7UL);
		}

		// Token: 0x06000166 RID: 358 RVA: 0x0000B373 File Offset: 0x00009573
		public static int KeyLevel(ulong key)
		{
			return (int)(key >> 51 & 511UL);
		}

		// Token: 0x06000167 RID: 359 RVA: 0x0000B384 File Offset: 0x00009584
		private static ulong Part1By1(uint v)
		{
			ulong x = (ulong)v;
			x &= 131071UL;
			x = ((x | x << 16) & 281470681808895UL);
			x = ((x | x << 8) & 71777214294589695UL);
			x = ((x | x << 4) & 1085102592571150095UL);
			x = ((x | x << 2) & 3689348814741910323UL);
			return (x | x << 1) & 6148914691236517205UL;
		}

		// Token: 0x06000168 RID: 360 RVA: 0x0000B3F4 File Offset: 0x000095F4
		private static uint Compact1By1(ulong x)
		{
			x &= 6148914691236517205UL;
			x = ((x | x >> 1) & 3689348814741910323UL);
			x = ((x | x >> 2) & 1085102592571150095UL);
			x = ((x | x >> 4) & 71777214294589695UL);
			x = ((x | x >> 8) & 281470681808895UL);
			x = ((x | x >> 16) & (ulong)-1);
			return (uint)x;
		}

		// Token: 0x06000169 RID: 361 RVA: 0x0000B464 File Offset: 0x00009664
		public static long AlignUp(long value, int alignment)
		{
			long i = value % (long)alignment;
			return (i == 0L) ? value : (value + ((long)alignment - i));
		}

		// Token: 0x0600016A RID: 362 RVA: 0x0000B488 File Offset: 0x00009688
		private static uint[] BuildCrc32Table()
		{
			uint[] table = new uint[256];
			for (uint i = 0U; i < 256U; i += 1U)
			{
				uint c = i;
				for (int j = 0; j < 8; j++)
				{
					c = (((c & 1U) != 0U) ? (3988292384U ^ c >> 1) : (c >> 1));
				}
				table[(int)i] = c;
			}
			return table;
		}

		// Token: 0x0600016B RID: 363 RVA: 0x0000B4EC File Offset: 0x000096EC
		public static uint Crc32(byte[] data, int offset, int count)
		{
			uint crc = uint.MaxValue;
			int end = offset + count;
			for (int i = offset; i < end; i++)
			{
				crc = (MirageArchiveFormat.s_Crc32Table[(int)((crc ^ (uint)data[i]) & 255U)] ^ crc >> 8);
			}
			return crc ^ uint.MaxValue;
		}

		// Token: 0x0600016C RID: 364 RVA: 0x0000B52F File Offset: 0x0000972F
		public static uint Crc32(byte[] data)
		{
			return MirageArchiveFormat.Crc32(data, 0, data.Length);
		}

		/// <summary>Raw decoded payload size (never stored — implied by format + dimensions).</summary>
		// Token: 0x0600016D RID: 365 RVA: 0x0000B53C File Offset: 0x0000973C
		public static int RawPayloadBytes(int format, int width, int height)
		{
			if (format != 4)
			{
				switch (format)
				{
				case 9:
					return width * height * 2;
				case 10:
					break;
				case 11:
					goto IL_6A;
				case 12:
					goto IL_4D;
				default:
					switch (format)
					{
					case 24:
					case 25:
					case 27:
						goto IL_4D;
					case 26:
						break;
					default:
						goto IL_6A;
					}
					break;
				}
				return MirageArchiveFormat.BlockCount(width, height) * 8;
				IL_4D:
				return MirageArchiveFormat.BlockCount(width, height) * 16;
				IL_6A:
				throw new ArgumentException(string.Format("RawPayloadBytes: unknown format {0}", format));
			}
			return width * height * 4;
		}

		// Token: 0x0600016E RID: 366 RVA: 0x0000B5CA File Offset: 0x000097CA
		private static int BlockCount(int width, int height)
		{
			return (width + 3) / 4 * ((height + 3) / 4);
		}

		/// <summary>Should this payload be plane-split before LZ4? Height R16 only.</summary>
		// Token: 0x0600016F RID: 367 RVA: 0x0000B5D7 File Offset: 0x000097D7
		public static bool UsePlaneSplit(int format)
		{
			return format == 9;
		}

		/// <summary>Deinterleave R16 (lo, hi, lo, hi, …) into [all lo][all hi].</summary>
		// Token: 0x06000170 RID: 368 RVA: 0x0000B5E0 File Offset: 0x000097E0
		public static byte[] PlaneSplitR16(byte[] r16)
		{
			int i = r16.Length / 2;
			byte[] planed = new byte[r16.Length];
			for (int j = 0; j < i; j++)
			{
				planed[j] = r16[2 * j];
				planed[i + j] = r16[2 * j + 1];
			}
			return planed;
		}

		/// <summary>Inverse of <see cref="M:Mirage.VirtualTexture.MirageArchiveFormat.PlaneSplitR16(System.Byte[])" />.</summary>
		// Token: 0x06000171 RID: 369 RVA: 0x0000B62C File Offset: 0x0000982C
		public static void PlaneUnsplitR16(byte[] planed, byte[] r16)
		{
			int i = r16.Length / 2;
			for (int j = 0; j < i; j++)
			{
				r16[2 * j] = planed[j];
				r16[2 * j + 1] = planed[i + j];
			}
		}

		/// <summary>Encode a raw LE R16 buffer with vertical delta + zigzag + per-block bitpacking.</summary>
		// Token: 0x06000172 RID: 370 RVA: 0x0000B668 File Offset: 0x00009868
		public static byte[] VDeltaBitpackEncode(byte[] r16, int width, int height)
		{
			int count = width * height;
			bool flag = r16.Length < count * 2;
			if (flag)
			{
				throw new ArgumentException("vdelta: source shorter than width*height*2");
			}
			int blockSize = 64;
			int nblocks = (count + blockSize - 1) / blockSize;
			ushort[] residuals = MirageArchiveFormat.ZigzagResiduals(r16, width, height);
			byte[] widths = new byte[nblocks];
			long bits = MirageArchiveFormat.MeasureBlocks(residuals, count, blockSize, widths);
			byte[] packed = new byte[5 + nblocks + (int)((bits + 7L) / 8L)];
			packed[0] = (byte)width;
			packed[1] = (byte)(width >> 8);
			packed[2] = (byte)height;
			packed[3] = (byte)(height >> 8);
			packed[4] = 6;
			Buffer.BlockCopy(widths, 0, packed, 5, nblocks);
			MirageArchiveFormat.PackBlocks(residuals, widths, count, blockSize, packed, 5 + nblocks);
			return packed;
		}

		// Token: 0x06000173 RID: 371 RVA: 0x0000B718 File Offset: 0x00009918
		private static ushort[] ZigzagResiduals(byte[] r16, int width, int height)
		{
			ushort[] zz = new ushort[width * height];
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					int i = y * width + x;
					ushort v = MirageArchiveFormat.LoadR16(r16, i);
					ushort pred = (y == 0) ? ((x == 0) ? 0 : MirageArchiveFormat.LoadR16(r16, i - 1)) : MirageArchiveFormat.LoadR16(r16, i - width);
					short d = (short)(v - pred);
					zz[i] = (ushort)(((int)d << 1 ^ d >> 15) & 65535);
				}
			}
			return zz;
		}

		// Token: 0x06000174 RID: 372 RVA: 0x0000B7A8 File Offset: 0x000099A8
		private static long MeasureBlocks(ushort[] zz, int count, int blockSize, byte[] widths)
		{
			long bits = 0L;
			for (int b = 0; b < widths.Length; b++)
			{
				int start = b * blockSize;
				int end = Math.Min(start + blockSize, count);
				ushort max = 0;
				for (int i = start; i < end; i++)
				{
					bool flag = zz[i] > max;
					if (flag)
					{
						max = zz[i];
					}
				}
				widths[b] = (byte)MirageArchiveFormat.BitsFor(max);
				bits += (long)((ulong)widths[b] * (ulong)((long)(end - start)));
			}
			return bits;
		}

		// Token: 0x06000175 RID: 373 RVA: 0x0000B828 File Offset: 0x00009A28
		private static void PackBlocks(ushort[] zz, byte[] widths, int count, int blockSize, byte[] dst, int p)
		{
			ulong acc = 0UL;
			int accBits = 0;
			for (int b = 0; b < widths.Length; b++)
			{
				int w = (int)widths[b];
				bool flag = w == 0;
				if (!flag)
				{
					int start = b * blockSize;
					int end = Math.Min(start + blockSize, count);
					for (int i = start; i < end; i++)
					{
						acc |= (ulong)zz[i] << accBits;
						for (accBits += w; accBits >= 8; accBits -= 8)
						{
							dst[p++] = (byte)acc;
							acc >>= 8;
						}
					}
				}
			}
			bool flag2 = accBits > 0;
			if (flag2)
			{
				dst[p] = (byte)acc;
			}
		}

		/// <summary>Inverse of <see cref="M:Mirage.VirtualTexture.MirageArchiveFormat.VDeltaBitpackEncode(System.Byte[],System.Int32,System.Int32)" />.</summary>
		// Token: 0x06000176 RID: 374 RVA: 0x0000B8D8 File Offset: 0x00009AD8
		public static void VDeltaBitpackDecode(byte[] src, int srcOffset, int srcLen, byte[] r16, int width, int height)
		{
			int count = width * height;
			bool flag = r16.Length < count * 2;
			if (flag)
			{
				throw new ArgumentException("vdelta: destination shorter than width*height*2");
			}
			int blockSize = MirageArchiveFormat.ReadVDeltaHeader(src, srcOffset, srcLen, width, height);
			int nblocks = (count + blockSize - 1) / blockSize;
			int widthTable = srcOffset + 5;
			bool flag2 = srcLen < 5 + nblocks;
			if (flag2)
			{
				throw new InvalidDataException("vdelta: truncated block-width table");
			}
			int p = widthTable + nblocks;
			int srcEnd = srcOffset + srcLen;
			ulong acc = 0UL;
			int accBits = 0;
			for (int b = 0; b < nblocks; b++)
			{
				int bw = (int)src[widthTable + b];
				bool flag3 = bw > 16;
				if (flag3)
				{
					throw new InvalidDataException(string.Format("vdelta: bad block width {0}", bw));
				}
				int start = b * blockSize;
				int end = Math.Min(start + blockSize, count);
				int x = start % width;
				int y = start / width;
				for (int i = start; i < end; i++)
				{
					uint z = 0U;
					bool flag4 = bw != 0;
					if (flag4)
					{
						while (accBits < bw)
						{
							bool flag5 = p >= srcEnd;
							if (flag5)
							{
								throw new InvalidDataException("vdelta: bitstream underrun");
							}
							acc |= (ulong)src[p++] << accBits;
							accBits += 8;
						}
						z = (uint)(acc & (1UL << bw) - 1UL);
						acc >>= bw;
						accBits -= bw;
					}
					short d = (short)(z >> 1 ^ -(z & 1U));
					ushort pred = (y == 0) ? ((x == 0) ? 0 : MirageArchiveFormat.LoadR16(r16, i - 1)) : MirageArchiveFormat.LoadR16(r16, i - width);
					ushort v = pred + (ushort)d;
					r16[2 * i] = (byte)v;
					r16[2 * i + 1] = (byte)(v >> 8);
					bool flag6 = ++x == width;
					if (flag6)
					{
						x = 0;
						y++;
					}
				}
			}
		}

		/// <summary>Validate a vdelta header against expected dimensions; returns block size.</summary>
		// Token: 0x06000177 RID: 375 RVA: 0x0000BAB4 File Offset: 0x00009CB4
		private static int ReadVDeltaHeader(byte[] src, int srcOffset, int srcLen, int width, int height)
		{
			bool flag = srcLen < 5;
			if (flag)
			{
				throw new InvalidDataException("vdelta: truncated header");
			}
			int w = (int)src[srcOffset] | (int)src[srcOffset + 1] << 8;
			int h = (int)src[srcOffset + 2] | (int)src[srcOffset + 3] << 8;
			int blockLog = (int)src[srcOffset + 4];
			bool flag2 = w != width || h != height;
			if (flag2)
			{
				throw new InvalidDataException(string.Format("vdelta: payload dims {0}x{1} != expected {2}x{3}", new object[]
				{
					w,
					h,
					width,
					height
				}));
			}
			bool flag3 = blockLog < 1 || blockLog > 16;
			if (flag3)
			{
				throw new InvalidDataException(string.Format("vdelta: bad blockLog {0}", blockLog));
			}
			return 1 << blockLog;
		}

		// Token: 0x06000178 RID: 376 RVA: 0x0000BB79 File Offset: 0x00009D79
		private static ushort LoadR16(byte[] b, int i)
		{
			return (ushort)((int)b[2 * i] | (int)b[2 * i + 1] << 8);
		}

		// Token: 0x06000179 RID: 377 RVA: 0x0000BB8C File Offset: 0x00009D8C
		private static int BitsFor(ushort v)
		{
			int i = 0;
			while (v > 0)
			{
				i++;
				v = (ushort)(v >> 1);
			}
			return i;
		}

		/// <summary>Pick the codec for a web-archive tile and return the bytes to store.</summary>
		// Token: 0x0600017A RID: 378 RVA: 0x0000BBB8 File Offset: 0x00009DB8
		public static byte[] EncodeForWeb(byte[] raw, int format, int width, int height, out TileCodec codec)
		{
			bool flag = format == 9;
			if (flag)
			{
				byte[] packed = MirageArchiveFormat.VDeltaBitpackEncode(raw, width, height);
				bool flag2 = packed.Length < raw.Length;
				if (flag2)
				{
					codec = TileCodec.HeightVDeltaBitpack;
					return packed;
				}
			}
			codec = TileCodec.None;
			return raw;
		}

		/// <summary>Decode a stored tile payload into raw texture bytes.</summary>
		// Token: 0x0600017B RID: 379 RVA: 0x0000BBF8 File Offset: 0x00009DF8
		public static void DecodeTilePayload(TileCodec codec, byte[] stored, int storedLen, byte[] raw, int rawLen)
		{
			switch (codec)
			{
			case TileCodec.None:
			{
				bool flag = storedLen != rawLen;
				if (flag)
				{
					throw new InvalidDataException(string.Format("stored {0} raw bytes, expected {1}", storedLen, rawLen));
				}
				Array.Copy(stored, 0, raw, 0, rawLen);
				return;
			}
			case TileCodec.Lz4:
				MirageArchiveFormat.DecodeLz4Exact(stored, storedLen, raw, rawLen);
				return;
			case TileCodec.HeightPlaneSplitLz4:
			{
				byte[] planed = new byte[rawLen];
				MirageArchiveFormat.DecodeLz4Exact(stored, storedLen, planed, rawLen);
				MirageArchiveFormat.PlaneUnsplitR16(planed, raw);
				return;
			}
			case TileCodec.HeightVDeltaBitpack:
				MirageArchiveFormat.DecodeVDelta(stored, storedLen, raw, rawLen);
				return;
			}
			throw new InvalidDataException(string.Format("unknown tile codec {0}", codec));
		}

		// Token: 0x0600017C RID: 380 RVA: 0x0000BCAC File Offset: 0x00009EAC
		private static void DecodeLz4Exact(byte[] stored, int storedLen, byte[] dst, int rawLen)
		{
			int i = MirageArchiveFormat.Lz4DecompressBlock(stored, 0, storedLen, dst, rawLen);
			bool flag = i != rawLen;
			if (flag)
			{
				throw new InvalidDataException(string.Format("LZ4 decode produced {0} bytes, expected {1}", i, rawLen));
			}
		}

		// Token: 0x0600017D RID: 381 RVA: 0x0000BCEC File Offset: 0x00009EEC
		private static void DecodeVDelta(byte[] stored, int storedLen, byte[] raw, int rawLen)
		{
			bool flag = storedLen < 5;
			if (flag)
			{
				throw new InvalidDataException("vdelta: truncated header");
			}
			int w = (int)stored[0] | (int)stored[1] << 8;
			int h = (int)stored[2] | (int)stored[3] << 8;
			bool flag2 = w * h * 2 != rawLen;
			if (flag2)
			{
				throw new InvalidDataException(string.Format("vdelta: payload dims {0}x{1} imply {2} raw bytes, expected {3}", new object[]
				{
					w,
					h,
					w * h * 2,
					rawLen
				}));
			}
			MirageArchiveFormat.VDeltaBitpackDecode(stored, 0, storedLen, raw, w, h);
		}

		/// <summary>Decompress one LZ4 block-format buffer, returning decoded byte count.</summary>
		// Token: 0x0600017E RID: 382 RVA: 0x0000BD7C File Offset: 0x00009F7C
		public static int Lz4DecompressBlock(byte[] src, int srcOffset, int srcLen, byte[] dst, int dstCap)
		{
			int s = srcOffset;
			int sEnd = srcOffset + srcLen;
			int d = 0;
			while (s < sEnd)
			{
				int token = (int)src[s++];
				int litLen = token >> 4;
				bool flag = litLen == 15;
				if (flag)
				{
					litLen += MirageArchiveFormat.ReadExtendedLength(src, ref s, sEnd);
				}
				bool flag2 = d + litLen > dstCap || s + litLen > sEnd;
				if (flag2)
				{
					throw new InvalidDataException("LZ4: corrupt literal run");
				}
				for (int i = 0; i < litLen; i++)
				{
					dst[d++] = src[s++];
				}
				bool flag3 = s >= sEnd;
				if (flag3)
				{
					break;
				}
				bool flag4 = s + 2 > sEnd;
				if (flag4)
				{
					throw new InvalidDataException("LZ4: truncated match offset");
				}
				int offset = (int)src[s] | (int)src[s + 1] << 8;
				s += 2;
				bool flag5 = offset == 0 || offset > d;
				if (flag5)
				{
					throw new InvalidDataException("LZ4: bad match offset");
				}
				int matchLen = token & 15;
				bool flag6 = matchLen == 15;
				if (flag6)
				{
					matchLen += MirageArchiveFormat.ReadExtendedLength(src, ref s, sEnd);
				}
				matchLen += 4;
				bool flag7 = d + matchLen > dstCap;
				if (flag7)
				{
					throw new InvalidDataException("LZ4: match overruns output");
				}
				int j = d - offset;
				for (int k = 0; k < matchLen; k++)
				{
					dst[d++] = dst[j++];
				}
			}
			return d;
		}

		// Token: 0x0600017F RID: 383 RVA: 0x0000BEE0 File Offset: 0x0000A0E0
		private static int ReadExtendedLength(byte[] src, ref int s, int sEnd)
		{
			int extra = 0;
			for (;;)
			{
				bool flag = s >= sEnd;
				if (flag)
				{
					break;
				}
				int num = s;
				s = num + 1;
				int b = (int)src[num];
				extra += b;
				if (b != 255)
				{
					return extra;
				}
			}
			throw new InvalidDataException("LZ4: truncated extended length");
		}

		// Token: 0x06000180 RID: 384 RVA: 0x0000BF2E File Offset: 0x0000A12E
		internal static void WriteMagic(BinaryWriter w, uint magic)
		{
			w.Write(magic);
		}

		// Token: 0x06000181 RID: 385 RVA: 0x0000BF38 File Offset: 0x0000A138
		internal static void ExpectMagic(BinaryReader r, uint expected, string what)
		{
			uint got = r.ReadUInt32();
			bool flag = got != expected;
			if (flag)
			{
				throw new InvalidDataException(string.Format("Mirage archive: bad {0} magic 0x{1:X8} (expected 0x{2:X8})", what, got, expected));
			}
		}

		// Token: 0x0400012C RID: 300
		public const ushort FormatVersion = 1;

		/// <summary>Number of <see cref="T:Mirage.VirtualTexture.ArchiveLayer" /> values — the length of a per-layer
		/// array.</summary>
		// Token: 0x0400012D RID: 301
		public const int LayerCount = 4;

		// Token: 0x0400012E RID: 302
		public const uint BlobMagic = 826365005U;

		// Token: 0x0400012F RID: 303
		public const uint IndexMagic = 826889293U;

		/// <summary>Tile starts are aligned to this many bytes inside a blob.</summary>
		// Token: 0x04000130 RID: 304
		public const int TileAlignment = 16;

		/// <summary>On-disk size of a framed <see cref="T:Mirage.VirtualTexture.TileHeader" />, padded to TileAlignment.</summary>
		// Token: 0x04000131 RID: 305
		public const int TileHeaderSize = 24;

		/// <summary>On-disk size of one <see cref="T:Mirage.VirtualTexture.IndexEntry" />.</summary>
		// Token: 0x04000132 RID: 306
		public const int IndexEntrySize = 22;

		/// <summary>Bits per coordinate — also the deepest addressable level.</summary>
		// Token: 0x04000133 RID: 307
		public const int MaxCoordBits = 17;

		// Token: 0x04000134 RID: 308
		private static readonly uint[] s_Crc32Table = MirageArchiveFormat.BuildCrc32Table();

		// Token: 0x04000135 RID: 309
		private const int VDeltaBlockLog = 6;

		// Token: 0x04000136 RID: 310
		private const int VDeltaHeaderBytes = 5;
	}
}
