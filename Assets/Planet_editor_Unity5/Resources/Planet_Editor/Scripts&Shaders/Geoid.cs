using UnityEngine;
using UnityEngine.EventSystems;

namespace Orogeny.Maps {
    public class Geoid : MonoBehaviour
    {
        public GameObject mainCamera;

        private UserInterface ui;
        private Vector2 dragLast = Vector2.zero;

        void Start() {
            ui = mainCamera.GetComponent<UserInterface>();
        }

        public void HandlePointerDown(BaseEventData data) {
            PointerEventData p = (PointerEventData)data;
            dragLast = p.position;
        }

        public void HandleDrag(BaseEventData data) {
            PointerEventData p = (PointerEventData)data;
            var deltaX = dragLast.x - p.position.x;
            var deltaY = dragLast.y - p.position.y;
            dragLast = p.position;

            ui.MouseRotation(deltaX, deltaY);
        }
    }
}