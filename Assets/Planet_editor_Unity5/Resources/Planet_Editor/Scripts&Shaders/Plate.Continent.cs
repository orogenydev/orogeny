using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Orogeny.Plates {
    public partial class Plate {    
        private void ContinentContinentCollision(Vector3 point, Plate other) {
            //Debug.Log("C-C");

            // Squish
            DeformPerimeterContinent(point);
            PropagateDeformationContinent();

            ui.TriggerBreakpoint(Breakpoint.AnyCollision);
            ui.TriggerBreakpoint(Breakpoint.ContinentalSquish);
        }

        private void ContinentOceanicCollision(Vector3 point, Plate other) {
            //Debug.Log("C-O");

            // They subduct
            // Accretionary wedge
            // Volcanic arc
        }

        public void DeformPerimeterContinent(Vector3 point) {
            var scale = 0.1f * collisionForce.magnitude;
            var threshold = 1.5f;

            foreach(int v in perimeter) {
                var vertex = mesh.vertices[v];
                var normal = mesh.normals[v];
                var dist = (vertex - point).magnitude;
                
                if (dist < threshold) {
                    var adjustedScale = scale / (1f + (dist * dist));
                    var q = CreateVertexRotation(-collisionForce, normal, adjustedScale);
                    vertexNext[v] = q * vertex;
                    vertexActions[v] = Color.white;
                    AddToCohort(v, 0);
                }
            }
        }

        public void PropagateDeformationContinent() {
            var pullbackDiscount = 0.1f;
            var liftDiscount = 0.85f;
            var threshold = 0.005f;
            var normals = mesh.normals;
            var cohort = 0;

            while (cohort < modificationCohorts.Count) {
                foreach (var v in modificationCohorts[cohort]) {
                    vertexPrev[v] = plateVertices[v];
                    var newVertex = vertexNext[v];
                    var newNormal = newVertex.normalized;

                    // Perimeter vertices stay on the floor
                    if (perimeter.Contains(v)) {
                        newVertex = newNormal * Plate.seaFloorRadius;
                    }

                    var q = WCS2MovementCS(collisionForce, newNormal);
                    var rotatedOld = q * plateVertices[v];
                    var rotatedNew = q * newVertex;

                    var distUp = rotatedNew.y - rotatedOld.y;
                    var distBack = rotatedOld.z - rotatedNew.z;

                    foreach (var n in neighbors[v]) {
                        if (IsInAnyCohort(n)) {
                            continue;
                        }
                        
                        vertexActions[n] = Color.yellow;
                        
                        if (distBack > threshold) {
                            if (IsVertexAhead(collisionForce, plateVertices[n], newVertex, newNormal)) {
                                // If we've passed any, drag them back to the line of scrimage
                                var discount = perimeter.Contains(n) ? pullbackDiscount : 1.0f;
                                vertexNext[n] = ProjectBack(collisionForce, plateVertices[n], newVertex, newNormal, discount);
                                vertexActions[n] = Color.red;
                                AddToCohort(n, cohort + 1);
                            } else {
                                // Lever the ones still behind us up
                                vertexNext[n] = Elevate(collisionForce, plateVertices[n], normals[n], distBack);
                                vertexActions[n] = Color.cyan;
                                AddToCohort(n, cohort + 1);
                            }
                        } else if (distUp > threshold) {
                            // Pull them up
                            vertexNext[n] = Elevate(collisionForce, plateVertices[n], normals[n], distUp * liftDiscount);
                            vertexActions[n] = Color.grey;
                            AddToCohort(n, cohort + 1);
                        }
                    }

                    plateVertices[v] = newVertex;
                    normals[v] = newNormal;
                }

                cohort++;
            }

            mesh.vertices = plateVertices;
            mesh.normals = normals;
            mesh.RecalculateBounds();
        }
    }
}