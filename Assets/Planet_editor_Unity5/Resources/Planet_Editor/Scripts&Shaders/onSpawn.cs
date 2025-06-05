using System.Collections;
using UnityEngine;

public class onSpawn : MonoBehaviour
{
    // Use this for initialization
    void Start()
    {
        MeshFilter pl_mesh = this.GetComponent<MeshFilter>();
        GameObject
            .Find("Main Camera")
            .gameObject
            .SendMessage("Generate", pl_mesh.mesh);
    }

    // Update is called once per frame
    void Update()
    {
    }
}
