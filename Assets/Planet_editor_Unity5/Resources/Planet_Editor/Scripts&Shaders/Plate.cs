using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

using Orogeny.Maps;

namespace Orogeny.Plates {
    public partial class Plate : MonoBehaviour
    {
        public Mesh mesh;
        private List<int> wavefrontVertices = new List<int>();
        public int plateNumber = 0;
        private System.Random rand;

        private Vector3 netForce;
        private Vector3 lockedScaledNetForce = Vector3.zero;
        private Vector3 collisionForce;
        private Color color;

        private UserInterface ui;

        private Vector3[] plateVertices;
        private List<int> perimeter;
        private List<GameObject> perimeterMarkers = new List<GameObject>();
        private List<int> realVertices = new List<int>();
        private Dictionary<int, List<int>> neighbors;
        private Vector3[] vertexHomes; // Will keep the original position (normalized) of each vertex, so they can squish around a bit as needed and still be reset
        private int plateRenderCount = 0;

        private Vector3[] vertexNext;
        private Vector3[] vertexPrev;

        private Vector3 prevNetScaledForce = Vector3.zero;
        public SubductionState[] vertexSubductionState;
        private Queue<List<Vector3>> historicalMORBs;
        private List<Vector3> recentMORBs;
        private List<Mesh> tornOffMeshes;

        private List<List<int>> modificationCohorts;

        private List<GameObject> eventsMORB = new List<GameObject>();
        private List<GameObject> eventsCollision = new List<GameObject>();
        private List<Magma> magmas = new List<Magma>();

        public void Initialize(Mesh _mesh,
                            int _plateNumber,
                            string _name,
                            Vector3 _centerPoint,
                            float _size,
                            float _centralHeight,
                            Color _color,
                            UserInterface _ui)
        {
            CommonInitialization(_plateNumber, _name, _mesh, _color, _ui);

            var centerV = FindClosestVertex(_centerPoint);
            AddTerrainToTheTerrane(centerV, new List<Plate>());
        }

        public void Initialize(int _plateNumber, string _name, Mesh _mesh, Color _color, UserInterface _ui)
        {
            CommonInitialization(_plateNumber, _name, _mesh, _color, _ui);
        }

        private void CommonInitialization(int _plateNumber, string _name, Mesh _mesh, Color _color, UserInterface _ui)
        {
            CopyMesh(_mesh);
            mesh.MarkDynamic();
            plateNumber = _plateNumber;
            name = _name;
            color = _color;
            ui = _ui;

            tornOffMeshes = new List<Mesh>();
            rand = new System.Random(42);
        }

        public bool ExpandCrust(List<Plate> plates)
        {
            var curWavefront = wavefrontVertices;
            wavefrontVertices = new List<int>();

            foreach (var v in curWavefront)
            {
                foreach (var n in neighbors[v])
                {
                    AddTerrainToTheTerrane(n, plates);
                }
            }

            return wavefrontVertices.Count > 0;
        }

        public void Finalize(Shader shader)
        {
            ProcessMesh(shader);
            ClearNetForce();
            UpdateVertexInfrastructure();
            ResetPropagation();
        }

        private void UpdateVertexInfrastructure()
        {
            vertexNext = new Vector3[mesh.vertices.Length];
            vertexPrev = new Vector3[mesh.vertices.Length];
        }

        private void AddTerrainToTheTerrane(int v, List<Plate> plates)
        {
            // Only claim a vertex for this plate if it is virtual in all other plates
            if (!plates.All(p => IsVirtual(p.mesh.vertices[v])))
            {
                return;
            }

            plateVertices = mesh.vertices;
            var uv2 = mesh.uv2;

            // XXX DEV HACK
            var frameCountAge = -framesToCool / (GetPlateNumber() + 1);
            // var frameCountAge = -framesToCool;
            // if (plateNumber == 0)
            // {
            //     frameCountAge = -framesToCool;
            // }
            // if (plateNumber == 1)
            // {
            //     frameCountAge = 0;
            // }

            var thickness = GetBumpyNewCrust() + hotCrustThicknessBonus * (1 - Mathf.Abs(frameCountAge) / framesToCool);
            plateVertices[v] = plateVertices[v].normalized * thickness;
            uv2[v] = new Vector2(frameCountAge, 0);

            mesh.vertices = plateVertices;
            mesh.uv2 = uv2;

            wavefrontVertices.Add(v);
        }

