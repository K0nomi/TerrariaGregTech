#nullable enable
using GregTechCEuTerraria.TerrariaCompat.Pipelike;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Pipes;

public static class SimplePipeRegistry
{
	private static readonly PipeSize[] ItemSizes =
		{ PipeSize.Small, PipeSize.Normal, PipeSize.Large, PipeSize.Huge };

	private static readonly PipeSize[] FluidSizes =
		{ PipeSize.Tiny, PipeSize.Small, PipeSize.Normal, PipeSize.Large, PipeSize.Huge };

	public static void Register(Mod mod)
	{
		foreach (var s in ItemSizes)
			mod.AddContent(new SimpleItemPipeItem(s));
		foreach (var s in FluidSizes)
			mod.AddContent(new SimpleFluidPipeItem(s));
	}
}
