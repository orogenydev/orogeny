using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Orogeny.Meshes
{
    public class MeshUtils
    {
        public static void MeshDumpToFile(Mesh mesh, string filename)
        {
            System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bf = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            System.IO.FileStream fs = new System.IO.FileStream(Application.dataPath + "\\" + filename, System.IO.FileMode.Create);
            SerializableMeshInfo smi = new SerializableMeshInfo(mesh);
            bf.Serialize(fs, smi);
            fs.Close();
        }

        public static string MeshDump(Mesh mesh)
        {
            SerializableMeshInfo smi = new SerializableMeshInfo(mesh);
            string jsonString = JsonUtility.ToJson(smi);
            return jsonString;
        }

        public static Mesh MeshUndumpFromFile(string filename)
        {
            if (!System.IO.File.Exists(Application.dataPath + "\\" + filename))
            {
                Debug.LogError(filename + " file does not exist.");
                return null;
            }
            System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bf = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            System.IO.FileStream fs = new System.IO.FileStream(Application.dataPath + "\\" + filename, System.IO.FileMode.Open);
            SerializableMeshInfo smi = (SerializableMeshInfo)bf.Deserialize(fs);
            var mesh = smi.GetMesh();
            fs.Close();

            return mesh;
        }

        public static Mesh MeshUndump(string jsonString)
        {
            SerializableMeshInfo smi = JsonUtility.FromJson<SerializableMeshInfo>(jsonString);
            return smi.GetMesh();
        }
    }
}