        private float GetBumpyNewCrust()
        {
            // Range seaFloorMinNormalRadius to seaFloorMaxNormalRadius
            var range = seaFloorMaxNormalRadius - seaFloorMinNormalRadius;
            return seaFloorMinNormalRadius + (float)rand.NextDouble() * range;
        }

        public void AddBump(float lon, float lat, float size, float height)
        {
            var position = MapProjection.Geodetic2Vector(lon, lat).normalized * Plate.seaFloorRadius;
            var v = FindClosestVertex(position);
            AddBump(v, size, height);
        }

        public void AddBump(int centerVertex, float size, float height)
        {
            plateVertices = mesh.vertices;

            for (var v = 0; v < plateVertices.Count(); v++)
            {
                float dist = (plateVertices[v] - plateVertices[centerVertex]).magnitude;

                if (dist < size && !perimeter.Contains(v))
                {
                    float scale = (size - dist) / size;
                    float continentalHeight = height * scale * scale;

                    var thickness = Plate.continentalCrustThickness + continentalHeight;
                    plateVertices[v] = plateVertices[v].normalized * (plateVertices[v].magnitude + thickness);
                }
            }
            
            mesh.vertices = plateVertices;
            mesh.RecalculateBounds();
        }

        public void InitializeStuff()
        {
            plateVertices = mesh.vertices;
            for (var v = 0; v < plateVertices.Length; v++)
            {
                if (!IsVirtual(plateVertices[v]))
                {
                    realVertices.Add(v);
                }
            }

            vertexSubductionState = new SubductionState[mesh.vertices.Length];
            for (int v = 0; v < vertexSubductionState.Length; v++)
            {
                vertexSubductionState[v] = SubductionState.None;
            }

            historicalMORBs = new Queue<List<Vector3>>();
            for (int i = 0; i < 25; i++)
            {
                historicalMORBs.Enqueue(new List<Vector3>());
            }
            recentMORBs = new List<Vector3>();

            perimeter = ExtractPerimeter();
            perimeterMarkers = CreatePerimeterMarkers(gameObject, color);
        }

        private void ProcessMesh(Shader shader)
        {
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
        }

        public Mesh GetMesh()
        {
            return mesh;
        }

        public Vector3[] GetPlateVertices()
        {
            return plateVertices;
        }

        public void SetPlateVertices(Vector3[] vertices)
        {
            plateVertices = vertices;
            mesh.vertices = vertices;
        }

        public Color GetColor()
        {
            return color;
        }

        public int GetPlateNumber()
        {
            return plateNumber;
        }

        public List<int> GetPerimeter()
        {
            return perimeter;
        }

        public List<int> GetExtendedPerimeter(int depth)
        {
            var extendedPerimeter = new Dictionary<int, bool>();
            perimeter.ForEach(v => extendedPerimeter[v] = true);

            for (int i = 0; i < depth; i++)
            {
                foreach (var v in extendedPerimeter.Keys.ToList())
                {
                    foreach (var n in neighbors[v])
                    {
                        if (!IsVirtual(plateVertices[n]))
                        {
                            extendedPerimeter[n] = true;
                        }
                    }
                }
            }

            return extendedPerimeter.Keys.ToList();
        }

        public Queue<List<Vector3>> GetHistoricalMORBs()
        {
            return historicalMORBs;
        }

        public void AddRidgePush(Vector3 point)
        {
            recentMORBs.Add(point);
        }

        public void ArchiveRecentMORBs()
        {
            historicalMORBs.Dequeue();
            historicalMORBs.Enqueue(recentMORBs);
            recentMORBs = new List<Vector3>();
        }

        public void AddTornMesh(Mesh mesh)
        {
            tornOffMeshes.Add(mesh);
        }

        public List<Mesh> GetTornMeshes()
        {
            var t = tornOffMeshes;
            tornOffMeshes = new List<Mesh>();
            return t;
        }

        public int GetPlateRenderCount()
        {
            return plateRenderCount;
        }

        public void SetPlateRenderCount(int _plateRenderCount)
        {
            plateRenderCount = _plateRenderCount;
        }

        public void ClearNetForce()
        {
            netForce = Vector3.zero;
            collisionForce = Vector3.zero;
        }

        public void AddForce(Vector3 force)
        {
            netForce += force;
        }

