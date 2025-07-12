using System.Collections.Generic;
using Verse;
using RimWorld;
using SaveOurShip2;
using UnityEngine;
using RimWorld.Planet;

namespace SaveOurShip2_OrbitalBombardment
{
    public class OrbitalBombardmentManager : MapComponent
    {
        private struct PendingShot
        {
            public Building_ShipTurret turret;
            public Map targetMap;
            public IntVec3 targetCell;
            public int ticksUntilFire;
            public int burstCount;
        }

        private List<PendingShot> pendingShots = new List<PendingShot>();
        private int shotsFired = 0;
        private IntVec3 lastTargetCell;
        private Map lastTargetMap;

        public OrbitalBombardmentManager(Map map) : base(map) { }

        public void QueueBombardment(CompShipHeatTacCon tacCon, Map targetMap, IntVec3 targetCell)
        {
            if (tacCon == null || tacCon.myNet == null || tacCon.myNet.Turrets == null) return;
            int tickOffset = 0;
            foreach (var heatComp in tacCon.myNet.Turrets)
            {
                if (heatComp == null || heatComp.parent == null) continue;
                var turret = ShipHeatNet.CESafeCastToTurret(heatComp.parent);
                if (turret == null) continue;
                int burstCount = 1;
                string verbLabel = "null";
                float missRadius = -1f;
                float originalMissRadius = -1f;
                try
                {
                    if (turret.GunCompEq != null && turret.AttackVerb != null && turret.AttackVerb.verbProps != null)
                    {
                        burstCount = turret.AttackVerb.verbProps.burstShotCount > 0 ? turret.AttackVerb.verbProps.burstShotCount : 1;
                        verbLabel = !string.IsNullOrEmpty(turret.AttackVerb.verbProps.label) ? turret.AttackVerb.verbProps.label : "null";
                        originalMissRadius = turret.AttackVerb.verbProps.ForcedMissRadius;
                        missRadius = originalMissRadius;
                        // Scale missRadius up to a maximum of 50 based on distance to target tile from ship
                        int sourceTile = turret.Map.Parent.Tile;
                        int targetTile = targetMap.Parent.Tile;
                        int tileDistance = Mathf.Max(1, (int)Find.WorldGrid.ApproxDistanceInTiles(sourceTile, targetTile));
                        float scaleFactor = Mathf.Clamp01(tileDistance / 200f); // 200 tiles = full scale
                        missRadius = Mathf.Min(originalMissRadius * (1f + scaleFactor * 29f), 30f); // scale up to 30 max
                    }
                }
                catch { }
                Log.Message($"OrbitalBombardmentManager: Turret {turret.LabelCap} verb={verbLabel} burstCount={burstCount} originalMissRadius={originalMissRadius} scaledMissRadius={missRadius}");

                bool isLaser = false;
                // Get projectile ThingDef from gun's verb
                ThingDef projDef = null;
                if (turret.GunCompEq != null && turret.GunCompEq.PrimaryVerb != null)
                {
                    projDef = turret.GunCompEq.PrimaryVerb.GetProjectile();
                }
                if (projDef != null)
                {
                    // You may need to adjust these ThingDefs to match your mod
                    isLaser = projDef == SaveOurShip2.ResourceBank.ThingDefOf.Bullet_Fake_Laser ||
                              projDef == SaveOurShip2.ResourceBank.ThingDefOf.Bullet_Ground_Laser ||
                              projDef == SaveOurShip2.ResourceBank.ThingDefOf.Bullet_Fake_Psychic;
                }

                if (isLaser && burstCount > 1)
                {
                    // Pick a random direction for the line
                    IntVec3 direction = new IntVec3(Rand.RangeInclusive(-1,1), 0, Rand.RangeInclusive(-1,1));
                    if (direction.x == 0 && direction.z == 0) direction = new IntVec3(1,0,0); // avoid zero vector
                    int spacing = 2; // cells between impacts

                    // Pick a random cell within miss radius for the first shot
                    IntVec3 startCell = targetCell;
                    // missRadius is now loaded above from AttackVerb
                    if (missRadius > 0.1f)
                    {
                        int dx = Rand.RangeInclusive(-(int)missRadius, (int)missRadius);
                        int dz = Rand.RangeInclusive(-(int)missRadius, (int)missRadius);
                        startCell = new IntVec3(
                            Mathf.Clamp(targetCell.x + dx, 0, targetMap.Size.x - 1),
                            0,
                            Mathf.Clamp(targetCell.z + dz, 0, targetMap.Size.z - 1)
                        );
                    }
                    Log.Message($"Laser burst: direction={direction} spacing={spacing} startCell={startCell}");

                    for (int i = 0; i < burstCount; i++)
                    {
                        IntVec3 burstCell = new IntVec3(
                            Mathf.Clamp(startCell.x + direction.x * i * spacing, 0, targetMap.Size.x - 1),
                            0,
                            Mathf.Clamp(startCell.z + direction.z * i * spacing, 0, targetMap.Size.z - 1)
                        );
                        Log.Message($"Laser burst {i + 1}/{burstCount}: burstCell={burstCell}");
                        int delayTicks = Rand.RangeInclusive(0, 30); // 0 to 0.5 seconds
                        pendingShots.Add(new PendingShot
                        {
                            turret = turret,
                            targetMap = targetMap,
                            targetCell = burstCell,
                            ticksUntilFire = tickOffset + delayTicks,
                            burstCount = 1
                        });
                        tickOffset += delayTicks;
                    }
                }
                else
                {
                    int delayTicks = Rand.RangeInclusive(0, 30); // 0 to 0.5 seconds
                    for (int i = 0; i < burstCount; i++)
                    {
                        IntVec3 impactCell = targetCell;
                        if (missRadius > 0.1f)
                        {
                            int dx = Rand.RangeInclusive(-(int)missRadius, (int)missRadius);
                            int dz = Rand.RangeInclusive(-(int)missRadius, (int)missRadius);
                            impactCell = new IntVec3(
                                Mathf.Clamp(targetCell.x + dx, 0, targetMap.Size.x - 1),
                                0,
                                Mathf.Clamp(targetCell.z + dz, 0, targetMap.Size.z - 1)
                            );
                        }
                        pendingShots.Add(new PendingShot
                        {
                            turret = turret,
                            targetMap = targetMap,
                            targetCell = impactCell,
                            ticksUntilFire = tickOffset + delayTicks,
                            burstCount = 1
                        });
                        tickOffset += delayTicks;
                    }
                }
            }
            lastTargetCell = targetCell;
            lastTargetMap = targetMap;
        }

