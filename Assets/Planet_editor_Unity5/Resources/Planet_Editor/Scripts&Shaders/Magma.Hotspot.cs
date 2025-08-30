using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Orogeny.Plates {
    public class MagmaHotspot : Magma {
        public MagmaHotspot(GameObject gameObject, Vector3 position) : base(gameObject, position)
        {
            
        }

        public override bool ShouldRandomlyErupt() {
            return Random.value < Plate.magmatismHotspotEruptionChance;
        }

        public override bool ShouldRandomlyDie() {
            return false;
        }

        public override void DoUplift(Plate plate)
        {
            plate.UpliftTerrane(position, 0.025f, 0.03f);
        }
    }
}