#nullable enable
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Machine.Rendering;
using GregTechCEuTerraria.TerrariaCompat.UI;
using GregTechCEuTerraria.TerrariaCompat.UI.Layouts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Tiles.Machines;

public class CrateTile : TieredMachineTile
{
	public CrateTile() { }
	public CrateTile(VoltageTier tier, MachineDefinition def) : base(tier, def) { }

	protected override Color MapColor     => new(170, 140, 95);
	protected override int   MineDustType => DustID.WoodFurniture;

	public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
	{
		CrateRenderer.EnsureTileTexture(Type, _def?.MaterialId);
		return true;
	}

	public override void WarmUpTexture() =>
		CrateRenderer.EnsureTileTexture(Type, _def?.MaterialId);

	public override bool RightClick(int i, int j)
	{
		if (!MachineCellResolver.TryFindAt<CrateMachine>(i, j, out var crate)) return false;

		// Tape mechanic temporarily disabled to avoid dupes
		MachineUISystem.OpenFor(crate, CrateLayout.Build(crate));
		return true;
	}
}