        public override void MapComponentTick()
        {
            if (pendingShots.Count == 0) return;
            for (int i = pendingShots.Count - 1; i >= 0; i--)
            {
                var shot = pendingShots[i];
                shot.ticksUntilFire--;
                if (shot.ticksUntilFire <= 0)
                {
                    bool result = false;
                    try
                    {
                        Building_ShipTurret_TryOrbitalBombardment_Prefix.Prefix(shot.turret, shot.targetMap, shot.targetCell, ref result);
                    }
                    catch { }
                    Log.Message($"Burst shot fired: turret={shot.turret.LabelCap} map={shot.targetMap} cell={shot.targetCell}");
                    if (result) shotsFired++;
                    pendingShots.RemoveAt(i);
                }
                else
                {
                    pendingShots[i] = shot;
                }
            }
            // When all shots are fired, show message
            if (pendingShots.Count == 0 && shotsFired > 0 && lastTargetMap != null)
            {
                try
                {
                    Messages.Message($"Orbital bombardment: {shotsFired} shots fired at {lastTargetCell}", new LookTargets(new GlobalTargetInfo(lastTargetCell, lastTargetMap)), MessageTypeDefOf.PositiveEvent);
                }
                catch { }
                shotsFired = 0;
                lastTargetMap = null;
            }
        }
    }
}
