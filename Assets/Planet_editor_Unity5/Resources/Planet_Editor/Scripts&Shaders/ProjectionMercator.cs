using System.Collections.Generic;
using UnityEngine;

namespace Orogeny.Maps {
    public class ProjectionMercator : IProjection {
        public (int, int) GetSize(int startWidth, int startHeight) {
            return (startWidth, startWidth);
        }

        public List<Vector2> GetMapCoordinates(float lonRad, float latRad, int width, int height) {
            var results = new List<Vector2>();

            var iScale = lonRad / (2 * Mathf.PI);
            var jScale = System.Math.Atanh(Mathf.Tan(latRad / 2)) / (Mathf.PI / 2);

            var i = (int)(iScale * width + (width / 2)); 
            var j = (int)(jScale * (height / 2) + (height / 2));

            results.Add(new Vector2(i, j));

            return results;
        }

        public (bool, float, float) GetGeodeticCoordinates(int i, int j, int width, int height) {
            var iScale = ((float)i - (width / 2)) / width;
            var jScale = ((float)(j - (height / 2))) / (height / 2);

            var lonRad = iScale * 2 * Mathf.PI;
            var latRad =  2 * Mathf.Atan((float)System.Math.Tanh(jScale * Mathf.PI / 2));

            return (true, lonRad, latRad);
        }

        public string GetName() {
            return "Mercator";
        }
    }
}