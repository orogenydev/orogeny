using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

namespace Orogeny.Plates {
    public class PlateCollisions {
        private NativeArray<RaycastCommand> raycastCommands;
        private NativeArray<RaycastHit> raycastHits;

        public void PerimeterCheck(List<Plate> plates) {
            foreach (var plate in plates) {
                PerimeterCheck(plate);
            }
        }

        public void PerimeterCheck(Plate plate) {
            var vertices = plate.GetPlateVertices();
            var perimeter = plate.GetPerimeter();
            var count = perimeter.Count;
            plate.ClearContactPoints();

            var mask = LayerMask.GetMask("Plates");
            raycastCommands = new NativeArray<RaycastCommand>(count, Allocator.Persistent);
            raycastHits = new NativeArray<RaycastHit>(count, Allocator.Persistent);

            for (int j = 0; j < count; j++) {
                Ray ray = new Ray(Vector3.zero, vertices[perimeter[j]]);
                ray.origin = ray.GetPoint(50);
                ray.direction = -ray.direction;
                raycastCommands[j] = new RaycastCommand(ray.origin, ray.direction, 50 - Plate.mohoRadius, mask);
            }
            
            JobHandle handle = RaycastCommand.ScheduleBatch(raycastCommands, 
                                                            raycastHits, 
                                                            1, 
                                                            default(JobHandle));
            handle.Complete();

            var collider = plate.gameObject.GetComponent<MeshCollider>();
            for (int j = 0; j < raycastHits.Length; j++) {
                RaycastHit hit = raycastHits[j];

                if (hit.collider != null && hit.collider != collider) {
                    var go = hit.collider.gameObject;
                    var otherPlate = go.GetComponent<Plate>();

                    plate.ProcessCollision(hit.point, otherPlate);
                    otherPlate.ProcessCollision(hit.point, plate);
                }
            }

            raycastCommands.Dispose();
            raycastHits.Dispose();
        }
    }
}