using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using Unity.Jobs;

using Orogeny.Convection;
using Orogeny.Maps;
using Orogeny.Meshes;
using Orogeny.Plates;

namespace Orogeny {
    public class sculpt : MonoBehaviour {
        //shaders and planet parameters:
        public Shader with_light;

        public Shader without_light;

        public Shader water_real;

        public Shader water_cartoon;

        public Shader wireframe;

        private float waterHeight = 1.04f;

        private float current_waterHeight = 1f;

        private float flatness = 2.5f;

        private float current_flatness = 0f;

        public Transform planet; //planet

        private Transform water; //water

        private Transform clouds; //clouds

        private Transform glow; //glow

        private Vector3 water_size_initial;

        private float pl_rad = Plate.seaFloorRadius; //planet basic mesh radius

        private Vector4 blend1; //planet shader parameters

        private Vector4 blend2;

        private Vector4 blend3;

        private Rect controlWindowRect = new Rect(Screen.width - 210, 10, 200, 75);

        private UserInterface ui;

        private enum Phase {
            Tectonic = 0,
            Tectonic2 = 1,
            Tectonic3 = 2,
            PerimeterCheck = 3,
            Uplift = 4,
            Trim = 5,
            Volcanism = 6,
            MORB = 7,
            Smooth = 8
        }

        private Vector3[] planetVertices;
        private List<Plate> plates = null;
        private int curPlate = 0;
        private Phase curPhase = Phase.Tectonic;
        private Dictionary<int, List<int>> neighbors;
        private Convector convector;
        private PlateCollisions plateCollisions;
        private Vector3[] convectorNormals;

        private List<Color> colors;

        private NativeArray<RaycastCommand> raycastCommandsTectonics;
        private NativeArray<RaycastHit> raycastHitsTectonics;
        private NativeArray<RaycastCommand> raycastCommandsUplift;
        private NativeArray<RaycastHit> raycastHitsUplift;

        public void Start() {
            blend1 =
                planet
                    .transform
                    .GetComponent<Renderer>()
                    .material
                    .GetVector("_Blend0to1and1to2");
            blend2 =
                planet
                    .transform
                    .GetComponent<Renderer>()
                    .material
                    .GetVector("_Blend2to3and3to4");
            blend3 =
                planet
                    .transform
                    .GetComponent<Renderer>()
                    .material
                    .GetVector("_Blend4to5and5to6");
            water = planet.transform.Find("water_sphere");
            water_size_initial = water.transform.localScale;

            planet.GetComponent<Renderer>().material.shader = without_light;

            clouds = planet.transform.Find("clouds_sphere").transform;
            glow = planet.transform.Find("Glow").transform;
            clouds.GetComponent<Renderer>().enabled = false;
            glow.GetComponent<Renderer>().enabled = false;

            planet.gameObject.layer = LayerMask.NameToLayer("Geoid");

            ui = gameObject.GetComponent<UserInterface>();

            var icosphere = IcoSphereCreator.Create(32, Plate.coreRadius);
            var icoNeighbors = Plate.ExtractNeighbors(icosphere);

            InitColors();

            plates = new List<Plate>();

            AddPlate(-67, 0, 5f, 1.5f, icosphere);
            AddPlate(-10, 5, 5f, 1.0f, icosphere);

            AddPlate(-70, 5, 5f, 1.5f, icosphere);
            AddPlate(-10, 5, 5f, 1.0f, icosphere);
            AddPlate(90, 10, 15f, 1.0f, icosphere);
            AddPlate(0, -80, 15f, 1.0f, icosphere);
            AddPlate(170, -10, 12f, 1.0f, icosphere);
            AddPlate(0, 85, 14f, 1.0f, icosphere);

            ExpandPlates(icosphere, icoNeighbors);

            FinalizePlates(icosphere);

            var mesh = planet.GetComponent<MeshFilter>().mesh;
            mesh.MarkDynamic();

            convector = new Convector(Plate.seaFloorRadius);
            plateCollisions = new PlateCollisions();

            InitializeTectonics();
            InitializeUplift();

            height_texture_set();
            UpliftPlanet();
        }

