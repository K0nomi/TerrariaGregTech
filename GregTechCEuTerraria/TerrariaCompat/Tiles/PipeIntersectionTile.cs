#nullable enable
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace GregTechCEuTerraria.TerrariaCompat.Tiles;

public sealed class PipeIntersectionTile : ModTile
{
	public override string Name => "pipe_intersection";
	public override string Texture => "GregTechCEuTerraria/Content/TerrariaCompat/PipeIntersectionTile";

	public override void SetStaticDefaults()
	{
		Pipelike.PipeIntersection.TileType = Type;

		Main.tileFrameImportant[Type] = true;
		Main.tileSolid[Type]          = false;
		Main.tileSolidTop[Type]       = true;
		Main.tileNoAttach[Type]       = false;
		Main.tileBlockLight[Type]     = false;
		Main.tileLavaDeath[Type]      = false;

		TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
		TileObjectData.newTile.LavaDeath    = false;
		TileObjectData.newTile.AnchorBottom = default(AnchorData);
		TileObjectData.addTile(Type);

		AddMapEntry(new Color(255, 150, 40),
			Language.GetOrRegister($"Mods.GregTechCEuTerraria.Tiles.{Name}.MapEntry",
				() => "Pipe Intersection"));

		DustType = DustID.Copper;
		HitSound = SoundID.Tink;
		MineResist = 1f;
		MinPick = 0;
	}

	public override void PlaceInWorld(int i, int j, Item item)
		=> Pipelike.PipeIntersection.OnPlaced(i, j, Main.LocalPlayer);

	public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
	{
		if (fail || effectOnly) return;
		Pipelike.PipeIntersection.OnRemoved(i, j);
	}
}
