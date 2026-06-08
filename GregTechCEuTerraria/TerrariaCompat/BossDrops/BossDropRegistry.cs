#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.TerrariaCompat.Items;
using GregTechCEuTerraria.TerrariaCompat.Items.Registry;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.BossDrops;

// Tier-keyed boss-drop table
public static class BossDropRegistry
{
	public readonly record struct Drop(int ItemType, int Min, int Max);
	public readonly record struct TierSpec(string Name, string[] Materials, string[]? Components);

	// Quantities - LV-baseline ranges, scaled by TierMultiplier[tierIdx]
	private const int RawHullMin     =  16, RawHullMax     = 256;
	private const int RawCableMin    =  16, RawCableMax    = 128;
	private const int DustHullMin    =  16, DustHullMax    = 64;
	private const int DustCableMin   =  16, DustCableMax   = 64;
	private const int ComponentMin   =   1, ComponentMax   = 8;
	private const int StackCeiling   = 9999;

	// 0=Steam unused - King Slime is hand-authored, 1=LV..8=UV)
	private static readonly double[] TierMultiplier =
		{ 1.0, 1.0, 1.5, 2.0, 2.5, 3.0, 4.0, 6.0, 10.0 };

	private static readonly TierSpec[] Tiers =
	{
		new("Steam", new[] { "bronze", "invar" },                                              null),
		new("LV",    new[] { "steel", "tin", "copper" },                                       new[] { "vacuum_tube", "resistor", "ender_pearl_gem" }),
		new("MV",    new[] { "aluminium", "copper", "nickel" },                                new[] { "diode", "transistor", "capacitor" }),
		new("HV",    new[] { "stainless_steel", "silver", "electrum" },                        new[] { "smd_diode", "smd_resistor", "smd_transistor" }),
		new("EV",    new[] { "titanium", "aluminium", "kanthal" },                             new[] { "smd_capacitor", "smd_inductor" }),
		new("IV",    new[] { "tungsten_steel", "tungsten", "graphene" },                       new[] { "advanced_smd_diode", "advanced_smd_resistor", "ram_wafer" }),
		new("LuV",   new[] { "hsss", "niobium_titanium", "ruridit" },                          new[] { "advanced_smd_transistor", "advanced_smd_capacitor", "nor_memory_chip" }),
		new("ZPM",   new[] { "osmiridium", "vanadium_gallium", "europium" },                   new[] { "advanced_smd_inductor", "nand_memory_chip", "cpu_wafer" }),
		new("UV",    new[] { "tritanium", "yttrium_barium_cuprate", "americium" },             new[] { "nano_cpu_wafer", "hpic_wafer" }),
	};

	private static readonly (string item, int min, int max)[] KingSlimeOverride =
	{
		("bronze_dust",  64, 128),
		("invar_dust",   16, 64),
		("copper_dust",  64, 256),
		("tin_dust",     64, 256)
	};

	private static readonly Dictionary<short, (string item, int min, int max)[]> BossExtraDrops = new()
	{
		{ NPCID.WallofFlesh, new[] { ("nether_star_gem", 1, 1) } },
	};

	private static readonly (short Npc, int TierIdx, bool Components)[] BossTable =
	{
		// Steam
		(NPCID.KingSlime,         0, false),

		// LV
		(NPCID.EyeofCthulhu,      1, true),
		(NPCID.EaterofWorldsHead, 1, true),
		(NPCID.EaterofWorldsBody, 1, true),
		(NPCID.EaterofWorldsTail, 1, true),
		(NPCID.BrainofCthulhu,    1, true),
		(NPCID.Deerclops,         1, false),

		// MV
		(NPCID.QueenBee,          2, false),
		(NPCID.SkeletronHead,     2, true),
		(NPCID.WallofFlesh,       2, true),

		// HV
		(NPCID.PirateShip,        3, false),
		(NPCID.TheDestroyer,      3, true),
		(NPCID.Retinazer,         3, true),
		(NPCID.Spazmatism,        3, true),
		(NPCID.SkeletronPrime,    3, true),

		// EV
		(NPCID.QueenSlimeBoss,    4, false),
		(NPCID.Plantera,          4, true),

		// IV
		(NPCID.MourningWood,      5, false),
		(NPCID.Everscream,        5, false),
		(NPCID.Pumpking,          5, true),
		(NPCID.SantaNK1,          5, true),
		(NPCID.IceQueen,          5, true),

		// LuV
		(NPCID.Golem,             6, false),
		(NPCID.MartianSaucerCore, 6, true),

		// ZPM
		(NPCID.DukeFishron,       7, false),
		(NPCID.CultistBoss,       7, true),

		// UV
		(NPCID.LunarTowerSolar,    8, false),
		(NPCID.LunarTowerVortex,   8, false),
		(NPCID.LunarTowerNebula,   8, false),
		(NPCID.LunarTowerStardust, 8, false),
		(NPCID.HallowBoss,         8, true), // Empress of Light
		(NPCID.MoonLordCore,       8, true),
	};

