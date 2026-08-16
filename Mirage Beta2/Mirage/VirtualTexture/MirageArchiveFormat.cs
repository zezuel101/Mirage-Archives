using System;
using System.IO;

namespace Mirage.VirtualTexture
{
	/// <summary>The frozen container constants + shared helpers (Morton key packing,
	/// alignment, CRC32).</summary>
	// Token: 0x02000044 RID: 68
	public static class MirageArchiveFormat
	{
		// Token: 0x0600019B RID: 411 RVA: 0x0000C414 File Offset: 0x0000A614
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

		// Token: 0x0600019C RID: 412 RVA: 0x0000C4A8 File Offset: 0x0000A6A8
		public static void UnpackKey(ulong key, out int face, out int level, out int x, out int y)
		{
			face = (int)(key >> 60 & 7UL);
			level = (int)(key >> 51 & 511UL);
			ulong interleaved = key & 17179869183UL;
			x = (int)MirageArchiveFormat.Compact1By1(interleaved);
			y = (int)MirageArchiveFormat.Compact1By1(interleaved >> 1);
		}

		// Token: 0x0600019D RID: 413 RVA: 0x0000C4ED File Offset: 0x0000A6ED
		public static int KeyFace(ulong key)
		{
			return (int)(key >> 60 & 7UL);
		}

		// Token: 0x0600019E RID: 414 RVA: 0x0000C4F7 File Offset: 0x0000A6F7
		public static int KeyLevel(ulong key)
		{
			return (int)(key >> 51 & 511UL);
		}

		// Token: 0x0600019F RID: 415 RVA: 0x0000C508 File Offset: 0x0000A708
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

		// Token: 0x060001A0 RID: 416 RVA: 0x0000C578 File Offset: 0x0000A778
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

		// Token: 0x060001A1 RID: 417 RVA: 0x0000C5E8 File Offset: 0x0000A7E8
		public static long AlignUp(long value, int alignment)
		{
			long i = value % (long)alignment;
			return (i == 0L) ? value : (value + ((long)alignment - i));
		}

		// Token: 0x060001A2 RID: 418 RVA: 0x0000C60C File Offset: 0x0000A80C
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

		// Token: 0x060001A3 RID: 419 RVA: 0x0000C670 File Offset: 0x0000A870
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

		// Token: 0x060001A4 RID: 420 RVA: 0x0000C6B3 File Offset: 0x0000A8B3
		public static uint Crc32(byte[] data)
		{
			return MirageArchiveFormat.Crc32(data, 0, data.Length);
		}

		/// <summary>Raw (decoded) payload size in bytes for a tile of the given format + dimensions. Used to
		/// size the decode target; the raw length is never stored (it's implied by format + tile dims).</summary>
		// Token: 0x060001A5 RID: 421 RVA: 0x0000C6C0 File Offset: 0x0000A8C0
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

		// Token: 0x060001A6 RID: 422 RVA: 0x0000C74E File Offset: 0x0000A94E
		private static int BlockCount(int width, int height)
		{
			return (width + 3) / 4 * ((height + 3) / 4);
		}

		/// <summary>Should this layer's raw payload be plane-split before LZ4 (height R16 only)?</summary>
		// Token: 0x060001A7 RID: 423 RVA: 0x0000C75B File Offset: 0x0000A95B
		public static bool UsePlaneSplit(int format)
		{
			return format == 9;
		}

		/// <summary>Deinterleave an R16 buffer (lo,hi,lo,hi,…) into [all lo bytes][all hi bytes]. Same length.</summary>
		// Token: 0x060001A8 RID: 424 RVA: 0x0000C764 File Offset: 0x0000A964
		public static byte[] PlaneSplitR16(byte[] r16)
		{
			int i = r16.Length / 2;
			byte[] outp = new byte[r16.Length];
			for (int j = 0; j < i; j++)
			{
				outp[j] = r16[2 * j];
				outp[i + j] = r16[2 * j + 1];
			}
			return outp;
		}

