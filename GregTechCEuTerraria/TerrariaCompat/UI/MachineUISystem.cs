#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace GregTechCEuTerraria.TerrariaCompat.UI;

// Tied to Main.playerInventory so the inventory panel shows alongside
public sealed class MachineUISystem : ModSystem
{
	private UserInterface? _ui;
	private MachineUIState? _state;

	private const string LayerName = "GregTechCEuTerraria: Machine UI";

	public override void Load()
	{
		if (Main.dedServ) return;
		_state = new MachineUIState();
		_ui = new UserInterface();
		UILayers.RegisterModal(LayerName, () => IsOpen);
	}

	public override void Unload()
	{
		_state = null;
		_ui = null;
	}

	public static void OpenFor(MetaMachine entity, MachineUILayout layout)
	{
		var sys = ModContent.GetInstance<MachineUISystem>();
		if (sys?._ui is null || sys._state is null) return;
		ModUIRegistry.OnOpen(Close);
		CloseVanillaChest();
		sys._state.Bind(entity, layout);
		sys._ui.SetState(sys._state);
		Main.playerInventory = true;
		SoundEngine.PlaySound(SoundID.MenuOpen);
		if (Main.netMode == NetmodeID.MultiplayerClient)
			TerrariaCompat.Net.MachineViewPacket.SendBegin(entity.Position);
	}

	public static void Close()
	{
		var sys = ModContent.GetInstance<MachineUISystem>();
		if (sys?._ui is null || sys._state is null) return;
		if (sys._ui.CurrentState == null) return;
		var entity = sys._state.Entity;
		sys._state.Unbind();
		sys._ui.SetState(null);
		ModUIRegistry.OnClose(Close);
		Widgets.UISearchBar.UnfocusAll();
		Widgets.UITextField.UnfocusAll();
		SoundEngine.PlaySound(SoundID.MenuClose);
		if (Main.netMode == NetmodeID.MultiplayerClient && entity != null)
			TerrariaCompat.Net.MachineViewPacket.SendEnd(entity.Position);
	}

	private static void CloseVanillaChest()
	{
		var plr = Main.LocalPlayer;
		if (plr.chest == -1) return;
		plr.chest = -1;
		Main.recBigList = false;
		Terraria.Recipe.FindRecipes();
		SoundEngine.PlaySound(SoundID.MenuClose);
		if (Main.netMode == NetmodeID.MultiplayerClient)
			NetMessage.SendData(MessageID.SyncPlayerChestIndex, -1, -1, null, Main.myPlayer, -1f);
	}

	public static bool IsOpen
	{
		get
		{
			var sys = ModContent.GetInstance<MachineUISystem>();
			return sys?._ui?.CurrentState != null;
		}
	}

	public static bool IsOccludedByHigherModal => UILayers.IsAnyHigherPriorityModalOpen(LayerName);

	public static MetaMachine? CurrentEntity
	{
		get
		{
			var sys = ModContent.GetInstance<MachineUISystem>();
			return sys?._state?.Entity;
		}
	}

	public override void UpdateUI(GameTime gameTime)
	{
		if (_ui is null) return;

		if (IsOpen && _state != null) ModalEscape.SuppressVanillaUIClicks(_state);

		if (_ui.CurrentState != null)
		{
			if (!Main.playerInventory) { Close(); return; }
			if (Main.LocalPlayer.chest != -1) { Close(); return; }

			var bound = _state?.Entity;
			if (bound != null)
			{
				if (!TileEntity.ByID.ContainsKey(bound.ID))
				{
					Close();
					return;
				}

				bool inReach = false;
				foreach (var (cx, cy) in bound.Cells())
				{
					if (Main.LocalPlayer.IsInTileInteractionRange(cx, cy, TileReachCheckSettings.Simple))
					{
						inReach = true;
						break;
					}
				}
				if (!inReach)
				{
					Close();
					return;
				}
			}
		}

		if (!UILayers.IsAnyHigherPriorityModalOpen(LayerName))
			_ui.Update(gameTime);
		ModalEscape.DbgMI_AfterUpdateUI = Main.LocalPlayer.mouseInterface;
	}

	public override void PostUpdateInput()
	{
		if (Main.dedServ || !IsOpen || _state is null) return;
		ModalEscape.SuppressItemUse(_state);
		ModalEscape.DbgMI_AfterPostUpdateInput = Main.LocalPlayer.mouseInterface;
	}

	public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
	{
		UILayers.InsertModal(layers,
			LayerName,
			() =>
			{
				if (_ui?.CurrentState != null)
				{
					_ui.Draw(Main.spriteBatch, new GameTime());
					ModalEscape.DebugDrawAreas(Main.spriteBatch, _ui.CurrentState, "MachineUI");
				}
				return true;
			});
	}
}