        // https://www.toptal.com/designers/colourcode/analogic-color-builder
        private void InitColors() {
            colors = new List<Color>();
            // colors.Add(ParseColor("#21205A"));
            // colors.Add(ParseColor("#2B205C"));
            // colors.Add(ParseColor("#35215E"));
            // colors.Add(ParseColor("#3F2160"));
            // colors.Add(ParseColor("#4A2163"));
            // colors.Add(ParseColor("#562265"));
            // colors.Add(ParseColor("#622267"));
            // colors.Add(ParseColor("#692264"));
            // colors.Add(ParseColor("#6C225B"));
            // colors.Add(ParseColor("#6E2352"));

            // colors.Add(ParseColor("#254B68"));
            // colors.Add(ParseColor("#25436A"));
            // colors.Add(ParseColor("#26396D"));
            // colors.Add(ParseColor("#262F6F"));
            // colors.Add(ParseColor("#282671"));
            // colors.Add(ParseColor("#342673"));
            // colors.Add(ParseColor("#412776"));
            // colors.Add(ParseColor("#4E2778"));
            // colors.Add(ParseColor("#5B277A"));
            // colors.Add(ParseColor("#6A277D"));

            colors.Add(ParseColor("#58C3BD"));
            colors.Add(ParseColor("#59BBC5"));
            colors.Add(ParseColor("#5AACC6"));
            colors.Add(ParseColor("#5C9DC7"));
            colors.Add(ParseColor("#5D8EC8"));
            colors.Add(ParseColor("#5E7FC9"));
            colors.Add(ParseColor("#6070CB"));
            colors.Add(ParseColor("#6162CC"));
            colors.Add(ParseColor("#7262CD"));
            colors.Add(ParseColor("#8364CE"));
        }