	private static readonly HashSet<short> MultiPartBosses = new()
	{
		NPCID.EaterofWorldsHead, NPCID.EaterofWorldsBody, NPCID.EaterofWorldsTail,
		NPCID.Retinazer, NPCID.Spazmatism,
	};

	public static bool IsMultiPart(short npcType) => MultiPartBosses.Contains(npcType);


	private static readonly Dictionary<short, List<Drop>> _resolved = new();
	private static readonly Dictionary<short, List<int>> _bagsByBoss = new();

	private static List<Drop>[]? _tierResolved;
	private static List<Drop>?[]? _tierComponents;

	public static bool TryGet(short npcType, out List<Drop> drops) => _resolved.TryGetValue(npcType, out drops!);

	public static List<Drop> GetTierDrops(int tierIdx, bool withComponents)
	{
		var list = new List<Drop>();
		if (_tierResolved is null || tierIdx < 0 || tierIdx >= _tierResolved.Length) return list;
		list.AddRange(_tierResolved[tierIdx]);
		if (withComponents && _tierComponents is not null && _tierComponents[tierIdx] is { } comps)
			list.AddRange(comps);
		return list;
	}

	public static bool TryGetBags(short npcType, out List<int> bagItemTypes) =>
		_bagsByBoss.TryGetValue(npcType, out bagItemTypes!);

	public static IEnumerable<short> BossesForTier(int tierIdx)
	{
		foreach (var (npc, idx, _) in BossTable)
			if (idx == tierIdx) yield return npc;
	}

