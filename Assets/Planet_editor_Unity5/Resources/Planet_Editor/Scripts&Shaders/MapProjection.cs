using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

using Orogeny.Plates;

namespace Orogeny.Maps {
    public class MapProjection : MonoBehaviour
    {
        public Transform planet;
        public Texture2D texture;
        public UnityEngine.UI.Image mapBorder;
        public TMPro.TMP_Dropdown dropdown;
        public GameObject mainCamera;

        private Color[] textColors0;
        private Color[] textColors1;
        private Color[] textColors2;
        private Color[] textColors3;
        private Color[] textColors4;
        private Color[] textColors5;
        private Color[] textColors6;

        private float[] blends = new float[12];

        private static IProjection[] projections = new IProjection[] { new ProjectionNaturalEarth(),
                                                                        new ProjectionOrthographicWE(),
                                                                        new ProjectionOrthographicNS(),
                                                                        new ProjectionOrthographicNESW(),
                                                                        new ProjectionMercator(),
                                                                        new ProjectionEquirectangular(),
                                                                        new ProjectionNone() };
        private IProjection curProjection;

        // Default camera position == 0,0 no matter what the coordinate system says
        private float lonRadOffset = -67 * Mathf.Deg2Rad;
        private float lonRadOffsetDragStart = 0;
        private Vector2 dragOrigin = Vector2.zero;

        private bool firstFrame = true;

        private int divisions = 16;
        private int curDivision = 0;
        private JobHandle jobHandle;
        private List<NativeArray<RaycastCommand>> raycastCommands;
        private List<NativeArray<RaycastCommand>> raycastCommandsWithMarkers;
        private List<NativeArray<RaycastHit>> raycastHits;

        private UserInterface ui;

        public static Vector3 Geodetic2Vector(float lon, float lat) {
            var lonRad = lon * Mathf.Deg2Rad;
            var latRad = (Mathf.PI / 2) - lat * Mathf.Deg2Rad; // [0, pi]

            var x = Mathf.Sin(latRad) * Mathf.Cos(lonRad);
            var y = Mathf.Cos(latRad);
            var z = Mathf.Sin(latRad) * Mathf.Sin(lonRad);

            return new Vector3(x, y, z).normalized;
        }

        public static (float, float) Vector2Geodetic(Vector3 position) {
            float lat = Mathf.Acos(position.y / position.magnitude) * Mathf.Rad2Deg;
        	float lon = Mathf.Atan2(position.x, position.z) * Mathf.Rad2Deg;

            return (lon, lat);
        }

        void Start() {
            InitProjections();

            raycastCommands = new List<NativeArray<RaycastCommand>>();
            raycastCommandsWithMarkers = new List<NativeArray<RaycastCommand>>();
            raycastHits = new List<NativeArray<RaycastHit>>();

            for (int i = 0; i < texture.width; i++) {
                for (int j = 0; j < texture.height; j++) {
                    texture.SetPixel(i, j, Color.black);
                }
            }

            texture.Apply();

            ui = mainCamera.GetComponent<UserInterface>();
        }

        private void OnDestroy() {
            jobHandle.Complete();
            foreach (var commandArray in raycastCommands) {
                commandArray.Dispose();
            }
            foreach (var commandArray in raycastCommandsWithMarkers) {
                commandArray.Dispose();
            }
            foreach (var hitArray in raycastHits) {
                hitArray.Dispose();
            }
        }

        private void InitProjections() {
            List<string> options = new List<string>();

            foreach (var projection in projections) {
                options.Add(projection.GetName());
            }

            dropdown.ClearOptions();
            dropdown.AddOptions(options);

            var defaultProjection = 0;

            HandleProjectionSelection(defaultProjection);
            dropdown.value = defaultProjection;
        }

