using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Orogeny.Meshes {
    public static class IcoSphereCreator {
        public static Mesh Create(int n, float radius) {
            int nn = n * 4;
            int vertexNum = (nn * nn / 16) * 24;
            Vector3[] vertices = new Vector3[vertexNum];
            int[] triangles = new int[vertexNum];
            Vector4[] tangents = new Vector4[vertexNum];
            Vector2[] uv = new Vector2[vertexNum];
            Vector2[] uv2 = new Vector2[vertexNum];

            Quaternion[] init_vectors = new Quaternion[24];
            // 0
            init_vectors[0] = new Quaternion(0, 1, 0, 0);   //the triangle vertical to (1,1,1)
            init_vectors[1] = new Quaternion(0, 0, 1, 0);
            init_vectors[2] = new Quaternion(1, 0, 0, 0);
            // 1
            init_vectors[3] = new Quaternion(0, -1, 0, 0);  //to (1,-1,1)
            init_vectors[4] = new Quaternion(1, 0, 0, 0);
            init_vectors[5] = new Quaternion(0, 0, 1, 0);
            // 2
            init_vectors[6] = new Quaternion(0, 1, 0, 0);   //to (-1,1,1)
            init_vectors[7] = new Quaternion(-1, 0, 0, 0);
            init_vectors[8] = new Quaternion(0, 0, 1, 0);
            // 3
            init_vectors[9] = new Quaternion(0, -1, 0, 0);  //to (-1,-1,1)
            init_vectors[10] = new Quaternion(0, 0, 1, 0);
            init_vectors[11] = new Quaternion(-1, 0, 0, 0);
            // 4
            init_vectors[12] = new Quaternion(0, 1, 0, 0);  //to (1,1,-1)
            init_vectors[13] = new Quaternion(1, 0, 0, 0);
            init_vectors[14] = new Quaternion(0, 0, -1, 0);
            // 5
            init_vectors[15] = new Quaternion(0, 1, 0, 0); //to (-1,1,-1)
            init_vectors[16] = new Quaternion(0, 0, -1, 0);
            init_vectors[17] = new Quaternion(-1, 0, 0, 0);
            // 6
            init_vectors[18] = new Quaternion(0, -1, 0, 0); //to (-1,-1,-1)
            init_vectors[19] = new Quaternion(-1, 0, 0, 0);
            init_vectors[20] = new Quaternion(0, 0, -1, 0);
            // 7
            init_vectors[21] = new Quaternion(0, -1, 0, 0);  //to (1,-1,-1)
            init_vectors[22] = new Quaternion(0, 0, -1, 0);
            init_vectors[23] = new Quaternion(1, 0, 0, 0);
            
            int j = 0;  //index on vectors[]

            for (int i = 0; i < 24; i += 3) {
                /*
                *                   c _________d
                *    ^ /\           /\        /
                *   / /  \         /  \      /
                *  p /    \       /    \    /
                *   /      \     /      \  /
                *  /________\   /________\/
                *     q->       a         b
                */
                for (int p = 0; p < n; p++) {
                    //edge index 1
                    Quaternion edge_p1 = Quaternion.Lerp(init_vectors[i], init_vectors[i + 2], (float)p / n);
                    Quaternion edge_p2 = Quaternion.Lerp(init_vectors[i + 1], init_vectors[i + 2], (float)p / n);
                    Quaternion edge_p3 = Quaternion.Lerp(init_vectors[i], init_vectors[i + 2], (float)(p + 1) / n);
                    Quaternion edge_p4 = Quaternion.Lerp(init_vectors[i + 1], init_vectors[i + 2], (float)(p + 1) / n);

                    for (int q = 0; q < (n - p); q++) {
                        //edge index 2
                        Quaternion a = Quaternion.Lerp(edge_p1, edge_p2, (float)q / (n - p));
                        Quaternion b = Quaternion.Lerp(edge_p1, edge_p2, (float)(q + 1) / (n - p));
                        Quaternion c, d;
                        if (edge_p3 == edge_p4) {
                            c = edge_p3;
                            d = edge_p3;
                        } else {
                            c = Quaternion.Lerp(edge_p3, edge_p4, (float)q / (n - p - 1));
                            d = Quaternion.Lerp(edge_p3, edge_p4, (float)(q + 1) / (n - p - 1));
                        }

                        triangles[j] = j;
                        vertices[j++] = new Vector3(a.x, a.y, a.z);
                        triangles[j] = j;
                        vertices[j++] = new Vector3(b.x, b.y, b.z);
                        triangles[j] = j;
                        vertices[j++] = new Vector3(c.x, c.y, c.z);
                        if (q < n - p - 1) {
                            triangles[j] = j;
                            vertices[j++] = new Vector3(c.x, c.y, c.z);
                            triangles[j] = j;
                            vertices[j++] = new Vector3(b.x, b.y, b.z);
                            triangles[j] = j;
                            vertices[j++] = new Vector3(d.x, d.y, d.z);
                        }
                    }
                }
            }
            Mesh mesh = new Mesh();
            mesh.name = "Icosphere";

            for (int i = 0; i < vertexNum; i++) {
                vertices[i] *= radius;
                tangents[i] = Vector4.zero;
                uv[i] = Vector2.zero;
                uv2[i] = Vector2.zero;
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.tangents = tangents;
            mesh.uv = uv;
            mesh.uv2 = uv2;

            mesh.RecalculateNormals();

            AutoWeld(mesh, 0.1f, 1f);

            var colors = new Color[mesh.vertices.Length];
            for (int i = 0; i < mesh.vertices.Length; i++)
            {
                colors[i] = Color.black;
            }
            mesh.colors = colors;

            return mesh;
        }

        public static void AutoWeld(Mesh mesh, float threshold, float bucketStep) {
            Vector3[] oldVertices = mesh.vertices;
            Vector3[] newVertices = new Vector3[oldVertices.Length];
            int[] old2new = new int[oldVertices.Length];
            int newSize = 0;
        
            // Find AABB
            Vector3 min = new Vector3 (float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3 (float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < oldVertices.Length; i++) {
                if (oldVertices[i].x < min.x) min.x = oldVertices[i].x;
                if (oldVertices[i].y < min.y) min.y = oldVertices[i].y;
                if (oldVertices[i].z < min.z) min.z = oldVertices[i].z;
                if (oldVertices[i].x > max.x) max.x = oldVertices[i].x;
                if (oldVertices[i].y > max.y) max.y = oldVertices[i].y;
                if (oldVertices[i].z > max.z) max.z = oldVertices[i].z;
            }
        
            // Make cubic buckets, each with dimensions "bucketStep"
            int bucketSizeX = Mathf.FloorToInt((max.x - min.x) / bucketStep) + 1;
            int bucketSizeY = Mathf.FloorToInt((max.y - min.y) / bucketStep) + 1;
            int bucketSizeZ = Mathf.FloorToInt((max.z - min.z) / bucketStep) + 1;
            List<int>[,,] buckets = new List<int>[bucketSizeX, bucketSizeY, bucketSizeZ];
        
            // Make new vertices
            for (int i = 0; i < oldVertices.Length; i++) {
                // Determine which bucket it belongs to
                int x = Mathf.FloorToInt((oldVertices[i].x - min.x) / bucketStep);
                int y = Mathf.FloorToInt((oldVertices[i].y - min.y) / bucketStep);
                int z = Mathf.FloorToInt((oldVertices[i].z - min.z) / bucketStep);
            
                // Check to see if it's already been added
                if (buckets[x, y, z] == null)
                    buckets[x, y, z] = new List<int> (); // Make buckets lazily
        
                    for (int j = 0; j < buckets[x, y, z].Count; j++) {
                        Vector3 to = newVertices[buckets[x, y, z][j]] - oldVertices[i];
                        if (Vector3.SqrMagnitude (to) < threshold) {
                            old2new[i] = buckets[x, y, z][j];
                            goto skip; // Skip to next old vertex if this one is already there
                        }
                    }
            
                // Add new vertex
                newVertices[newSize] = oldVertices[i];
                buckets[x, y, z].Add(newSize);
                old2new[i] = newSize;
                newSize++;
            
                skip:;
            }
        
            // Make new triangles
            int[] oldTris = mesh.triangles;
            int[] newTris = new int[oldTris.Length];
            for (int i = 0; i < oldTris.Length; i++) {
                newTris[i] = old2new[oldTris[i]];
            }
            
            Vector3[] finalVertices = new Vector3[newSize];
            Vector2[] finalUV = new Vector2[newSize];
            Vector2[] finalUV2 = new Vector2[newSize];
            for (int i = 0; i < newSize; i++) {
                finalVertices[i] = newVertices[i];
                finalUV[i] = new Vector2(-10000f, 0f);
                finalUV2[i] = Vector2.zero;
            }
        
            mesh.Clear();
            mesh.vertices = finalVertices;
            mesh.triangles = newTris;
            mesh.uv = finalUV;
            mesh.uv2 = finalUV2;
            mesh.RecalculateNormals ();
            mesh.Optimize ();
        }
    }
}