	public static void Resolve(Mod mod)
	{
		_resolved.Clear();
		int totalDrops = 0, missing = 0;

		Language.GetOrRegister("Mods.GregTechCEuTerraria.BossDrops.ConditionDescription", () => "Requires boss drops enabled in config.");
		Language.GetOrRegister("Mods.GregTechCEuTerraria.Configs.GTConfig.DisplayName", () => "GregTech");
		Language.GetOrRegister("Mods.GregTechCEuTerraria.Configs.GTConfig.EnableBossDrops.Label", () => "Enable GregTech boss drops");
		Language.GetOrRegister("Mods.GregTechCEuTerraria.Configs.GTConfig.EnableBossDrops.Tooltip", () => "If enabled, vanilla bosses drop tier-appropriate GregTech raw ores, dusts, and circuit components.");

		var tierResolved = new List<Drop>[Tiers.Length];
		var tierComponents = new List<Drop>?[Tiers.Length];
		for (int i = 0; i < Tiers.Length; i++)
		{
			var t = Tiers[i];
			double mult = TierMultiplier[i];
			var mats = new List<Drop>();
			for (int j = 0; j < t.Materials.Length; j++)
			{
				bool isHull = j == 0;
				if (TryResolveMaterial(t.Materials[j], out int itemType, out bool isRaw))
				{
					int baseMin = isRaw ? (isHull ? RawHullMin  : RawCableMin)
					                    : (isHull ? DustHullMin : DustCableMin);
					int baseMax = isRaw ? (isHull ? RawHullMax  : RawCableMax)
					                    : (isHull ? DustHullMax : DustCableMax);
					mats.Add(new Drop(itemType, Scale(baseMin, mult), Scale(baseMax, mult)));
				}
				else
					{ mod.Logger.Warn($"[BossDrops] Tier {t.Name}: no item for material '{t.Materials[j]}' (tried raw_{t.Materials[j]} + {t.Materials[j]}_dust)"); missing++; }
			}
			tierResolved[i] = mats;

			if (t.Components is not null)
			{
				var comps = new List<Drop>();
				foreach (var c in t.Components)
				{
					if (TryResolveBareId(c, out int itemType))
						comps.Add(new Drop(itemType, Scale(ComponentMin, mult), Scale(ComponentMax, mult)));
					else
						{ mod.Logger.Warn($"[BossDrops] Tier {t.Name}: component '{c}' not found in registry dump"); missing++; }
				}
				tierComponents[i] = comps;
			}
		}

		_tierResolved = tierResolved;
		_tierComponents = tierComponents;

		foreach (var (npc, tierIdx, withComponents) in BossTable)
		{
			if (npc == NPCID.KingSlime) continue;
			var list = new List<Drop>(tierResolved[tierIdx]);
			if (withComponents && tierComponents[tierIdx] is { } comps)
				list.AddRange(comps);
			_resolved[npc] = list;
			totalDrops += list.Count;
		}

		var ks = new List<Drop>();
		foreach (var (id, min, max) in KingSlimeOverride)
		{
			if (TryResolveBareId(id, out int t)) ks.Add(new Drop(t, min, max));
			else { mod.Logger.Warn($"[BossDrops] King Slime override: '{id}' not found"); missing++; }
		}
		_resolved[NPCID.KingSlime] = ks;
		totalDrops += ks.Count;

		foreach (var (npc, extras) in BossExtraDrops)
		{
			if (!_resolved.TryGetValue(npc, out var list))
				_resolved[npc] = list = new List<Drop>();
			foreach (var (id, min, max) in extras)
			{
				if (TryResolveBareId(id, out int t)) { list.Add(new Drop(t, min, max)); totalDrops++; }
				else { mod.Logger.Warn($"[BossDrops] Extra drop for NPC {npc}: '{id}' not found"); missing++; }
			}
		}

		mod.Logger.Info($"[BossDrops] Resolved {_resolved.Count} bosses, {totalDrops} drop entries" +
			(missing > 0 ? $" ({missing} unresolved - logged above)" : ""));

		ResolveMultiblockBags(mod);
	}

	private static void ResolveMultiblockBags(Mod mod)
	{
		_bagsByBoss.Clear();
		int totalLinks = 0;
		foreach (var kv in MultiblockBag.MultiblockBagLoader.All)
		{
			int tierIdx = MultiblockBag.MultiblockBagTierMap.GetTier(kv.Key);
			foreach (var npc in BossesForTier(tierIdx))
			{
				if (!_bagsByBoss.TryGetValue(npc, out var list))
					_bagsByBoss[npc] = list = new List<int>();
				list.Add(kv.Value);
				totalLinks++;
			}
		}
		int bagCount = System.Linq.Enumerable.Count(MultiblockBag.MultiblockBagLoader.All);
		mod.Logger.Info($"[BossDrops] Linked {bagCount} multiblock bags to bosses ({totalLinks} (bag, boss) links).");
	}

	public static void Unload()
	{
		_resolved.Clear();
		_bagsByBoss.Clear();
		_tierResolved = null;
		_tierComponents = null;
	}

	private static int Scale(int baseValue, double mult)
	{
		int v = (int)System.Math.Round(baseValue * mult);
		return v > StackCeiling ? StackCeiling : (v < 1 ? 1 : v);
	}

	private static bool TryResolveMaterial(string material, out int itemType, out bool isRaw)
	{
		if (MaterialItemRegistry.TryGetByUpstreamId("raw_" + material, out itemType)) { isRaw = true;  return true; }
		if (MaterialItemRegistry.TryGetByUpstreamId(material + "_dust", out itemType)) { isRaw = false; return true; }
		itemType = 0;
		isRaw = false;
		return false;
	}

	private static bool TryResolveBareId(string bareId, out int itemType)
	{
		if (MaterialItemRegistry.TryGetByUpstreamId(bareId, out itemType)) return true;
		if (RegistryItemLoader.TryGet("gtceu:" + bareId, out itemType)) return true;
		itemType = 0;
		return false;
	}
}
