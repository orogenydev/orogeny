using System.Collections.Generic;
using UnityEngine;

namespace Orogeny.Maps {
    public class ProjectionEquirectangular : IProjection {
        public (int, int) GetSize(int startWidth, int startHeight) {
            return (startWidth, startWidth / 2);
        }

        public List<Vector2> GetMapCoordinates(float lonRad, float latRad, int width, int height) {
            var results = new List<Vector2>();
            var iScale = lonRad / (2 * Mathf.PI);
            var jScale = latRad / Mathf.PI;

            var i = (int)(iScale * width);
            var j = (int)(jScale * height + (height / 2));

            results.Add(new Vector2(i, j));

            return results;
        }

        public (bool, float, float) GetGeodeticCoordinates(int i, int j, int width, int height) {
            var iScale = ((float)i - (width / 2)) / width; 
            var jScale = ((float)(j - (height / 2))) / height;

            var lonRad = iScale * 2 * Mathf.PI;
            var latRad = jScale * Mathf.PI;

            return (true, lonRad, latRad);
        }

        public string GetName() {
            return "Equirectangular";
        }
    }
}