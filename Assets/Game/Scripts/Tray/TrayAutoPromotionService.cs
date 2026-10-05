using System.Collections.Generic;
using FishPuzzle.Domain;
using FishPuzzle.Tanks;

namespace FishPuzzle.Tray
{
    /// <summary>
    /// Finds the next waiting-tray fish that can enter an active tank.
    /// Scan order is left to right. Tank choice stays with <see cref="Tanks.FishRoutingService"/>.
    /// </summary>
    public static class TrayAutoPromotionService
    {
        public static bool TryFindFirstMove(
            WaitingTrayState tray,
            IReadOnlyList<TankRuntimeState> tanks,
            FishRoutingService routing,
            out int trayIndex,
            out int tankSlotIndex)
        {
            trayIndex = -1;
            tankSlotIndex = -1;
            if (tray == null || routing == null)
            {
                return false;
            }

            for (var i = 0; i < tray.Count; i++)
            {
                var fish = tray.GetFishAt(i);
                if (fish == null)
                {
                    continue;
                }

                var choice = routing.SelectTank(fish.Type, tanks);
                if (!choice.Found)
                {
                    continue;
                }

                trayIndex = i;
                tankSlotIndex = choice.SlotIndex;
                return true;
            }

            return false;
        }
    }
}
