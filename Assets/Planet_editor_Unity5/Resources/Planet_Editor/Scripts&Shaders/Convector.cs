using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Orogeny.Maps;
using Orogeny.Meshes;
using Orogeny.Plates;

namespace Orogeny.Convection {
    public class Convector
    {
        private Mesh convector;
        private float radius;
        private List<Vector3> upwellings = new List<Vector3>();
        private List<int> modifiedVertices = new List<int>();
        private float upwellingRadius = 3;
        private float hackyLongitudeAdjustement = -73; // This aligns the camera
        private bool useOriginalMesh = true;

        public Convector(float _radius)
        {
            //convector = IcoSphereCreator.Create(32, _radius);
            convector = MeshUtils.MeshUndumpFromFile("convection.dat");
            radius = _radius;

            //Initialize();
            //MeshUtils.MeshDumpToFile(convector, "convection5.dat");
        }

        public Mesh GetMesh()
        {
            return convector;
        }

        public void SetMesh(Mesh mesh)
        {
            convector = mesh;
        }

        public void ToggleMesh()
        {
            useOriginalMesh = !useOriginalMesh;
            convector = MeshUtils.MeshUndumpFromFile(useOriginalMesh ? "convection.dat" : "convection3.dat");
        }

        public void Initialize()
        {
            // Add upwellings
            AddUpwelling(0, 0);
            AddUpwelling(0, 90);
            AddUpwelling(0, 180);
            AddUpwelling(0, -90);
            AddUpwelling(90, 0);
            AddUpwelling(-90, 0);

            ExpandConvection();
        }

        void AddUpwelling(float _lon, float _lat)
        {
            var lon = _lon + hackyLongitudeAdjustement;
            var point = MapProjection.Geodetic2Vector(lon, _lat).normalized * radius;

            //var gameObject = new GameObject();
            //Plate.CreateEventMarker(gameObject, new Color(0.9f, 0f, 0.5f, 0.5f), point.normalized * 25, 3.0f, "EventMarkers", "Upwelling");

            var amount = 30;
            var pivot = (Vector3.up + Vector3.down + Vector3.left).normalized;
            var q = Quaternion.AngleAxis(amount, pivot);
            point = q * point;

            upwellings.Add(point);
        }

        private void ExpandConvection()
        {
            var vertices = convector.vertices;
            var normals = convector.normals;

            for (int i = 0; i < vertices.Length; i++)
            {
                if (modifiedVertices.Contains(i))
                {
                    continue;
                }

                var (upwelling1, dist1, upwelling2, dist2) = GetNearestUpwellings(convector.vertices[i]);
                Debug.Log($"{upwelling1} {dist1} {upwelling2} {dist2}");
                normals[i] = RotateConvectionAwayFromSource(upwelling1, normals[i], dist1, dist2);
            }

            convector.normals = normals;
        }

        private Vector3 RotateConvectionAwayFromSource(Vector3 source, Vector3 target, float dist1, float dist2)
        {
            // If dist1 < upwellingRadius, scale from 0 to 90 based on that
            // If (dist1 - dist2) < upwellingRadius, scale from 90 to 180 based on that
            // Otherwise, go to 90 
            var radians = Mathf.PI / 2;

            if (dist1 < upwellingRadius)
            {
                radians = (Mathf.PI / 2) * Mathf.Sin((Mathf.PI / 2) * (dist1 / upwellingRadius));
            }
            else if (Mathf.Abs(dist1 - dist2) < upwellingRadius)
            {
                radians = Mathf.PI / 2 + (Mathf.PI / 2) * Mathf.Cos((Mathf.PI / 2) * (Mathf.Abs(dist1 - dist2) / upwellingRadius));
            }

            // Find perpendicular to rotate about
            var perp = Vector3.Cross(source, target);

            var theta = radians * Mathf.Rad2Deg;

            return Quaternion.AngleAxis(theta, perp) * target;
        }

        private (Vector3, float, Vector3, float) GetNearestUpwellings(Vector3 point)
        {
            float minDist1 = float.MaxValue;
            Vector3 minUpwelling1 = upwellings[0];
            float minDist2 = float.MaxValue;
            Vector3 minUpwelling2 = upwellings[0];

            foreach (Vector3 upwelling in upwellings)
            {
                var dist = (upwelling - point).magnitude;
                if (dist < minDist1)
                {
                    minDist2 = minDist1;
                    minUpwelling2 = minUpwelling1;
                    minDist1 = dist;
                    minUpwelling1 = upwelling;
                }
                else if (dist < minDist2)
                {
                    minDist2 = dist;
                    minUpwelling2 = upwelling;
                }
            }

            return (minUpwelling1, minDist1, minUpwelling2, minDist2);
        }
    }
}