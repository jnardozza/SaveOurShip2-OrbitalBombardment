using RimWorld;
using RimWorld.Planet;
using Verse;
using SaveOurShip2;

namespace SaveOurShip2_OrbitalBombardment
{
    public static class OrbitalBombardmentHandler
    {
        public static void DoBombardment(CompShipHeatTacCon tacCon, Map targetMap, IntVec3 targetCell)
        {
            if (tacCon == null || targetMap == null) return;
            var manager = targetMap.GetComponent<OrbitalBombardmentManager>();
            if (manager == null)
            {
                manager = new OrbitalBombardmentManager(targetMap);
                targetMap.components.Add(manager);
            }
            manager.QueueBombardment(tacCon, targetMap, targetCell);
        }
    }
}
