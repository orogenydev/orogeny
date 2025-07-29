using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

namespace Orogeny.Plates {
    public class PlateCollisions {
        public void PerimeterCheck(List<Plate> plates) {
            foreach (var plate in plates) {
                PerimeterCheck(plate);
            }
        }

        public void PerimeterCheck(Plate plate) {
            var vertices = plate.GetPlateVertices();
            var perimeter = plate.GetExtendedPerimeter(1);
            var count = perimeter.Count;
            plate.ClearContactPoints();

            var mask = LayerMask.GetMask("Plates");
            var castCommands = new NativeArray<SpherecastCommand>(count, Allocator.Persistent);
            //var castHits = new NativeArray<RaycastHit>(count, Allocator.Persistent);
            var castHits = new NativeArray<RaycastHit>(count * 3, Allocator.TempJob);
            
            for (int j = 0; j < count; j++)
            {
                // Standard out-and-back routine
                Ray ray = new Ray(Vector3.zero, vertices[perimeter[j]]);
                ray.origin = ray.GetPoint(50);
                ray.direction = -ray.direction;
                castCommands[j] = new SpherecastCommand(ray.origin, 0.25f, ray.direction, 50 - Plate.mohoRadius, mask);
            }
            
            JobHandle handle = SpherecastCommand.ScheduleBatch(castCommands,
                                                            castHits, 
                                                            1, 
                                                            3,
                                                            default(JobHandle));
            handle.Complete();

            var collider = plate.gameObject.GetComponent<MeshCollider>();
            for (int j = 0; j < castHits.Length; j += 3) {
                for (int k = 0; k < 3; k++)
                {
                    var hit = castHits[j + k];

                    if (hit.collider != null && hit.collider != collider)
                    {
                        int perimeterIndex = perimeter[j / 3];
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

                        // Ignore if what we hit was any of the mesh diving down to the virtual vertices down by the core
                        if (plate.IsVirtual(point0) || plate.IsVirtual(point1) || plate.IsVirtual(point2))
                            continue;

                        var otherGO = hit.collider.gameObject;
                        var otherPlate = otherGO.GetComponent<Plate>();

                        plate.ProcessCollision(hit.point, perimeterIndex, otherPlate, v0, v1, v2);
                        otherPlate.ProcessCollision(hit.point, v0, v1, v2, plate, perimeterIndex);
                    }
                }
            }

            castCommands.Dispose();
            castHits.Dispose();
        }
    }
}