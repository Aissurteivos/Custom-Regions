﻿using CustomRegions.Mod;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CustomRegions.RegionProperties
{
    internal static class ScavengerHooks
    {
        public static void ApplyHooks()
        {

            IL.ScavengerAbstractAI.TryAssembleSquad += ScavengerAbstractAI_TryAssembleSquad;
            On.ScavengerAbstractAI.UpdateMissionAppropriateGear += ScavengerAbstractAI_UpdateMissionAppropriateGear;
            On.ScavengerAbstractAI.TradeItem += ScavengerAbstractAI_TradeItem;
            On.ScavengerAI.CollectScore_PhysicalObject_bool += ScavengerAI_CollectScore_PhysicalObject_bool;
            IL.ScavengerTreasury.ctor += ScavengerTreasury_ctor;
            IL.ScavengerAbstractAI.InitGearUp += ScavengerAbstractAI_InitGearUp;
        }

        private static void ScavengerAbstractAI_InitGearUp(ILContext il)
        {
            var c = new ILCursor(il);
            int index = 0;
            if (c.TryGotoNext(MoveType.AfterLabel,
                x => x.MatchLdloc(out index),
                x => x.MatchLdcI4(0),
                x => x.MatchBlt(out _),
                x => x.MatchCall(typeof(UnityEngine.Random), "get_value"),
                x => x.MatchLdcR4(0.6f)
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc, index);
                c.EmitDelegate((ScavengerAbstractAI self, int i) =>
                {
                    var p = self.parent.world.region?.GetCRSProperties();
                    if (p == null) return i;
                    bool elite = self.parent.creatureTemplate.type == MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.ScavengerElite;
                    var items = elite ? p.eliteScavGearItems : p.scavGearItems;
                    var room = self.world.GetAbstractRoom(self.parent.pos);

                    if (items == null || i < 0) return i;
                    foreach (AbstractPhysicalObject.AbstractObjectType type in items.Keys)
                    {
                        if (UnityEngine.Random.value < items[type])
                        {
                            var obj = GenerateDefaultObject(room.world, type, self.parent.pos);
                            room.AddEntity(obj);
                            new AbstractPhysicalObject.CreatureGripStick(self.parent, obj, i, true);
                            i--;
                            if (i < 0) break;
                        }
                    }
                    return -1;
                });
                c.Emit(OpCodes.Stloc, index);
            }
            else { CustomRegionsMod.BepLogError("failed to il match ScavengerAbstractAI.InitGearUp"); }
        }
        private static bool SpecialRequirements(ScavengerAI self, PhysicalObject obj)
        {
            if (self.scavenger.room != null)
            {
                SocialEventRecognizer.OwnedItemOnGround ownedItemOnGround = self.scavenger.room.socialEventRecognizer.ItemOwnership(obj);
                if (ownedItemOnGround != null && ownedItemOnGround.offeredTo != null && ownedItemOnGround.offeredTo != self.scavenger)
                {
                    return false;
                }
            }
            if (obj is Spear)
            {
                if (ModManager.MMF && MoreSlugcats.MMF.cfgHunterBackspearProtect.Value && (obj as Spear).onPlayerBack)
                    return false;

                if ((obj as Spear).mode == Weapon.Mode.StuckInWall)
                    return false;

                if (obj is ExplosiveSpear e && e.Ignited)
                    return false;

                else if (obj is MoreSlugcats.ElectricSpear l && l.abstractSpear.electricCharge >= 0)
                    return false;
            }
            else if (obj is FirecrackerPlant f && f.fuseCounter != 0)
                return false;

            else if (obj is JellyFish j && j.electricCounter >= 1)
                return false;

            else if (obj is SporePlant p && !p.UsableAsWeapon)
                return false;

            else if (obj is MoreSlugcats.LillyPuck l && l.BitesLeft != 3)
                return false;

            return true;
        }

        public static AbstractPhysicalObject.AbstractObjectType FakeExplosiveSpear = new("ExplosiveSpear", false);
        public static AbstractPhysicalObject.AbstractObjectType FakeNone = new("None", false);

        public static AbstractPhysicalObject GenerateDefaultObject(World world, AbstractPhysicalObject.AbstractObjectType type, WorldCoordinate pos)
        {
            var id = world.game.GetNewID();
            if (type == FakeNone)
            {
                return null;
            }
            if (type == FakeExplosiveSpear)
            {
                return new AbstractSpear(world, null, pos, id, true);
            }
            if (type == AbstractPhysicalObject.AbstractObjectType.Spear)
            {
                return new AbstractSpear(world, null, pos, id, false);
            }
            if (type == AbstractPhysicalObject.AbstractObjectType.WaterNut)
            {
                return new WaterNut.AbstractWaterNut(world, null, pos, id, -1, -1, null, false);
            }
            if (type == AbstractPhysicalObject.AbstractObjectType.SporePlant)
            {
                return new SporePlant.AbstractSporePlant(world, null, pos, id, -1, -1, null, false, true);
            }
            if (type == AbstractPhysicalObject.AbstractObjectType.BubbleGrass)
            {
                return new BubbleGrass.AbstractBubbleGrass(world, null, pos, id, 1f, -1, -1, null);
            }
            if (type == AbstractPhysicalObject.AbstractObjectType.DataPearl)
            {
                return new DataPearl.AbstractDataPearl(world, type, null, pos, id, -1, -1, null, DataPearl.AbstractDataPearl.DataPearlType.Misc);
            }
            if (type == AbstractPhysicalObject.AbstractObjectType.VultureMask)
            {
                return new VultureMask.AbstractVultureMask(world, null, pos, id, id.RandomSeed, false);
            }
            if (type == MoreSlugcats.MoreSlugcatsEnums.AbstractObjectType.FireEgg)
            {
                return new MoreSlugcats.FireEgg.AbstractBugEgg(world, null, pos, id, Mathf.Lerp(0.35f, 0.6f, RWCustom.Custom.ClampedRandomVariation(0.5f, 0.5f, 2f)));
            }
            if (type == MoreSlugcats.MoreSlugcatsEnums.AbstractObjectType.JokeRifle)
            {
                return new JokeRifle.AbstractRifle(world, null, pos, id, JokeRifle.AbstractRifle.AmmoType.Rock);
            }
            if (type == MoreSlugcats.MoreSlugcatsEnums.AbstractObjectType.LillyPuck)
            {
                return new MoreSlugcats.LillyPuck.AbstractLillyPuck(world, null, pos, id, 3, -1, -1, null);
            }

            if (AbstractConsumable.IsTypeConsumable(type))
            {
                return new AbstractConsumable(world, type, null, pos, world.game.GetNewID(), -1, -1, null);
            }
            return new AbstractPhysicalObject(world, type, null, pos, world.game.GetNewID());
        }

        private static int ScavengerAI_CollectScore_PhysicalObject_bool(On.ScavengerAI.orig_CollectScore_PhysicalObject_bool orig, ScavengerAI self, PhysicalObject obj, bool weaponFiltered)
        {
            var dict = self.scavenger.room?.world.region?.GetCRSProperties().scavScoreItems;
            var type = obj.abstractPhysicalObject.type;
            if (dict != null && dict.ContainsKey(type) && SpecialRequirements(self, obj))
            {
                return (int)dict[type];
            }

            return orig(self, obj, weaponFiltered);
        }

        private static AbstractPhysicalObject ScavengerAbstractAI_TradeItem(On.ScavengerAbstractAI.orig_TradeItem orig, ScavengerAbstractAI self, bool main)
        {
            if (main)
            {
                if (self.world?.region?.GetCRSProperties().scavMainTradeItem is AbstractPhysicalObject.AbstractObjectType type && type.index != -1)
                {
                    return GenerateDefaultObject(self.world, type, self.parent.pos);
                }
            }
            else
            {
                if (self.world?.region?.GetCRSProperties().scavTradeItems != null)
                {
                    foreach (KeyValuePair<AbstractPhysicalObject.AbstractObjectType, float> pair in self.world?.region?.GetCRSProperties().scavTradeItems)
                    {
                        if (UnityEngine.Random.value < pair.Value && pair.Key.index != -1)
                        {
                            return GenerateDefaultObject(self.world, pair.Key, self.parent.pos);
                        }
                    }
                }
            }
            return orig(self, main);

        }

        private static void ScavengerAbstractAI_UpdateMissionAppropriateGear(On.ScavengerAbstractAI.orig_UpdateMissionAppropriateGear orig, ScavengerAbstractAI self)
        {
            orig(self);

            if (self.squad == null || self.squad.missionType != ScavengerAbstractAI.ScavengerSquad.MissionID.Trade)
            {
                return;
            }
            if (self.world?.region?.GetCRSProperties().scavMainTradeItem is not AbstractPhysicalObject.AbstractObjectType type)
            {
                return;
            }
            self.missionAppropriateGear = false;
            foreach (AbstractPhysicalObject.AbstractObjectStick stick in self.parent.stuckObjects)
            {
                if (stick is AbstractPhysicalObject.CreatureGripStick && stick.A == self.parent && stick.B.type == type)
                {
                    self.missionAppropriateGear = true;
                    return;
                }
            }
        }

        private static void ScavengerAbstractAI_TryAssembleSquad(ILContext il)
        {
            var c = new ILCursor(il);
            int loc = 2;
            int loc2 = 2;
            ILLabel label = null;
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdloc(out loc),
                x => x.MatchLdcI4(2),
                x => x.MatchBge(out label)
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc, loc);
                c.EmitDelegate((ScavengerAbstractAI self, int scavsInDen) =>
                {
                    int? min = self.world.region?.GetCRSProperties().minScavSquad;
                    if (min is null) return false;
                    return scavsInDen < min - 1;
                });
                c.Emit(OpCodes.Brfalse, label);
            }

            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdloc(out loc),
                x => x.MatchLdcI4(2),
                x => x.MatchLdloc(out loc2),
                x => x.MatchCall(typeof(UnityEngine.Random), "Range"),
                x => x.MatchCall(typeof(Math), "Min")
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc, loc);
                c.Emit(OpCodes.Ldloc, loc2);
                c.EmitDelegate((int orig, ScavengerAbstractAI self, int scavsInDen, int origMax) =>
                {
                    int? min = self.world.region?.GetCRSProperties().minScavSquad;
                    int? max = self.world.region?.GetCRSProperties().maxScavSquad;
                    if (min != null || max != null)
                    { return Math.Min(scavsInDen, UnityEngine.Random.Range(min ?? 3, max ?? origMax + 1) - 1); }
                    return orig;
                });
            }
            else { CustomRegionsMod.BepLogError("failed to il match ScavengerAbstractAI.TryAssembleSquad"); }
        }

        private static void ScavengerTreasury_ctor(ILContext il)
        {
            var c = new ILCursor(il);
            int index = 8;
            if (c.TryGotoNext(MoveType.AfterLabel,
                x => x.MatchLdarg(0),
                x => x.MatchLdfld<ScavengerTreasury>(nameof(ScavengerTreasury.property)),
                x => x.MatchLdloc(out index)
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloca, index);
                c.Emit(OpCodes.Ldloc, index - 1);
                c.EmitDelegate((ScavengerTreasury self, ref AbstractPhysicalObject obj, int i) =>
                {
                    var items = self.room.world.region?.GetCRSProperties().scavTreasuryItems;
                    if (items != null)
                    {
                        obj = null;
                        foreach (AbstractPhysicalObject.AbstractObjectType type in items.Keys)
                        {
                            if (UnityEngine.Random.value < items[type])
                            {
                                obj = GenerateDefaultObject(self.room.world, type, self.room.GetWorldCoordinate(self.tiles[i]));
                                break;
                            }
                        }
                    }

                });
            }

            else { CustomRegionsMod.BepLogError("failed to il match ScavengerTreasury.ctor"); }
        }
    }
}