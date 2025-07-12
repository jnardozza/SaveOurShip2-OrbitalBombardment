using System.Collections.Generic;
using RimWorld.Planet;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
using SaveOurShip2;

namespace SaveOurShip2_OrbitalBombardment
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches_OrbitalBombardment
    {
        static HarmonyPatches_OrbitalBombardment()
        {
            var harmony = new Harmony("SOS2.OrbitalBombardment");
            harmony.Patch(
                AccessTools.Method(typeof(CompShipHeatTacCon), "CompGetGizmosExtra"),
                postfix: new HarmonyMethod(typeof(HarmonyPatches_OrbitalBombardment), nameof(CompGetGizmosExtra_Postfix))
            );
        }

        public static void CompGetGizmosExtra_Postfix(CompShipHeatTacCon __instance, ref IEnumerable<Gizmo> __result)
        {
            List<Gizmo> gizmos = new List<Gizmo>(__result);
            // Only show if player faction and ship is in orbit (customize as needed)
            if (__instance.parent.Faction == Faction.OfPlayer)
            {
                gizmos.Add(new Command_Action
                {
                    defaultLabel = "Orbital Bombardment",
                    defaultDesc = "Target a location on the planet below for orbital bombardment.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower"),
                    action = delegate {
                        Find.World.renderer.wantedMode = WorldRenderMode.Planet;
                        Find.WorldTargeter.BeginTargeting(
                            delegate (GlobalTargetInfo worldTarget)
                            {
                                var map = Find.Maps.FirstOrDefault(m => m.Tile == worldTarget.Tile);
                                if (map == null)
                                {
                                    Messages.Message("No map found for selected world tile!", MessageTypeDefOf.RejectInput);
                                    return false;
                                }
                                Current.Game.CurrentMap = map;
                                CameraJumper.TryJump(new GlobalTargetInfo(new IntVec3(map.Size.x / 2, 0, map.Size.z / 2), map), CameraJumper.MovementMode.Pan);
                                Find.Targeter.BeginTargeting(
                                    new TargetingParameters { canTargetLocations = true, canTargetBuildings = true, canTargetPawns = true },
                                    cellTarget => {
                                        if (cellTarget.Cell.IsValid)
                                        {
                                            SaveOurShip2_OrbitalBombardment.OrbitalBombardmentHandler.DoBombardment(__instance, map, cellTarget.Cell);
                                        }
                                    },
                                    null, null, null, null, null, true, null, null
                                );
                                return true;
                            },
                            true
                        );
                    }
                });
            }
            __result = gizmos;
        }
    }
}
