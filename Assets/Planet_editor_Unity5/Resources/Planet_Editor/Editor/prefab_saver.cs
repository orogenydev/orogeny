using UnityEditor;
using UnityEngine;
using System;  

// Creates a prefab from a selected game object.

class prefab_saver 
{
    const string menuName = "GameObject/Create Prefab From Selected";
  
   
    // Adds a menu named "Create Prefab From Selected" to the GameObject menu.
   
    [MenuItem(menuName)]
    static void CreatePrefabMenu ()
    {
		UnityEngine.GameObject go_ = Selection.activeGameObject;
		UnityEngine.GameObject go = (UnityEngine.GameObject)DuplicateSelected(go_);



		go.GetComponent<MeshFilter>().mesh = CopyMesh(go_.GetComponent<MeshFilter>().mesh);
		go.GetComponent<Renderer>().material.shader = go_.GetComponent<Renderer>().material.shader;
		go.GetComponent<Renderer>().material.CopyPropertiesFromMaterial(go_.GetComponent<Renderer>().material);

		go.transform.localScale = go_.transform.localScale;

		go.transform.Find("Glow").GetComponent<Renderer>().material.CopyPropertiesFromMaterial(go_.transform.Find("Glow").GetComponent<Renderer>().material);
		//go.transform.Find("clouds_sphere").GetComponent<Renderer>().material.CopyPropertiesFromMaterial(go_.transform.Find("Glow").GetComponent<Renderer>().material);
		//go.transform.Find("water_sphere").GetComponent<Renderer>().material.CopyPropertiesFromMaterial(go_.transform.Find("Glow").GetComponent<Renderer>().material);

		go.transform.Find("Glow").transform.localScale = go_.transform.Find("Glow").transform.localScale;
		go.transform.Find("clouds_sphere").transform.localScale = go_.transform.Find("clouds_sphere").transform.localScale;
		go.transform.Find("water_sphere").transform.localScale = go_.transform.Find("water_sphere").transform.localScale;



		DateTime epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		int currentEpochTime = (int)(DateTime.UtcNow - epochStart).TotalSeconds;
		var name = go.name + currentEpochTime ;
		//Debug.Log(go.GetComponent<MeshFilter>().mesh);
		Mesh m1 =go.GetComponent<MeshFilter>().mesh;//getting planet mesh
		//Debug.Log(m1);
		AssetDatabase.CreateAsset(m1, "Assets/Planet_editor_Unity5/Resources/savedMesh/" + name +"_M" + ".asset"); // saving mesh as asset
  
  
		var prefab =PrefabUtility.CreateEmptyPrefab("Assets/Planet_editor_Unity5/Resources/savedMesh/" + name+ ".prefab");//creating empty prefab
		var material_pl = go.GetComponent<Renderer>().material;
		var material_gl = go.transform.Find("Glow").GetComponent<Renderer>().material;
		var material_cl = go.transform.Find("clouds_sphere").GetComponent<Renderer>().material;
		var material_w = go.transform.Find("water_sphere").GetComponent<Renderer>().material;
		
		
		AssetDatabase.CreateAsset(material_pl, "Assets/Planet_editor_Unity5/Resources/savedMesh/mat_pl_"+name+".mat");//saving materials
		AssetDatabase.CreateAsset(material_gl, "Assets/Planet_editor_Unity5/Resources/savedMesh/mat_gl_"+name+".mat");//saving materials
		AssetDatabase.CreateAsset(material_cl, "Assets/Planet_editor_Unity5/Resources/savedMesh/mat_cl_"+name+".mat");//saving materials
		AssetDatabase.CreateAsset(material_w, "Assets/Planet_editor_Unity5/Resources/savedMesh/mat_w_"+name+".mat");//saving materials
		
		go.GetComponent<Renderer>().material=Resources.Load("savedMesh/mat_pl_"+name) as Material;//loading materials and assigning them to a prefab
		go.transform.Find("Glow").GetComponent<Renderer>().material=Resources.Load("savedMesh/mat_gl_"+name) as Material;//loading materials and assigning them to a prefab
		go.transform.Find("clouds_sphere").GetComponent<Renderer>().material=Resources.Load("savedMesh/mat_cl_"+name) as Material;//loading materials and assigning them to a prefab
		go.transform.Find("water_sphere").GetComponent<Renderer>().material=Resources.Load("savedMesh/mat_w_"+name) as Material;//loading materials and assigning them to a prefab
		
		PrefabUtility.ReplacePrefab(go, prefab);
		
	



        AssetDatabase.Refresh();
		UnityEngine.Object.DestroyImmediate(go);
    }
  

	public static UnityEngine.Object DuplicateSelected (UnityEngine.Object obj)
	{
		UnityEngine.Object prefabRoot = PrefabUtility.GetCorrespondingObjectFromSource (obj);
		UnityEngine.Object obj_;
        
		if (prefabRoot != null)
			 obj_ = PrefabUtility.InstantiatePrefab (prefabRoot);
		else
			obj_ = GameObject.Instantiate(Selection.activeGameObject as GameObject) as GameObject;

        return obj_;
	}

	static Mesh CopyMesh(Mesh mesh)
	{
		
		Mesh newmesh = new Mesh();
		newmesh.vertices = mesh.vertices;
		newmesh.triangles = mesh.triangles;
		newmesh.uv = mesh.uv;
		newmesh.normals = mesh.normals;
		newmesh.colors = mesh.colors;
		newmesh.tangents = mesh.tangents;
		return newmesh;

	}

    // Validates the menu.
    // The item will be disabled if no game object is selected.

    // <returns>True if the menu item is valid.</returns>
    [MenuItem(menuName, true)]
    static bool ValidateCreatePrefabMenu ()
    {
        return Selection.activeGameObject != null;
    }
	
}