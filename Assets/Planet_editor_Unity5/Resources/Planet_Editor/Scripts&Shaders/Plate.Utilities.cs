using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Orogeny.Plates {
    public enum CrustType {
        Continental,
        Oceanic,
        Subducted,
        Virtual
    }

    public enum SubductionState
    {
        None,
        Adjacent,
        Initiated,
        Active
    }

	public enum PlateMeshDisplayMode
    {
        None,
        Surface,
        Subducting,
        Entire
    }

    public partial class Plate
    {
        public static float continentalCrustThickness = 0.75f;
        public static float hotCrustThicknessBonus = 0.3f;
        public static float framesToCool = 1000;

        public static float seaFloorRadius = 25;
        public static float seaFloorMinNormalRadius = seaFloorRadius - 0.1f;
        public static float seaFloorMaxNormalRadius = seaFloorRadius + 0.2f;
        public static float trenchRadius = 24.5f;
        public static float mohoRadius = 18;
        public static float coreRadius = 12;

        public static float vertexSquishAllowance = 0.3f;

        // Slab pull should be 85-90% of the forces on a plate, but these values aren't directly comparable that way.
        public static float slabPullScale = 20.0f;
        public static float ridgePushScale = 0.40f;

        public static float magmatismDepth = seaFloorRadius - 1f;
        public static float magmatismUpperLimit = seaFloorRadius - 2f;
        public static float magmatismLowerLimit = seaFloorRadius - 2.5f;
        public static double magmatismBirthChance = 0.1;
        public static double magmatismEruptionChance = 0.4;
        public static double magmatismHotspotEruptionChance = 1.0; //0.8;
        public static double magmatismDeathChance = 0.05;

        public static Dictionary<int, List<int>> ExtractNeighbors(Mesh mesh)
        {
            var neighbors = new Dictionary<int, List<int>>();
            var triangles = mesh.triangles;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                for (int j = 0; j < 3; j++)
                {
                    AddNeighbors(neighbors,
                                triangles[i + j],
                                new int[] { triangles[i + (j + 1) % 3],
                                            triangles[i + (j + 2) % 3] });
                }
            }

            return neighbors;
        }

        public static void AddNeighbors(Dictionary<int, List<int>> neighbors, int v, int[] others)
        {
            if (!neighbors.ContainsKey(v))
            {
                neighbors.Add(v, new List<int>());
            }

            for (int i = 0; i < others.Length; i++)
            {
                if (!neighbors[v].Contains(others[i]))
                {
                    neighbors[v].Add(others[i]);
                }
            }
        }

        public static GameObject CreateEventMarker(GameObject parent, Color color, Vector3 position, float diameter, string layer, string name = "EventMarker")
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.position = position;
            sphere.transform.parent = parent.transform;
            sphere.transform.localScale = new Vector3(diameter, diameter, diameter);
            sphere.layer = LayerMask.NameToLayer(layer);
            sphere.name = name;

            var renderer = sphere.GetComponent<Renderer>();
            var transparency = Shader.Find("Transparent/Diffuse");
            renderer.material.shader = transparency;
            renderer.material.SetColor("_Color", color);
            renderer.allowOcclusionWhenDynamic = false;

            return sphere;
        }

        private static void ClearEvents(List<GameObject> events)
        {
            foreach (var e in events)
            {
                Destroy(e);
            }

            events.Clear();
        }

        public int FindClosestVertex(Vector3 point)
        {
            return FindClosest(point, mesh.vertices);
        }

        public int FindClosestPerimeter(Vector3 point)
        {
            return FindClosest(point, perimeter.Select(p => mesh.vertices[p]).ToArray());
        }

        private int FindClosest(Vector3 point, Vector3[] points)
        {
            var closest = -1;
            var minDist = float.MaxValue;

            for (int i = 0; i < points.Length; i++)
            {
                var dist = (points[i] - point).magnitude;
                if (dist < minDist)
                {
                    closest = i;
                    minDist = dist;
                }
            }

            return closest;
        }

        public List<int> ExtractPerimeter()
        {
            var perimeter = new List<int>();

            foreach (int v in neighbors.Keys)
            {
                if (IsPerimeterVertex(v))
                {
                    perimeter.Add(v);
                }
            }

            return perimeter;
        }

        public bool IsPerimeterVertex(int v)
        {
            if (!IsSubducted(plateVertices[v]) &&
                !IsVirtual(plateVertices[v]) &&
                !IsSubducting(v) &&
                InvalidNeighborCount(v) > 0)
            {
                return true;
            }

            return false;
        }

        public List<GameObject> CreatePerimeterMarkers(GameObject parent, Color color)
        {
            var markers = new List<GameObject>();

            foreach (var v in perimeter)
            {
                markers.Add(CreateEventMarker(parent, color, plateVertices[v], 0.5f, "PerimeterPoints", "Perimeter"));
            }

            return markers;
        }

        public CrustType GetCrustType(Vector3 vertex)
        {
            if (vertex.magnitude > seaFloorRadius + continentalCrustThickness)
            {
                return CrustType.Continental;
            }

            if (vertex.magnitude > trenchRadius)
            {
                return CrustType.Oceanic;
            }

            if (vertex.magnitude > mohoRadius)
            {
                return CrustType.Subducted;
            }

            return CrustType.Virtual;
        }

        public bool IsContinental(Vector3 vertex)
        {
            var sampleType = GetCrustType(vertex);
            return sampleType == CrustType.Continental;
        }

        public bool IsOceanic(Vector3 vertex)
        {
            var sampleType = GetCrustType(vertex);
            return sampleType == CrustType.Oceanic;
        }

        public bool IsOceanicOrContinental(Vector3 vertex)
        {
            var sampleType = GetCrustType(vertex);
            return sampleType == CrustType.Oceanic || sampleType == CrustType.Continental;
        }

        public bool IsSubducted(Vector3 vertex)
        {
            var sampleType = GetCrustType(vertex);
            return sampleType == CrustType.Subducted;
        }

        public bool IsSubducting(int v)
        {
            return !IsVirtual(plateVertices[v]) && vertexSubductionState[v] == SubductionState.Active;
        }

        public bool IsVirtual(Vector3 vertex)
        {
            var sampleType = GetCrustType(vertex);
            return sampleType == CrustType.Virtual;
        }

        public bool AreAnySubducting(List<int> vertices)
        {
            return vertices.Where(v => IsSubducting(v)).Any();
        }

        // WCS -> local movement CS
        // Returns a rotation that will align Z with collisionForce and Y with the up vector
        public Quaternion WCS2MovementCS(Vector3 collisionForce, Vector3 up)
        {
            return Quaternion.Inverse(Quaternion.LookRotation(collisionForce, up));
        }

        public bool IsVertexAhead(Vector3 collisionForce, Vector3 target, Vector3 origin, Vector3 up, float threshold = 1.0f)
        {
            var q = WCS2MovementCS(collisionForce, up);

            var targetP = q * target;
            var originP = q * origin;

            if (targetP.z < originP.z)
            {
                return false;
            }

            return (targetP - originP).magnitude < threshold;
        }

        public Vector3 ProjectBack(Vector3 collisionForce, Vector3 target, Vector3 origin, Vector3 up, float discount)
        {
            var q = WCS2MovementCS(collisionForce, up);

            var targetP = q * target;
            var originP = q * origin;
            var diff = targetP.z - originP.z;

            targetP.z -= diff * discount;

            return Quaternion.Inverse(q) * targetP;
        }

        public void UpliftTerrane(Vector3 position, float amount, float threshold = 0.01f)
        {
            for (int i = 0; i < plateVertices.Length; i++)
            {
                // Work from normalized positions, otherwise the uplift slows as the node is uplifted away from the volcanism!
                var dist = (plateVertices[i].normalized - position.normalized).magnitude;

                if (dist < threshold)
                {
                    var adjustedScale = amount / (1f + dist);

                    plateVertices[i] = plateVertices[i] + plateVertices[i].normalized * adjustedScale;
                }
            }
        }

        // Have to use this instead of the normal, because they don't always line up perfectly.
        public Vector3 Elevate(Vector3 collisionForce, Vector3 target, Vector3 up, float amount)
        {
            var q = WCS2MovementCS(collisionForce, up);

            var targetP = q * target;

            targetP.y += amount;

            return Quaternion.Inverse(q) * targetP;
        }

        public bool IsClearForMORBingAtPoint(Vector3 point, MeshCollider localCollider, float radius = 0.5f)
        {
            Ray ray = new Ray(Vector3.zero, point);
            ray.origin = ray.GetPoint(50);
            ray.direction = -ray.direction;
            var mask = LayerMask.GetMask("Plates");

            var hits = Physics.SphereCastAll(ray, radius, 50 - Plate.mohoRadius, mask);

            for (var i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider != localCollider)
                {
                    // Can't just look at IsVirtual for the hit point, because we get a false negative if it is on the side of a triangle sloping down to the core
                    // If ANY vertex of the hit triangle is virtual, we're good to go
                    MeshCollider meshCollider = hits[i].collider as MeshCollider;
                    Mesh mesh = meshCollider.sharedMesh;
                    Vector3[] meshVertices = mesh.vertices;
                    int[] meshTriangles = mesh.triangles;

                    var v0 = meshTriangles[hits[i].triangleIndex * 3 + 0];
                    var v1 = meshTriangles[hits[i].triangleIndex * 3 + 1];
                    var v2 = meshTriangles[hits[i].triangleIndex * 3 + 2];

                    Vector3 point0 = meshVertices[v0];
                    Vector3 point1 = meshVertices[v1];
                    Vector3 point2 = meshVertices[v2];

                    if (!IsVirtual(point0) && !IsVirtual(point1) && !IsVirtual(point2))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // Need to prevent anomalous MORBing from the overriding plate when a spreading ridge is being subducted.
        // Check a wide spherecast for other plates. If any of them are subducting under this plate, we shouldn't MORB.
        public bool AreSubductingPlatesNearby(Vector3 point, MeshCollider localCollider, SubductionTracker subductionTracker)
        {
            Ray ray = new Ray(Vector3.zero, point);
            ray.origin = ray.GetPoint(50);
            ray.direction = -ray.direction;
            var mask = LayerMask.GetMask("Plates");

            var hits = Physics.SphereCastAll(ray, 3.0f, 50 - Plate.mohoRadius, mask);

            for (var i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider != localCollider)
                {
                    var otherGO = hits[i].collider.gameObject;
                    var otherPlate = otherGO.GetComponent<Plate>();

                    if (subductionTracker.IsXAlreadySubductingUnderY(otherPlate, this))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static void DoRegionalRidgePush(Vector3 point)
        {
            Ray ray = new Ray(Vector3.zero, point);
            ray.origin = ray.GetPoint(50);
            ray.direction = -ray.direction;
            var mask = LayerMask.GetMask("Plates");

            var hits = Physics.SphereCastAll(ray, 3.0f, 50 - Plate.mohoRadius, mask);

            for (var i = 0; i < hits.Length; i++)
            {
                var go = hits[i].collider.gameObject;
                var plate = go.GetComponent<Plate>();
                plate.AddRidgePush(point);
            }            
        }

        public (Plate, int, int, int) IsFullyCoveredByOtherPlate(Vector3 point, MeshCollider localCollider, float radius = 0.05f)
        {
            Ray ray = new Ray(Vector3.zero, point);
            ray.origin = ray.GetPoint(50);
            ray.direction = -ray.direction;
            var mask = LayerMask.GetMask("Plates");

            var hits = Physics.SphereCastAll(ray, radius, 50 - Plate.mohoRadius, mask);

            for (var i = 0; i < hits.Length; i++)
            {
                MeshCollider otherCollider = hits[i].collider as MeshCollider;

                if (otherCollider != localCollider)
                {
                    var otherGO = otherCollider.gameObject;
                    var otherPlate = otherGO.GetComponent<Plate>();
                    Mesh otherMesh = otherCollider.sharedMesh;
                    int[] otherTriangles = otherMesh.triangles;

                    var v0 = otherTriangles[hits[i].triangleIndex * 3 + 0];
                    var v1 = otherTriangles[hits[i].triangleIndex * 3 + 1];
                    var v2 = otherTriangles[hits[i].triangleIndex * 3 + 2];

                    return (otherPlate, v0, v1, v2);
                }
            }

            return (null, -1, -1, -1);
        }

        public bool IsCoveredBySpecificPlate(Vector3 point, Plate otherPlate)
        {
            Ray ray = new Ray(Vector3.zero, point);
            ray.origin = ray.GetPoint(50);
            ray.direction = -ray.direction;
            var mask = LayerMask.GetMask("Plates");

            var hits = Physics.SphereCastAll(ray, 3.0f, 50 - Plate.mohoRadius, mask);

            for (var i = 0; i < hits.Length; i++)
            {
                MeshCollider hitCollider = hits[i].collider as MeshCollider;
                var hitGO = hitCollider.gameObject;
                var hitPlate = hitGO.GetComponent<Plate>();

                if (hitPlate == otherPlate)
                {
                    return true;
                }
            }

            return false;
        }

        private int ValidNeighborCount(int v)
        {
            var count = 0;

            foreach (var n in neighbors[v])
            {
                if (IsOceanicOrContinental(plateVertices[n]))
                {
                    count++;
                }
            }

            return count;
        }

        public List<int> GetValidNeighbors(int startingVertex, int depth)
        {
            var foundNeighbors = new Dictionary<int, bool> { { startingVertex, true } };

            for (var i = 0; i < depth; i++)
            {
                var prevs = foundNeighbors.Keys.ToList<int>();

                foreach (var v in prevs)
                {
                    foreach (var n in neighbors[v])
                    {
                        if (IsOceanicOrContinental(plateVertices[n]))
                        {
                            foundNeighbors[n] = true;
                        }
                    }
                }
            }

            return foundNeighbors.Keys.ToList<int>();
        }

        public List<int> GetExtendedNeighbors(int startingVertex, int depth)
        {
            return GetExtendedNeighbors(new List<int>(startingVertex), depth);
        }

        public List<int> GetExtendedNeighbors(List<int> startingVertices, int depth)
        {
            var foundNeighbors = new Dictionary<int, bool>();
            startingVertices.ForEach(v => foundNeighbors.Add(v, true));

            for (var i = 0; i < depth; i++)
            {
                var prevs = foundNeighbors.Keys.ToList<int>();

                foreach (var v in prevs)
                {
                    foreach (var n in neighbors[v])
                    {
                        foundNeighbors[n] = true;
                    }
                }
            }

            return foundNeighbors.Keys.ToList<int>();
        }

        public int InvalidNeighborCount(int v)
        {
            var invalids = 0;

            foreach (var n in neighbors[v])
            {
                if (IsSubducted(plateVertices[n]) || IsVirtual(plateVertices[n]) || IsSubducting(n))
                {
                    invalids++;
                }
            }

            return invalids;
        }

        public int RealNeighborCount(int v)
        {
            var reals = 0;

            foreach (var n in neighbors[v])
            {
                if (realVertices.Contains(v))
                {
                    reals++;
                }
            }

            return reals;
        }

        public int SubductingNeighborCount(int v)
        {
            var subductings = 0;

            foreach (var n in neighbors[v])
            {
                if (IsSubducting(n))
                {
                    subductings++;
                }
            }

            return subductings;
        }

        private void AddToPerimeter(int v)
        {
            perimeter.Add(v);
            perimeterMarkers.Add(CreateEventMarker(gameObject, color, plateVertices[v], 0.5f, "PerimeterPoints", "Perimeter"));
        }

        private void RemoveFromPerimeter(int v)
        {
            if (perimeter.Contains(v))
            {
                var index = perimeter.IndexOf(v);
                perimeter.RemoveAt(index);
                Destroy(perimeterMarkers[index]);
                perimeterMarkers.RemoveAt(index);

                CheckNeighborsForPerimeterness(v);
            }
        }

        private void CheckNeighborsForPerimeterness(int v)
        {
            foreach (var n in neighbors[v])
            {
                CheckForPerimeterness(n);
            }
        }

        private void CheckForPerimeterness(int v)
        {
            if (!perimeter.Contains(v) && IsPerimeterVertex(v))
            {
                AddToPerimeter(v);
            }
            else if (perimeter.Contains(v) && !IsPerimeterVertex(v))
            {
                RemoveFromPerimeter(v);
            }
        }

        public List<int> GetReal()
        {
            return realVertices;
        }

        public void AddReal(int v)
        {
            if (!realVertices.Contains(v))
            {
                realVertices.Add(v);
            }
        }

        public void RemoveReal(int v)
        {
            if (realVertices.Contains(v))
            {
                realVertices.Remove(v);
            }
        }

        public void ResurrectVertex(int v)
        {
            plateVertices[v] = vertexHomes[v] * (GetBumpyNewCrust() + Plate.hotCrustThicknessBonus);
            mesh.uv2[v] = new Vector2(plateRenderCount, plateRenderCount);
            vertexSubductionState[v] = SubductionState.None;
            realVertices.Add(v);
            CheckForPerimeterness(v);
            CheckNeighborsForPerimeterness(v);

            mesh.vertices = plateVertices;
        }

        public List<int> GetNonPerimeterNeighbors(int v)
        {
            return GetNonPerimeterNeighbors(new List<int> { v });
        }

        public List<int> GetNonPerimeterNeighbors(List<int> targets)
        {
            var found = new Dictionary<int, bool>();

            foreach (var v in targets)
            {
                foreach (var n in neighbors[v])
                {
                    if (realVertices.Contains(n) && !perimeter.Contains(n) && vertexSubductionState[n] != SubductionState.Active)
                    {
                        found[n] = true;
                    }
                }
            }

            return found.Keys.ToList();
        }

        public float GetVertexSquishedness(int v)
        {
            return (plateVertices[v] - vertexHomes[v] * plateVertices[v].magnitude).magnitude;
        }

        private void ResetVertexSquishedness(int v)
        {
            plateVertices[v] = vertexHomes[v] * plateVertices[v].magnitude;
            mesh.vertices = plateVertices;
            mesh.RecalculateBounds();
        }

        public void SquishVertex(int v, Vector3 pointOfContact, bool squishAway = true)
        {
            var diff = squishAway ? plateVertices[v] - pointOfContact : pointOfContact - plateVertices[v];
            plateVertices[v] = (plateVertices[v] + diff).normalized * plateVertices[v].magnitude;

            mesh.vertices = plateVertices;
            mesh.RecalculateBounds();            
        }

        public void DoMassSlumping(List<int> targets, Quaternion q)
        {
            for (var i = 0; i < targets.Count(); i++)
            {
                var v = targets[i];

                // Subaerial vs submarine angle of repose
                var aor = plateVertices[v].magnitude > 25.9 ? 25 : 40;

                // Find next position of the target after another application of plate movement
                var targetPrime = q * plateVertices[v];
                var targetDelta = (targetPrime - plateVertices[v]).magnitude;

                // Find the lowest neighbor, plus directional groupings while we're at it
                var maxDiff = 0.0f;
                var lowestIndex = -1;
                var aheadVertices = new List<int>();
                var sideVertices = new List<int>();
                foreach (var n in neighbors[v])
                {
                    // Don't let subducting/subducted vertices influence the surplus elevation calcs
                    if (!realVertices.Contains(n) || vertexSubductionState[n] != SubductionState.None)
                    {
                        continue;
                    }

                    var diff = plateVertices[v].magnitude - plateVertices[n].magnitude;
                    if (diff > maxDiff)
                    {
                        maxDiff = diff;
                        lowestIndex = n;
                    }

                    // Include perimeter for surplus elevation calcs, but not for mass redistribution
                    if (IsPerimeterVertex(n))
                    {
                        continue;
                    }

                    // Compare this neighbor with the target/targetPrime to see how inline it is with collisional movement
                    var dist = (plateVertices[v] - plateVertices[n]).magnitude;
                    var distPrime = (targetPrime - plateVertices[n]).magnitude;
                    var distDelta = dist - distPrime; // Positive means target is moving towards this vertex

                    // Greatest possible movement is targetDelta, directly inline with plate motion
                    if (distDelta > targetDelta * 0.5f)
                    {
                        aheadVertices.Add(n);
                    }
                    else if (distDelta > -targetDelta * 0.5f)
                    {
                        sideVertices.Add(n);
                    }
                }

                // The target might be the lowest in the area
                if (lowestIndex < 0)
                {
                    continue;
                }

                // Calculate how much has to be moved to maintain aor to lowest neighbor
                var lowestDist = (plateVertices[v].normalized * seaFloorRadius - plateVertices[lowestIndex].normalized * seaFloorRadius).magnitude;
                var highestAllowed = Mathf.Tan(aor * Mathf.Deg2Rad) * lowestDist + plateVertices[lowestIndex].magnitude;
                var surplusElevation = plateVertices[v].magnitude - highestAllowed;

                if (surplusElevation < 0)
                {
                    continue;
                }
                plateVertices[v] = plateVertices[v].normalized * (plateVertices[v].magnitude - surplusElevation);

                // Divy up the surplus elevation, giving most to the neighbors ahead in plate motion, some to the ones on the side, and none behind
                foreach (var n in aheadVertices)
                {
                    var aheadShare = surplusElevation * 0.25f / aheadVertices.Count();
                    plateVertices[n] = plateVertices[n].normalized * (plateVertices[n].magnitude + aheadShare);
                    if (!targets.Contains(n))
                        targets.Add(n);
                }

                foreach (var n in sideVertices)
                {
                    var sideShare = surplusElevation * 0.75f / sideVertices.Count();
                    plateVertices[n] = plateVertices[n].normalized * (plateVertices[n].magnitude + sideShare);
                    if (!targets.Contains(n))
                        targets.Add(n);
                }
            }

            mesh.vertices = plateVertices;
        }

        public Mesh TearOffSubductingVertices(Plate overPlate)
        {
            var newMesh = CloneMesh(mesh);
            var newPlateVertices = newMesh.vertices;

            for (int v = 0; v < mesh.vertices.Length; v++)
            {
                if ((vertexSubductionState[v] == SubductionState.Initiated || vertexSubductionState[v] == SubductionState.Active) && IsCoveredBySpecificPlate(mesh.vertices[v], overPlate))
                {
                    RemoveReal(v);
                    RemoveFromPerimeter(v);
                    vertexSubductionState[v] = SubductionState.None;
                    plateVertices[v] = vertexHomes[v] * coreRadius;
                }
                else
                {
                    newPlateVertices[v] = vertexHomes[v] * coreRadius;
                }
            }

            mesh.vertices = plateVertices;
            mesh.RecalculateBounds();
            newMesh.vertices = newPlateVertices;
            newMesh.RecalculateBounds();

            return newMesh;
        }

        private void CopyMesh(Mesh _mesh)
        {
            mesh = CloneMesh(_mesh);
            mesh.name = "Mesh " + plateNumber;

            neighbors = ExtractNeighbors(mesh);

            vertexHomes = _mesh.vertices;
            for (int i = 0; i < vertexHomes.Length; i++)
            {
                vertexHomes[i] = vertexHomes[i].normalized;
            }
        }

        public Mesh CloneMesh(Mesh _mesh)
        {
            var newMesh = new Mesh();
            newMesh.vertices = _mesh.vertices;
            newMesh.triangles = _mesh.triangles;
            newMesh.normals = _mesh.normals;
            newMesh.tangents = _mesh.tangents;
            newMesh.uv = _mesh.uv;
            newMesh.uv2 = _mesh.uv2;
            return newMesh;
        }
    }
}