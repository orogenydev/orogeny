using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

using Orogeny.Maps;

namespace Orogeny {
	public enum SubsystemFeature {
		Tectonics,
		CollisionCheck,
		ClearAndShrink,
		Volcanism,
		MORB,
		Unused
	}

	public enum Breakpoint {
		AnyCollision,
		Subduction,
		ContinentalSquish,
		MagmaBirth,
		MagmaEruption,
		MORB,
		MagmaDeath
	}

	public enum DisplayFeature {
		Terrain,
		PlateMeshes,
		PlatePerimeterPoints,
		EventMarkers,
		Convection,
		Unused
	}

	public class UserInterface : MonoBehaviour {
		public Transform planet;
		public TextMeshProUGUI fps;
		public TextMeshProUGUI latLon;
		public UnityEngine.UI.Image playButtonImage;
		public UnityEngine.UI.Image pauseButtonImage;

		public UnityEngine.UI.Toggle subsystemFeatureToggle1;
		public UnityEngine.UI.Toggle subsystemFeatureToggle2;
		public UnityEngine.UI.Toggle subsystemFeatureToggle3;
		public UnityEngine.UI.Toggle subsystemFeatureToggle4;
		public UnityEngine.UI.Toggle subsystemFeatureToggle5;
		public UnityEngine.UI.Toggle subsystemFeatureToggle6;

		public UnityEngine.UI.Toggle breakpointToggle1;
		public UnityEngine.UI.Toggle breakpointToggle2;
		public UnityEngine.UI.Toggle breakpointToggle3;
		public UnityEngine.UI.Toggle breakpointToggle4;
		public UnityEngine.UI.Toggle breakpointToggle5;
		public UnityEngine.UI.Toggle breakpointToggle6;

		public UnityEngine.UI.Toggle displayFeatureToggle1;
		public UnityEngine.UI.Toggle displayFeatureToggle2;
		public UnityEngine.UI.Toggle displayFeatureToggle3;
		public UnityEngine.UI.Toggle displayFeatureToggle4;
		public UnityEngine.UI.Toggle displayFeatureToggle5;
		public UnityEngine.UI.Toggle displayFeatureToggle6;

		public static int radius = 25;

		private static UnityEngine.UI.Toggle[] subsystemFeatureToggles;
		private static UnityEngine.UI.Toggle[] breakpointToggles;
		private static UnityEngine.UI.Toggle[] displayFeatureToggles;

		private int[] lastFrames = new int[5] {0, 0, 0, 0, 0};

		private bool[] subsystemFeatures;
		private bool[] breakpoints;
		private bool[] displayFeatures;
		private bool isPlaying;

        private Vector2 dragOrigin = Vector2.zero;

		private float lonOffset = 157;
		private float lonOffsetBase = 157;

		void Start() {
			InitToggles();

			SetPlaying(false);

			InvokeRepeating(nameof(UpdateFPS), 1, 0.5f);
		}

		void UpdateFPS() {
			for (int i = 4; i > 0; i--) {
				lastFrames[i] = lastFrames[i - 1];
			}
			lastFrames[0] = Time.frameCount;
			fps.text = "FPS: " + (Time.frameCount - lastFrames[4]) / 2.5f;
		}

		void LateUpdate() {
			float multiplier = 2;

			if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
				multiplier = 8;
			} else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) {
				multiplier = 0.5f;
			}

			// Don't zoom past the center of the planet, it gets confusing
			var here = this.transform.position;
			var hopLength = multiplier * radius;
			var dir = Input.GetAxis("Mouse ScrollWheel");

			var destination = here - dir *
				(here - planet.transform.position).normalized * hopLength;

			if (dir < 0 || (destination - here).magnitude < here.magnitude) {
				this.transform.position = destination;
			}

			// Left/right arrows
			this.transform.RotateAround(planet.transform.position,
										Camera.main.transform.up,
										Input.GetAxis("Horizontal") * multiplier);
			
			// Up/down arrows
			this.transform.RotateAround(planet.transform.position,
										Camera.main.transform.right,
										-Input.GetAxis("Vertical") * multiplier);

			// Z/X keys
			this.transform.RotateAround(planet.transform.position,
										Camera.main.transform.forward,
										Input.GetAxis("Rotation") * multiplier);

