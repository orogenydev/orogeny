using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

namespace Orogeny.Plates {
    public partial class Plate : MonoBehaviour {
        private Mesh mesh;
        private List<int> wavefrontVertices;
        private int plateNumber = 0;
        private System.Random rand;

        private Vector3 centerPoint;
        private float size;
        private float centralHeight;

        private Vector3 netForce;
        private Vector3 collisionForce;
        private Color color;

        private UserInterface ui;

        private Vector3[] plateVertices;
        private List<int> perimeter;
        private List<GameObject> perimeterMarkers;

        private List<int> realVertices;
        private Dictionary<int, List<int>> neighbors;
        private Vector3[] vertexNext;
        private Vector3[] vertexPrev;
        private Color[] vertexActions;

        private float[] vertexScaling;

        private List<List<int>> modificationCohorts;

        private List<GameObject> eventsMORB;
        private List<GameObject> eventsCollision;
        private List<Magma> magmas;

        public void Initialize(Mesh _mesh,
                            int _plateNumber,
                            Vector3 _centerPoint,
                            float _size,
                            float _centralHeight,
                            Color _color,
                            UserInterface _ui)
        {
            plateNumber = _plateNumber;
            centerPoint = _centerPoint;
            size = _size;
            centralHeight = _centralHeight;
            color = _color;
            ui = _ui;

            wavefrontVertices = new List<int>();
            realVertices = new List<int>();
            eventsMORB = new List<GameObject>();
            eventsCollision = new List<GameObject>();
            magmas = new List<Magma>();
            rand = new System.Random(42);

            var centerV = Plate.FindClosestVertex(_mesh.vertices, _centerPoint);
            AddTerrainToTheTerrane(_mesh, centerV);
        }

        public bool ExpandCrust(Mesh _mesh, Dictionary<int, List<int>> _neighbors) {
            var curWavefront = wavefrontVertices;
            wavefrontVertices = new List<int>();

            foreach (var v in curWavefront) {
                foreach (var n in _neighbors[v]) {
                    AddTerrainToTheTerrane(_mesh, n);
                }
            }

            return wavefrontVertices.Count > 0;
        }

        public void Finalize(Mesh _mesh, Shader shader) {           
            ProcessMesh(_mesh, shader);
            ClearNetForce();
            UpdateVertexInfrastructure();
            ResetPropagation();
        }

        private void UpdateVertexInfrastructure()
        {
            vertexNext = new Vector3[mesh.vertices.Length];
            vertexPrev = new Vector3[mesh.vertices.Length];
            vertexActions = new Color[mesh.vertices.Length];
        }

        private void AddTerrainToTheTerrane(Mesh _mesh, int v) {
            var vertices = _mesh.vertices;

            if (!IsVirtual(vertices[v])) {
                return;
            }

            var thickness = Plate.seaFloorRadius + (float)rand.NextDouble() / 6;
            float distance = ((vertices[v].normalized * Plate.seaFloorRadius) - centerPoint).magnitude;

            if (distance < size) {
                float continentalHeight = centralHeight * (size - distance) / size;
                //thickness += Plate.continentalCrustThickness + continentalHeight;
            }

            vertices[v] = vertices[v].normalized * thickness;

            _mesh.vertices = vertices;

            realVertices.Add(v);
            wavefrontVertices.Add(v);
        }

        private void ProcessMesh(Mesh _mesh, Shader shader) {
            CopyMesh(_mesh);
            mesh.MarkDynamic();

            // Virtualize all vertices not part of this terrane
            plateVertices = mesh.vertices;
            var zapped = 0;
            for (var i = 0; i < plateVertices.Length; i++) {
                if (!realVertices.Contains(i))
                {
                    zapped++;
                    plateVertices[i] = plateVertices[i].normalized * Plate.coreRadius;
                }
            }
            mesh.vertices = plateVertices;
            mesh.RecalculateBounds();

            var collider = gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;

            var filter = gameObject.AddComponent<MeshFilter>();
            filter.mesh = mesh;

            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.allowOcclusionWhenDynamic = false;
            renderer.material.shader = shader;
            renderer.material.SetVector("_WireframeColor", color);
            renderer.enabled = false;

            vertexScaling = new float[mesh.vertices.Length];
            for (int v = 0; v < vertexScaling.Length; v++)
            {
                vertexScaling[v] = 1.0f;
            }

            neighbors = ExtractNeighbors(mesh);
            perimeter = ExtractPerimeter();
            perimeterMarkers = CreatePerimeterMarkers(gameObject, color);

            Debug.Log(name + ": " + plateVertices.Length + " " + zapped);
        }

