#nullable enable
using GregTechCEuTerraria.TerrariaCompat.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Items;

public sealed class PipeIntersectionItem : ModItem
{
	public override string Name => "pipe_intersection";
	public override string Texture => "GregTechCEuTerraria/Content/TerrariaCompat/PipeIntersectionItem";

	public override void SetStaticDefaults()
		=> Language.GetOrRegister($"Mods.GregTechCEuTerraria.Items.{Name}.DisplayName",
			() => "Pipe Intersection");

	public override void SetDefaults()
	{
		Item.DefaultToPlaceableTile(ModContent.TileType<PipeIntersectionTile>());
		Item.width    = 24;
		Item.height   = 24;
		Item.maxStack = 999;
		Item.rare     = ItemRarityID.Green;
		Item.value    = Item.buyPrice(silver: 1);
	}

	public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
	{
		tooltips.Add(new TooltipLine(Mod, "Crossover", "Lets cable / item / fluid pipes cross without connecting"));
		tooltips.Add(new TooltipLine(Mod, "CrossoverNote", "[c/A0A0A0:Up<->Down and Left<->Right pass through independently]"));
	}

	public override void AddRecipes()
	{
		CreateRecipe()
			.AddIngredient(ItemID.StoneBlock, 4)
			.Register();
	}
}
