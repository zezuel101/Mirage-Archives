using System;
using System.Globalization;

namespace Mirage.Configuration
{
	/// <summary>Typed reads from a <see cref="T:ConfigNode" /> with fallback on absent/bad values.</summary>
	// Token: 0x0200007F RID: 127
	internal static class ConfigNodeExtensions
	{
		// Token: 0x06000399 RID: 921 RVA: 0x0001B5C0 File Offset: 0x000197C0
		public static int ParseInt(this ConfigNode node, string key, int fallback)
		{
			string value = node.GetValue(key);
			bool flag = string.IsNullOrEmpty(value);
			int result;
			if (flag)
			{
				result = fallback;
			}
			else
			{
				int parsed;
				result = (int.TryParse(value.Trim(), out parsed) ? parsed : fallback);
			}
			return result;
		}

		/// <summary>Invariant-culture float parse.</summary>
		// Token: 0x0600039A RID: 922 RVA: 0x0001B5FC File Offset: 0x000197FC
		public static float ParseFloat(this ConfigNode node, string key, float fallback)
		{
			string value = node.GetValue(key);
			bool flag = string.IsNullOrEmpty(value);
			float result;
			if (flag)
			{
				result = fallback;
			}
			else
			{
				float parsed;
				result = (float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback);
			}
			return result;
		}

		/// <summary>Accepts True/False (any case) and 1/0.</summary>
		// Token: 0x0600039B RID: 923 RVA: 0x0001B644 File Offset: 0x00019844
		public static bool ParseBool(this ConfigNode node, string key, bool fallback)
		{
			string value2 = node.GetValue(key);
			string value = (value2 != null) ? value2.Trim() : null;
			bool flag = string.IsNullOrEmpty(value);
			bool result;
			if (flag)
			{
				result = fallback;
			}
			else
			{
				bool parsed;
				bool flag2 = bool.TryParse(value, out parsed);
				if (flag2)
				{
					result = parsed;
				}
				else
				{
					bool flag3 = value == "1";
					if (flag3)
					{
						result = true;
					}
					else
					{
						bool flag4 = value == "0";
						result = (!flag4 && fallback);
					}
				}
			}
			return result;
		}

		// Token: 0x0600039C RID: 924 RVA: 0x0001B6B8 File Offset: 0x000198B8
		public static string ParseString(this ConfigNode node, string key, string fallback)
		{
			string value = node.GetValue(key);
			return string.IsNullOrEmpty(value) ? fallback : value.Trim();
		}
	}
}