			CheckToggles();
			UpdateLatLon();
		}

		public void MouseRotation(float x, float y) {
			float scale = 0.4f;

			this.transform.RotateAround(planet.transform.position,
										Camera.main.transform.up,
										-x * scale);
			
			this.transform.RotateAround(planet.transform.position,
										Camera.main.transform.right,
										 y * scale);

			UpdateLatLon();
		}

		public void SetLonOffset(float _lonOffset) {
			lonOffset = normalizeLongitude(-(_lonOffset - 90)); // spooky magic numbers
		}

		void UpdateLatLon() {
			var latDir = "N";
			var lonDir = "E";
			(float lon, float lat) = MapProjection.Vector2Geodetic(this.transform.position);

			// Lat 0 = N, 180 = S
			lat -= 90;
			lat = (int)lat;
			if (lat > 0) {
				latDir = "S";
			}
			lat = Mathf.Abs(lat);

			// [-180, 180]
			// Lon < 0 = E, > 0 = W 
	        // Default camera position == 0,0 no matter what the coordinate system says
			lon = (lon - lonOffset);
			lon = normalizeLongitude(lon);
			lon = (int)lon;
			if (lon >= 0) {
				lonDir = "W";
			}
			lon = Mathf.Abs(lon);

			latLon.text = latDir + " " + (int)lat + "°, " + lonDir + " " + (int)lon + "°";
		}

		private float normalizeLongitude(float lon) {
			if (lon < -180) {
				lon = 180 - (-180 - lon);
			}

			if (lon > 180) {
				lon = -180 - (180 - lon);
			}

			return lon;
		}

		public bool IsPlaying() {
			return isPlaying;
		}

		public void TriggerBreakpoint(Breakpoint bp) {
			if (IsBreakpointOn(bp)) {
				SetPlaying(false);
				Debug.Log("Breakpoint triggered: " + bp);
			}
		}

		private void SetPlaying(bool value) {
			isPlaying = value;
			pauseButtonImage.enabled = value;
			playButtonImage.enabled = !value;
		}

 		private void InitToggles() {
			subsystemFeatures = new bool[System.Enum.GetNames(typeof(SubsystemFeature)).Length];
			breakpoints = new bool[System.Enum.GetNames(typeof(Breakpoint)).Length];
			displayFeatures = new bool[System.Enum.GetNames(typeof(DisplayFeature)).Length];

			subsystemFeatureToggles = new UnityEngine.UI.Toggle[] { subsystemFeatureToggle1,
																	subsystemFeatureToggle2,
																	subsystemFeatureToggle3,
																	subsystemFeatureToggle4,
																	subsystemFeatureToggle5,
																	subsystemFeatureToggle6 };

			breakpointToggles = new UnityEngine.UI.Toggle[] { breakpointToggle1, 
																breakpointToggle2,
																breakpointToggle3,
																breakpointToggle4,
																breakpointToggle5,
																breakpointToggle6 };

			displayFeatureToggles = new UnityEngine.UI.Toggle[] { displayFeatureToggle1, 
																displayFeatureToggle2,
																displayFeatureToggle3,
																displayFeatureToggle4,
																displayFeatureToggle5,
																displayFeatureToggle6 };
		}

		private void CheckToggles() {
			for (var i = 0; i < subsystemFeatureToggles.Length; i++) {
				subsystemFeatures[i] = subsystemFeatureToggles[i].isOn;
			}

			for (var i = 0; i < breakpointToggles.Length; i++) {
				breakpoints[i] = breakpointToggles[i].isOn;
			}

			for (var i = 0; i < displayFeatureToggles.Length; i++) {
				displayFeatures[i] = displayFeatureToggles[i].isOn;
			}
		}

		public bool IsSubsystemFeatureOn(SubsystemFeature rf) {
			return subsystemFeatures[(int)rf];
		}

		public bool IsBreakpointOn(Breakpoint bp) {
			return breakpoints[(int)bp];
		}

		public bool IsDisplayFeatureOn(DisplayFeature gf) {
			return displayFeatures[(int)gf];
		}

		public void HandleResetButtonClick() {
			//Debug.Log("Reset button clicked");

			// Go back to original position
			// Then rotate as needed to account for current lonOffset
			// Otherwise planet screen position gets weird!
			var pos = MapProjection.Geodetic2Vector(-67, 0);
			this.transform.position = pos * new Vector3(26, 0, -62).magnitude;
			this.transform.eulerAngles = Vector3.zero;
			this.transform.RotateAround(planet.transform.position,
										Camera.main.transform.up,
										lonOffset - lonOffsetBase);
		}

		public void HandlePlayButtonClick() {
			//Debug.Log("Play button clicked");

			SetPlaying(true);
		}

		public void HandlePauseButtonClick() {
			//Debug.Log("Pause button clicked");

			SetPlaying(false);
		}
	}
}