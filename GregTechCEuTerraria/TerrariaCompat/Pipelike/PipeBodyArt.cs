#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike;

public static class PipeBodyArt
{
	private const int Src = 16;

	private static readonly Dictionary<string, Texture2D?> _texCache = new();

	public static Texture2D? Tex(string modPath)
	{
		if (_texCache.TryGetValue(modPath, out var hit)) return hit;
		Texture2D? tex = null;
		if (!Main.dedServ && ModContent.HasAsset(modPath))
			tex = ModContent.Request<Texture2D>(modPath, AssetRequestMode.ImmediateLoad).Value;
		_texCache[modPath] = tex;
		return tex;
	}

	public static void DrawCell(SpriteBatch sb, Texture2D tex, Vector2 cellPos,
		int mask, int thickness, Color tint, float scale = 1f)
	{
		Span<StripRect> strips = stackalloc StripRect[2];
		int count = Strips(mask, thickness, scale, strips);
		for (int i = 0; i < count; i++)
		{
			var r = strips[i];
			var src  = new Rectangle(r.Sx, r.Sy, r.W, r.H);
			var dest = cellPos + new Vector2(r.Sx, r.Sy) * scale + r.Extra;
			sb.Draw(tex, dest, src, tint, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}
	}

	public static void DrawCellStretch(SpriteBatch sb, Texture2D tex, Rectangle frame,
		Vector2 cellPos, int mask, int thickness, Color tint, float scale = 1f)
	{
		if (frame.Width <= 0 || frame.Height <= 0) return;
		Span<StripRect> strips = stackalloc StripRect[2];
		int count = Strips(mask, thickness, scale, strips);
		for (int i = 0; i < count; i++)
		{
			var r = strips[i];
			var dest = cellPos + new Vector2(r.Sx, r.Sy) * scale + r.Extra;
			var sv   = new Vector2((float)r.W * scale / frame.Width, (float)r.H * scale / frame.Height);
			sb.Draw(tex, dest, frame, tint, 0f, Vector2.Zero, sv, SpriteEffects.None, 0f);
		}
	}

	private struct StripRect { public int Sx, Sy, W, H; public Vector2 Extra; }

	private static int Strips(int mask, int thickness, float scale, Span<StripRect> outp)
	{
		int n = thickness;
		int lo = 8 - n / 2;
		int hi = lo + n;
		bool horiz = (mask & 12) != 0;
		bool vert  = (mask & 3)  != 0;
		float half = (n & 1) != 0 ? -0.5f * scale : 0f;
		int c = 0;
		if (!horiz && !vert)
		{
			outp[c++] = new StripRect { Sx = lo, Sy = lo, W = n, H = n, Extra = new Vector2(half, half) };
			return c;
		}
		if (horiz)
		{
			int cL = (mask & 4) != 0 ? 0   : lo;
			int cR = (mask & 8) != 0 ? Src : hi;
			outp[c++] = new StripRect { Sx = cL, Sy = lo, W = cR - cL, H = n, Extra = new Vector2(0, half) };
		}
		if (vert)
		{
			int rT = (mask & 1) != 0 ? 0   : lo;
			int rB = (mask & 2) != 0 ? Src : hi;
			outp[c++] = new StripRect { Sx = lo, Sy = rT, W = n, H = rB - rT, Extra = new Vector2(half, 0) };
		}
		return c;
	}
}