        void Update() {
            if (curProjection.GetName() == "None") {
                return;
            }

            var width = texture.width;
            var height = texture.height;
            var pointsPerDivision = width * height / divisions;

            if (firstFrame) {
                InitializeColors();

                foreach (var commandArray in raycastCommands) {
                    commandArray.Dispose();
                }
                foreach (var commandArray in raycastCommandsWithMarkers) {
                    commandArray.Dispose();
                }
                foreach (var hitArray in raycastHits) {
                    hitArray.Dispose();
                }

                raycastCommands = new List<NativeArray<RaycastCommand>>();
                raycastCommandsWithMarkers = new List<NativeArray<RaycastCommand>>();
                raycastHits = new List<NativeArray<RaycastHit>>();
                for (var i = 0; i < divisions; i++) {
                    raycastCommands.Add(new NativeArray<RaycastCommand>(pointsPerDivision, Allocator.Persistent));
                    raycastCommandsWithMarkers.Add(new NativeArray<RaycastCommand>(pointsPerDivision, Allocator.Persistent));
                    raycastHits.Add(new NativeArray<RaycastHit>(pointsPerDivision, Allocator.Persistent));
                }

                for (int i = 0; i < width; i++) {
                    for (int j = 0; j < height; j++) {
                        texture.SetPixel(i, j, Color.black);

                        var (valid, point) = TranslateCartesian(i, j);

                        if (!valid) {
                            continue;
                        }

                        var index = i * height + j;

                        var commandArray = raycastCommands[index / pointsPerDivision];
                        var mask = LayerMask.GetMask("Geoid");
                        commandArray[index % pointsPerDivision] = GetPointRaycast(point, mask);

                        commandArray = raycastCommandsWithMarkers[index / pointsPerDivision];
                        mask = LayerMask.GetMask("Geoid", "EventMarkers");
                        commandArray[index % pointsPerDivision] = GetPointRaycast(point, mask);
                    }
                }

                curDivision = 0;
                firstFrame = false;
            }

            JobHandle handle;
            if (ui.IsDisplayFeatureOn(DisplayFeature.EventMarkers)) {
                handle = RaycastCommand.ScheduleBatch(raycastCommandsWithMarkers[curDivision],
                                                                raycastHits[curDivision],
                                                                1,
                                                                default(JobHandle));
            } else {
                handle = RaycastCommand.ScheduleBatch(raycastCommands[curDivision],
                                                                raycastHits[curDivision],
                                                                1,
                                                                default(JobHandle));
            }
            handle.Complete();

            var cameraPos = Camera.main.transform.position;
            var cameraDist = cameraPos.magnitude;
            var tangentDist = 0f;

            if (cameraDist > Plate.seaFloorRadius) {
                tangentDist = Mathf.Sqrt(cameraDist * cameraDist - Plate.seaFloorRadius * Plate.seaFloorRadius);
            }
            
            for (int i = 0; i < pointsPerDivision; i++) {
                RaycastHit hit = raycastHits[curDivision][i];

                if (hit.collider != null) {
                    var index = curDivision * pointsPerDivision + i;
                    var color = Color.black;

                    if (hit.collider.name == "EventMORB") {
                        color = Color.red;                        
                    } else {
                        // Change pixel value based on point distance to camera
                        // to create a terminator line showing what is visible on the globe
                        var pointDist = (hit.point - cameraPos).magnitude;
                        float scale = 1.0f - Logistic(0.25f, 1f, 0, pointDist - tangentDist);
                        color = GetRaycastColor(hit);
                        Color.RGBToHSV(color, out var h, out var s, out var v);
                        color = Color.HSVToRGB(h, s, v * scale);
                    }

                    texture.SetPixel(index / height, index % height, color);
                }
            }

            for (float lon = -180; lon < 180; lon += 1) {
                for (float lat = -90; lat < 90; lat += 30) {
                    AddGridPoint(lon, lat);
                }
            }

            for (float lat = -90; lat < 90; lat += 1) {
                for (float lon = -180; lon < 180; lon += 30) {
                    AddGridPoint(lon, lat);
                }
            }

            curDivision = (curDivision + 1) % divisions;

            if (curDivision == 0) {
                texture.Apply();
            }
        }

        public void AddGridPoint(float lon, float lat) {
            var lonRad = lon * Mathf.Deg2Rad;
            var latRad = lat * Mathf.Deg2Rad;
            var points = curProjection.GetMapCoordinates(lonRad, latRad, texture.width, texture.height);

            foreach (var point in points) {
                texture.SetPixel((int)point.x, (int)point.y, new Color(0.05f, 0.05f, 0.05f, 1));
            }
        }

        public float Logistic(float l, float k, float x0, float x) {
            return l / (1 + Mathf.Exp(-1 * k * (x - x0)));
        }

        public void HandleProjectionSelection(int val) {
            var startWidth = 400;
            var startHeight = texture.height;

            curProjection = projections[val];

            (var width, var height) = curProjection.GetSize(startWidth, startHeight);

            mapBorder.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width + 10);
            mapBorder.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height + 10);
            texture.Reinitialize(width, height);
            firstFrame = true;

