#nullable enable
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.BossDrops;

// Adds tier-appropriate GregTech drops onto vanilla bosses
public sealed class BossDropGlobalNPC : GlobalNPC
{
	private static readonly BossDropCondition Condition = new();
	private static readonly BossDropLastPartCondition LastPartCondition = new();
	private const int BagDropChance = 100;

	public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
	{
		IItemDropRuleCondition condition = BossDropRegistry.IsMultiPart((short)npc.type) ? LastPartCondition : Condition;

		if (BossDropRegistry.TryGet((short)npc.type, out var drops))
		{
			foreach (var d in drops)
				npcLoot.Add(new ItemDropWithConditionRule(d.ItemType, chanceDenominator: 1, amountDroppedMinimum: d.Min, amountDroppedMaximum: d.Max, condition));
		}

		if (BossDropRegistry.TryGetBags((short)npc.type, out var bags))
		{
			foreach (var bagItemType in bags)
				npcLoot.Add(new ItemDropWithConditionRule(bagItemType, chanceDenominator: BagDropChance, amountDroppedMinimum: 1, amountDroppedMaximum: 1, condition));
		}
	}
}
