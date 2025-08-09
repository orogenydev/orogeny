using Defective.JSON;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using Unity.Jobs;


using Orogeny.Convection;
using Orogeny.Maps;
using Orogeny.Meshes;
using Orogeny.Plates;
using Orogeny.Hotspots;

namespace Orogeny {
    public class sculpt : MonoBehaviour
    {
        public Shader without_light;
        public Shader wireframe;

        public Transform planet; //planet
        private Transform water; //water
        private Transform glow; //glow

        // Deeper
        public float radiusDeeperWaterTop = 24.75f;
        // Deeper/deep blend
        public float radiusDeepWaterBottom = Plate.seaFloorRadius; // 25
        // Deep
        public float radiusDeepWaterTop = 25.33f;
        // Deep/water blend
        public float radiusWaterBottom2 = 25.6f;

        // Water
        public float radiusWaterTop = 25.9f;
        // Water/sand blend
        public float radiusSandBottom = 26.1f;
        // Sand
        public float radiusSandTop = 26.1f;
        // Sand/grass blend
        public float radiusGrassBottom = 26.2f;

        // Grass
        public float radiusGrassTop = 27f;
        // Grass/rock blend
        public float radiusRockBottom = 27.33f;
        // Rock
        public float radiusRockTop = 27.66f;
        // Rock/snow blend
        public float radiusSnowBottom = 28.0f;
        // Snow
        public float radiusSnowTop = 28.75f; // Nothing should ever be taller than this

        private Rect controlWindowRect = new Rect(Screen.width - 210, 10, 200, 75);

        private UserInterface ui;
        private System.Random rand;

        private enum Phase
        {
            Tectonic = 0,
            Tectonic2 = 1,
            Tectonic3 = 2,
            PerimeterCheck = 3,
            Volcanism = 4,
            MORB = 5,
            Erode = 6,
            Trim = 7,
            Shrinkwrap = 8
        }

        private Vector3[] planetVertices;
        private List<Plate> plates = new List<Plate>();
        private int curPlate = 0;
        private Plate[] curPlateOrder;
        private Phase curPhase = Phase.Tectonic;
        public Convector convector;
        private List<Hotspot> hotspots = new List<Hotspot>();

        private int nextPlateNumber = 0;
        private List<Color> colors = new List<Color>();

        private NativeArray<RaycastCommand> raycastCommandsTectonics;
        private NativeArray<RaycastHit> raycastHitsTectonics;
        private NativeArray<RaycastCommand> raycastCommandsShrinkwrap;
        private NativeArray<RaycastHit> raycastHitsShrinkwrap;

        private float hackyLongitudeAdjustement = -73; // This aligns the camera

        public void Start()
        {
            water = planet.transform.Find("water_sphere");

            planet.GetComponent<Renderer>().material.shader = without_light;

            planet.gameObject.layer = LayerMask.NameToLayer("Geoid");

            var glow = planet.transform.Find("Glow").transform;
            glow.GetComponent<Renderer>().enabled = false;

            ui = gameObject.GetComponent<UserInterface>();
            rand = new System.Random(42);

            InitColors();

            LoadPlates();

            LoadHotspots();

            var mesh = planet.GetComponent<MeshFilter>().mesh;
            mesh.MarkDynamic();

            convector = new Convector(Plate.seaFloorRadius);

            InitializeTectonics();
            InitializeShrinkwrap();

            UpdatePlanetTextures();
            ShrinkwrapPlanet();
        }

