using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Orogeny.Plates {
    public class Magma {
        private Vector3 position;
        private bool active;
        private GameObject marker;

        public Magma(GameObject gameObject, Vector3 _position) {
            position = _position;
            active = true;
            
            marker = Plate.CreateEventMarker(gameObject, new Color(1.0f, 0f, 1.0f, 0.5f), position, 1.5f, "EventMarkers", "EventMagma");
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
            marker = Plate.CreateEventMarker(gameObject, new Color(0.5f, 0.5f, 0.5f, 0.5f), position, 1.5f, "EventMarkers", "EventMagmaDead");
            return oldMarker;
        }
    }
}