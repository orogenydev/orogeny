using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Orogeny.Plates;

namespace Orogeny.Hotspots {
    public class Hotspot {
        private Vector3 position;
        private Plate curPlate;
        private Magma curMagma;
        private GameObject curGO;

        public Hotspot(GameObject gameObject, Vector3 _position) {
            position = _position;
            
            Plate.CreateEventMarker(gameObject, new Color(1.0f, 0f, 0f, 1.0f), position.normalized * 25, 1.0f, "EventMarkers", "Hotspot");
        }

        public Vector3 GetPosition() {
            return position;
        }

        private void CreateMagma() {
            // Find which plate we are currently under
            Ray ray = new Ray(Vector3.zero, position);
            ray.origin = ray.GetPoint(50);
            ray.direction = -ray.direction;
            var mask = LayerMask.GetMask("Plates");

            Physics.SphereCast(ray, 0.1f, out var hit, 50 - Plate.mohoRadius, mask);
            curGO = hit.collider.gameObject;
            curPlate = curGO.GetComponent<Plate>();

            curMagma = curPlate.AddMagma(position, MagmaType.Hotspot);
        }

        private void KillMagma() {
            curPlate.KillMagma(curMagma);
            curMagma = null;
            curPlate = null;
            curGO = null;
        }

        public void DoVolcanism() {
            var maxDist = 4.0f;

            // Check distance to curMagma, if too far kill it and start a new one straight above
            if (curMagma != null)
            {
                var dist = (position - curMagma.GetPosition()).magnitude;

                if (dist > maxDist)
                {
                    //Debug.Log("New hotspot magma");
                    KillMagma();
                    CreateMagma();
                }
            }
            else
            {
                CreateMagma();
            }
        }
    }
}