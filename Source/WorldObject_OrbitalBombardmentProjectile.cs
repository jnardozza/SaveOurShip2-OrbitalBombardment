using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using SaveOurShip2;

namespace SaveOurShip2_OrbitalBombardment
{
    public class WorldObject_OrbitalBombardmentProjectile : WorldObject
    {
        public int ticksToImpact = -1; // Will be set based on projectile speed
        public int ticksPassed = 0;
        public int sourceTile;
        public int targetTile;
        public Map targetMap;
        public IntVec3 targetCell;
        public ThingDef projectileDef;
        public float missRadius = 2f; // Set from gun's verb (forcedMissRadius)

        public override void Tick()
        {
            base.Tick();
            ticksPassed++;
            if (ticksToImpact < 0)
            {
                // Set ticksToImpact based on projectile speed
                float speed = 100f;
                if (projectileDef?.projectile != null)
                {
                    speed = projectileDef.projectile.speed;
                }
                else
                {
                }
                // Base ticks: .5 second per tile at speed 100
                int tileDistance = Mathf.Max(1, (int)Find.WorldGrid.ApproxDistanceInTiles(sourceTile, targetTile));
                ticksToImpact = Mathf.Clamp((int)(30f * tileDistance * (100f / Mathf.Max(speed, 0.1f))), 1, 60000);
            }
            if (ticksPassed >= ticksToImpact)
            {
                ArriveOnTargetMap();
                Find.WorldObjects.Remove(this);
            }
        }

        public void ArriveOnTargetMap()
        {
            if (targetMap != null && projectileDef != null)
            {
                // Extract stats
                float speed = 100f;
                int accBoost = 0;
                if (projectileDef.projectile != null)
                {
                    speed = projectileDef.projectile.speed;
                }

                // Use targetCell directly; miss radius is handled by the manager
                IntVec3 impactCell = targetCell;

                var shipProj = new SaveOurShip2.ShipCombatProjectile();
                shipProj.target = new Verse.LocalTargetInfo(impactCell);
                shipProj.range = (impactCell - FindEdgeCell(targetMap, impactCell)).LengthHorizontal;
                shipProj.spawnProjectile = projectileDef;
                shipProj.burstLoc = FindEdgeCell(targetMap, impactCell);
                shipProj.Map = targetMap;
                shipProj.speed = speed;
                shipProj.missRadius = missRadius;
                shipProj.accBoost = accBoost;

                // Logging for laser motes
                bool isLaser = projectileDef == SaveOurShip2.ResourceBank.ThingDefOf.Bullet_Fake_Laser ||
                               projectileDef == SaveOurShip2.ResourceBank.ThingDefOf.Bullet_Ground_Laser ||
                               projectileDef == SaveOurShip2.ResourceBank.ThingDefOf.Bullet_Fake_Psychic;
                Log.Message($"ArriveOnTargetMap: projectileDef={projectileDef?.defName}, isLaser={isLaser}, burstLoc={shipProj.burstLoc}, impactCell={impactCell}");

                // Spawn ShipCombatLaserMote for laser projectiles
                if (isLaser)
                {
                    var laserMote = (SaveOurShip2.ShipCombatLaserMote)ThingMaker.MakeThing(SaveOurShip2.ResourceBank.ThingDefOf.ShipCombatLaserMote);
                    // Set origin to above the map, so the laser comes from orbit
                    Vector3 topOfMap = new Vector3(impactCell.x, 0f, targetMap.Size.z + 10f); // 10 cells above the top edge
                    laserMote.origin = topOfMap;
                    laserMote.destination = impactCell.ToVector3Shifted();
                    laserMote.large = false; // You can set this based on damage or other logic if needed
                    laserMote.color = Color.red; // Set color as needed, or extract from turretDef
                    laserMote.Attach(null); // No launcher, since this is orbital
                    Log.Message($"Spawning ShipCombatLaserMote: origin={laserMote.origin}, destination={laserMote.destination}, color={laserMote.color}");
                    GenSpawn.Spawn(laserMote, impactCell, targetMap, 0); // Spawn at impact cell for visibility
                }

                // Spawn and set up non-laser projectile to travel from orbit
                if (!isLaser)
                {
                    // Spawn position: above the impact cell
                    Vector3 spawnPos = new Vector3(impactCell.x, 0f, targetMap.Size.z + 10f); // 10 cells above map
                    Log.Message($"Spawning projectile: def={projectileDef?.defName}, spawnPos={spawnPos}, impactCell={impactCell}");
                    var projectileThing = ThingMaker.MakeThing(projectileDef);
                    GenSpawn.Spawn(projectileThing, impactCell, targetMap); // Spawn at impact cell for visibility
                    var projectileObj = projectileThing as SaveOurShip2.Projectile_ExplosiveShip;
                    if (projectileObj != null)
                    {
                        var originField = typeof(Verse.Projectile).GetField("origin", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.FlattenHierarchy);
                        var destinationField = typeof(Verse.Projectile).GetField("destination", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.FlattenHierarchy);
                        var speedField = typeof(Verse.Projectile).GetField("speed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.FlattenHierarchy);
                        if (originField != null) originField.SetValue(projectileObj, spawnPos);
                        if (destinationField != null) destinationField.SetValue(projectileObj, impactCell.ToVector3Shifted());
                        if (speedField != null) speedField.SetValue(projectileObj, 20f); // Slow speed for visible travel
                        Log.Message($"Set projectile trajectory: origin={spawnPos}, destination={impactCell.ToVector3Shifted()}, speed=20");
                    }
                }
            }
        }

        private IntVec3 FindEdgeCell(Map map, IntVec3 target)
        {
            if (target.x < map.Size.x / 2) return new IntVec3(0, 0, target.z);
            if (target.x > map.Size.x / 2) return new IntVec3(map.Size.x - 1, 0, target.z);
            if (target.z < map.Size.z / 2) return new IntVec3(target.x, 0, 0);
            return new IntVec3(target.x, 0, map.Size.z - 1);
        }

        public override string Label => "Orbital Bombardment Projectile";
    }
}
