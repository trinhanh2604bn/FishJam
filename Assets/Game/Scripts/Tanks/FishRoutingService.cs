using System.Collections.Generic;
using FishPuzzle.Domain;

namespace FishPuzzle.Tanks
{
    /// <summary>
    /// Chooses the accepting tank for one fish type.
    /// Highest fill wins. Equal fill uses the lowest tank slot index.
    /// </summary>
    public sealed class FishRoutingService
    {
        public TankRouteChoice SelectTank(FishType fishType, IReadOnlyList<TankRuntimeState> tanks)
        {
            var found = false;
            var bestSlot = -1;
            var bestFill = -1;
            if (tanks == null)
            {
                return TankRouteChoice.None;
            }

            for (var i = 0; i < tanks.Count; i++)
            {
                var tank = tanks[i];
                if (tank == null || !tank.CanAccept(fishType))
                {
                    continue;
                }

                var better = !found
                    || tank.FillCount > bestFill
                    || (tank.FillCount == bestFill && tank.SlotIndex < bestSlot);
                if (!better)
                {
                    continue;
                }

                found = true;
                bestFill = tank.FillCount;
                bestSlot = tank.SlotIndex;
            }

            return found ? TankRouteChoice.ToSlot(bestSlot) : TankRouteChoice.None;
        }
    }

    public readonly struct TankRouteChoice
    {
        private TankRouteChoice(bool found, int slotIndex)
        {
            Found = found;
            SlotIndex = slotIndex;
        }

        public bool Found { get; }

        public int SlotIndex { get; }

        public static TankRouteChoice None => new TankRouteChoice(false, -1);

        public static TankRouteChoice ToSlot(int slotIndex)
        {
            return new TankRouteChoice(true, slotIndex);
        }
    }
}
