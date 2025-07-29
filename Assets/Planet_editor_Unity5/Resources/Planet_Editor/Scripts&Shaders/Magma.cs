using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Orogeny.Plates {
    public enum MagmaType {
        Subduction,
        Hotspot
    }

    public abstract class Magma {
        protected Vector3 position;
        protected bool active;
        protected GameObject marker;

        protected Magma(GameObject gameObject, Vector3 _position)
        {
            // Mix up the exact position a bit
            var posRange = 0.7f;
            var x = _position[0] + Random.Range(-posRange, posRange);
            var y = _position[1] + Random.Range(-posRange, posRange);
            var z = _position[2] + Random.Range(-posRange, posRange);
            position = new Vector3(x, y, z).normalized * Plate.magmatismDepth;
            active = true;

            marker = Plate.CreateEventMarker(gameObject, new Color(1.0f, 0f, 1.0f, 0.5f), position.normalized * 25, 1.0f, "EventMarkers", "EventMagma");
        }

        public Vector3 GetPosition() {
            return position;
        }

        public void SetPosition(Vector3 _position) {
            position = _position;
            marker.transform.position = position;
        }

        public bool GetActive() {
            return active;
        }

        public GameObject Kill(GameObject gameObject) {
            var oldMarker = marker;
            active = false;
            marker = Plate.CreateEventMarker(gameObject, new Color(0.5f, 0.5f, 0.5f, 0.5f), position.normalized * 25, 1.0f, "EventMarkers", "EventMagmaDead");
            return oldMarker;
        }

        public abstract bool ShouldRandomlyErupt();

        public abstract bool ShouldRandomlyDie();

        public abstract void DoUplift(Plate plate);

        public static Magma Create(MagmaType type, Vector3 position, GameObject gameObject) 
        {
            return type == MagmaType.Subduction ? new MagmaSubduction(gameObject, position) : new MagmaHotspot(gameObject, position);
        }
    }
}