        public Vector3 GetNetForce()
        {
            return netForce;
        }

        public Vector3 GetScaledNetForce()
        {
            if (lockedScaledNetForce.magnitude > 0)
            {
                return lockedScaledNetForce;
            }

            // if (plateNumber > 0)
            // {
            //     return Vector3.zero;
            // }

            return GetNetForce() / mesh.vertices.Length;
        }

        // For torn off subducting plates that need to just keep going
        public void LockScaledNetForce(Vector3 force)
        {
            lockedScaledNetForce = force;
        }

        public Quaternion GetRotation()
        {
            var axis = GetScaledNetForce();
            return Quaternion.AngleAxis(axis.magnitude, axis);
        }

        public Vector3 GetPrevScaledNetForce()
        {
            return prevNetScaledForce;
        }

        public void UpdatePrevScaledNetForce()
        {
            prevNetScaledForce = GetScaledNetForce();
        }

        public void ApplyForces(int phase)
        {
            var q = GetRotation();

            for (int i = phase; i < plateVertices.Length; i += 2)
            {
                var point = q * plateVertices[i];

                if (vertexSubductionState[i] == SubductionState.Active)
                {
                    var datumIndex = FindClosestPerimeter(plateVertices[i]);
                    if (datumIndex < 0)
                        datumIndex = i;

                    // Measure movement from the nearest perimeter point
                    // This lets us scale the subduction angle (if any) according to age/density better
                    var datum = plateVertices[datumIndex];
                    var dist1 = (datum - plateVertices[i]).magnitude;
                    var dist2 = (datum - point).magnitude;

                    var dist = Mathf.Abs(dist1 - dist2);
                    var age = Mathf.Clamp(plateRenderCount - mesh.uv2[i].x, 0, framesToCool);
                    // Angle is scaled between 30 and 70 degrees, 
                    var angle = 30.0f + 50.0f * age / framesToCool;
                    var drop = Mathf.Tan(angle * Mathf.Deg2Rad) * dist;
                    point = point.normalized * (point.magnitude - drop);
                }
                plateVertices[i] = point;

                vertexHomes[i] = q * vertexHomes[i];
            }

            foreach (var magma in magmas)
            {
                var position = q * magma.GetPosition();
                magma.SetPosition(position);
            }

            for (int i = 0; i < perimeter.Count; i++)
            {
                perimeterMarkers[i].transform.position = plateVertices[perimeter[i]];
            }

            mesh.vertices = plateVertices;
            mesh.RecalculateBounds();

            gameObject.GetComponent<MeshCollider>().sharedMesh = null;
            gameObject.GetComponent<MeshCollider>().sharedMesh = mesh;

            plateRenderCount++;
        }

        public void CombineCollisionForces(Vector3 scaledOtherForce)
        {
            collisionForce = GetScaledNetForce() - scaledOtherForce;
        }

        public void SetMeshDisplay(PlateMeshDisplayMode mode)
        {
            switch (mode)
            {
                case PlateMeshDisplayMode.None:
                    gameObject.GetComponent<MeshRenderer>().enabled = false;
                    break;
                case PlateMeshDisplayMode.Surface:
                    gameObject.GetComponent<MeshRenderer>().enabled = true;
                    gameObject.GetComponent<MeshRenderer>().material.SetFloat("_CullRadius", trenchRadius);
                    break;
                case PlateMeshDisplayMode.Subducting:
                    gameObject.GetComponent<MeshRenderer>().enabled = true;
                    gameObject.GetComponent<MeshRenderer>().material.SetFloat("_CullRadius", mohoRadius);
                    break;
                case PlateMeshDisplayMode.Entire:
                    gameObject.GetComponent<MeshRenderer>().enabled = true;
                    gameObject.GetComponent<MeshRenderer>().material.SetFloat("_CullRadius", coreRadius - 1);
                    break;
            }
        }

        public void SetDeepMeshDisplay(bool value)
        {
            gameObject.GetComponent<MeshRenderer>().material.SetFloat("_CullRadius", value ? 10.0f : 13.0f);
        }

        public Magma AddMagma(Vector3 position, MagmaType type)
        {
            var magma = Magma.Create(type, position, gameObject);
            magmas.Add(magma);
            magma.DoUplift(this);
            ui.TriggerBreakpoint(Breakpoint.MagmaBirth);

            return magma;
        }

