#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Cover;
using GregTechCEuTerraria.Api.Machine;
using GregTechCEuTerraria.TerrariaCompat.Capabilities;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Machine.Rendering;
using GregTechCEuTerraria.TerrariaCompat.Net;
using GregTechCEuTerraria.TerrariaCompat.Pipelike;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Pipes;

// throughput rate 0.25
public sealed class SimpleItemPipeItem : ModItem, ITextureWarmUp
{
	private readonly PipeSize? _size;

	public SimpleItemPipeItem() { }
	public SimpleItemPipeItem(PipeSize size) { _size = size; }

	public override bool IsLoadingEnabled(Mod mod) => _size != null;

	private PipeSize Size     => _size ?? PipeSize.Normal;
	private string   SizeWord => PipeSizes.Word(Size);

	public override string Name => Size == PipeSize.Normal
		? "simple_item_pipe"
		: $"simple_item_pipe_{SizeWord}";

	public override string Texture => $"GregTechCEuTerraria/Content/Textures/block/pipe/pipe_{SizeWord}_in";
	protected override bool CloneNewInstances => true;

	public override void SetStaticDefaults()
	{
		string label = Size == PipeSize.Normal
			? "Simple Item Pipe"
			: $"{Capitalize(SizeWord)} Simple Item Pipe";
		Language.GetOrRegister(
			$"Mods.GregTechCEuTerraria.Items.{Name}.DisplayName",
			() => label);
	}

	public override void SetDefaults()
	{
		Item.maxStack = 999;
		Item.width = 32; Item.height = 32;
		Item.useTime = 8; Item.useAnimation = 8;
		Item.useStyle = ItemUseStyleID.Swing;
		Item.autoReuse = true;
		Item.consumable = false;
		Item.rare = ItemRarityID.White;
		Item.UseSound = SoundID.Item50;
	}

	public override void ModifyTooltips(List<TooltipLine> tooltips)
	{
		base.ModifyTooltips(tooltips);
		tooltips.Add(new TooltipLine(Mod, "PipeKind", $"{Capitalize(SizeWord)} Simple Item Pipe"));

		float rate = 0.25f * Pipelike.ItemPipe.ItemPipeSizeModifier.For(Size, false).RateMultiplier;
		string rateLine = (rate % 1 != 0f)
			? $"[c/55FFFF:Transfer Rate:] {(int)((rate * 64) + 0.5f)} items/s"
			: $"[c/55FFFF:Transfer Rate:] {(int)rate} stacks/s";
		tooltips.Add(new TooltipLine(Mod, "PipeRate", rateLine));
		tooltips.Add(new TooltipLine(Mod, "PipeSimple", "[c/AAFFAA:Auto-connects to adjacent storage on placement.]"));
		tooltips.Add(new TooltipLine(Mod, "PipeSimpleUI", "[c/AAFFAA:Right-click to toggle per-side mode (Off / Insert / Extract).]"));
	}

	public override bool? UseItem(Player player)
	{
		if (Main.myPlayer != player.whoAmI) return null;
		int x = (int)(Main.MouseWorld.X / 16);
		int y = (int)(Main.MouseWorld.Y / 16);
		if (x <= 0 || x >= Main.maxTilesX - 1 || y <= 0 || y >= Main.maxTilesY - 1) return false;
		if (Item.stack <= 0) return false;

		var mod      = Pipelike.ItemPipe.ItemPipeSizeModifier.For(Size, restrictive: false);
		int priority = (int)((1 * mod.ResistanceMultiplier) + 0.5f);
		float rate   = 0.25f * mod.RateMultiplier;

		var cell = new Pipelike.ItemPipe.ItemPipeCell(
			MaterialId:   "simple_item",
			Size:         Size,
			Restrictive:  false,
			Priority:     priority,
			TransferRate: rate,
			IsSimple:     true);

		if (!Pipelike.ItemPipe.ItemPipeLayerHandle.Instance.TryPlace(cell, x, y, player))
			return false;

		AutoInsertOnAdjacentStorage(Pipelike.PipeKind.Item, x, y);

		Item.stack--;
		return true;
	}

	internal static void AutoInsertOnAdjacentStorage(Pipelike.PipeKind layer, int x, int y)
	{
		foreach (var side in CoverSides.All)
		{
			var (dx, dy) = side switch
			{
				CoverSide.Up    => (0, -1),
				CoverSide.Down  => (0,  1),
				CoverSide.Left  => (-1, 0),
				CoverSide.Right => ( 1, 0),
				_               => (0, 0),
			};
			var arrival = WorldCapability.ToIODirection(side).Opposite();
			bool hasStorage = layer == Pipelike.PipeKind.Item
				? WorldCapability.ItemHandlerAt (x + dx, y + dy, arrival) != null
				: WorldCapability.FluidHandlerAt(x + dx, y + dy, arrival) != null;
			if (!hasStorage) continue;
			SimplePipeSideSetPacket.Send(layer, x, y, side, SimpleSideMode.Insert);
		}
	}

	void ITextureWarmUp.WarmUpTexture() => ItemIconBaker.Install(Item.type, Texture);

	private int _removeCooldown;

	public override void HoldItem(Player player)
	{
		((ITextureWarmUp)this).WarmUpTexture();
		PipeHeldItemBehavior.Tick(player, Pipelike.PipeKind.Item, "Simple Item Pipe",
			Pipelike.ItemPipe.ItemPipeLayerHandle.Instance,
			ref _removeCooldown, Item.useTime);
	}

	private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
