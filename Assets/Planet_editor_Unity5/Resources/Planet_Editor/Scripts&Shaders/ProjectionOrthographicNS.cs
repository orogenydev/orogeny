using System.Collections.Generic;
using UnityEngine;

namespace Orogeny.Maps {
    public class ProjectionOrthographicNS : IProjection {
        private float r = 95;
        private float lonRadCenter = 0f;
        private float latRadCenter = Mathf.PI / 2;

        public (int, int) GetSize(int startWidth, int startHeight) {
            // The orthgraphic hemispheres are prone to some weird moire sampling effects
            // So they're configured by radius, to let that be tuned away
            var height = 2 * r;
            var width = 4 * r;

            return ((int)width, (int)height);
        }

        public List<Vector2>  GetMapCoordinates(float lonRad, float latRad, int width, int height) {
            var results = new List<Vector2>();
            var iCenter = width / 4;
            var jCenter = height / 2;

            var latRadCenterPrime = latRadCenter;

            if (latRad == 0) {
                return results;
            } else if (latRad < 0) {
                latRadCenterPrime = -latRadCenter;
                iCenter = width * 3 / 4;            
            }

            var cosc = Mathf.Sin(latRadCenterPrime) * Mathf.Sin(latRad) + Mathf.Cos(latRadCenterPrime) * Mathf.Cos(latRad) * Mathf.Cos(lonRad - lonRadCenter);

            if (cosc < 0) {
                return results;
            }

            var i = (int)(r * Mathf.Cos(latRad) * Mathf.Sin(lonRad - lonRadCenter));
            var j = (int)(r * (Mathf.Cos(latRadCenterPrime) * Mathf.Sin(latRad) - Mathf.Sin(latRadCenterPrime) * Mathf.Cos(latRad) * Mathf.Cos(lonRad - lonRadCenter)));

            results.Add(new Vector2(i + iCenter, j + jCenter));

            return results;
        }

        public (bool, float, float) GetGeodeticCoordinates(int i, int j, int width, int height) {
            var iCenter = width / 4;
            var jCenter = height / 2;

            var latRadCenterPrime = latRadCenter;

            if (i > width / 2) {
                latRadCenterPrime = -latRadCenter;
                iCenter = width * 3 / 4;            
            }

            if (Mathf.Sqrt(Mathf.Pow(i - iCenter, 2) + Mathf.Pow(j - jCenter, 2)) > height / 2) {
                return (false, 0, 0);
            }

            var iScale = ((float)(i - iCenter)) / (width / 2);
            var jScale = ((float)(j - jCenter)) / height;

            var p = Mathf.Sqrt(Mathf.Pow(iScale, 2) + Mathf.Pow(jScale, 2));
            var c = Mathf.Asin(p / 0.5f);

            var latRad = Mathf.Asin( Mathf.Cos(c) * Mathf.Sin(latRadCenterPrime) + jScale * Mathf.Sin(c) * Mathf.Cos(latRadCenterPrime) / p);
            var lonRad = lonRadCenter + Mathf.Atan2( iScale * Mathf.Sin(c), 
                    (p * Mathf.Cos(latRadCenterPrime) * Mathf.Cos(c) - jScale * Mathf.Sin(latRadCenterPrime) * Mathf.Sin(c)) );

            return (true, lonRad, latRad);
        }

        public string GetName() {
            return "Orthographic (NS)";
        }
    }
}