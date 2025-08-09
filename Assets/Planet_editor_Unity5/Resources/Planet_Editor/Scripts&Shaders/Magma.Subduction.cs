using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Orogeny.Plates {
    public class MagmaSubduction : Magma {
        public MagmaSubduction(GameObject gameObject, Vector3 position) : base(gameObject, position)
        {
            
        }

        public static bool ShouldRandomlyGenerate()
        {
            return Random.value < Plate.magmatismBirthChance;
        }

        public override bool ShouldRandomlyErupt() {
            return Random.value < Plate.magmatismEruptionChance;
        }

        public override bool ShouldRandomlyDie() {
            return Random.value < Plate.magmatismDeathChance;
        }

        public override void DoUplift(Plate plate) {
            plate.UpliftTerrane(position, 0.012f);
        }
    }
}