        // #rrggbb
        private Color ParseColor(string rgb) {
            int r = int.Parse(rgb.Substring(1, 2), System.Globalization.NumberStyles.HexNumber);
            int g = int.Parse(rgb.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
            int b = int.Parse(rgb.Substring(5, 2), System.Globalization.NumberStyles.HexNumber);
            return new Color(r / 255f, g / 255f, b / 255f);
        }

        private void OnDestroy() {
            raycastCommandsTectonics.Dispose();
            raycastHitsTectonics.Dispose();
            raycastCommandsUplift.Dispose();
            raycastHitsUplift.Dispose();
        }

        private void AddPlate(float lon, float lat, float size, float height, Mesh icosphere) {
            var name = "Plate " + plates.Count;
            var color = colors[(plates.Count * 3) % colors.Count];
            var gameObject = new GameObject(name);
            
            gameObject.layer = LayerMask.NameToLayer("Plates");
            var plate = gameObject.AddComponent<Plate>();
            plates.Add(plate);

            var position = MapProjection.Geodetic2Vector(lon, lat) * Plate.seaFloorRadius;
            plate.Initialize(icosphere, plates.Count - 1, position, size, height, color, ui);
        }

        private void ExpandPlates(Mesh icosphere, Dictionary<int, List<int>> neighbors) {
            var more = true;

            //while (more) {
            for (var i = 0; i < 10; i++) {
                more = false;
                foreach (var plate in plates) {
                    more |= plate.ExpandCrust(icosphere, neighbors);
                }
            }
        }

        private void FinalizePlates(Mesh icosphere) {
            foreach (var plate in plates) {
                plate.Finalize(icosphere, wireframe);
            }
        }

        private void InitializeTectonics() {
            var mesh = convector.GetMesh();
            var mask = LayerMask.GetMask("Plates");

            raycastCommandsTectonics = new NativeArray<RaycastCommand>(mesh.vertices.Length, Allocator.Persistent);
            raycastHitsTectonics = new NativeArray<RaycastHit>(mesh.vertices.Length, Allocator.Persistent);

            for (int i = 0; i < mesh.vertices.Length; i++) {
                // Raycast out from origin through that vertex
                // Except rays won't hit the wrong sides of meshes, so we need to flip it around
                Ray ray = new Ray(Vector3.zero, mesh.vertices[i]);
                ray.origin = ray.GetPoint(50);
                ray.direction = -ray.direction;
                raycastCommandsTectonics[i] = new RaycastCommand(ray.origin, ray.direction, 50 - Plate.mohoRadius, mask);
            }

            convectorNormals = mesh.normals;
        }

        private void TectonicAllThePlates() {
            // Need to sum all the forces
            // For every convection vertex
            // Raycast out and back to see if it hits a plate
            // Sum the forces for each plate

            JobHandle handle = RaycastCommand.ScheduleBatch(raycastCommandsTectonics, 
                                                            raycastHitsTectonics, 
                                                            1, 
                                                            default(JobHandle));
            handle.Complete();

            foreach (Plate p in plates)
            {
                p.ClearNetForce();
            }

            for (int i = 0; i < convectorNormals.Length; i++) {
                RaycastHit hit = raycastHitsTectonics[i];

                if (hit.collider != null) {
                    var id = hit.collider.GetInstanceID();
                    var p = Plate.FindPlateByInstanceID(plates, id);

                    if (p != null) {
                        p.AddForce(hit.point, convectorNormals[i]);
                    }
                }
            }
        }

        private void TectonicAllThePlates2() {
            var plate = plates[curPlate];

            // if (plate.name != "Plate 0") {
            //     plate.ClearNetForce();
            // }

            plate.ApplyForces(0);
        }

        private void TectonicAllThePlates3() {
            var plate = plates[curPlate];

            // if (plate.name != "Plate 0") {
            //     plate.ClearNetForce();
            // }

            plate.ApplyForces(1);
        }

        private void InitializeUplift() {
            Mesh mesh = planet.GetComponent<MeshFilter>().mesh;
            var mask = LayerMask.GetMask("Plates");
            planetVertices = mesh.vertices;

            raycastCommandsUplift = new NativeArray<RaycastCommand>(planetVertices.Length, Allocator.Persistent);
            raycastHitsUplift = new NativeArray<RaycastHit>(planetVertices.Length, Allocator.Persistent);

            for (int i = 0; i < planetVertices.Length; i++) {
                // Raycast out from origin through that vertex
                // Except rays won't hit the wrong sides of meshes, so we need to flip it around
                Ray ray = new Ray(Vector3.zero, planetVertices[i]);
                ray.origin = ray.GetPoint(50);
                ray.direction = -ray.direction;
                raycastCommandsUplift[i] = new RaycastCommand(ray.origin, ray.direction, 50 - Plate.mohoRadius, mask);
            }
        }

        private void UpliftPlanet() {
            var filter = planet.GetComponent<MeshFilter>();
            var collider = planet.GetComponent<MeshCollider>();

            Mesh mesh = filter.mesh;

            JobHandle handle = RaycastCommand.ScheduleBatch(raycastCommandsUplift, 
                                                            raycastHitsUplift, 
                                                            1, 
                                                            default(JobHandle));
            handle.Complete();

            for (int i = 0; i < planetVertices.Length; i++) {
                RaycastHit hit = raycastHitsUplift[i];

                if (hit.collider == null || hit.point.magnitude < Plate.trenchRadius) {
                    planetVertices[i] = planetVertices[i].normalized * Plate.seaFloorRadius;
                } else {
                    planetVertices[i] = hit.point;
                }
            }

            mesh.vertices = planetVertices;
            mesh.RecalculateBounds();
            collider.sharedMesh = null;
            collider.sharedMesh = mesh; 
        }

        private void SmoothPlanet() {
            var mesh = planet.GetComponent<MeshFilter>().mesh;
            Vector3[] newVerts = new Vector3[mesh.vertices.Length];

            for (var v = 0; v < mesh.vertices.Length; v++) {
                if (neighbors[v].Count > 0) {
                    var sum = Vector3.zero;
                    foreach (var n in neighbors[v]) {
                        sum += mesh.vertices[n];
                    }

                    newVerts[v] = mesh.normals[v] * (sum / mesh.vertices.Length).magnitude;
                } else {
                    newVerts[v] = mesh.vertices[v];
                }
            }

            mesh.SetVertices(newVerts);
            mesh.RecalculateBounds();
        }

        public void OnDrawGizmos () {
            if (ui == null || plates == null)
                return;

            if (ui.IsDisplayFeatureOn(DisplayFeature.Convection)) {
                var convectionMesh = convector.GetMesh();
                var vertices = convectionMesh.vertices;
                var normals = convectionMesh.normals;

                var cameraPos = Camera.main.transform.position;
                var cameraDist = cameraPos.magnitude;
                var radius = Plate.seaFloorRadius;
                var tangentDist = Mathf.Sqrt(cameraDist * cameraDist - radius * radius);

                for (int i = 0; i < vertices.Length; i++) {
                    var vertex = vertices[i];
                    var vertexDist = (vertices[i] - cameraPos).magnitude;
                    if (vertexDist < tangentDist) {
                        Gizmos.color = new Color(normals[i].x, normals[i].y, normals[i].z);
                        Gizmos.DrawLine(vertex, vertex + normals[i]);
                    }
                }
            }
        }

        public void Update() {
            if (ui.IsDisplayFeatureOn(DisplayFeature.Terrain)) {
                GetComponent<Camera>().cullingMask |= 1 << LayerMask.NameToLayer("Geoid");
            } else {
                GetComponent<Camera>().cullingMask &= ~(1 << LayerMask.NameToLayer("Geoid"));
            }

            if (ui.IsDisplayFeatureOn(DisplayFeature.PlatePerimeterPoints)) {
                GetComponent<Camera>().cullingMask |= 1 << LayerMask.NameToLayer("PerimeterPoints");
            } else {
                GetComponent<Camera>().cullingMask &= ~(1 << LayerMask.NameToLayer("PerimeterPoints"));
            }

            if (ui.IsDisplayFeatureOn(DisplayFeature.EventMarkers)) {
                GetComponent<Camera>().cullingMask |= 1 << LayerMask.NameToLayer("EventMarkers");
            } else {
                GetComponent<Camera>().cullingMask &= ~(1 << LayerMask.NameToLayer("EventMarkers"));
            }

            foreach (var plate in plates) {
                plate.SetMeshDisplay(ui.IsDisplayFeatureOn(DisplayFeature.PlateMeshes));
            }

            if (ui.IsPlaying()) {
                switch (curPhase) {
                    case Phase.Tectonic:
                        if (ui.IsSubsystemFeatureOn(SubsystemFeature.Tectonics)) {
                            TectonicAllThePlates();
                        }
                        break;
                    case Phase.Tectonic2:
                        if (ui.IsSubsystemFeatureOn(SubsystemFeature.Tectonics)) {
                            TectonicAllThePlates2();
                        }
                        break;
                    case Phase.Tectonic3:
                        if (ui.IsSubsystemFeatureOn(SubsystemFeature.Tectonics)) {
                            TectonicAllThePlates3();
                        }
                        break;
                    case Phase.PerimeterCheck:
                        if (ui.IsSubsystemFeatureOn(SubsystemFeature.CollisionCheck)) {
                            plateCollisions.PerimeterCheck(plates[curPlate]);
                        }
                        break;
                    case Phase.Uplift:
                        height_texture_set();
                        UpliftPlanet();
                        break;
                    case Phase.Trim:
                        if (ui.IsSubsystemFeatureOn(SubsystemFeature.ClearAndShrink)) {
                            plates[curPlate].UpdateVertices();
                        }
                        break;
                    case Phase.Volcanism:
                        if (ui.IsSubsystemFeatureOn(SubsystemFeature.Volcanism)) {
                            plates[curPlate].DoVolcanism();
                        }
                        break;
                    case Phase.MORB:
                        if (ui.IsSubsystemFeatureOn(SubsystemFeature.MORB)) {
                            plates[curPlate].MORBingTime();
                        }
                        break;
                    case Phase.Smooth:
                        //SmoothPlanet();
                        break;
                }

                curPhase = (Phase)((((int)curPhase) + 1) % ((int)Phase.MORB + 1));
                if (curPhase == 0) {
                    curPlate = (curPlate + 1) % plates.Count;
                }
            }
        }

        private void height_texture_set()
        {
            //setting shader parameters. (Basically applying texture junction, mesh flatness and water height values)
            if (planet != null && (current_waterHeight != waterHeight || current_flatness != flatness)) {
                current_waterHeight = waterHeight;
                current_flatness = flatness;

                Vector4 pl_rad4 = new Vector4(pl_rad, pl_rad, pl_rad, pl_rad);
                planet
                    .transform
                    .GetComponent<Renderer>()
                    .material
                    .SetVector("_Blend0to1and1to2",
                    (pl_rad4 - (pl_rad4 - blend1) / flatness) * waterHeight);
                planet
                    .transform
                    .GetComponent<Renderer>()
                    .material
                    .SetVector("_Blend2to3and3to4",
                    (pl_rad4 - (pl_rad4 - blend2) / flatness) * waterHeight);
                planet
                    .transform
                    .GetComponent<Renderer>()
                    .material
                    .SetVector("_Blend4to5and5to6",
                    (pl_rad4 - (pl_rad4 - blend3) / flatness) * waterHeight);
                water.transform.localScale = (new Vector3(1f, 1f, 1f) - (new Vector3(1f, 1f, 1f) - water_size_initial) / flatness) * waterHeight;
            }
        }
    }
}