        public void KillMagma(Magma magma)
        {
            var marker = magma.Kill(gameObject);
            Destroy(marker);
            ui.TriggerBreakpoint(Breakpoint.MagmaDeath);
        }

        public void ClearContactPoints()
        {
            ClearEvents(eventsCollision);
        }

        // One of our perimeters vertices v has hit another triangle (v0, v1, v2) at point
        public void ProcessCollision(Vector3 point, int v, Plate other, int v0, int v1, int v2, SubductionTracker subductionTracker)
        {
            ui.TriggerBreakpoint(Breakpoint.AnyCollision);
            eventsCollision.Add(CreateEventMarker(gameObject, new Color(0f, 0f, 1.0f, 0.5f), point, 0.5f, "EventMarkers", "EventCollision"));

            CombineCollisionForces(other.GetScaledNetForce());
            ResetPropagation();

            var localContinental = IsContinental(plateVertices[v]);
            var otherContinental = IsContinental(other.GetPlateVertices()[v0]) || IsContinental(other.GetPlateVertices()[v1]) || IsContinental(other.GetPlateVertices()[v2]);

            if (localContinental && otherContinental)
            {
                //Debug.Log("!!! 1");
                //ContinentContinentCollision(point, other);
            }
            else if (!localContinental && otherContinental)
            {
                //Debug.Log("!!! 2");
                OceanicContinentCollision(v, other, v0, v1, v2, subductionTracker);
            }
            else if (localContinental && !otherContinental)
            {
                //Debug.Log("!!! 3");
                //ContinentOceanicCollision(point, other);
            }
            else
            {
                OceanicOceanicCollision(v, other, v0, v1, v2, subductionTracker);
            }
        }