        // https://www.toptal.com/designers/colourcode/analogic-color-builder
        private void InitColors()
        {
            colors = new List<Color>();

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
        private Color ParseColor(string rgb)
        {
            int r = int.Parse(rgb.Substring(1, 2), System.Globalization.NumberStyles.HexNumber);
            int g = int.Parse(rgb.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
            int b = int.Parse(rgb.Substring(5, 2), System.Globalization.NumberStyles.HexNumber);
            return new Color(r / 255f, g / 255f, b / 255f);
        }

        private void LoadPlates(JSONObject json = null)
        {
            foreach (var plate in plates)
            {
                Destroy(plate.gameObject);
                Destroy(plate);
            }

            plates = new List<Plate>();

            if (json == null)
            {
                var icosphere = IcoSphereCreator.Create(32, Plate.coreRadius);
                //var icosphere = IcoSphereCreator.Create(4, Plate.coreRadius);

                AddPlate(0, 0, 5f, 3.0f, icosphere);
                AddPlate(180, 0, 5f, 2.0f, icosphere);

                // AddPlate(120, 0, 15f, 1.5f, icosphere);
                // AddPlate(-180, 0, 15f, 1.0f, icosphere);

                // AddPlate(-120, 0, 15f, 1.5f, icosphere);
                // AddPlate(-45, 0, 5f, -1.0f, icosphere);

                // AddPlate(0, 60, 15f, 1.0f, icosphere);
                // AddPlate(120, 60, 15f, 1.0f, icosphere);
                // AddPlate(-120, 60, 15f, 1.0f, icosphere);

                // AddPlate(0, -60, 15f, 1.0f, icosphere);
                // AddPlate(120, -60, 15f, 1.0f, icosphere);
                // AddPlate(-120, -60, 15f, 1.0f, icosphere);

                ExpandNewPlates();
            }
            else
            {
                foreach (var plate in json)
                {
                    var plateNumber = plate["plateNumber"].intValue;
                    var name = plate["name"].stringValue;
                    var color = plate["color"].ToColor();
                    var meshString = plate["mesh"].ToString();

                    AddPlate(plateNumber, name, color, MeshUtils.MeshUndump(meshString));
                }
            }

            FinalizePlates();
        }

        private void AddPlate(float lon, float lat, float size, float height, Mesh icosphere)
        {
            lon = lon + hackyLongitudeAdjustement;
            var position = MapProjection.Geodetic2Vector(lon, lat).normalized * Plate.seaFloorRadius;
            var plateNumber = nextPlateNumber;
            var name = "Plate " + plateNumber;
            var color = colors[(plateNumber * 3) % colors.Count];

            var plate = CreatePlate(name);
            plate.Initialize(icosphere, plateNumber, name, position, size, height, color, ui);
        }

        private Plate AddPlate(int plateNumber, string name, Color color, Mesh mesh)
        {
            var plate = CreatePlate(name);
            plate.Initialize(plateNumber, name, mesh, color, ui);
            return plate;
        }

        private Plate CreatePlate(string name)
        {
            var gameObject = new GameObject(name);
            var plate = gameObject.AddComponent<Plate>();
            gameObject.layer = LayerMask.NameToLayer("Plates");
            plates.Add(plate);
            nextPlateNumber++;
            return plate;
        }

        private void ExpandNewPlates()
        {
            var more = true;
            var count = 0;

            while (more)
            {
                more = false;
                foreach (var plate in plates)
                {
                    if (plate.GetPlateNumber() == 0 && count > 10)
                    {
                        continue;
                    }
                    more |= plate.ExpandCrust(plates);
                }
                count++;
            }
        }

        private void FinalizePlates()
        {
            foreach (var plate in plates)
            {
                plate.Finalize(wireframe);
            }
        }

        private void InitializeTectonics()
        {
            var mesh = convector.GetMesh();
            var mask = LayerMask.GetMask("Plates");
            var qparams = new QueryParameters(mask, true, QueryTriggerInteraction.Ignore, false);

            raycastCommandsTectonics = new NativeArray<RaycastCommand>(mesh.vertices.Length, Allocator.Persistent);
            raycastHitsTectonics = new NativeArray<RaycastHit>(mesh.vertices.Length, Allocator.Persistent);

            for (int i = 0; i < mesh.vertices.Length; i++)
            {
                // Raycast out from origin through that vertex
                // Except rays won't hit the wrong sides of meshes, so we need to flip it around
                Ray ray = new Ray(Vector3.zero, mesh.vertices[i]);
                ray.origin = ray.GetPoint(50);
                ray.direction = -ray.direction;
                raycastCommandsTectonics[i] = new RaycastCommand(ray.origin, ray.direction, qparams, 50 - Plate.mohoRadius);
            }
        }

        private void TectonicAllThePlates()
        {
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

            var normals = convector.GetMesh().normals;
            for (int i = 0; i < normals.Length; i++)
            {
                RaycastHit hit = raycastHitsTectonics[i];

                if (hit.collider != null)
                {
                    var otherGO = hit.collider.gameObject;
                    var p = otherGO.GetComponent<Plate>();

                    if (p != null)
                    {
                        p.AddForce(hit.point, normals[i]);
                    }
                }
            }

            // Check for tearing
            var plate = plates[curPlate];
            var force = plate.GetScaledNetForce();
            var angle = Vector3.SignedAngle(force, plate.GetPrevScaledNetForce(), Vector3.up);
            plate.UpdatePrevScaledNetForce();

            // Maybe make this more nuanced eventually, but for now, just tear off
            // ALL subducting vertices on a sufficiently large angle change
            if (Mathf.Abs(angle) > 10)
            {
                var newMesh = plate.TearOffSubductingVertices();
                var newPlate = AddPlate(nextPlateNumber, $"{plate.name} tear", colors[(nextPlateNumber * 3) % colors.Count], newMesh);
                newPlate.SetPlateRenderCount(plate.GetPlateRenderCount());
                newPlate.gameObject.layer = LayerMask.NameToLayer("TornPlates");
                newPlate.Finalize(wireframe);

                for (int v = 0; v < newPlate.mesh.vertices.Length; v++)
                {
                    if (plate.IsSubducting(v))
                    {
                        plate.vertexSubducting[v] = false;
                        newPlate.vertexSubducting[v] = true;
                    }
                }
            }
        }

        private void TectonicAllThePlates2()
        {
            var plate = plates[curPlate];

            plate.ApplyForces(0);
        }

        private void TectonicAllThePlates3()
        {
            var plate = plates[curPlate];

            plate.ApplyForces(1);
        }

        private void InitializeShrinkwrap()
        {
            Mesh mesh = planet.GetComponent<MeshFilter>().mesh;
            var mask = LayerMask.GetMask("Plates");
            var qparams = new QueryParameters(mask, true, QueryTriggerInteraction.Ignore, false);
            planetVertices = mesh.vertices;

            raycastCommandsShrinkwrap = new NativeArray<RaycastCommand>(planetVertices.Length, Allocator.Persistent);
            raycastHitsShrinkwrap = new NativeArray<RaycastHit>(planetVertices.Length, Allocator.Persistent);

            for (int i = 0; i < planetVertices.Length; i++)
            {
                // Raycast out from origin through that vertex
                // Except rays won't hit the wrong sides of meshes, so we need to flip it around
                Ray ray = new Ray(Vector3.zero, planetVertices[i]);
                ray.origin = ray.GetPoint(50);
                ray.direction = -ray.direction;
                raycastCommandsShrinkwrap[i] = new RaycastCommand(ray.origin, ray.direction, qparams, 50 - Plate.mohoRadius);
            }
        }

        private void ShrinkwrapPlanet()
        {
            var filter = planet.GetComponent<MeshFilter>();
            var collider = planet.GetComponent<MeshCollider>();

            Mesh mesh = filter.mesh;

            JobHandle handle = RaycastCommand.ScheduleBatch(raycastCommandsShrinkwrap,
                                                            raycastHitsShrinkwrap,
                                                            1,
                                                            default(JobHandle));
            handle.Complete();

            for (int i = 0; i < planetVertices.Length; i++)
            {
                RaycastHit hit = raycastHitsShrinkwrap[i];

                if (hit.collider == null || hit.point.magnitude < Plate.trenchRadius)
                {
                    // Computationally cheap hack to try to smooth out gaps between plates
                    if (i > 0)
                    {
                        planetVertices[i] = planetVertices[i].normalized * planetVertices[i - 1].magnitude;
                    }
                    else
                    {
                        planetVertices[i] = planetVertices[i].normalized * Plate.trenchRadius;
                    }
                }
                else
                {
                    planetVertices[i] = hit.point;
                }
            }

            mesh.vertices = planetVertices;
            mesh.RecalculateBounds();
            collider.sharedMesh = null;
            collider.sharedMesh = mesh; 
        }

        private void LoadHotspots(JSONObject json = null)
        {
            foreach (var hotspot in hotspots)
            {
                Destroy(hotspot.gameObject);
                Destroy(hotspot);
            }

            hotspots = new List<Hotspot>();

            if (json == null)
            {
                //AddHotspot(0, 0);
                //AddHotspot(-90, 0);
            }
            else
            {
                foreach (var hotspot in json)
                {
                    var x = hotspot["position"]["x"].floatValue;
                    var y = hotspot["position"]["y"].floatValue;
                    var z = hotspot["position"]["z"].floatValue;
                    AddHotspot(new Vector3(x, y, z));
                }
            }
        }

        private void AddHotspot(float _lon, float _lat)
        {
            var lon = _lon + hackyLongitudeAdjustement;
            var position = MapProjection.Geodetic2Vector(lon, _lat);
            AddHotspot(position);
        }

        private void AddHotspot(Vector3 _position)
        {
            var position = _position.normalized * Plate.magmatismDepth;
            var name = $"Hotspot {hotspots.Count}";
            var gameObject = new GameObject(name);
            var hotspot = gameObject.AddComponent<Hotspot>();
            hotspot.Initialize(position, name);
            hotspots.Add(hotspot);
        }

        public void OnDrawGizmos()
        {
            if (ui == null || plates == null)
                return;

            if (ui.IsDisplayFeatureOn(DisplayFeature.Convection))
            {
                var convectionMesh = convector.GetMesh();
                var vertices = convectionMesh.vertices;
                var normals = convectionMesh.normals;

                // Don't draw any on the far side of the planet, it just gets confusing
                var cameraPos = Camera.main.transform.position;
                var cameraDist = cameraPos.magnitude;
                var radius = Plate.seaFloorRadius;
                var tangentDist = Mathf.Sqrt(cameraDist * cameraDist - radius * radius);

                for (int i = 0; i < vertices.Length; i++)
                {
                    var vertex = vertices[i];
                    var vertexDist = (vertices[i] - cameraPos).magnitude;
                    if (vertexDist < tangentDist)
                    {
                        Gizmos.color = new Color(normals[i].x, normals[i].y, normals[i].z);
                        Gizmos.DrawLine(vertex, vertex + normals[i]);
                    }
                }
            }
        }

        public void Update()
        {
            if (ui.IsDisplayFeatureOn(DisplayFeature.Terrain))
            {
                GetComponent<Camera>().cullingMask |= 1 << LayerMask.NameToLayer("Geoid");
            }
            else
            {
                GetComponent<Camera>().cullingMask &= ~(1 << LayerMask.NameToLayer("Geoid"));
            }

            if (ui.IsDisplayFeatureOn(DisplayFeature.PlatePerimeterPoints))
            {
                GetComponent<Camera>().cullingMask |= 1 << LayerMask.NameToLayer("PerimeterPoints");
            }
            else
            {
                GetComponent<Camera>().cullingMask &= ~(1 << LayerMask.NameToLayer("PerimeterPoints"));
            }

            if (ui.IsDisplayFeatureOn(DisplayFeature.EventMarkers))
            {
                GetComponent<Camera>().cullingMask |= 1 << LayerMask.NameToLayer("EventMarkers");
            }
            else
            {
                GetComponent<Camera>().cullingMask &= ~(1 << LayerMask.NameToLayer("EventMarkers"));
            }

            for (var i = 0; i < plates.Count; i++)
            {
                plates[i].SetMeshDisplay(ui.IsDisplayFeatureOn(DisplayFeature.PlateMeshes) && ui.IsDisplayPlateOn(i));
            }

            if (ui.IsPlaying())
            {
                // if (curPhase == 0)
                // {
                //     curPlate = (curPlate + 1) % plates.Count;
                //     curPlateOrder = plates.ToArray().OrderBy(x => rand.Next()).ToArray();
                // }

                if (curPlate == 0)
                {
                    curPhase = (Phase)((((int)curPhase) + 1) % ((int)Phase.Shrinkwrap + 1));
                    curPlateOrder = plates.ToArray().OrderBy(x => rand.Next()).ToArray();
                }

                switch (curPhase)
                {
                    case Phase.Tectonic:
                        if (ui.IsSubsystemFeatureOn(SubsystemFeature.Tectonics))
                        {
                            TectonicAllThePlates();
                        }
                        break;
                    case Phase.Tectonic2:
                        if (ui.IsSubsystemFeatureOn(SubsystemFeature.Tectonics))
                        {
                            TectonicAllThePlates2();
                        }
                        break;
                    case Phase.Tectonic3:
                        if (ui.IsSubsystemFeatureOn(SubsystemFeature.Tectonics))
                        {
                            TectonicAllThePlates3();
                        }
                        break;
                    case Phase.PerimeterCheck:
                        if (ui.IsSubsystemFeatureOn(SubsystemFeature.CollisionCheck))
                        {
                            PlateCollisions.PerimeterCheck(curPlateOrder[curPlate]);
                        }
                        break;
                    case Phase.Volcanism:
                        if (ui.IsSubsystemFeatureOn(SubsystemFeature.Volcanism))
                        {
                            foreach (var hotspot in hotspots)
                            {
                                hotspot.DoVolcanism();
                            }

                            curPlateOrder[curPlate].DoVolcanism();
                        }
                        break;
                    case Phase.MORB:
                        if (ui.IsSubsystemFeatureOn(SubsystemFeature.MORB))
                        {
                            curPlateOrder[curPlate].MORBingTime();
                        }
                        break;
                    case Phase.Erode:
                        if (ui.IsSubsystemFeatureOn(SubsystemFeature.Erosion))
                        {
                            curPlateOrder[curPlate].DoErosion(radiusWaterTop, radiusSnowTop);
                        }
                        break;
                    case Phase.Trim:
                        curPlateOrder[curPlate].UpdateVertices();
                        break;
                    case Phase.Shrinkwrap:
                        ShrinkwrapPlanet();
                        break;
                }

                curPlate = (curPlate + 1) % plates.Count;

                //curPhase = (Phase)((((int)curPhase) + 1) % ((int)Phase.Shrinkwrap + 1));
            }

            if (ui.IsFileSaveRequested())
            {
                var filename = ui.GetFileSaveName();
                ui.ClearFileSaveName();
                Debug.Log($"Saving state to {filename}...");

                File.WriteAllText(filename, $"{{\n\"plates\":[");
                File.AppendAllText(filename, string.Join(",\n", plates.Select(x => $"{{\"plateNumber\":{x.plateNumber},\"name\":\"{x.name}\",\"color\":{x.GetColor().ToJson()},\"mesh\":{MeshUtils.MeshDump(x.mesh)}}}")));
                File.AppendAllText(filename, $"],\n\n");

                File.AppendAllText(filename, $"\"hotspots\":[");
                File.AppendAllText(filename, string.Join(",\n", hotspots.Select(x => JsonUtility.ToJson(x))));
                File.AppendAllText(filename, $"]\n}}\n");
            }

            if (ui.IsFileLoadRequested())
            {
                var filename = ui.GetFileLoadName();
                ui.ClearFileLoadName();
                Debug.Log($"Loading state from {filename}...");

                var jsonString = File.ReadAllText(filename);
                var json = new JSONObject(jsonString);

                LoadPlates(json["plates"]);
                LoadHotspots(json["hotspots"]);
                curPlate = 0;
                curPhase = 0;
            }
        }

        private void UpdatePlanetTextures()
        {
            // Textures:
            //   water_deeper
            //   water_deep
            //   water
            //   sand
            //   grass
            //   rock
            //   snow

            var new_Blend0to1and1to2 = new Vector4(radiusDeeperWaterTop, radiusDeepWaterBottom, radiusDeepWaterTop, radiusWaterBottom2);
            var new_Blend2to3and3to4 = new Vector4(radiusWaterTop, radiusSandBottom, radiusSandTop, radiusGrassBottom);
            var new_Blend4to5and5to6 = new Vector4(radiusGrassTop, radiusRockBottom, radiusRockTop, radiusSnowBottom);

            planet
                .transform
                .GetComponent<Renderer>()
                .material
                .SetVector("_Blend0to1and1to2",
                new_Blend0to1and1to2);
            planet
                .transform
                .GetComponent<Renderer>()
                .material
                .SetVector("_Blend2to3and3to4",
                new_Blend2to3and3to4);
            planet
                .transform
                .GetComponent<Renderer>()
                .material
                .SetVector("_Blend4to5and5to6",
                new_Blend4to5and5to6);

            // Water sphere scale 1 is radius 25, AKA seafloor
            // Keep it right between water top and sand bottom
            water.transform.localScale = Vector3.one * ((radiusWaterTop + radiusSandBottom) / 2 / 25);
        }
        
        private void OnDestroy() {
            raycastCommandsTectonics.Dispose();
            raycastHitsTectonics.Dispose();
            raycastCommandsShrinkwrap.Dispose();
            raycastHitsShrinkwrap.Dispose();
        }
    }
}