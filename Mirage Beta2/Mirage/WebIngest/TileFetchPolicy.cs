using System;

namespace Mirage.WebIngest
{
	/// <summary>
	/// The fetcher's DECISION logic, split out from <see cref="T:Mirage.WebIngest.WebTileFetcher" />'s transport.
	///
	/// <b>This exists because the fetcher was the one component with no gate, and it produced two separate
	/// in-game failures — both here, in the decisions, not in the networking.</b> First it treated any PNG as
	/// EOX's no-coverage sentinel, which declined every tile of a PNG DEM as permanently uncoverable. Then, while
	/// fixing that, the JPEG-only frame-kind bookkeeping got hoisted onto the success path's condition
	/// (<c>if (!failed &amp;&amp; Format == Jpeg)</c>), so every PNG fetch fell through to the retry branch and was
	/// reported as a permanent failure — on an HTTP 200 with a perfectly good 25 KB body.
	///
	/// Coroutines and <c>UnityWebRequest</c> cannot be linked into the offline packer; a pure function over
	/// (expected format, content type, bytes) can. So the transport stays untestable and the part that was
	/// actually wrong twice does not.
	/// </summary>
	// Token: 0x02000029 RID: 41
	public static class TileFetchPolicy
	{
		/// <summary>
		/// Classify a successfully-transported response.
		///
		/// <paramref name="expected" /> is what the endpoint serves when it HAS data, and it is load-bearing: a
		/// PNG means "no coverage" from a <c>.jpg</c> imagery endpoint and "here is your elevation" from
		/// Terrarium. The same bytes, opposite meanings — which is why the caller's expectation, not the body
		/// alone, decides.
		/// </summary>
		// Token: 0x060000F0 RID: 240 RVA: 0x0000907C File Offset: 0x0000727C
		public static FetchVerdict Classify(TileImageFormat expected, string contentType, byte[] body, int length, out string error, out JpegInfo info)
		{
			error = null;
			info = default(JpegInfo);
			bool flag = !JpegProbe.ContentTypeLooksLikeImage(contentType);
			FetchVerdict result;
			if (flag)
			{
				error = "content validation failed (Content-Type='" + contentType + "')";
				result = FetchVerdict.Reject;
			}
			else
			{
				bool flag2 = body == null || length <= 0;
				if (flag2)
				{
					error = "content validation failed (empty body)";
					result = FetchVerdict.Reject;
				}
				else
				{
					bool flag3 = !JpegProbe.ContainerMatches(expected, body, length);
					if (flag3)
					{
						bool flag4 = expected == TileImageFormat.Jpeg && JpegProbe.IsPng(body, length);
						if (flag4)
						{
							result = FetchVerdict.NoCoverage;
						}
						else
						{
							error = string.Format("content validation failed (body is not {0})", expected);
							result = FetchVerdict.Reject;
						}
					}
					else
					{
						bool flag5 = expected == TileImageFormat.Png;
						if (flag5)
						{
							bool flag6 = !JpegProbe.IsPlausiblePng(body, length);
							if (flag6)
							{
								error = "content validation failed (not a plausible PNG body)";
								result = FetchVerdict.Reject;
							}
							else
							{
								result = FetchVerdict.Success;
							}
						}
						else
						{
							info = JpegProbe.Probe(body, length);
							bool flag7 = !info.Valid;
							if (flag7)
							{
								error = "content validation failed (" + info.Error + ")";
								result = FetchVerdict.Reject;
							}
							else
							{
								result = FetchVerdict.Success;
							}
						}
					}
				}
			}
			return result;
		}
	}
}