        // One of our triangles (v0, v1, v2) has been hit by another perimeter vertex v at point
        public void ProcessCollision(Vector3 point, int v0, int v1, int v2, Plate other, int v, SubductionTracker subductionTracker)
        {
            ui.TriggerBreakpoint(Breakpoint.AnyCollision);
            eventsCollision.Add(CreateEventMarker(gameObject, new Color(0f, 0f, 1.0f, 0.5f), point, 0.5f, "EventMarkers", "EventCollision"));

            CombineCollisionForces(other.GetScaledNetForce());
            ResetPropagation();

            var localContinental = IsContinental(other.GetPlateVertices()[v0]) || IsContinental(other.GetPlateVertices()[v1]) || IsContinental(other.GetPlateVertices()[v2]);
            var otherContinental = IsContinental(plateVertices[v]);

            if (localContinental && otherContinental)
            {
                //Debug.Log("!!! 4");
                //ContinentContinentCollision(point, other);
            }
            else if (!localContinental && otherContinental)
            {
                //Debug.Log("!!! 5");
                //OceanicContinentCollision(point, other);
            }
            else if (localContinental && !otherContinental)
            {
                //Debug.Log("!!! 6");
                //ContinentOceanicCollision(point, other);
            }
            else
            {
                OceanicOceanicCollision(v0, v1, v2, other, v, subductionTracker);
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

        private bool IsInAnyCohort(int vertex)
        {
            foreach (var cohort in modificationCohorts)
            {
                if (cohort.Contains(vertex))
                {
                    return true;
                }
            }

            return false;
        }

        public void ResetPropagation()
        {
            for (int i = 0; i < plateVertices.Length; i++)
            {
                vertexNext[i] = plateVertices[i];
                vertexPrev[i] = plateVertices[i];
            }

            modificationCohorts = new List<List<int>>();
            modificationCohorts.Add(new List<int>());
        }

        public void UpdateVertices()
        {
            var uv2 = mesh.uv2;

            var melted = new List<int>();

            foreach (var v in realVertices)
            {
                CheckForPerimeterness(v);

                if (plateVertices[v].magnitude < Plate.mohoRadius)
                {
                    melted.Add(v);
                    continue;
                }

                // Check to see if we can unsquish
                var collider = gameObject.GetComponent<MeshCollider>();
                if (GetVertexSquishedness(v) > 0.01 && IsClearForMORBingAtPoint(vertexHomes[v], collider, vertexSquishAllowance))
                {
                    //SquishVertex(v, vertexHomes[v], false);
                    ResetVertexSquishedness(v);
                }

                // Check to see if the vertex can start sinking
                if (vertexSubductionState[v] == SubductionState.Initiated)
                {
                    var (cPlate, v0, v1, v2) = IsFullyCoveredByOtherPlate(plateVertices[v], collider);
                    if (cPlate != null)
                    {
                        vertexSubductionState[v] = SubductionState.Active;

                        // If any mass is left after trenchification, obduct it over to the overriding vertices
                        if (plateVertices[v].magnitude > trenchRadius)
                        {
                            var targets = cPlate.GetNonPerimeterNeighbors(new List<int> { v0, v1, v2 });
                            var massShare = (plateVertices[v].magnitude - trenchRadius) / targets.Count();

                            var cVertices = cPlate.GetPlateVertices();
                            foreach (var target in targets)
                            {
                                cVertices[target] = cVertices[target].normalized * (cVertices[target].magnitude + massShare);
                            }
                            cPlate.SetPlateVertices(cVertices);

                            var q = GetRotation();
                            cPlate.DoMassSlumping(targets, q);

                            eventsMORB.Add(CreateEventMarker(gameObject, new Color(0.0f, 1f, 1f, 0.5f), plateVertices[v], 1f, "EventMarkers", "EventTrenchClogging"));
                        }
                    }
                }

                // Apply thermal contraction
                var age = plateRenderCount - uv2[v].x;
                if (age < Plate.framesToCool)
                {
                    var delta = plateRenderCount - uv2[v].y;
                    var shrinkage = delta * Plate.hotCrustThicknessBonus / Plate.framesToCool;
                    plateVertices[v] = plateVertices[v].normalized * (plateVertices[v].magnitude - shrinkage);
                    uv2[v].y = plateRenderCount;
                }
            }

            foreach (var v in melted)
            {
                RemoveReal(v);
                RemoveFromPerimeter(v);
                vertexSubductionState[v] = SubductionState.None;
                plateVertices[v] = plateVertices[v].normalized * Plate.coreRadius;                        
            }

            mesh.vertices = plateVertices;
            mesh.uv2 = uv2;
            mesh.RecalculateBounds();
        }

        public void DoVolcanism()
        {
            DoMagmaticScan();
            UpdateVolcanism();
        }

        private void DoMagmaticScan()
        {
            // Look down at every point (later optmize to just near the perimeter if needed)
            // If there is another plate down there within a magic distance range
            // Randomly add a magma if there isn't already
            // Uplift area somewhat
            var count = realVertices.Count;
            var mask = LayerMask.GetMask("Plates", "TornPlates");
            var qparams = new QueryParameters(mask, true, QueryTriggerInteraction.Ignore, false);
            var castCommands = new NativeArray<SpherecastCommand>(count, Allocator.Persistent);
            var castHits = new NativeArray<RaycastHit>(count * 3, Allocator.Persistent);

            for (int j = 0; j < count; j++)
            {
                // Standard out-and-back routine
                var point = plateVertices[realVertices[j]];
                Ray ray = new Ray(Vector3.zero, point);
                ray.origin = ray.GetPoint(50);
                ray.direction = -ray.direction;
                castCommands[j] = new SpherecastCommand(ray.origin, 0.1f, ray.direction, qparams, 50 - Plate.mohoRadius);
            }

            JobHandle handle = SpherecastCommand.ScheduleBatch(castCommands,
                                                            castHits,
                                                            1,
                                                            3,
                                                            default(JobHandle));
            handle.Complete();

            var collider = gameObject.GetComponent<MeshCollider>();
            for (int j = 0; j < castHits.Length; j += 3)
            {
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

        private void UpdateVolcanism()
        {
            // For each active magma in the plate
            // If (high probability) random, add to the plate above
            // If (low probability) random, set to inactive
            var killed = new List<Magma>();

            foreach (var magma in magmas)
            {
                if (magma.GetActive())
                {
                    if (magma.ShouldRandomlyErupt())
                    {
                        magma.DoUplift(this);
                        ui.TriggerBreakpoint(Breakpoint.MagmaEruption);
                    }

                    if (magma.ShouldRandomlyDie())
                    {
                        KillMagma(magma);
                        killed.Add(magma);
                    }
                }
            }

            killed.Select(m => magmas.Remove(m));

            mesh.vertices = plateVertices;
            mesh.RecalculateBounds();
        }

        public void MORBingTime(SubductionTracker subductionTracker)
        {
            ClearEvents(eventsMORB);

            var collider = gameObject.GetComponent<MeshCollider>();
            var newMORBS = false;
            for (int pIndex = 0; pIndex < perimeter.Count; pIndex++)
            {
                var p = perimeter[pIndex];

                foreach (var n in neighbors[p])
                {
                    var neighborCount = neighbors[n].Count;
                    var validNeighborCount = ValidNeighborCount(n);

                    // Is this a potential MORBing site?
                    // Include subducting-but-not-virtual vertices because if a plate runs long enough, 
                    // previously subducted areas will eventually start to be re-exposed.
                    // (This happens even quicker if the plate boundary passes though one of its own pivot points!)
                    // Check both current and home position to account for squishing, can result in anamolous MORBing in a subduction context otherwise
                    // Lowering check below trenchRadius to prevent anomalous MORBing in a subduction zone, kind of hacky
                    if (plateVertices[n].magnitude < (trenchRadius - 2.0f) &&
                        validNeighborCount > 1 &&
                        IsClearForMORBingAtPoint(plateVertices[n], collider) &&
                        IsClearForMORBingAtPoint(vertexHomes[n], collider) &&
                        !AreSubductingPlatesNearby(plateVertices[n], collider, subductionTracker))
                    {
                        // Need to check halfway between this potential new vertex and all its existing valid neighbors, to catch intruding spikes of other plates
                        var clearAtHalfwayPoints = true;
                        foreach (var n2 in neighbors[n])
                        {
                            if (clearAtHalfwayPoints && IsOceanicOrContinental(plateVertices[n2]))
                            {
                                var midpoint = (plateVertices[n] + plateVertices[n2]) / 2;
                                clearAtHalfwayPoints &= IsClearForMORBingAtPoint(midpoint, collider, 0.1f);
                            }
                        }

                        if (!clearAtHalfwayPoints)
                            continue;

                        // Add stochastic factor based on number of neighboring perimeter vertices
                        // This should help concentrate growth in concavities, and maybe help even out which plate gets the growth at a spreading ridge
                        var neighborFactor = (double)validNeighborCount / (double)neighborCount;
                        if (rand.NextDouble() > neighborFactor * neighborFactor)
                            continue;

                        ResurrectVertex(n);

                        // Sometimes anomalous subduction in a spreading ridge will leave a little perimeter pocket of lowered, pre-subducted crust
                        foreach (var n2 in GetExtendedNeighbors(n, 1))
                        {
                            if (vertexSubductionState[n2] != SubductionState.None &&
                            IsClearForMORBingAtPoint(plateVertices[n2], collider) &&
                            IsClearForMORBingAtPoint(vertexHomes[n2], collider))
                            {
                                plateVertices[n2] = vertexHomes[n2] * seaFloorMinNormalRadius;
                                vertexSubductionState[n2] = SubductionState.None;
                                CheckForPerimeterness(n2);
                                CheckNeighborsForPerimeterness(n2);
                            }
                        }

                        newMORBS = true;
                        Plate.DoRegionalRidgePush(plateVertices[n]);
                        eventsMORB.Add(CreateEventMarker(gameObject, new Color(1.0f, 0f, 0f, 0.5f), plateVertices[n], 1f, "EventMarkers", "EventMORB"));
                    }
                }
            }
            mesh.RecalculateBounds();

            if (newMORBS)
            {
                ui.TriggerBreakpoint(Breakpoint.MORB);
            }
        }

        public void DoErosion(float sealevel, float toplevel)
        {
            var erosionAmount = 0.03f;

            // Go through all our vertices
            // Lower them following some nonlinear relation to height above sealevel
            foreach (var v in realVertices)
            {
                var elevationFactor = (plateVertices[v].magnitude - sealevel) / (toplevel - sealevel);
                if (elevationFactor > 0)
                {
                    var shrinkage = elevationFactor * elevationFactor * erosionAmount;
                    plateVertices[v] = plateVertices[v].normalized * (plateVertices[v].magnitude - shrinkage);
                }
            }

            // TODO erode in such a way to make mountains less conical!
        }

        public void OnDestroy()
        {
            Debug.Log($"Destroying {name}");

            foreach (var marker in perimeterMarkers)
            {
                Destroy(marker);
            }

            foreach (var marker in eventsMORB)
            {
                Destroy(marker);
            }

            foreach (var marker in eventsCollision)
            {
                Destroy(marker);
            }
        }
    }
}