using System.Collections.Generic;
using UnityEngine;

namespace Orogeny.Maps {
    public class ProjectionNone : IProjection {
        public (int, int) GetSize(int startWidth, int startHeight) {
            return (startWidth, 0);
        }

        public List<Vector2> GetMapCoordinates(float lonRad, float latRad, int width, int height) {
            return new List<Vector2>();
        }

        public (bool, float, float) GetGeodeticCoordinates(int i, int j, int width, int height) {
            return (false, 0, 0);
        }

        public string GetName() {
            return "None";
        }
    }
}