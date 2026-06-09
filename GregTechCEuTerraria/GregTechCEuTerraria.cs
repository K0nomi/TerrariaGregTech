#nullable enable
using System.IO;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.Common.Materials;
using GregTechCEuTerraria.Api.Data.Chemical.Material;
using GregTechCEuTerraria.TerrariaCompat.Net;
using GregTechCEuTerraria.TerrariaCompat.Recipes;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Api.Recipe.Ingredient;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.TerrariaCompat.Worldgen;
using GregTechCEuTerraria.TerrariaCompat.Items;
using GregTechCEuTerraria.TerrariaCompat.Items.Cables;
using GregTechCEuTerraria.TerrariaCompat.Items.Machines;
using GregTechCEuTerraria.TerrariaCompat.Tiles;
using GregTechCEuTerraria.TerrariaCompat.Tiles.Machines;
using System;
using System.Reflection;
using Terraria.ModLoader;

namespace GregTechCEuTerraria;

public sealed class GregTechCEuTerraria : Mod
{
	private static readonly Action<string>? _setSubProgress = ResolveSubProgressSetter();

	private static Action<string>? ResolveSubProgressSetter()
	{
		try
		{
			var asm = typeof(Mod).Assembly;
			var interfaceType = asm.GetType("Terraria.ModLoader.UI.Interface");
			var field = interfaceType?.GetField("loadMods", BindingFlags.NonPublic | BindingFlags.Static);
			var loadMods = field?.GetValue(null);
			if (loadMods == null) return null;
			var prop = loadMods.GetType().GetProperty("SubProgressText", BindingFlags.Public | BindingFlags.Instance);
			if (prop == null) return null;
			return text => prop.SetValue(loadMods, text);
		}
		catch { return null; }
	}

	private void Stage(string text)
	{
		_setSubProgress?.Invoke("GregTechCEuTerraria: " + text);
		Logger.Info(text);
	}

	public override void Load()
	{
		Api.TickScale.SimulationSpeedProvider = () => Config.GTConfig.Instance?.SimulationSpeed ?? 1.0f;

		Stage("Loading materials");
		TerrariaCompat.Materials.MaterialJsonLoader.Load(this);
		Stage("Parsing registry dump");
		TerrariaCompat.Items.Registry.RegistryDump.Load(this);
		Stage("Registering fluids");
		TerrariaCompat.Loaders.FluidLoader.RegisterAll(this);
		Stage("Registering fluid buckets");
		TerrariaCompat.Items.Fluids.FluidBucketRegistry.Register(this);
		Stage("Registering material block tiles");
		TerrariaCompat.Tiles.MaterialBlockTileRegistry.Register(this);
		Stage("Registering material items");
		MaterialItemRegistry.Register(this);
		Stage("Registering ore tiles");
		OreTileRegistry.Register(this);
		Stage("Loading ore veins");
		VeinJsonLoader.Load(this);
		Stage("Registering wires & cables");
		WireItemRegistry.Register(this);
		Stage("Registering pipes");
		TerrariaCompat.Items.Pipes.PipeItemRegistry.Register(this);
		TerrariaCompat.Items.Pipes.SimplePipeRegistry.Register(this);
		Stage("Registering batteries");
		TerrariaCompat.Items.Batteries.BatteryItemLoader.Register(this);
		Stage("Registering item magnets");
		TerrariaCompat.Items.Magnets.MagnetItemLoader.Register(this);
		Stage("Registering GregTech tools");
		TerrariaCompat.Items.Tools.ToolItemLoader.Register(this);
		Stage("Registering Gregith weapons");
		TerrariaCompat.Items.Tools.GregithItemLoader.Register(this);
		Stage("Registering power armor");
		TerrariaCompat.Items.Armor.ArmorItemLoader.Register(this);
		Stage("Registering inert components from dump");
		TerrariaCompat.Items.Registry.RegistryItemLoader.Load(this);
		Stage("Registering item tags");
		TerrariaCompat.Items.Registry.RegistryTagLoader.Load(this);
		Stage("Registering covers");
		TerrariaCompat.Cover.GTCovers.Register();
		TerrariaCompat.Items.Covers.CoverItemLoader.Register(this);
		TerrariaCompat.Cover.GTCovers.RegisterFilterItems();
		Stage("Registering fluid cells");
		TerrariaCompat.Items.Fluids.FluidCellRegistry.Register(this);
		Stage("Building machine definitions");
		MachineDefinitions.RegisterAll();
		TerrariaCompat.Pipelike.LongDistance.LongDistanceLocale.RegisterAll();
		TerrariaCompat.Machine.Multiblock.MultiblockLocale.RegisterAll();
		Stage("Registering machine tiles & items");
		TieredMachineFactory.RegisterAll(this);
		Stage("Registering casing blocks");
		TerrariaCompat.Tiles.Casings.CasingRegistry.Register(this);
		Stage("Registering turbine rotors");
		TerrariaCompat.Items.TurbineRotorItemLoader.Register(this);
		TerrariaCompat.Pipelike.PipeIntersection.InstallHook();

		var resolver = TerrariaCompat.Recipes.IngredientResolverImpl.Instance;
		IIngredientResolver.Default = resolver;

		Stage("Loading recipes (~32k)");
		RecipeJsonLoader.Load(this, resolver);
		Stage("Synthesising biome world-I/O recipes");
		TerrariaCompat.Machine.Multiblock.Electric.BiomeWorldIORecipeSynth.Register(this);
		Stage("Verifying recipe coverage");
		TerrariaCompat.Recipes.RecipeCoverageCheck.Verify(this);
		Stage("Registering multiblock bags");
		TerrariaCompat.BossDrops.MultiblockBag.MultiblockBagLoader.Register(this);
		Stage("Resolving boss drops");
		TerrariaCompat.BossDrops.BossDropRegistry.Resolve(this);

		Stage("Ready");
	}

	// Multiplayer packet dispatch
	public override void HandlePacket(BinaryReader reader, int whoAmI)
	{
		NetRouter.Handle(reader, whoAmI);
	}

	public override void Unload()
	{
		RuntimeTextureRegistry.DisposeAll();

		TerrariaCompat.Pipelike.PipeIntersection.UninstallHook();
		IIngredientResolver.Default = null;

		TerrariaCompat.BossDrops.BossDropRegistry.Unload();
		TerrariaCompat.BossDrops.MultiblockBag.MultiblockBagLoader.Unload();
		RecipeRegistry.Clear();
		TerrariaCompat.Items.Tools.ToolItemLoader.Unload();
		TerrariaCompat.Items.Tools.GregithItemLoader.Unload();
		TerrariaCompat.Items.Armor.ArmorItemLoader.Unload();
		TerrariaCompat.Items.TurbineRotorItemLoader.Unload();
		TerrariaCompat.Items.Registry.RegistryItemLoader.Unload();
		TerrariaCompat.Items.Registry.RegistryTagLoader.Unload();
		TerrariaCompat.Items.Covers.CoverItemLoader.Unload();
		TerrariaCompat.Items.Registry.TagMembership.Clear();
		Api.Cover.Filter.FilterItemRegistry.Clear();
		Api.Cover.CoverRegistry.Clear();
		TerrariaCompat.Items.Registry.RegistryDump.Unload();
		WireItemRegistry.Unload();
		TerrariaCompat.Items.Pipes.PipeItemRegistry.Unload();
		VeinRegistry.Clear();
		OreTileRegistry.Unload();
		TerrariaCompat.Tiles.MaterialBlockTileRegistry.Unload();
		MaterialItemRegistry.Unload();
		MaterialRegistry.Clear();
		MachineRegistry.Clear();
	}
}