		/// <summary>Inverse of <see cref="M:Mirage.VirtualTexture.MirageArchiveFormat.PlaneSplitR16(System.Byte[])" />: reinterleave planes back to R16 into <paramref name="r16" />.</summary>
		// Token: 0x060001A9 RID: 425 RVA: 0x0000C7B0 File Offset: 0x0000A9B0
		public static void PlaneUnsplitR16(byte[] planed, byte[] r16)
		{
			int i = r16.Length / 2;
			for (int j = 0; j < i; j++)
			{
				r16[2 * j] = planed[j];
				r16[2 * j + 1] = planed[i + j];
			}
		}

		// Token: 0x060001AA RID: 426 RVA: 0x0000C7EA File Offset: 0x0000A9EA
		private static ushort LoadR16(byte[] b, int i)
		{
			return (ushort)((int)b[2 * i] | (int)b[2 * i + 1] << 8);
		}

		// Token: 0x060001AB RID: 427 RVA: 0x0000C7FC File Offset: 0x0000A9FC
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

		/// <summary>Encode a raw little-endian R16 buffer with vertical-delta + zigzag + per-block bitpacking.</summary>
		// Token: 0x060001AC RID: 428 RVA: 0x0000C828 File Offset: 0x0000AA28
		public static byte[] VDeltaBitpackEncode(byte[] r16, int width, int height)
		{
			int count = width * height;
			bool flag = r16.Length < count * 2;
			if (flag)
			{
				throw new ArgumentException("vdelta: source shorter than width*height*2");
			}
			ushort[] zz = new ushort[count];
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
			int blockSize = 64;
			int nblocks = (count + blockSize - 1) / blockSize;
			byte[] widths = new byte[nblocks];
			long bits = 0L;
			for (int b = 0; b < nblocks; b++)
			{
				int s = b * blockSize;
				int e = Math.Min(s + blockSize, count);
				ushort max = 0;
				for (int j = s; j < e; j++)
				{
					bool flag2 = zz[j] > max;
					if (flag2)
					{
						max = zz[j];
					}
				}
				widths[b] = (byte)MirageArchiveFormat.BitsFor(max);
				bits += (long)((ulong)widths[b] * (ulong)((long)(e - s)));
			}
			byte[] outp = new byte[5 + nblocks + (int)((bits + 7L) / 8L)];
			outp[0] = (byte)width;
			outp[1] = (byte)(width >> 8);
			outp[2] = (byte)height;
			outp[3] = (byte)(height >> 8);
			outp[4] = 6;
			Buffer.BlockCopy(widths, 0, outp, 5, nblocks);
			int p = 5 + nblocks;
			ulong acc = 0UL;
			int accBits = 0;
			for (int b2 = 0; b2 < nblocks; b2++)
			{
				int w = (int)widths[b2];
				bool flag3 = w == 0;
				if (!flag3)
				{
					int s2 = b2 * blockSize;
					int e2 = Math.Min(s2 + blockSize, count);
					for (int k = s2; k < e2; k++)
					{
						acc |= (ulong)zz[k] << accBits;
						for (accBits += w; accBits >= 8; accBits -= 8)
						{
							outp[p++] = (byte)acc;
							acc >>= 8;
						}
					}
				}
			}
			bool flag4 = accBits > 0;
			if (flag4)
			{
				outp[p++] = (byte)acc;
			}
			return outp;
		}

