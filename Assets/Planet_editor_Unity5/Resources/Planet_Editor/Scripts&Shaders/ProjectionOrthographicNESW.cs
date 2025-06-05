using System.Collections.Generic;
using UnityEngine;

namespace Orogeny.Maps {
    public class ProjectionOrthographicNESW : IProjection {
        private float r = 95;

        public (int, int) GetSize(int startWidth, int startHeight) {
            // The orthgraphic hemispheres are prone to some weird moire sampling effects
            // So they're configured by radius, to let that be tuned away
            var height = r * 4;
            var width = (int)(2 * r + 2 * Mathf.Sqrt(3 * r * r));

            return ((int)width, (int)height);
        }

        public List<Vector2> GetMapCoordinates(float lonRad, float latRad, int width, int height) {
            var results = new List<Vector2>();
            (var pixelR, var centerN, var centerE, var centerS, var centerW) = GetPixelCenters(width, height);

            (var validNorth, var north) = GetMapCoordinatesHemisphere(lonRad, latRad, width, height, centerN);
            if (validNorth) {
                results.Add(north);
            }

            (var validEast, var east) = GetMapCoordinatesHemisphere(lonRad, latRad, width, height, centerE);
            if (validEast) {
                results.Add(east);
            }

            (var validSouth, var south) = GetMapCoordinatesHemisphere(lonRad, latRad, width, height, centerS);
            if (validSouth) {
                results.Add(south);
            }

            (var validWest, var west) = GetMapCoordinatesHemisphere(lonRad, latRad, width, height, centerW);
            if (validWest) {
                results.Add(west);
            }

            return results;
        }

        private (bool, Vector2) GetMapCoordinatesHemisphere(float lonRad, float latRad, int width, int height, Vector2 center) {
            (var valid, var lonRadCenter, var latRadCenter, var iCenter, var jCenter) = GetCenters(width, height, (int)center.x, (int)center.y);

            if (!valid) {
                return (false, Vector2.zero);
            }

            var cosc = Mathf.Sin(latRadCenter) * Mathf.Sin(latRad) + Mathf.Cos(latRadCenter) * Mathf.Cos(latRad) * Mathf.Cos(lonRad - lonRadCenter);

            if (cosc < 0) {
                return (false, Vector2.zero);
            }

            var i = (int)(r * Mathf.Cos(latRad) * Mathf.Sin(lonRad - lonRadCenter));
            var j = (int)(r * (Mathf.Cos(latRadCenter) * Mathf.Sin(latRad) - Mathf.Sin(latRadCenter) * Mathf.Cos(latRad) * Mathf.Cos(lonRad - lonRadCenter)));

            return (true, new Vector2((int)(i + iCenter), (int)(j + jCenter)));
        }

        public (bool, float, float) GetGeodeticCoordinates(int i, int j, int width, int height) {
            (var valid, var lonRadCenter, var latRadCenter, var iCenter, var jCenter) = GetCenters(width, height, i, j);

            if (!valid) {
                return (false, 0, 0);
            }

            var iScale = ((float)(i - iCenter)) / (2 * r);
            var jScale = ((float)(j - jCenter)) / (2 * r);

            var p = Mathf.Sqrt(Mathf.Pow(iScale, 2) + Mathf.Pow(jScale, 2));
            var c = Mathf.Asin(p / 0.5f);

            var latRad = Mathf.Asin( Mathf.Cos(c) * Mathf.Sin(latRadCenter) + jScale * Mathf.Sin(c) * Mathf.Cos(latRadCenter) / p);
            var lonRad = lonRadCenter + Mathf.Atan2( iScale * Mathf.Sin(c), 
                    (p * Mathf.Cos(latRadCenter) * Mathf.Cos(c) - jScale * Mathf.Sin(latRadCenter) * Mathf.Sin(c)) );

            return (true, lonRad, latRad);
        }

        public string GetName() {
            return "Orthographic (NESW)";
        }

        private (float, Vector2, Vector2, Vector2, Vector2) GetPixelCenters(float width, float height) {
            var pixelR = height / 4;
            var centerN = new Vector2(width / 2, 3 * pixelR);
            var centerE = new Vector2(pixelR + 2 * Mathf.Sqrt(3 * pixelR * pixelR), height / 2);
            var centerS = new Vector2(width / 2, pixelR);
            var centerW = new Vector2(pixelR, height / 2);

            return (pixelR, centerN, centerE, centerS, centerW);
        }

        private (bool, float, float, float, float) GetCenters(float width, float height, int i, int j) {
            (var pixelR, var centerN, var centerE, var centerS, var centerW) = GetPixelCenters(width, height);
            var point = new Vector2(i, j);

            var lonRadCenter = 0f;
            var latRadCenter = 0f;
            var iCenter = 0f;
            var jCenter = 0f;

            if ((centerN - point).magnitude < pixelR) {
                lonRadCenter = Mathf.PI;
                latRadCenter = Mathf.PI / 2;
                iCenter = centerN.x;
                jCenter = centerN.y;
            } else if ((centerE - point).magnitude < pixelR) {
                lonRadCenter = -Mathf.PI / 2;
                iCenter = centerE.x;
                jCenter = centerE.y;
            } else if ((centerS - point).magnitude < pixelR) {
                lonRadCenter = Mathf.PI;
                latRadCenter = -Mathf.PI / 2;
                iCenter = centerS.x;
                jCenter = centerS.y;
            } else if ((centerW - point).magnitude < pixelR) {
                lonRadCenter = Mathf.PI / 2;
                iCenter = centerW.x;
                jCenter = centerW.y;        
            } else {
                return (false, 0, 0, 0, 0);
            }

            return (true, lonRadCenter, latRadCenter, iCenter, jCenter);
        }
    }
}