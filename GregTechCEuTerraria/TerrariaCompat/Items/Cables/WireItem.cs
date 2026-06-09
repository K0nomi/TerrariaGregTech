#nullable enable
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.TerrariaCompat.Machine.Rendering;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Cable;
using GregTechCEuTerraria.Common.Materials;
using GregTechCEuTerraria.Api.Data.Chemical.Material;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Cables;

// Material-keyed placeable wire / cable. One instance per (Material, WireSize,
// Insulated) via WireItemRegistry. LMB places a CableCell, RMB removes/refunds.
public sealed class WireItem : ModItem, ITextureWarmUp
{
	private readonly string? _id;
	[CloneByReference] private readonly Material? _material;
	private readonly byte _wireSize;
	private readonly bool _insulated;
	private int _removeCooldown;

	public WireItem() { }
	public WireItem(string id, Material material, byte wireSize, bool insulated)
	{
		_id = id;
		_material = material;
		_wireSize = wireSize;
		_insulated = insulated;
	}

	public override string Name => _id ?? nameof(WireItem);

	public override string Texture => "GregTechCEuTerraria/Content/Textures/block/material_sets/dull/wire_end";
	protected override bool CloneNewInstances => true;
	public override bool IsLoadingEnabled(Mod mod) => _material != null;

	public override void SetDefaults()
	{
		Item.maxStack = Item.CommonMaxStack;
		Item.width = 32;
		Item.height = 32;
		Item.useTime = 2;
		Item.useAnimation = 6;
		Item.useStyle = ItemUseStyleID.Swing;
		Item.autoReuse = true;
		Item.consumable = false;  // manual stack management
		Item.rare = ItemRarityID.White;
		Item.UseSound = null;
	}

	public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
	{
		base.ModifyTooltips(tooltips);
		if (_material is null) return;
		var cell = BuildCell();
		long voltage = VoltageTiers.Voltage(cell.Voltage);
		string kind = cell.Insulated ? "Cable" : "Wire";
		string sizeWord = WireSizeWord(cell.WireSize);

		tooltips.Add(new TooltipLine(Mod, "WireTier",
			$"{VoltageTiers.ShortName(cell.Voltage)} - {voltage:N0} EU/t"));
		tooltips.Add(new TooltipLine(Mod, "WireAmp",
			$"{cell.TotalAmperage}A ({cell.BaseAmperage} x {cell.WireSize})"));
		tooltips.Add(new TooltipLine(Mod, "WireLoss",
			$"Loss: {cell.LossPerAmp} EU per amp per cable"));
		tooltips.Add(new TooltipLine(Mod, "WireKind",
			$"{sizeWord} {kind}{(cell.Insulated ? " (insulated)" : "")}"));
	}

	public override bool? UseItem(Player player)
	{
		if (_material is null) return null;
		if (Main.myPlayer != player.whoAmI) return null;

		int x = (int)(Main.MouseWorld.X / 16);
		int y = (int)(Main.MouseWorld.Y / 16);
		if (x <= 0 || x >= Main.maxTilesX - 1 || y <= 0 || y >= Main.maxTilesY - 1)
			return false;
		if (Item.stack <= 0) return false;

		if (!CableLayerHandle.Instance.TryPlace(BuildCell(), x, y, player))
			return false;
		Terraria.Audio.SoundEngine.PlaySound(SoundID.Item50, new Microsoft.Xna.Framework.Vector2(x * 16, y * 16));
		Item.stack--;
		return true;
	}

	public override void HoldItem(Player player)
	{
		EnsureTextureBaked();
		if (Main.myPlayer != player.whoAmI) return;
		if (_material is null) return;
		HandleHoverTooltip(player);
		HandleHeldRightClickRemove(player);
	}

	void ITextureWarmUp.WarmUpTexture() => EnsureTextureBaked();

	private void EnsureTextureBaked() =>
		ItemIconBaker.Install(Item.type,
			new IconLayer(Texture, Tint(), DotScaleForWireSize(_wireSize)));

	private static float DotScaleForWireSize(byte wireSize) => wireSize switch
	{
		1  => 0.35f,
		2  => 0.50f,
		4  => 0.65f,
		8  => 0.80f,
		16 => 1.00f,
		_  => 0.50f,
	};

