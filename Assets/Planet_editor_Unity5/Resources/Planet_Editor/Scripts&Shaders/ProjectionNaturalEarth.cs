using System.Collections.Generic;
using UnityEngine;

namespace Orogeny.Maps {
    public class ProjectionNaturalEarth : IProjection {
        private static double A0 = 0.8707;
        private static double A1 = -0.131979;
        private static double A2 = -0.013791;
        private static double A3 = 0.003971;
        private static double A4 = -0.001529;
        private static double B0 = 1.007226;
        private static double B1 = 0.015085;
        private static double B2 = -0.044475;
        private static double B3 = 0.028874;
        private static double B4 = -0.005916;
        private static double C0 = B0;
        private static double C1 = 3 * B1;
        private static double C2 = 7 * B2;
        private static double C3 = 9 * B3;
        private static double C4 = 11 * B4;
        private static double EPS = 1e-6;

        public (int, int) GetSize(int startWidth, int startHeight) {
            return (startWidth, (int)(startWidth * 1.42239f / 2.73539f));
        }

        public List<Vector2> GetMapCoordinates(float lonRad, float latRad, int width, int height) {
            var results = new List<Vector2>();
            float phi2 = latRad * latRad;
            float phi4 = phi2 * phi2;

            var xScale = lonRad * (A0 + phi2 * (A1 + phi2 * (A2 + phi4 * phi2 * (A3 + phi2 * A4))));
            var yScale = latRad * (B0 + phi2 * (B1 + phi4 * (B2 + B3 * phi2 + B4 * phi4)));

            var x = (int)(xScale / 2.73539f * (width / 2) + (width / 2));
            var y = (int)(yScale / 1.42239f * (height / 2) + (height / 2));

            results.Add(new Vector2(x, y));

            return results;
        }

        // https://www.shadedrelief.com/NE_proj/code.html
        public (bool, float, float) GetGeodeticCoordinates(int x, int y, int width, int height) {
            var xScale = ((float)(x - (width / 2))) / (width / 2) * 2.73539f;
            var yScale = ((float)(y - (height / 2))) / (height / 2) * 1.42239f;

            bool valid = true;
            float lonRad = 0;
            float latRad = 0;

            // latitude
            double yc = yScale;
            double tol;
            for (int i = 0; i < 10; i++) { // Newton-Raphson
                double y2p = yc * yc;
                double y4 = y2p * y2p;
                double f = (yc * (B0 + y2p * (B1 + y4 * (B2 + B3 * y2p + B4 * y4)))) - yScale;
                double fder = C0 + y2p * (C1 + y4 * (C2 + C3 * y2p + C4 * y4));
                tol = f / fder;
                yc -= tol;
                //Debug.Log(tol);
                if (Mathf.Abs((float)tol) < EPS) {
                    break;
                }
            }
            latRad = (float)yc;

            // longitude
            double y2 = yc * yc;
            double phi = A0 + y2 * (A1 + y2 * (A2 + y2 * y2 * y2 * (A3 + y2 * A4)));
            lonRad = (float)(xScale / phi);

            if (lonRad < -Mathf.PI || lonRad > Mathf.PI) {
                valid = false;
            }

            return (valid, (float)lonRad, (float)latRad);
        }

        public string GetName() {
            return "Natural Earth";
        }
    }
}