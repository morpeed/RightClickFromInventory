using MonoMod.Cil;
using RightClickFromInventory.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace RightClickFromInventory
{
	// Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
	public class RightClickFromInventory : Mod
	{
        public static HashSet<int> ItemHasAltFunctionUse { get; } =
        [
            ItemID.RubblemakerSmall,
            ItemID.RubblemakerMedium,
            ItemID.RubblemakerLarge,
            ItemID.EncumberingStone,
            ItemID.UncumberingStone,
            ItemID.ClosedVoidBag,
            ItemID.VoidLens,
            ItemID.DontHurtComboBook,
            ItemID.DontHurtComboBookInactive,
            ItemID.DontHurtCrittersBook,
            ItemID.DontHurtCrittersBookInactive,
            ItemID.DontHurtNatureBook,
            ItemID.DontHurtNatureBookInactive,
            ItemID.Shellphone,
            ItemID.ShellphoneOcean,
            ItemID.ShellphoneHell,
            ItemID.ShellphoneSpawn,
            ItemID.PortalGun,
            ItemID.MonkStaffT3,
            ItemID.DrumStick,
            ItemID.BookStaff,
            ItemID.BouncingShield,
            ItemID.DD2SquireDemonSword,
            ItemID.MulticolorWrench,
            ItemID.WireKite
        ];
        public HashSet<string> DisabledItems { get; } =
        [
            "Psychedelic_Prism/PsychedelicPrism"
        ];

        public static bool HasAltUse(Item item)
        {
            if (ItemHasAltFunctionUse.Contains(item.type) || ProjectileID.Sets.TurretFeature[item.shoot] || ProjectileID.Sets.MinionTargettingFeature[item.shoot])
            {
                return true;
            }
            return false;
        }

        public override void Load()
        {
            IL_Player.ItemCheck_ManageRightClickFeatures += (context) =>
            {
                var cursor = new ILCursor(context);

                if (cursor.TryGotoNext(MoveType.Before,
                    i => i.MatchLdarg(0),
                    i => i.MatchLdfld<Player>("selectedItem"),
                    i => i.MatchLdcI4(out _),
                    i => i.MatchBeq(out _)
                    ))
                {
                    cursor.RemoveRange(4);
                }

                if (cursor.TryGotoNext(MoveType.Before,
                    i => i.MatchLdarg(0),
                    i => i.MatchLdfld<Player>("inventory"),
                    i => i.MatchLdarg(0),
                    i => i.MatchLdfld<Player>("selectedItem"),
                    i => i.MatchLdelemRef(),
                    i => i.MatchLdloc(5),
                    i => i.MatchCallvirt<Item>("ChangeItemType")
                    ))
                {
                    cursor.EmitLdarg(0);
                    cursor.EmitLdloc(5);
                    cursor.EmitDelegate((Player player, int changeTo) =>
                    {
                        if (RightClickFromInventoryConfig.Instance.enabled && player.selectedItem == 58)
                        {
                            Main.mouseItem.ChangeItemType(changeTo);
                        }
                    });
                }

                var label = cursor.DefineLabel();
                if (cursor.TryGotoNext(MoveType.Before,
                    i => i.MatchLdloc(1),
                    i => i.MatchBrfalse(out label),

                    i => i.MatchLdarg(0),
                    i => i.MatchLdfld<Player>("altFunctionUse"),
                    i => i.MatchBrtrue(out _),

                    i => i.MatchLdarg(0),
                    i => i.MatchLdfld<Player>("inventory"),
                    i => i.MatchLdarg(0),
                    i => i.MatchLdfld<Player>("selectedItem"),
                    i => i.MatchLdelemRef(),
                    i => i.MatchLdarg(0),
                    i => i.MatchCall("Terraria.ModLoader.ItemLoader", "AltFunctionUse"),
                    i => i.MatchBrfalse(out _)
                    ))
                {
                    var firstOriginalLabel = cursor.DefineLabel();
                    cursor.EmitLdarg0();
                    var firstNewLabel = cursor.MarkLabel(cursor.Prev);
                    cursor.EmitDelegate((Player player) =>
                    {
                        return RightClickFromInventoryConfig.Instance.enabled && player.selectedItem == 58 && (player.HeldItem.ModItem == null || !DisabledItems.Contains(player.HeldItem.ModItem.FullName));
                    });
                    var ignore1 = cursor.EmitBrfalse(firstOriginalLabel).Prev; // Goto regular instructions 

                    cursor.EmitLdarg0();
                    cursor.EmitLdloc(1);
                    cursor.EmitDelegate((Player player, bool someFlag) =>
                    {
                        var item = player.inventory[player.selectedItem];
                        bool altFunctionUse = ItemLoader.AltFunctionUse(item, player);
                        if (altFunctionUse)
                        {
                            ItemHasAltFunctionUse.Add(item.type);
                        }
                        if (someFlag && player.altFunctionUse == 0 && altFunctionUse)
                        {
                            player.altFunctionUse = 1;
                            player.controlUseItem = true;
                        }
                    });

                    cursor.EmitBr(label); // Skip regular instructions

                    cursor.MarkLabel(firstOriginalLabel);
                    cursor.RedirectBranchOperands(firstOriginalLabel.Target, firstNewLabel.Target, ignore1);

                    cursor.UpdateInstructionOffsets();
                }

                //MonoModHooks.DumpIL(ModContent.GetInstance<RightClickFromInventory>(), context);
            };

            IL_Player.dropItemCheck += (context) =>
            {
                var cursor = new ILCursor(context);
                if (cursor.TryGotoNext(MoveType.Before,
                        i => i.MatchLdarg(0),
                        i => i.MatchCall<Player>("DropSelectedItem")
                        ))
                {
                    var branch = (ILLabel)cursor.Prev.Operand;

                    cursor.EmitLdarg(0);
                    cursor.EmitDelegate((Player player) =>
                    {
                        if (!ModContent.GetInstance<RightClickFromInventoryConfig>().enabled || player.controlThrow || (Main.mouseRight && !player.mouseInterface && Main.mouseRightRelease && player.controlTorch && Main.playerInventory))
                        {
                            return true;
                        }

                        return !HasAltUse(player.inventory[player.selectedItem]);
                    });
                    cursor.EmitBrfalse(branch);
                }
            };

            IL_WiresUI.WiresRadial.FlowerUpdate += (context) =>
            {
                var cursor = new ILCursor(context);
                if (cursor.TryGotoNext(MoveType.Before,
                    i => i.MatchLdsfld("Terraria.Main", "mouseItem"),
                    i => i.MatchLdfld("Terraria.Item", "type"),
                    i => i.MatchLdcI4(out _),
                    i => i.MatchBle(out _)
                    ))
                {
                    cursor.RemoveRange(3);
                    cursor.EmitDelegate(() =>
                    {
                        return RightClickFromInventoryConfig.Instance.enabled || Main.mouseItem.type <= ItemID.None;
                    });
                    cursor.Next.OpCode = Mono.Cecil.Cil.OpCodes.Brtrue;
                }
            };

            IL_WiresUI.WiresRadial.LineUpdate += (context) =>
            {
                var cursor = new ILCursor(context);
                if (cursor.TryGotoNext(MoveType.Before,
                    i => i.MatchLdsfld("Terraria.Main", "mouseItem"),
                    i => i.MatchLdfld("Terraria.Item", "type"),
                    i => i.MatchLdcI4(out _),
                    i => i.MatchBle(out _)
                    ))
                {
                    cursor.RemoveRange(3);
                    cursor.EmitDelegate(() =>
                    {
                        return RightClickFromInventoryConfig.Instance.enabled || Main.mouseItem.type <= ItemID.None;
                    });
                    cursor.Next.OpCode = Mono.Cecil.Cil.OpCodes.Brtrue;
                }
            };
        }
	}

    public class RightClickFromInventoryPlayer : ModPlayer
    {
        public override void PostItemCheck()
        {
            if (RightClickFromInventoryConfig.Instance.enabled && RightClickFromInventoryConfig.Instance.showHoldThrowText && Player.whoAmI == Main.myPlayer && Main.mouseItem != null)
            {
                if (Main.mouseRight && !Player.mouseInterface && Main.playerInventory && RightClickFromInventory.HasAltUse(Main.mouseItem))
                {
                    string input = "UNBOUND";
                    if (PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard].KeyStatus["SmartSelect"].Count > 0)
                    {
                        input = string.Join("/", PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard].KeyStatus["SmartSelect"]);
                    }
                    Main.instance.MouseText(Language.GetTextValue("Mods.RightClickFromInventory.HoldThrow", input));
                }
            }

            if (Main.LocalPlayer.HeldItem.ModItem != null)
            {
                Main.NewText(Main.LocalPlayer.HeldItem.ModItem.FullName);
            }
        }
    }

    public class RightClickFromInventoryConfig : ModConfig
    {
        public static RightClickFromInventoryConfig Instance;
        public override ConfigScope Mode => ConfigScope.ClientSide;

        [DefaultValue(true)]
        public bool enabled;

        [DefaultValue(true)]
        public bool showHoldThrowText;

    }
}
