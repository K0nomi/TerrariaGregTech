#nullable enable
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Items;

// Fallen star but better
public sealed class NetherStarAmmoGlobalItem : GlobalItem
{
	private const float DamageMultiplier = 32f;

	private static int _netherStarType = -1;

	private static int NetherStarType()
	{
		if (_netherStarType != -1) return _netherStarType;
		var mod = ModContent.GetInstance<GregTechCEuTerraria>();
		_netherStarType = mod.TryFind<ModItem>("nether_star_gem", out var mi) ? mi.Type : ItemID.None;
		return _netherStarType;
	}

	public override void SetDefaults(Item item)
	{
		if (item.type != NetherStarType() || item.type == ItemID.None) return;
		item.ammo = AmmoID.FallenStar;
		item.consumable = true;
	}

	public override void PickAmmo(Item weapon, Item ammo, Player player, ref int type, ref float speed,
		ref StatModifier damage, ref float knockback)
	{
		if (ammo.type != NetherStarType() || ammo.type == ItemID.None) return;
		damage.Flat *= DamageMultiplier;
	}
}