		/// <summary>Inverse of <see cref="M:Mirage.VirtualTexture.MirageArchiveFormat.VDeltaBitpackEncode(System.Byte[],System.Int32,System.Int32)" />: unpack into a raw little-endian R16 buffer.
		/// Single pass — each texel's predictor is read back from the part of <paramref name="r16" /> already
		/// written (the row above, or the texel to the left on row 0), so no scratch buffer is needed.</summary>
		// Token: 0x060001AD RID: 429 RVA: 0x0000CA78 File Offset: 0x0000AC78
		public static void VDeltaBitpackDecode(byte[] src, int srcOffset, int srcLen, byte[] r16, int width, int height)
		{
			bool flag = srcLen < 5;
			if (flag)
			{
				throw new InvalidDataException("vdelta: truncated header");
			}
			int w0 = (int)src[srcOffset] | (int)src[srcOffset + 1] << 8;
			int h0 = (int)src[srcOffset + 2] | (int)src[srcOffset + 3] << 8;
			int blockLog = (int)src[srcOffset + 4];
			bool flag2 = w0 != width || h0 != height;
			if (flag2)
			{
				throw new InvalidDataException(string.Format("vdelta: payload dims {0}x{1} != expected {2}x{3}", new object[]
				{
					w0,
					h0,
					width,
					height
				}));
			}
			bool flag3 = blockLog < 1 || blockLog > 16;
			if (flag3)
			{
				throw new InvalidDataException(string.Format("vdelta: bad blockLog {0}", blockLog));
			}
			int count = width * height;
			bool flag4 = r16.Length < count * 2;
			if (flag4)
			{
				throw new ArgumentException("vdelta: destination shorter than width*height*2");
			}
			int blockSize = 1 << blockLog;
			int nblocks = (count + blockSize - 1) / blockSize;
			int wp = srcOffset + 5;
			bool flag5 = srcLen < 5 + nblocks;
			if (flag5)
			{
				throw new InvalidDataException("vdelta: truncated block-width table");
			}
			int p = wp + nblocks;
			int sEnd = srcOffset + srcLen;
			ulong acc = 0UL;
			int accBits = 0;
			for (int b = 0; b < nblocks; b++)
			{
				int bw = (int)src[wp + b];
				bool flag6 = bw > 16;
				if (flag6)
				{
					throw new InvalidDataException(string.Format("vdelta: bad block width {0}", bw));
				}
				int s = b * blockSize;
				int e = Math.Min(s + blockSize, count);
				int x = s % width;
				int y = s / width;
				for (int i = s; i < e; i++)
				{
					uint z = 0U;
					bool flag7 = bw != 0;
					if (flag7)
					{
						while (accBits < bw)
						{
							bool flag8 = p >= sEnd;
							if (flag8)
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
					bool flag9 = ++x == width;
					if (flag9)
					{
						x = 0;
						y++;
					}
				}
			}
		}

		/// <summary>
		/// Pick the codec for a tile being baked into a <b>web</b> archive at runtime and return the bytes to
		/// store. Unlike the offline packer — which links K4os and can afford to try every codec and keep the
		/// smallest — the runtime ships no third-party compressor, so the choice here is between the codecs whose
		/// ENCODER is in this file. That is exactly one: vdelta-bitpack, which is pure managed C# with no
		/// dependency, and is both smaller and faster to decode than LZ4 on R16 anyway. Everything else (BC7
		/// color, BC5 normals) stores raw: LZ4 is the only thing that helps BCn and we cannot encode it here, and
		/// BCn is already block-compressed, so raw costs nothing but disk.
		/// </summary>
		// Token: 0x060001AE RID: 430 RVA: 0x0000CD08 File Offset: 0x0000AF08
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

		/// <summary>Decode a stored tile payload (as read from the blob) into its raw texture bytes. The raw
		/// length is derived from the format + tile dims by the caller (not stored). Throws on a malformed stream.</summary>
		// Token: 0x060001AF RID: 431 RVA: 0x0000CD48 File Offset: 0x0000AF48
		public static void DecodeTilePayload(TileCodec codec, byte[] stored, int storedLen, byte[] raw, int rawLen)
		{
			switch (codec)
			{
			case TileCodec.None:
				Array.Copy(stored, 0, raw, 0, rawLen);
				return;
			case TileCodec.Lz4:
			{
				int i = MirageArchiveFormat.Lz4DecompressBlock(stored, 0, storedLen, raw, rawLen);
				bool flag = i != rawLen;
				if (flag)
				{
					throw new InvalidDataException(string.Format("LZ4 decode produced {0} bytes, expected {1}", i, rawLen));
				}
				return;
			}
			case TileCodec.HeightPlaneSplitLz4:
			{
				byte[] planed = new byte[rawLen];
				int j = MirageArchiveFormat.Lz4DecompressBlock(stored, 0, storedLen, planed, rawLen);
				bool flag2 = j != rawLen;
				if (flag2)
				{
					throw new InvalidDataException(string.Format("LZ4 decode produced {0} bytes, expected {1}", j, rawLen));
				}
				MirageArchiveFormat.PlaneUnsplitR16(planed, raw);
				return;
			}
			case TileCodec.HeightVDeltaBitpack:
			{
				bool flag3 = storedLen < 5;
				if (flag3)
				{
					throw new InvalidDataException("vdelta: truncated header");
				}
				int w = (int)stored[0] | (int)stored[1] << 8;
				int h = (int)stored[2] | (int)stored[3] << 8;
				bool flag4 = w * h * 2 != rawLen;
				if (flag4)
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
				return;
			}
			}
			throw new InvalidDataException(string.Format("unknown tile codec {0}", codec));
		}

		/// <summary>
		/// Decompress one LZ4 <b>block</b> (not frame) format buffer. Standard LZ4 sequences: a token byte
		/// (high nibble = literal length, low nibble = match length−4), optional extended-length bytes (0xFF
		/// continues), the literals, then a 2-byte little-endian back-offset and the match copy (byte-wise to
		/// honour overlap). Interoperates with K4os <c>LZ4Codec.Encode</c> used by the packer. Returns the number
		/// of decoded bytes.
		/// </summary>
		// Token: 0x060001B0 RID: 432 RVA: 0x0000CEC4 File Offset: 0x0000B0C4
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
					int b;
					do
					{
						b = (int)src[s++];
						litLen += b;
					}
					while (b == 255);
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
				int offset = (int)src[s] | (int)src[s + 1] << 8;
				s += 2;
				bool flag4 = offset == 0 || offset > d;
				if (flag4)
				{
					throw new InvalidDataException("LZ4: bad match offset");
				}
				int matchLen = token & 15;
				bool flag5 = matchLen == 15;
				if (flag5)
				{
					int b2;
					do
					{
						b2 = (int)src[s++];
						matchLen += b2;
					}
					while (b2 == 255);
				}
				matchLen += 4;
				bool flag6 = d + matchLen > dstCap;
				if (flag6)
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

		// Token: 0x060001B1 RID: 433 RVA: 0x0000D03A File Offset: 0x0000B23A
		internal static void WriteMagic(BinaryWriter w, uint magic)
		{
			w.Write(magic);
		}

		// Token: 0x060001B2 RID: 434 RVA: 0x0000D044 File Offset: 0x0000B244
		internal static void ExpectMagic(BinaryReader r, uint expected, string what)
		{
			uint got = r.ReadUInt32();
			bool flag = got != expected;
			if (flag)
			{
				throw new InvalidDataException(string.Format("Mirage archive: bad {0} magic 0x{1:X8} (expected 0x{2:X8})", what, got, expected));
			}
		}

		// Token: 0x0400016D RID: 365
		public const ushort FormatVersion = 1;

		// Token: 0x0400016E RID: 366
		public const uint BlobMagic = 826365005U;

		// Token: 0x0400016F RID: 367
		public const uint IndexMagic = 826889293U;

		/// <summary>Tile starts are aligned to this many bytes inside a blob: BC blocks
		/// are 16 B, so 16-B alignment keeps CopyTexture/mmap happy and lets a payload
		/// go to the GPU without a realigning copy. Gap is ≤15 B/tile (alignment, not
		/// addressing — see design §3).</summary>
		// Token: 0x04000170 RID: 368
		public const int TileAlignment = 16;

		/// <summary>On-disk size of a framed <see cref="T:Mirage.VirtualTexture.TileHeader" /> (fields padded to a
		/// 16-B multiple so the payload that follows is aligned too).</summary>
		// Token: 0x04000171 RID: 369
		public const int TileHeaderSize = 24;

		/// <summary>On-disk size of one <see cref="T:Mirage.VirtualTexture.IndexEntry" />.</summary>
		// Token: 0x04000172 RID: 370
		public const int IndexEntrySize = 22;

		// Token: 0x04000173 RID: 371
		public const int MaxCoordBits = 17;

		// Token: 0x04000174 RID: 372
		private static readonly uint[] s_Crc32Table = MirageArchiveFormat.BuildCrc32Table();

		// Token: 0x04000175 RID: 373
		private const int VDeltaBlockLog = 6;

		// Token: 0x04000176 RID: 374
		private const int VDeltaHeaderBytes = 5;
	}
}
