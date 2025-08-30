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
    enum Phase
    {
        Tectonic = 0,
        Tectonic2 = 1,
        Tectonic3 = 2,
        Subduction = 3,
        Volcanism = 4,
        MORB = 5,
        Erosion = 6,
        Trim = 7,
        Shrinkwrap = 8
    }

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
        public float radiusWaterTop = 25.9f; // When eventually making sea level variable, adjust all of these together
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

        private Vector3[] planetVertices;
        private Dictionary<int, List<int>> planetNeighbors;
        private List<Plate> plates = new List<Plate>();
        private int curPlate = 0;
        private Plate[] curPlateOrder;
        private Phase curPhase = Phase.Tectonic;
        private List<Hotspot> hotspots = new List<Hotspot>();
        private SubductionTracker subductionTracker = new SubductionTracker();

        private int nextPlateNumber = 0;
        private List<Color> colors = new List<Color>();
        private int renderCount = 0;

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

                // AddPlate(-0, 0, 5f, 3.0f, icosphere);
                // AddPlate(90, 0, 5f, 3.0f, icosphere);
                // AddPlate(-90, 0, 5f, 3.0f, icosphere);
                // AddPlate(180, 0, 5f, 2.0f, icosphere);
                // AddPlate(0, 90, 5f, 3.0f, icosphere);
                // AddPlate(0, -90, 5f, 3.0f, icosphere);

                for (var i = 0; i < Random.Range(8, 12); i++)
                {
                    var lon = Random.Range(-180, 180);
                    var lat = Random.Range(-90, 90);
                    var size = Random.Range(1, 8);
                    var height = Random.Range(1, 3);

                    AddPlate(lon, lat, size, height, icosphere);
                }

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

            AddTestingFeatures();

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

            while (more)
            {
                more = false;
                foreach (var plate in plates)
                {
                    more |= plate.ExpandCrust(plates);
                }
            }

            foreach (var plate in plates)
            {
                plate.InitializeStuff();
            }
        }

        private void FinalizePlates()
        {
            foreach (var plate in plates)
            {
                plate.Finalize(wireframe);
            }
        }

        private void AddTestingFeatures()
        {
            for (var i = 0; i < plates.Count; i++)
            {
                var p = plates[i];
                var reals = p.GetReal();
                var center = reals[Random.Range(0, reals.Count())];
                var size = Random.Range(3, 12);
                var height = Random.Range(1, 3);

                p.AddBump(center, size, height);
            }
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
                var pos = Random.onUnitSphere;
                AddHotspot(pos);

                for (var i = 0; i < Random.Range(2, 4); i++)
                {
                    pos = Random.onUnitSphere;
                    while (Hotspot.DistanceToClosestHotspot(pos, hotspots) < 0.5f)
                    {
                        pos = Random.onUnitSphere;
                    }

                    AddHotspot(pos);
                }
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

        private (Plate, int) FindNearestAlienPerimeterVertex(Plate myPlate, Vector3 point)
        {
            Plate nearestPlate = null;
            int nearestVertex = -1;
            float nearestDist = float.MaxValue;

            foreach (Plate otherPlate in plates)
            {
                if (otherPlate != myPlate)
                {
                    foreach (var p in otherPlate.GetPerimeter())
                    {
                        var dist = (point - otherPlate.GetPlateVertices()[p]).magnitude;

                        if (dist < nearestDist)
                        {
                            nearestPlate = otherPlate;
                            nearestVertex = p;
                            nearestDist = dist;
                        }
                    }
                }
            }

            return (nearestPlate, nearestVertex);
        }

        private void CalculatePlateForces()
        {
            var myPlate = plates[curPlate];
            myPlate.ClearNetForce();
            myPlate.ClearSubductionPolarityFlippingCollisionCount();

            if (ui.GetPlateForceToggleState())
            {
                foreach (var myVertex in myPlate.GetPerimeter())
                {
                    var myPoint = myPlate.GetPlateVertices()[myVertex];

                    // Continental crust will never subduct, so there will never be any slab pull coming from it
                    if (myPlate.IsContinental(myPoint))
                    {
                        continue;
                    }

                    var (otherPlate, otherVertex) = FindNearestAlienPerimeterVertex(myPlate, myPoint);
                    var otherPoint = otherPlate.GetPlateVertices()[otherVertex];

                    // If they're subducting under us, no slab pull.
                    if (subductionTracker.IsXAlreadySubductingUnderY(otherPlate, myPlate))
                    {
                        continue;
                    }

                    var myUV = myPlate.GetMesh().uv2[myVertex].x;
                    var otherUV = otherPlate.GetMesh().uv2[otherVertex].x;

                    if (myUV < otherUV || subductionTracker.IsXAlreadySubductingUnderY(myPlate, otherPlate))
                    {
                        // We need the rotation from our point to the other point
                        var perp = Vector3.Cross(myPoint, otherPoint).normalized;

                        // Scaled by the difference in density
                        var densityScale = Mathf.Max((otherUV - myUV) / Plate.framesToCool, 1.0f);

                        myPlate.AddForce(perp * densityScale * Plate.slabPullScale);
                    }
                }
            }

            // Ridge push
            myPlate.ArchiveRecentMORBs();
            var historicalMORBs = myPlate.GetHistoricalMORBs();
            foreach (var recents in historicalMORBs)
            {
                foreach (var morbPoint in recents)
                {
                    // A push coming from morbPoint, in the direction of the nearest perimeter point
                    // Except the nearest point will frequently be morbPoint!
                    // So apply it to all its neighbors instead
                    var v = myPlate.FindClosestPerimeter(morbPoint);
                    foreach (var n in myPlate.GetValidNeighbors(v, 1))
                    {
                        var neighborPoint = myPlate.GetPlateVertices()[n];
                        var perp = Vector3.Cross(morbPoint, neighborPoint).normalized;
                        myPlate.AddForce(perp * Plate.ridgePushScale);
                    }
                }
            }
        }

        private void ApplyForces1()
        {
            var plate = plates[curPlate];

            plate.ApplyForces(0);
        }

        private void ApplyForces2()
        {
            var plate = plates[curPlate];

            plate.ApplyForces(1);
        }

        private void ApotheosizeTornMeshes()
        {
            var myPlate = plates[curPlate];
            var meshes = myPlate.GetTornMeshes();

            foreach (var mesh in meshes)
            {
                var newPlate = AddPlate(nextPlateNumber, $"Plate {nextPlateNumber} ({myPlate.name} tear)", colors[(nextPlateNumber * 3) % colors.Count], mesh);
                newPlate.SetPlateRenderCount(myPlate.GetPlateRenderCount());
                newPlate.gameObject.layer = LayerMask.NameToLayer("TornPlates");
                newPlate.Finalize(wireframe);
                newPlate.LockScaledNetForce(myPlate.GetScaledNetForce());

                for (int v = 0; v < newPlate.mesh.vertices.Length; v++)
                {
                    if (newPlate.mesh.vertices[v].magnitude > Plate.coreRadius)
                    {
                        newPlate.vertexSubductionState[v] = SubductionState.Active;
                        newPlate.AddReal(v);
                    }
                }
                
            }
        }

        private void CheckForPlateDeath()
        {
            var myPlate = plates[curPlate];

            if (myPlate.GetReal().Count == 0)
            {
                plates.Remove(myPlate);
                Destroy(myPlate.gameObject);
                Destroy(myPlate);
            }
        }

        private void InitializeShrinkwrap()
        {
            Mesh mesh = planet.GetComponent<MeshFilter>().mesh;
            var mask = LayerMask.GetMask("Plates");
            var qparams = new QueryParameters(mask, true, QueryTriggerInteraction.Ignore, false);
            planetVertices = mesh.vertices;
            planetNeighbors = Plate.ExtractNeighbors(mesh);

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
                    var avgHeight = planetNeighbors[i].Select(v => planetVertices[v].magnitude).Average();
                    planetVertices[i] = planetVertices[i].normalized * avgHeight;
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

        // public void OnDrawGizmos()
        // {
        //     if (ui == null || plates == null)
        //         return;

        //     if (ui.IsDisplayFeatureOn(DisplayFeature.Convection))
        //     {
        //         var convectionMesh = convector.GetMesh();
        //         var vertices = convectionMesh.vertices;
        //         var normals = convectionMesh.normals;

        //         // Don't draw any on the far side of the planet, it just gets confusing
        //         var cameraPos = Camera.main.transform.position;
        //         var cameraDist = cameraPos.magnitude;
        //         var radius = Plate.seaFloorRadius;
        //         var tangentDist = Mathf.Sqrt(cameraDist * cameraDist - radius * radius);

        //         for (int i = 0; i < vertices.Length; i++)
        //         {
        //             var vertex = vertices[i];
        //             var vertexDist = (vertices[i] - cameraPos).magnitude;
        //             if (vertexDist < tangentDist)
        //             {
        //                 Gizmos.color = new Color(normals[i].x, normals[i].y, normals[i].z);
        //                 Gizmos.DrawLine(vertex, vertex + normals[i]);
        //             }
        //         }
        //     }
        // }

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

            if (ui.IsDisplayFeatureOn(DisplayFeature.Hotspots))
            {
                GetComponent<Camera>().cullingMask |= 1 << LayerMask.NameToLayer("Hotspots");
            }
            else
            {
                GetComponent<Camera>().cullingMask &= ~(1 << LayerMask.NameToLayer("Hotspots"));
            }

            for (var i = 0; i < plates.Count; i++)
            {
                if (ui.IsDisplayFeatureOn(DisplayFeature.PlateMeshes)) {
                    plates[i].SetMeshDisplay(ui.GetPlateMeshDisplayMode(i));
                } else {
                    plates[i].SetMeshDisplay(PlateMeshDisplayMode.None);
                }
            }

            if (ui.IsPlaying())
            {
                // Randomize the order of plates each time to reduce uneven event distribution when the number of plates evenly divides the number of steps
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
                            CalculatePlateForces();
                        }
                        break;
                    case Phase.Tectonic2:
                        if (ui.IsSubsystemFeatureOn(SubsystemFeature.Tectonics))
                        {
                            ApplyForces1();
                        }
                        break;
                    case Phase.Tectonic3:
                        if (ui.IsSubsystemFeatureOn(SubsystemFeature.Tectonics))
                        {
                            ApplyForces2();
                        }
                        break;
                    case Phase.Subduction:
                        if (ui.IsSubsystemFeatureOn(SubsystemFeature.CollisionCheck))
                        {
                            PlateCollisions.SubductionCheck(curPlateOrder[curPlate], subductionTracker);
                            ApotheosizeTornMeshes();
                            CheckForPlateDeath();
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
                            curPlateOrder[curPlate].MORBingTime(subductionTracker);
                        }
                        break;
                    case Phase.Erosion:
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

                curPlate = (curPlate + 1) % curPlateOrder.Count();
                renderCount++;
            }

            ui.UpdateTimestamp(renderCount);

            ui.UpdatePlateID();

            if (renderCount % 100 == 0)
            {
                //subductionTracker.PrintState();
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

        private void OnDestroy()
        {
            raycastCommandsShrinkwrap.Dispose();
            raycastHitsShrinkwrap.Dispose();
        }
    }
}