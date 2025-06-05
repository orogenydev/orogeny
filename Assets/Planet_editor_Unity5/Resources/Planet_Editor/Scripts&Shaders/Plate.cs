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

        private List<int> experimeter;
        private List<int> realVertices;
        private Dictionary<int, List<int>> neighbors;
        private Vector3[] vertexNext;
        private Vector3[] vertexPrev;
        private Color[] vertexActions;
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
                            UserInterface _ui) {
            plateNumber = _plateNumber;
            centerPoint = _centerPoint;
            size = _size;
            centralHeight = _centralHeight;
            color = _color;
            ui = _ui;

            wavefrontVertices = new List<int>();
            experimeter = new List<int>();
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

        private void UpdateVertexInfrastructure() {
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
                thickness += Plate.continentalCrustThickness + continentalHeight;
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
                if (!realVertices.Contains(i)) {
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

        public void ClearNetForce() {
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

            for (int i = phase; i < plateVertices.Length; i += 2) {
                plateVertices[i] = q * plateVertices[i];
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

        public void ProcessCollision(Vector3 point, Plate other) {
            eventsCollision.Add(CreateEventMarker(gameObject, new Color(0f, 1.0f, 1.0f, 0.5f), point, 0.75f, "EventMarkers", "EventCollision"));

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
        }

        private void AddToCohort(int vertex, int cohort) {
            while (cohort >= modificationCohorts.Count) {
                modificationCohorts.Add(new List<int>());
            }

            if (!modificationCohorts[cohort].Contains(vertex)) {
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

            foreach (var v in realVertices) {
                CheckForPerimeterness(v);

                if (plateVertices[v].magnitude < Plate.mohoRadius) {
                    RemoveFromPerimeter(v);
                    RemoveReal(v);
                    plateVertices[v] = plateVertices[v].normalized * Plate.coreRadius;
                }
                
                var age = Time.frameCount - uv2[v].x;
                if (age < Plate.framesToCool) {
                    var delta = Time.frameCount - uv2[v].y;                    
                    var shrinkage = delta * (Plate.hotCrustThicknessBonus) / Plate.framesToCool;
                    plateVertices[v] = plateVertices[v].normalized * (plateVertices[v].magnitude - shrinkage);
                    uv2[v].y = Time.frameCount;
                }
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
            var raycastCommands = new NativeArray<RaycastCommand>(count, Allocator.Persistent);
            var raycastHits = new NativeArray<RaycastHit>(count, Allocator.Persistent);

            for (int j = 0; j < count; j++) {
                raycastCommands[j] = new RaycastCommand(plateVertices[realVertices[j]], -plateVertices[realVertices[j]], 10, mask);
            }
            
            JobHandle handle = RaycastCommand.ScheduleBatch(raycastCommands, 
                                                            raycastHits, 
                                                            1, 
                                                            default(JobHandle));
            handle.Complete();

            var normals = mesh.normals;

            var collider = gameObject.GetComponent<MeshCollider>();
            for (int j = 0; j < count; j++) {
                RaycastHit hit = raycastHits[j];
                if (hit.collider != null && hit.collider != collider) {
                    if (hit.point.magnitude < Plate.magmatismUpperLimit &&
                        hit.point.magnitude > Plate.magmatismLowerLimit) {

                        if (rand.NextDouble() < Plate.magmatismBirthChance) {
                            var position = hit.point.normalized * Plate.magmatismDepth;
                            var magma = new Magma(gameObject, position);
                            magmas.Add(magma);
                            UpliftTerrane(normals, position, 0.2f, 1.5f);
                            ui.TriggerBreakpoint(Breakpoint.MagmaBirth);
                        }
                    }
                }
            }

            mesh.vertices = plateVertices;
            mesh.RecalculateBounds();

            raycastCommands.Dispose();
            raycastHits.Dispose();
        }

        private void UpdateVolcanism() {
            // For each active magma in the plate
            // If (high probability) random, add to the plate above
            // If (low probability) random, set to inactive
            var normals = mesh.normals;

            foreach (var magma in magmas) {
                if (magma.GetActive()) {
                    if (rand.NextDouble() < Plate.magmatismEruptionChance) {
                        UpliftTerrane(normals, magma.GetPosition(), 0.01f, 0);
                        ui.TriggerBreakpoint(Breakpoint.MagmaEruption);
                    }

                    if (rand.NextDouble() < Plate.magmatismDeathChance) {
                        var oldMarker = magma.Kill(gameObject);
                        Destroy(oldMarker);
                        ui.TriggerBreakpoint(Breakpoint.MagmaDeath);
                    }
                }
            }

            mesh.vertices = plateVertices;
            mesh.RecalculateBounds();
        }

        public void MORBingTime() {
            Vector2[] uv2 = mesh.uv2;
            ClearEvents(eventsMORB);
 
            var newMORBS = false;
            // Iterate through a copy of the perimeter list as it might be modified during the loop
            List<int> currentPerimeter = new List<int>(perimeter);

            foreach (var p in currentPerimeter) {
                // Check if p is still a perimeter vertex, as it might have been removed by previous iterations
                if (!perimeter.Contains(p)) {
                    continue;
                }

                // It's important to collect neighbors first, as modifying plateVertices and perimeter
                // inside the loop might affect subsequent neighbor checks or IsVirtual status.
                List<int> neighborsOfP = new List<int>(neighbors[p]);

                foreach (var n in neighborsOfP) {
                    if (IsVirtual(plateVertices[n]) &&
                        IsOnlyVirtualAtPoint(plateVertices[n]) &&
                        ValidNeighborCount(n) > 1) { // Assuming ValidNeighborCount refers to real neighbors

                        plateVertices[n] = plateVertices[n].normalized * (Plate.seaFloorRadius + Plate.hotCrustThicknessBonus);
                        uv2[n] = new Vector2(Time.frameCount, Time.frameCount);
                        
                        if (!realVertices.Contains(n)) { // Ensure we don't add duplicates if logic allows
                            realVertices.Add(n);
                        }

                        AddToPerimeter(n); // This should add n to the main perimeter list
                                           // and remove it from virtual status implicitly or explicitly.

                        // After adding n, n is now a real vertex.
                        // We need to update the perimeter status of its neighbors, including p.
                        CheckNeighborPerimeterStatus(n);
                        CheckPerimeterness(p); // p might no longer be a perimeter vertex if all its virtual neighbors are filled.

                        eventsMORB.Add(CreateEventMarker(gameObject, new Color(1.0f, 0f, 0f, 0.5f), plateVertices[n], 0.75f, "EventMarkers", "EventMORB"));
                        newMORBS = true;
                    }
                }
            }

            mesh.vertices = plateVertices;
            mesh.uv2 = uv2;
            mesh.RecalculateBounds();

            if (newMORBS) {
                ui.TriggerBreakpoint(Breakpoint.MORB);
            }
        }
    }
}