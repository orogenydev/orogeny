using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Orogeny.Plates
{
    public class SubductionTracker
    {
        private Dictionary<int, List<int>> SubductionRegistry;

        public SubductionTracker()
        {
            SubductionRegistry = new Dictionary<int, List<int>>();
        }

        public bool IsXSubductingUnderY(Plate plateX, Plate plateY, List<int> localX, List<int> localY, float uvX, float uvY)
        {
            var numX = plateX.GetPlateNumber();
            var numY = plateY.GetPlateNumber();

            if (SubductionRegistry.TryGetValue(numX, out var overX) && overX.Contains(numY))
            {
                return true;
            }

            if (SubductionRegistry.TryGetValue(numY, out var overY) && overY.Contains(numX))
            {
                return false;
            }

            // These area subduction checks might be obsolete now with this global tracker...
            if (plateX.AreAnySubducting(localX))
            {
                SubductXUnderY(numX, numY);
                return true;
            }

            if (plateY.AreAnySubducting(localY))
            {
                SubductXUnderY(numY, numX);
                return false;
            }

            if (uvX < uvY)
            {
                SubductXUnderY(numX, numY);
                return true;
            }

            // Final tie breaker
            if (numX < numY)
            {
                SubductXUnderY(numX, numY);
                return true;
            }

            SubductXUnderY(numY, numX);
            return false;
        }

        public bool IsXAlreadySubductingUnderY(Plate plateX, Plate plateY)
        {
            var numX = plateX.GetPlateNumber();
            var numY = plateY.GetPlateNumber();

            if (SubductionRegistry.TryGetValue(numX, out var overX) && overX.Contains(numY))
            {
                return true;
            }

            return false;
        }

        public void ForceXSubductUnderY(Plate plateX, Plate plateY)
        {
            var numX = plateX.GetPlateNumber();
            var numY = plateY.GetPlateNumber();

            SubductXUnderY(numX, numY);
        }

        public void PrintState()
        {
            foreach (var key in SubductionRegistry.Keys)
            {
                Debug.Log($"{key} < {SubductionRegistry[key]}");
            }
            Debug.Log("");
        }

        private void SubductXUnderY(int numX, int numY)
        {
            if (!SubductionRegistry.ContainsKey(numX))
            {
                SubductionRegistry[numX] = new List<int>();
            }

            SubductionRegistry[numX].Add(numY);

            if (SubductionRegistry.ContainsKey(numY))
            {
                SubductionRegistry[numY].Remove(numX);
            }
        }
    }
}