        public Mesh GetMesh()
        {
            return mesh;
        }

        public Vector3[] GetPlateVertices() {
            return plateVertices;
        }

        public Color GetColor() {
            return color;
        }

        public int GetPlateNumber() {
            return plateNumber;
        }

        public List<int> GetPerimeter() {
            return perimeter;
        }

        public List<int> GetExtendedPerimeter(int depth) {
            var extendedPerimeter = new List<int>(perimeter);

            for (int i = 0; i < depth; i++)
            {
                var additions = new List<int>();

                foreach (var v in extendedPerimeter)
                {
                    foreach (var n in neighbors[v])
                    {
                        if (!IsVirtual(plateVertices[n]))
                        {
                            additions.Add(n);
                        }
                    }
                }

                foreach (var v in additions)
                {
                    if (!extendedPerimeter.Contains(v))
                    {
                        extendedPerimeter.Add(v);
                    }
                }
            }

            return extendedPerimeter;
        }

        public void ClearNetForce()
        {
            netForce = Vector3.zero;
            collisionForce = Vector3.zero;
        }

        public void AddForce(Vector3 point, Vector3 force) {
            netForce += force;
        }

        public Vector3 GetNetForce() {
            return netForce;
        }

        public Vector3 GetScaledNetForce() {
            float scale = 0.1f;

            if (plateNumber == 0) {
                return Vector3.left * scale;
            } else {
                return Vector3.right * scale;
            }
            //return netForce / mesh.vertices.Length;
        }

        public void ApplyForces(int phase)
        {
            float scale = 0.3f;

            var closest = (int)plateVertices.Length / 2; // ridiculous hack
            var q = CreateVertexRotation(GetScaledNetForce(), mesh.normals[closest], scale);

            for (int i = phase; i < plateVertices.Length; i += 2)
            {
                var s = vertexScaling[i];
                var v = q * plateVertices[i];
                plateVertices[i] = Vector3.Scale(v, new Vector3(s, s, s));
            }

            foreach (var magma in magmas) {
                var position = q * magma.GetPosition();
                magma.SetPosition(position);
            }

            for (int i = 0; i < perimeter.Count; i++) {
                perimeterMarkers[i].transform.position = plateVertices[perimeter[i]];
            }

            mesh.vertices = plateVertices;
            mesh.RecalculateBounds();

            gameObject.GetComponent<MeshCollider>().sharedMesh = null;
            gameObject.GetComponent<MeshCollider>().sharedMesh = mesh;
        }

        public void CombineCollisionForces(Vector3 scaledOtherForce) {
            collisionForce = GetScaledNetForce() - scaledOtherForce;
        }

        public void SetMeshDisplay(bool value) {
            gameObject.GetComponent<MeshRenderer>().enabled = value;
        }

        public Magma AddMagma(Vector3 position, MagmaType type)
        {
            var magma = Magma.Create(type, position, gameObject);
            magmas.Add(magma);
            magma.DoUplift(this);
            ui.TriggerBreakpoint(Breakpoint.MagmaBirth);

            return magma;
        }

        public void KillMagma(Magma magma) {
            var oldMarker = magma.Kill(gameObject);
            Destroy(oldMarker);
            ui.TriggerBreakpoint(Breakpoint.MagmaDeath);
        }

