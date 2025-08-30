using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Orogeny.Plates;

namespace Orogeny.Hotspots {
    [Serializable]
    public class Hotspot : MonoBehaviour
    {
        public Vector3 position;

        private Plate curPlate;
        private Magma curMagma;
        private GameObject curGO;
        private GameObject marker;

        public void Initialize(Vector3 _position, string _name)
        {
            position = _position;
            name = _name;

            marker = Plate.CreateEventMarker(gameObject, Color.red, position.normalized * 25, 2.0f, "Hotspots", name);
        }

        public Vector3 GetPosition()
        {
            return position;
        }

        private void CreateMagma()
        {
            // Find which plate we are currently under
            Ray ray = new Ray(Vector3.zero, position);
            ray.origin = ray.GetPoint(50);
            ray.direction = -ray.direction;
            var mask = LayerMask.GetMask("Plates");

            Physics.SphereCast(ray, 0.1f, out var hit, 50 - Plate.mohoRadius, mask);

            if (hit.collider != null)
            {
                curGO = hit.collider.gameObject;
                curPlate = curGO.GetComponent<Plate>();
                curMagma = curPlate.AddMagma(position, MagmaType.Hotspot);
            }
        }

        private void KillMagma()
        {
            curPlate.KillMagma(curMagma);
            curMagma = null;
            curPlate = null;
            curGO = null;
        }

        public void DoVolcanism()
        {
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

        public static float DistanceToClosestHotspot(Vector3 pos, List<Hotspot> hotspots)
        {
            var min = float.MaxValue;

            foreach (var hotspot in hotspots)
            {
                min = Mathf.Min(min, (pos.normalized - hotspot.position.normalized).magnitude);
            }

            return min;
        }

        public void OnDestroy()
        {
            Destroy(marker);
        }
    }
}