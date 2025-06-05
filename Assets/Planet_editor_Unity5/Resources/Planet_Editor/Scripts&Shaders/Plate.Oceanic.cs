using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Orogeny.Plates {
    public partial class Plate {    
        private void OceanicContinentCollision(Vector3 point, Plate other) {
            //Debug.Log("O-C");

            ProcessSubduction(point);

            ui.TriggerBreakpoint(Breakpoint.AnyCollision);
            ui.TriggerBreakpoint(Breakpoint.Subduction);
        }

        private void OceanicOceanicCollision(Vector3 point, Plate other) {
            //Debug.Log("O-O");

            // Base this on relative density eventually...
            if (plateNumber < other.GetPlateNumber()) {
                // We subduct
                ProcessSubduction(point);

                ui.TriggerBreakpoint(Breakpoint.AnyCollision);
                ui.TriggerBreakpoint(Breakpoint.Subduction);
            } else {
                // They subduct
                // Island arc
            }
        }

        private void ProcessSubduction(Vector3 point) {
            var deformed = DeformPerimeterOceanic(point);
            PropagateDeformationOceanic();
            
            foreach (var v in deformed) {
                RemoveFromPerimeter(v);
            }
        }

        public List<int> DeformPerimeterOceanic(Vector3 point) {
            List<int> deformed = new List<int>();
            var scale = 0.5f * collisionForce.magnitude;
            var threshold = 1.5f;

            foreach (int v in perimeter) { // Should this be all vertices?
                var vertex = mesh.vertices[v];
                var normal = mesh.normals[v];
                var dist = (vertex - point).magnitude;
                
                if (dist < threshold) {
                    var adjustedScale = -scale / (1f + (dist));
                    vertexNext[v] = Elevate(collisionForce, vertex, normal, adjustedScale);
                    vertexActions[v] = Color.white;
                    AddToCohort(v, 0);

                    deformed.Add(v);
                }
            }

            return deformed;
        }

        public void PropagateDeformationOceanic() {
            var pushdownDiscount = 0.9f;
            var threshold = 0.005f;
            var normals = mesh.normals;
            var cohort = 0;

            while (cohort < modificationCohorts.Count) {
                foreach (var v in modificationCohorts[cohort]) {
                    vertexPrev[v] = plateVertices[v];
                    var newVertex = vertexNext[v];
                    var newNormal = newVertex.normalized;

                    var q = WCS2MovementCS(collisionForce, newNormal);
                    var rotatedOld = q * plateVertices[v];
                    var rotatedNew = q * newVertex;

                    var distDown = rotatedOld.y - rotatedNew.y;

                    foreach (var n in neighbors[v]) {
                        if (IsInAnyCohort(n)) {
                            continue;
                        }
                        
                        vertexActions[n] = Color.yellow;
                        
                        if (distDown > threshold) {
                            if (IsVertexAhead(collisionForce, plateVertices[n], newVertex, newNormal) &&
                                !IsVirtual(plateVertices[n])) {
                                // Push down everything (that isn't virtual) ahead of us
                                vertexNext[n] = Elevate(collisionForce, plateVertices[n], normals[n], -distDown * pushdownDiscount);
                                vertexActions[n] = Color.grey;
                                AddToCohort(n, cohort + 1);
                            }
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