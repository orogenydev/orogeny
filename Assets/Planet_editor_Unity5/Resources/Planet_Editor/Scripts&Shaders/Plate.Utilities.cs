using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Orogeny.Plates {
    public enum CrustType {
        Continental,
        Oceanic,
        Subducted,
        Virtual
    }

    public partial class Plate {
        public static float continentalCrustThickness = 0.5f;
        public static float hotCrustThicknessBonus = 0.4f;
        public static float framesToCool = 10000;

        public static float seaFloorRadius = 25;
        public static float trenchRadius = 24.5f;
        public static float mohoRadius = 18;
        public static float coreRadius = 12;

        public static float distanceThreshold = 0.5f;

        public static float magmatismDepth = seaFloorRadius - 1f;
        public static float magmatismUpperLimit = seaFloorRadius - 2f;
        public static float magmatismLowerLimit = seaFloorRadius - 3f;
        public static double magmatismBirthChance = 0.005;
        public static double magmatismEruptionChance = 0.06;
        public static double magmatismDeathChance = 0.01;

        public static Dictionary<int, List<int>> ExtractNeighbors(Mesh mesh) {
            var neighbors = new Dictionary<int, List<int>>();
            var triangles = mesh.triangles;

            // Fill in the neighbors dictionary
            for (int i = 0; i < triangles.Length; i += 3) {
                for (int j = 0; j < 3; j++) {
                    AddNeighbors(neighbors,
                                triangles[i + j], 
                                new int[] { triangles[i + (j + 1) % 3],
                                            triangles[i + (j + 2) % 3] } );
                }
            }

            return neighbors;
        }

        public static void AddNeighbors(Dictionary<int, List<int>> neighbors, int v, int[] others) {
            if (!neighbors.ContainsKey(v)) {
                neighbors.Add(v, new List<int>());
            }

            for (int i = 0; i < others.Length; i++) {
                if (!neighbors[v].Contains(others[i])) {
                    neighbors[v].Add(others[i]);
                }
            }
        }

        public static GameObject CreateEventMarker(GameObject parent, Color color, Vector3 position, float scale, string layer, string name = "EventMarker") {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.position = position;
            sphere.transform.parent = parent.transform;
            sphere.transform.localScale = new Vector3(scale, scale, scale);
            sphere.layer = LayerMask.NameToLayer(layer);
            sphere.name = name;

            var renderer = sphere.GetComponent<Renderer>();
            var transparency = Shader.Find("Transparent/Diffuse");
            renderer.material.shader = transparency;
            renderer.material.SetColor("_Color", color);
            renderer.allowOcclusionWhenDynamic = false;

            return sphere;
        }

        public static Plate FindPlateByInstanceID(List<Plate> plates, int id) {
            foreach (Plate plate in plates) {
                var collider = plate.gameObject.GetComponent<MeshCollider>();
                if (collider.GetInstanceID() == id) {
                    return plate;
                }
            }

            return null;
        }

        public static int FindClosestVertex(Vector3[] vertices, Vector3 point) {
            var closest = -1;
            var minDist = float.MaxValue;

            for (int i = 0; i < vertices.Length; i++) {
                var dist = (vertices[i] - point).magnitude;
                if (dist < minDist) {
                    closest = i;
                    minDist = dist;
                }
            }

            return closest;
        }

        public List<int> ExtractPerimeter() {
            var perimeter = new List<int>();

            foreach (int v in neighbors.Keys) {
                if (IsPerimeterVertex(v)) {
                    perimeter.Add(v);
                }
            }

            return perimeter;
        }

        public bool IsPerimeterVertex(int v) {
            if (!IsSubducted(plateVertices[v]) &&
                !IsVirtual(plateVertices[v]) &&
                !experimeter.Contains(v) &&
                InvalidNeighborCount(v) > 0) {
                    return true;
            }

            return false;
        }

        public int InvalidNeighborCount(int v) {
            var invalids = 0;

            foreach (var n in neighbors[v]) {
                if (IsSubducted(plateVertices[n]) || IsVirtual(plateVertices[n]) || experimeter.Contains(n)) {
                    invalids++;
                }
            }

            return invalids;
        }

        public List<GameObject> CreatePerimeterMarkers(GameObject parent, Color color) {
            var markers = new List<GameObject>();

            foreach (var v in perimeter) {
                markers.Add(CreateEventMarker(parent, color, plateVertices[v], 0.5f, "PerimeterPoints", "Perimeter"));
            }

            return markers;
        }

        public int FindClosestPerimeter(Vector3 point) {
            var closest = -1;
            var minDist = float.MaxValue;

            foreach (var p in perimeter) {
                var dist = (plateVertices[p] - point).magnitude;
                if (dist < minDist) {
                    closest = p;
                    minDist = dist;
                }
            }

            return closest;
        }

        public CrustType GetCrustType(Vector3 vertex) {            
            if (vertex.magnitude > seaFloorRadius + continentalCrustThickness / 2.0f) {
                return CrustType.Continental;
            }

            if (vertex.magnitude > trenchRadius) {
                return CrustType.Oceanic;
            }

            if (vertex.magnitude > mohoRadius) {
                return CrustType.Subducted;
            }

            return CrustType.Virtual;
        }

        public bool IsContinental(Vector3 vertex) {
            var sampleType = GetCrustType(vertex);
            return sampleType == CrustType.Continental;
        }

        public bool IsOceanic(Vector3 vertex) {
            var sampleType = GetCrustType(vertex);
            return sampleType == CrustType.Oceanic;
        }

        public bool IsOceanicOrContinental(Vector3 vertex) {
            var sampleType = GetCrustType(vertex);
            return sampleType == CrustType.Oceanic || sampleType == CrustType.Continental;
        }

        public bool IsSubducted(Vector3 vertex) {
            var sampleType = GetCrustType(vertex);
            return sampleType == CrustType.Subducted;
        }

        public bool IsVirtual(Vector3 vertex) {
            var sampleType = GetCrustType(vertex);
            return sampleType == CrustType.Virtual;
        }

        public Quaternion CreateVertexRotation(Vector3 direction, Vector3 normal, float scale) {
            var pivot = Vector3.Cross(normal, direction);
            var amount = direction.magnitude * scale;
            return Quaternion.AngleAxis(amount, pivot);
        }

        // WCS -> local movement CS
        // Returns a rotation that will align Z with collisionForce and Y with the up vector
        public Quaternion WCS2MovementCS(Vector3 collisionForce, Vector3 up) {
            return Quaternion.Inverse(Quaternion.LookRotation(collisionForce, up));
        }

        public bool IsVertexAhead(Vector3 collisionForce, Vector3 target, Vector3 origin, Vector3 up) {
            var q = WCS2MovementCS(collisionForce, up);

            var targetP = q * target;
            var originP = q * origin;
        
            return targetP.z > originP.z;
        }

        public Vector3 ProjectBack(Vector3 collisionForce, Vector3 target, Vector3 origin, Vector3 up, float discount) {
            var q = WCS2MovementCS(collisionForce, up);

            var targetP = q * target;
            var originP = q * origin;
            var diff = targetP.z - originP.z;

            targetP.z -= diff * discount;

            return Quaternion.Inverse(q) * targetP;
        }

        private void UpliftTerrane(Vector3[] normals, Vector3 position, float amount, float width) {
            var threshold = 3f;

            for (int i = 0; i < plateVertices.Length; i++) {
                var dist = (plateVertices[i] - position).magnitude;
                dist = Mathf.Max(0, dist - width);

                if (dist < threshold) {
                    var adjustedScale = amount / (1f + dist);

                    plateVertices[i] = Elevate(netForce, plateVertices[i], normals[i], adjustedScale);
                }
            }
        }

        // Have to use this instead of the normal, because they don't always line up perfectly.
        public Vector3 Elevate(Vector3 collisionForce, Vector3 target, Vector3 up, float amount) {
            var q = WCS2MovementCS(collisionForce, up);

            var targetP = q * target;

            targetP.y += amount;

            return Quaternion.Inverse(q) * targetP;
        }

        public bool IsOnlyVirtualAtPoint(Vector3 point) {
            Ray ray = new Ray(Vector3.zero, point);
            ray.origin = ray.GetPoint(50);
            ray.direction = -ray.direction;
            var mask = LayerMask.GetMask("Plates");

            if (!Physics.Raycast(ray.origin, ray.direction, out var hit, 50 - Plate.mohoRadius, mask)) {
                return true;
            }

            return IsVirtual(hit.point);
        }

       private static void ClearEvents(List<GameObject> events) {
            foreach (var e in events) {
                Destroy(e);
            }

            events.Clear();
        }

        private int ValidNeighborCount(int v) {
            var count = 0;

            foreach (var n in neighbors[v]) {
                if (IsOceanicOrContinental(plateVertices[n])) {
                    count++;
                }
            }

            return count;
        }

        private void CheckNeighborPerimeterStatus(int v) {
            foreach (var n in neighbors[v]) {
                if (InvalidNeighborCount(n) == 0) {
                    RemoveFromPerimeter(n);
                }
            }
        }

        private void AddToPerimeter(int v) {
            perimeter.Add(v);
            perimeterMarkers.Add(CreateEventMarker(gameObject, color, plateVertices[v], 0.5f, "PerimeterPoints", "Perimeter"));
        }

        private void RemoveFromPerimeter(int v) {
            if (perimeter.Contains(v)) {
                var index = perimeter.IndexOf(v);
                perimeter.RemoveAt(index);
                Destroy(perimeterMarkers[index]);
                perimeterMarkers.RemoveAt(index);
                experimeter.Add(v);

                CheckNeighborsForPerimeterness(v);
            }
        }

        private void CheckNeighborsForPerimeterness(int v) {
            foreach (var n in neighbors[v]) {
                CheckForPerimeterness(n);
            }
        }

        private void CheckForPerimeterness(int v) {
            if (!perimeter.Contains(v) && IsPerimeterVertex(v)) {
                AddToPerimeter(v);
            }
        }

        private void RemoveReal(int v) {
            if (realVertices.Contains(v)) {
                realVertices.Remove(v);
            }
        }
 
        private void CopyMesh(Mesh _mesh) {
            mesh = new Mesh();
            mesh.vertices = _mesh.vertices;
            mesh.triangles = _mesh.triangles;
            mesh.normals = _mesh.normals;
            mesh.tangents = _mesh.tangents;
            mesh.uv = _mesh.uv;
            mesh.uv2 = _mesh.uv2;
            mesh.name = "Mesh " + plateNumber;
        }
   }
}