        void OnDrawGizmos () {
            // Scale things down to be inside the sphere, makes it easier to see the meshes
            //var gizmoScale = 1f;

            // if (ui.IsDisplayFeatureOn(DisplayFeature.CollisionPoints)) {
            //     Gizmos.color = Color.yellow;
            //     foreach (var contactPoint in contactPoints) {
            //         Gizmos.DrawSphere(contactPoint / gizmoScale, 0.5f / gizmoScale);
            //     }
            // }

            // if (ui.IsDisplayFeatureOn(DisplayFeature.Magmas)) {
            //     foreach (var magma in magmas) {
            //         if (magma.GetActive()) {
            //             Gizmos.color = Color.magenta;
            //         } else {
            //             Gizmos.color = Color.grey;
            //         }
            //         Gizmos.DrawSphere(magma.GetPosition() / gizmoScale, 1.0f / gizmoScale);
            //     }
            // }

            // if (ui.IsDebugFeatureOn(DebugFeature.MORB)) {
            //     Gizmos.color = new Color(1.0f, 0f, 0f, 0.5f);
            //     foreach (var morb in latestMORBs) {
            //         Gizmos.DrawSphere(morb / gizmoScale, 0.5f / gizmoScale);
            //     }
            // }
        }

        public void ClearContactPoints() {
            ClearEvents(eventsCollision);
        }

/*         public void ProcessCollision(Vector3 point, Plate other) {
            eventsCollision.Add(CreateEventMarker(gameObject, color, point, 0.5f, "EventMarkers", "EventCollision"));

            CombineCollisionForces(other.GetScaledNetForce());
            ResetPropagation();

            var localVertexIndex = FindClosestPerimeter(point);
            var otherVertexIndex = other.FindClosestPerimeter(point);

            if (localVertexIndex < 0 || otherVertexIndex < 0) {
                return;
            }

            var localContinental = IsContinental(plateVertices[localVertexIndex]);
            var otherContinental = IsContinental(other.GetPlateVertices()[otherVertexIndex]);

            if (localContinental && otherContinental) {
                ContinentContinentCollision(point, other);
            } else if (!localContinental && otherContinental) {
                OceanicContinentCollision(point, other);
            } else if (localContinental && !otherContinental) {
                ContinentOceanicCollision(point, other);
            } else {
                OceanicOceanicCollision(point, other);
            }
        } */

        // One of our perimeters vertices v has hit another triangle (v0, v1, v2) at point
        public void ProcessCollision(Vector3 point, int v, Plate other, int v0, int v1, int v2)
        {
            ui.TriggerBreakpoint(Breakpoint.AnyCollision);
            eventsCollision.Add(CreateEventMarker(gameObject, new Color(0f, 0f, 1.0f, 0.5f), point, 0.5f, "EventMarkers", "EventCollision"));

            CombineCollisionForces(other.GetScaledNetForce());
            ResetPropagation();

            var localContinental = IsContinental(plateVertices[v]);
            var otherContinental = IsContinental(other.GetPlateVertices()[v0]) || IsContinental(other.GetPlateVertices()[v1]) || IsContinental(other.GetPlateVertices()[v2]);

            if (localContinental && otherContinental)
            {
                //ContinentContinentCollision(point, other);
            }
            else if (!localContinental && otherContinental)
            {
                //OceanicContinentCollision(point, other);
            }
            else if (localContinental && !otherContinental)
            {
                //ContinentOceanicCollision(point, other);
            }
            else
            {
                OceanicOceanicCollision(v, other);
            }
        }

        // One of our triangles (v0, v1, v2) has been hit by another perimeter vertex v at point
        public void ProcessCollision(Vector3 point, int v0, int v1, int v2, Plate other, int v)
        {
            ui.TriggerBreakpoint(Breakpoint.AnyCollision);
            eventsCollision.Add(CreateEventMarker(gameObject, new Color(0f, 0f, 1.0f, 0.5f), point, 0.5f, "EventMarkers", "EventCollision"));

            CombineCollisionForces(other.GetScaledNetForce());
            ResetPropagation();

            var localContinental = IsContinental(other.GetPlateVertices()[v0]) || IsContinental(other.GetPlateVertices()[v1]) || IsContinental(other.GetPlateVertices()[v2]);
            var otherContinental = IsContinental(plateVertices[v]);

            if (localContinental && otherContinental)
            {
                //ContinentContinentCollision(point, other);
            }
            else if (!localContinental && otherContinental)
            {
                //OceanicContinentCollision(point, other);
            }
            else if (localContinental && !otherContinental)
            {
                //ContinentOceanicCollision(point, other);
            }
            else
            {
                OceanicOceanicCollision(v0, v1, v2, other);
            }
        }