	private void HandleHoverTooltip(Player player)
	{
		if (player.mouseInterface || Main.gameMenu) return;

		int x = (int)(Main.MouseWorld.X / 16);
		int y = (int)(Main.MouseWorld.Y / 16);
		if (x <= 0 || x >= Main.maxTilesX - 1 || y <= 0 || y >= Main.maxTilesY - 1) return;

		string heldKind = _insulated ? "Cable" : "Wire";
		string controlsLine = $"[c/A0E0FF:LMB]: Place {heldKind}   [c/FFA0A0:RMB]: Remove {heldKind}";

		var cell = CableLayerSystem.Cables.CellAt(x, y);
		if (cell is null)
		{
			TerrariaCompat.UI.WorldHoverTooltip.Set(controlsLine);
			return;
		}

		var net = TerrariaCompat.Pipelike.Cable.EnergyNetSystem.NetAt(x, y);

		string kind = cell.Value.Insulated ? "Cable" : "Wire";
		string sizeWord = WireSizeWord(cell.Value.WireSize);
		long voltage = VoltageTiers.Voltage(cell.Value.Voltage);
		string cellLine = $"{HumanizeMaterial(cell.Value.MaterialId)} {(cell.Value.WireSize > 1 ? sizeWord + " " : "")}{kind}";
		string electrical = $"{VoltageTiers.ShortName(cell.Value.Voltage)} {voltage:N0} EU/t  *  {cell.Value.TotalAmperage}A  *  loss {cell.Value.LossPerAmp}/A";

		string networkLine = net != null
			? $"Network: {net.Cells.Count} cables  *  effective {VoltageTiers.ShortName(net.EffectiveTier)} {net.EffectiveAmperage}A (cap {net.PerTickCapacity:N0} EU/t)"
			: "Network: not initialized";

		string endpointsLine = net != null
			? $"Endpoints: {net.Producers.Count} producers, {net.Consumers.Count} consumers"
			: "Endpoints: -";

		string throughputLine;
		if (net != null)
		{
			var (ex, de) = TerrariaCompat.Pipelike.Cable.EnergyNetSystem.GetThroughput(net);
			throughputLine = $"Throughput: {ex:N0} / {de:N0} EU/t  *  loss {(ex - de):N0}";
		}
		else
		{
			throughputLine = "Throughput: -";
		}

		string? highLossLine = null;
		if (net is not null)
		{
			float lossPct = net.GetCableLossPercent(x, y);
			if (lossPct >= 0.5f)
			{
				int pct = (int)(lossPct * 100);
				highLossLine =
					$"[c/FF5555:! High-loss path: ~{pct}% of source voltage already lost here]\n" +
					$"[c/FF8888:Energy delivered downstream of this cable is heavily reduced.]\n" +
					$"[c/FF8888:Shorten the cable run, use a higher-tier wire (lower loss), or insulate it.]";
			}
		}

		string lines = string.Join("\n", controlsLine, cellLine, electrical, networkLine, endpointsLine, throughputLine);
		if (highLossLine is not null) lines += "\n" + highLossLine;

		TerrariaCompat.UI.WorldHoverTooltip.Set(lines);
	}

	private static string HumanizeMaterial(string materialId)
	{
		string key = $"Mods.GregTechCEuTerraria.Materials.{materialId}";
		string text = Terraria.Localization.Language.GetTextValue(key);
		return text == key ? TitleCase(materialId) : text;
	}

	private static string TitleCase(string snake)
	{
		var sb = new System.Text.StringBuilder(snake.Length);
		bool capNext = true;
		foreach (char c in snake)
		{
			if (c == '_') { sb.Append(' '); capNext = true; continue; }
			sb.Append(capNext ? char.ToUpperInvariant(c) : c);
			capNext = false;
		}
		return sb.ToString();
	}

	private void HandleHeldRightClickRemove(Player player)
	{
		if (!Main.mouseRight) { _removeCooldown = 0; return; }
		if (_removeCooldown > 0) { _removeCooldown--; return; }

		int x = (int)(Main.MouseWorld.X / 16);
		int y = (int)(Main.MouseWorld.Y / 16);
		if (CutCableAt(player, x, y))
			_removeCooldown = Item.useTime;
	}

	public static bool CutCableAt(Player player, int x, int y) =>
		CableLayerHandle.Instance.CutAt(x, y, player);

	private CableCell BuildCell()
	{
		var mat = _material!;
		var tier = TryParseTier(mat.CableTier) ?? VoltageTier.ULV;
		int baseAmp = mat.CableAmperage ?? 1;
		return new CableCell(mat.Id, _wireSize, _insulated, tier, baseAmp, ComputeLoss(mat));
	}

	private int ComputeLoss(Material mat)
	{
		int lossMult = _insulated ? 1 : (_wireSize <= 2 ? 2 : 3);
		int baseLoss = mat.CableLoss ?? 0;
		bool superconductor = mat.CableIsSuperconductor ?? false;
		if (!superconductor && baseLoss == 0)
			return (int)(0.75 * lossMult);
		return baseLoss * lossMult;
	}

	private static VoltageTier? TryParseTier(string? name) =>
		System.Enum.TryParse<VoltageTier>(name, ignoreCase: false, out var t) ? t : null;

	private Color Tint()
	{
		uint c = _material?.Color ?? 0xFFFFFFu;
		var color = new Color((byte)((c >> 16) & 0xFF), (byte)((c >> 8) & 0xFF), (byte)(c & 0xFF));
		return _insulated ? CableRenderer.DarkenForInsulation(color) : color;
	}

	public static string WireSizeWord(byte wireSize) => wireSize switch
	{
		1  => "single",
		2  => "double",
		4  => "quadruple",
		8  => "octal",
		16 => "hex",
		_  => "single",
	};
}
