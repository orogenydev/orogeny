using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Orogeny.Meshes;

namespace Orogeny.Convection {
    public class Convector {
        Mesh convector;
        float radius;
        List <Vector3> upwellings;
        List<int> modifiedVertices;
        float upwellingRadius = 3;

        public Convector(float _radius) {
            //convector = IcoSphereCreator.Create(32, _radius);
            convector = MeshUndump("convection.dat");
            radius = _radius;
            upwellings = new List<Vector3>();
            modifiedVertices = new List<int>();

            // Initialize();
            // MeshDump(convector, "convection.dat");
        }

        public Mesh GetMesh() {
            return convector;
        }

        public void SetMesh(Mesh mesh) {
            convector = mesh;
        }

        public void Initialize() {
            // Add upwellings
            AddUpwelling(Vector3.back * radius);
            AddUpwelling(Vector3.left * radius);
            AddUpwelling(Vector3.right * radius);
            AddUpwelling(Vector3.up * radius);
            AddUpwelling(Vector3.forward * radius);
            AddUpwelling(Vector3.down * radius);

            ExpandConvection();
        }

        void AddUpwelling(Vector3 point) {
            upwellings.Add(point);
        }

        private void ExpandConvection() {
            var vertices = convector.vertices;
            var normals = convector.normals;

            for (int i = 0; i < vertices.Length; i++) {
                if (modifiedVertices.Contains(i)) {
                    continue;
                }

                var (upwelling1, dist1, upwelling2, dist2) = GetNearestUpwellings(convector.vertices[i]);
                normals[i] = RotateConvectionAway(upwelling1, normals[i], dist1, dist2);
            }

            convector.normals = normals;
        }

        private Vector3 RotateConvectionAway(Vector3 anchor, Vector3 target, float dist1, float dist2) {
            // If dist1 < upwellingRadius, scale from 0 to 90 based on that
            // If (dist1 - dist2) < upwellingRadius, scale from 90 to 180 based on that
            // Otherwise, go to 90 
            var radians = Mathf.PI / 2;

            if (dist1 < upwellingRadius) {
                radians = (Mathf.PI / 2) * Mathf.Sin((Mathf.PI / 2) * (dist1 / upwellingRadius));
            } else if (Mathf.Abs(dist1 - dist2) < upwellingRadius) {
                radians = Mathf.PI / 2 + (Mathf.PI / 2) * Mathf.Cos((Mathf.PI / 2) * (Mathf.Abs(dist1 - dist2) / upwellingRadius));
            }

            // Find perpendicular to rotate about
            var perp = Vector3.Cross(anchor, target);

            var theta = radians * Mathf.Rad2Deg;

            return Quaternion.AngleAxis(theta, perp) * target;
        }

        private List<int> GetNeighbors(int index) {
            var triangles = convector.triangles;
            List<int> neighbors = new List<int>();

            for (int i = 0; i < triangles.Length; i += 3) {
                if (triangles[i] == index) {
                    neighbors.Add(triangles[i + 1]);
                    neighbors.Add(triangles[i + 2]);
                }
                if (triangles[i + 1] == index) {
                    neighbors.Add(triangles[i]);
                    neighbors.Add(triangles[i + 2]);
                }
                if (triangles[i + 2] == index) {
                    neighbors.Add(triangles[i]);
                    neighbors.Add(triangles[i + 1]);
                }
            }

            return neighbors;
        }

        private (Vector3, float, Vector3, float) GetNearestUpwellings(Vector3 point) {
            float minDist1 = 1000000;
            Vector3 minUpwelling1 = upwellings[0];
            float minDist2 = 1000000;
            Vector3 minUpwelling2 = upwellings[0];

            foreach (Vector3 upwelling in upwellings) {
                var dist = (upwelling - point).magnitude;
                if (dist < minDist1) {
                    minDist2 = minDist1;
                    minUpwelling2 = minUpwelling1;
                    minDist1 = dist;
                    minUpwelling1 = upwelling;
                } else if (dist < minDist2) {
                    minDist2 = dist;
                    minUpwelling2 = upwelling;
                }
            }

            return (minUpwelling1, minDist1, minUpwelling2, minDist2);
        }

        // https://www.riccardostecca.net/articles/save_and_load_mesh_data_in_unity/
        private void MeshDump(Mesh mesh, string filename)
        {
            System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bf = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            System.IO.FileStream fs = new System.IO.FileStream(Application.dataPath + "\\" + filename, System.IO.FileMode.Create);
            SerializableMeshInfo smi = new SerializableMeshInfo(mesh);
            bf.Serialize(fs, smi);
            fs.Close();
        }

        private Mesh MeshUndump(string filename)
        {
            if (!System.IO.File.Exists(Application.dataPath + "\\" + filename)) {
                Debug.LogError(filename + " file does not exist.");
                return null;
            }
            System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bf = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            System.IO.FileStream fs = new System.IO.FileStream(Application.dataPath + "\\" + filename, System.IO.FileMode.Open);
            SerializableMeshInfo smi = (SerializableMeshInfo) bf.Deserialize(fs);
            var mesh = smi.GetMesh();
            fs.Close();

            return mesh;
        }
    }
}