        private void AddToCohort(int vertex, int cohort)
        {
            while (cohort >= modificationCohorts.Count)
            {
                modificationCohorts.Add(new List<int>());
            }

            if (!modificationCohorts[cohort].Contains(vertex))
            {
                modificationCohorts[cohort].Add(vertex);
            }
        }

        private bool IsInAnyCohort(int vertex) {
            foreach (var cohort in modificationCohorts) {
                if (cohort.Contains(vertex)) {
                    return true;
                }
            }

            return false;
        }

        public void ResetPropagation() {
            for (int i = 0; i < plateVertices.Length; i++) {
                vertexNext[i] = plateVertices[i];
                vertexPrev[i] = plateVertices[i];
                vertexActions[i] = color;
            }

            modificationCohorts = new List<List<int>>();
            modificationCohorts.Add(new List<int>());
        }

        public void UpdateVertices() {
            var uv2 = mesh.uv2;

            var melted = new List<int>();

            foreach (var v in realVertices)
            {
                CheckForPerimeterness(v);

                if (plateVertices[v].magnitude < Plate.mohoRadius)
                {
                    melted.Add(v);
                }

                var age = Time.frameCount - uv2[v].x;
                if (age < Plate.framesToCool)
                {
                    var delta = Time.frameCount - uv2[v].y;
                    var shrinkage = delta * (Plate.hotCrustThicknessBonus) / Plate.framesToCool;
                    plateVertices[v] = plateVertices[v].normalized * (plateVertices[v].magnitude - shrinkage);
                    uv2[v].y = Time.frameCount;
                }
            }

            foreach (var v in melted)
            {
                RemoveReal(v);                
                RemoveFromPerimeter(v);
                plateVertices[v] = plateVertices[v].normalized * Plate.coreRadius;
            }

            mesh.vertices = plateVertices;
            mesh.uv2 = uv2;
            mesh.RecalculateBounds();
        }

        public void DoVolcanism() {
            DoMagmaticScan();
            UpdateVolcanism();
        }

        private void DoMagmaticScan() {
            // Look down at every point (later optmize to just near the perimeter if needed)
            // If there is another plate down there within a magic distance range
            // Randomly add a magma if there isn't already
            // Uplift area somewhat
            var count = realVertices.Count;
            var mask = LayerMask.GetMask("Plates");
            var castCommands = new NativeArray<SpherecastCommand>(count, Allocator.Persistent);
            var castHits = new NativeArray<RaycastHit>(count * 3, Allocator.Persistent);

            for (int j = 0; j < count; j++) {
                // Standard out-and-back routine
                var point = plateVertices[realVertices[j]];
                Ray ray = new Ray(Vector3.zero, point);
                ray.origin = ray.GetPoint(50);
                ray.direction = -ray.direction;
                castCommands[j] = new SpherecastCommand(ray.origin, 0.1f, ray.direction, 50 - Plate.mohoRadius, mask);
            }
            
            JobHandle handle = SpherecastCommand.ScheduleBatch(castCommands,
                                                            castHits, 
                                                            1,
                                                            3,
                                                            default(JobHandle));
            handle.Complete();

            var collider = gameObject.GetComponent<MeshCollider>();
            for (int j = 0; j < castHits.Length; j += 3) {
                for (int k = 0; k < 3; k++)
                {
                    var hit = castHits[j + k];

                    if (hit.collider != null && hit.collider != collider)
                    {
                        MeshCollider meshCollider = hit.collider as MeshCollider;
                        Mesh mesh = meshCollider.sharedMesh;
                        Vector3[] meshVertices = mesh.vertices;
                        int[] meshTriangles = mesh.triangles;

                        var v0 = meshTriangles[hit.triangleIndex * 3 + 0];
                        var v1 = meshTriangles[hit.triangleIndex * 3 + 1];
                        var v2 = meshTriangles[hit.triangleIndex * 3 + 2];

                        Vector3 point0 = meshVertices[v0];
                        Vector3 point1 = meshVertices[v1];
                        Vector3 point2 = meshVertices[v2];

                        if (IsVirtual(point0) || IsVirtual(point1) || IsVirtual(point2))
                        {
                            continue;
                        }

                        if (hit.point.magnitude < Plate.magmatismUpperLimit &&
                            hit.point.magnitude > Plate.magmatismLowerLimit)
                        {

                            if (MagmaSubduction.ShouldRandomlyGenerate())
                            {
                                var position = hit.point.normalized * Plate.magmatismDepth;
                                AddMagma(position, MagmaType.Subduction);
                            }
                        }
                    }
                }
            }

            mesh.vertices = plateVertices;
            mesh.RecalculateBounds();

            castCommands.Dispose();
            castHits.Dispose();
        }

