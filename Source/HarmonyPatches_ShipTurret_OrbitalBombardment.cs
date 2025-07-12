using System.Reflection;
using RimWorld.Planet;
using UnityEngine;
using HarmonyLib;
using RimWorld;
using Verse;
using SaveOurShip2;
using SaveOurShip2_OrbitalBombardment;

namespace SaveOurShip2_OrbitalBombardment
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches_ShipTurret_OrbitalBombardment
    {
        // Harmony patch registration removed; method is called via reflection from handler
    }
}

// Inject TryOrbitalBombardment method into Building_ShipTurret
[HarmonyPatch(typeof(Building_ShipTurret))]
public static class Building_ShipTurret_TryOrbitalBombardment_Patch
{
    [HarmonyReversePatch]
    [HarmonyPatch("TryOrbitalBombardment")]
    public static bool TryOrbitalBombardment(Building_ShipTurret __instance, Map targetMap, IntVec3 targetCell)
    {
        // This will be replaced by the prefix below
        return false;
    }
}

// Prefix for injected method
[HarmonyPatch(typeof(Building_ShipTurret), "TryOrbitalBombardment")]
public static class Building_ShipTurret_TryOrbitalBombardment_Prefix
{
    public static bool Prefix(Building_ShipTurret __instance, Map targetMap, IntVec3 targetCell, ref bool __result)
    {
        // Null checks for projectile creation (check gun, not turret building)
        if (__instance.gun == null)
        {
            __result = false;
            return false;
        }
        if (__instance.gun.def == null)
        {
            __result = false;
            return false;
        }
        if (__instance.gun.def.Verbs == null)
        {
            __result = false;
            return false;
        }
        if (__instance.gun.def.Verbs.Count == 0)
        {
            __result = false;
            return false;
        }
        if (targetMap == null)
        {
            __result = false;
            return false;
        }
        {
            // Only fire if turret is active, not on cooldown, and has resources
            if (!__instance.Active || __instance.burstCooldownTicksLeft > 0 || __instance.holdFire)
            {
                __result = false;
                return false;
            }
            // Get burst count and projectile def
            int burstCount = 1;
            ThingDef projDef = null;
            if (__instance.gun.def != null && __instance.gun.def.Verbs != null && __instance.gun.def.Verbs.Count > 0)
            {
                var verbProps = __instance.gun.def.Verbs[0];
                if (verbProps != null)
                {
                    burstCount = verbProps.burstShotCount > 0 ? verbProps.burstShotCount : 1;
                    if (verbProps.defaultProjectile != null)
                        projDef = verbProps.defaultProjectile;
                }
            }
            if (projDef == null)
            {
                projDef = DefDatabase<ThingDef>.GetNamedSilentFail("Projectile_OrbitalBombardment");
                if (projDef == null)
                {
                    __result = false;
                    return false;
                }
            }
            // Laser check: treat as laser if spawnDef is Bullet_Fake_Laser or projectileDef is Proj_ShipSpinalLance40k
            bool isLaser = false;
            if (__instance.gun.def.Verbs != null && __instance.gun.def.Verbs.Count > 0)
            {
                var verbProps = __instance.gun.def.Verbs[0];
                if (verbProps.spawnDef == SaveOurShip2.ResourceBank.ThingDefOf.Bullet_Fake_Laser ||
                    projDef == DefDatabase<ThingDef>.GetNamedSilentFail("Proj_ShipSpinalLance40k") ||
                    projDef == SaveOurShip2.ResourceBank.ThingDefOf.Bullet_Fake_Laser ||
                    projDef == SaveOurShip2.ResourceBank.ThingDefOf.Bullet_Ground_Laser ||
                    projDef == SaveOurShip2.ResourceBank.ThingDefOf.Bullet_Fake_Psychic)
                {
                    isLaser = true;
                }
            }
            // Consume resources and set cooldown
            __instance.burstCooldownTicksLeft = __instance.BurstCooldownTime().SecondsToTicks();
            if (__instance.fuelComp != null && __instance.fuelComp.Fuel > 0)
            {
                __instance.fuelComp.ConsumeFuel(1);
            }
            if (__instance.powerComp != null)
            {
                foreach (CompPowerBattery bat in __instance.powerComp.PowerNet.batteryComps)
                {
                    bat.DrawPower(Mathf.Min(__instance.EnergyToFire * bat.StoredEnergy / __instance.powerComp.PowerNet.CurrentStoredEnergy(), bat.StoredEnergy));
                }
            }
            if (__instance.heatComp != null && __instance.heatComp.Props.heatPerPulse > 0)
            {
                __instance.heatComp.AddHeatToNetwork(__instance.HeatToFire);
            }
            if (targetMap == null)
            {
                __result = false;
                return false;
            }
            var worldObjDef = DefDatabase<WorldObjectDef>.GetNamed("OrbitalBombardmentProjectile");
            if (worldObjDef == null)
            {
                __result = false;
                return false;
            }
            // Burst logic
            for (int i = 0; i < burstCount; i++)
            {
                IntVec3 burstCell = targetCell;
                if (isLaser && burstCount > 1)
                {
                    // Laser burst: spread shots in a line
                    int spacing = 2;
                    IntVec3 direction = new IntVec3(Rand.RangeInclusive(-1,1), 0, Rand.RangeInclusive(-1,1));
                    if (direction.x == 0 && direction.z == 0) direction = new IntVec3(1,0,0);
                    int dx = Rand.RangeInclusive(-1, 1);
                    int dz = Rand.RangeInclusive(-1, 1);
                    IntVec3 startCell = new IntVec3(
                        Mathf.Clamp(targetCell.x + dx, 0, targetMap.Size.x - 1),
                        0,
                        Mathf.Clamp(targetCell.z + dz, 0, targetMap.Size.z - 1)
                    );
                    burstCell = new IntVec3(
                        Mathf.Clamp(startCell.x + direction.x * i * spacing, 0, targetMap.Size.x - 1),
                        0,
                        Mathf.Clamp(startCell.z + direction.z * i * spacing, 0, targetMap.Size.z - 1)
                    );
                }
                var worldProjObj = WorldObjectMaker.MakeWorldObject(worldObjDef);
                var worldProj = worldProjObj as WorldObject_OrbitalBombardmentProjectile;
                if (worldProj == null)
                {
                    __result = false;
                    return false;
                }
                worldProj.sourceTile = __instance.Map.Tile;
                worldProj.targetTile = targetMap.Tile;
                worldProj.targetMap = targetMap;
                worldProj.targetCell = burstCell;
                worldProj.projectileDef = projDef;
                worldProj.Tile = __instance.Map.Tile;
                worldProj.missRadius = __instance.AttackVerb?.verbProps?.ForcedMissRadius ?? 0f;
                Find.WorldObjects.Add(worldProj);
            }
            __result = true;
            return false; // Skip original method (if any)
        }
    }
}