            Debug.Log("Map resize for " + curProjection.GetName() + ": " + texture.width + " x " + texture.height);
        }

        public void HandlePointerDown(BaseEventData data) {
            PointerEventData p = (PointerEventData)data;
            dragOrigin = p.position;
            lonRadOffsetDragStart = lonRadOffset;
        }

        public void HandleDrag(BaseEventData data) {
            PointerEventData p = (PointerEventData)data;
            var pixelDelta = dragOrigin.x - p.position.x;
            var circ = 2 * Mathf.PI;
            var lonRadDelta = (pixelDelta / texture.width) * circ;
            lonRadOffset = (lonRadOffsetDragStart + lonRadDelta) % circ;
            firstFrame = true;

            ui.SetLonOffset(lonRadOffset * Mathf.Rad2Deg);
        }

        private (bool, Vector3) TranslateCartesian(int i, int j) {
            // Convert to geodetic (radians)
            // lonRad [0, 2pi], latRad [-pi/2, pi/2]

            float lonRad = 0, latRad = 0;
            bool valid = false;
            (valid, lonRad, latRad) = curProjection.GetGeodeticCoordinates(i, j, texture.width, texture.height);

            if (!valid || float.IsNaN(lonRad) || float.IsNaN(latRad)) {
                return (false, Vector3.zero);
            }

            // Convert to world point
            lonRad += lonRadOffset;
            latRad = (Mathf.PI / 2) - latRad; // [0, pi]
            var x = Mathf.Sin(latRad) * Mathf.Cos(lonRad);
            var y = Mathf.Cos(latRad);
            var z = Mathf.Sin(latRad) * Mathf.Sin(lonRad);
            
            var vector = new Vector3(x, y, z) * 25;

            return (true, vector);
        }

        private RaycastCommand GetPointRaycast(Vector3 point, LayerMask mask) {
            // Failing when Z is too close to 0, no clue why
            if (Mathf.Abs(point.z) < 0.00001) {
                point.z = 0.00001f;
            }

            // Out and then back in to hit the right side of the mesh
            Ray ray = new Ray(Vector3.zero, point);
            ray.origin = ray.GetPoint(50);
            ray.direction = -ray.direction;

            return new RaycastCommand(ray.origin, ray.direction, 50 - Plate.mohoRadius, mask);
        }

        private Color GetRaycastColor(RaycastHit raycastHit) {
            // Calculate distance from origin
            var altitude = raycastHit.point.magnitude;

            var color = InterpolateColor(blends, altitude, raycastHit.point);

            return color;
        }

        // Reimplement shader conditional/lerp code
        private Color InterpolateColor(float[] blends, float altitude, Vector2 point) {
            if (altitude < blends[0]) 
                return GetColor(textColors0, point);

            if (altitude < blends[1]) 
                return Color.Lerp(GetColor(textColors0, point),
                                GetColor(textColors1, point),
                                ((altitude - blends[0]) / (blends[1] - blends[0])));

            if (altitude < blends[2])
                return GetColor(textColors1, point);

            if (altitude < blends[3])
                return Color.Lerp(GetColor(textColors1, point),
                                GetColor(textColors2, point),
                                ((altitude - blends[2]) / (blends[3] - blends[2])));

            if (altitude < blends[4])
                return GetColor(textColors2, point);

            if (altitude < blends[5])
                return Color.Lerp(GetColor(textColors2, point),
                                GetColor(textColors3, point),
                                ((altitude - blends[4]) / (blends[5] - blends[4])));

            if (altitude < blends[6])
                return GetColor(textColors3, point);

            if (altitude < blends[7])
                return Color.Lerp(GetColor(textColors3, point),
                                GetColor(textColors4, point),
                                ((altitude - blends[6]) / (blends[7] - blends[6])));

            if (altitude < blends[8])
                return GetColor(textColors4, point);

            if (altitude < blends[9])
                return Color.Lerp(GetColor(textColors4, point),
                                GetColor(textColors5, point),
                                ((altitude - blends[8]) / (blends[9] - blends[8])));

            if (altitude < blends[10])
                return GetColor(textColors5, point);

            if (altitude < blends[11])
                return Color.Lerp(GetColor(textColors5, point),
                                GetColor(textColors6, point),
                                ((altitude - blends[10]) / (blends[11] - blends[10])));

            return GetColor(textColors6, point); 
        }

        private Color GetColor(Color[] textColor, Vector2 coords) {
            var x = Mathf.Abs((int)(coords.x * 512)) % 512;
            var y = Mathf.Abs((int)(coords.y * 512)) % 512;
            
            return textColor[x * 512 + y];
        }

        private void InitializeColors() {
            var renderer = planet.GetComponent<Renderer>();
            var material = renderer.sharedMaterial;

            textColors0 = ((Texture2D)material.GetTexture("_Tex0")).GetPixels();
            textColors1 = ((Texture2D)material.GetTexture("_Tex1")).GetPixels();
            textColors2 = ((Texture2D)material.GetTexture("_Tex2")).GetPixels();
            textColors3 = ((Texture2D)material.GetTexture("_Tex3")).GetPixels();
            textColors4 = ((Texture2D)material.GetTexture("_Tex4")).GetPixels();
            textColors5 = ((Texture2D)material.GetTexture("_Tex5")).GetPixels();
            textColors6 = ((Texture2D)material.GetTexture("_Tex6")).GetPixels();

            var prop012 = material.GetVector("_Blend0to1and1to2");
            var prop234 = material.GetVector("_Blend2to3and3to4");
            var prop456 = material.GetVector("_Blend4to5and5to6");

            blends[0] = prop012.x;
            blends[1] = prop012.y;
            blends[2] = prop012.z;
            blends[3] = prop012.w;
            blends[4] = prop234.x;
            blends[5] = prop234.y;
            blends[6] = prop234.z;
            blends[7] = prop234.w;
            blends[8] = prop456.x;
            blends[9] = prop456.y;
            blends[10] = prop456.z;
            blends[11] = prop456.w;
        }
    }
}