        private void UpdateVolcanism() {
            // For each active magma in the plate
            // If (high probability) random, add to the plate above
            // If (low probability) random, set to inactive

            foreach (var magma in magmas) {
                if (magma.GetActive()) {
                    if (magma.ShouldRandomlyErupt()) {
                        magma.DoUplift(this);
                        ui.TriggerBreakpoint(Breakpoint.MagmaEruption);
                    }

                    if (magma.ShouldRandomlyDie()) {
                        KillMagma(magma);
                    }
                }
            }

            mesh.vertices = plateVertices;
            mesh.RecalculateBounds();
        }

        public void MORBingTime() {
            Vector2[] uv2 = mesh.uv2;
            ClearEvents(eventsMORB);

            var collider = gameObject.GetComponent<MeshCollider>();
            var newMORBS = false;
            for (int pIndex = 0; pIndex < perimeter.Count; pIndex++) {
                var p = perimeter[pIndex];

                foreach (var n in neighbors[p]) {
                    if (IsVirtual(plateVertices[n]) &&
                        IsOnlyVirtualAtPoint(plateVertices[n], collider) &&
                        ValidNeighborCount(n) > 1) {

                        var newCrustPos = plateVertices[n].normalized * (Plate.seaFloorRadius + Plate.hotCrustThicknessBonus);

                        // Need to check halfway between this potential new vertex and all its valid neighbors, to catch intruding spikes of other plates
                        var clearAtHalfwayPoints = true;
                        foreach (var n2 in neighbors[n])
                        {
                            if (IsOceanicOrContinental(plateVertices[n2]))
                            {
                                //eventsMORB.Add(CreateEventMarker(gameObject, new Color(1.0f, 0f, 1.0f, 0.5f), plateVertices[n2], 1f, "EventMarkers", "EventMORB"));
                                
                                var midpoint = (newCrustPos + plateVertices[n2]) / 2;
                                //eventsMORB.Add(CreateEventMarker(gameObject, new Color(0f, 1.0f, 0f, 0.5f), midpoint, 1f, "EventMarkers", "EventMORB"));
                                clearAtHalfwayPoints &= IsOnlyVirtualAtPoint(midpoint, collider);
                            }
                        }

                        if (!clearAtHalfwayPoints)
                            continue;
                        //eventsMORB.Add(CreateEventMarker(gameObject, new Color(1.0f, 0f, 1.0f, 0.5f), Vector3.zero, coreRadius * 2, "EventMarkers", "EventMORB"));

                        plateVertices[n] = newCrustPos;
                        uv2[n] = new Vector2(Time.frameCount, Time.frameCount);
                        vertexScaling[n] = 1.0f;
                        realVertices.Add(n);
                        AddToPerimeter(n);
                        CheckNeighborPerimeterStatus(n);

                        mesh.vertices = plateVertices;
                        mesh.uv2 = uv2;
                        mesh.RecalculateBounds();

                        newMORBS = true;
                        eventsMORB.Add(CreateEventMarker(gameObject, new Color(1.0f, 0f, 0f, 0.5f), newCrustPos, 1f, "EventMarkers", "EventMORB"));
                    }
                }
            }

            if (newMORBS) {
                ui.TriggerBreakpoint(Breakpoint.MORB);
            }
        }
    }
}