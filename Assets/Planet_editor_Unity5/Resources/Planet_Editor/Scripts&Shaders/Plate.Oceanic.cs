using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Orogeny.Plates {
    public partial class Plate {    
        private void OceanicContinentCollision(Vector3 point, Plate other) {
            //Debug.Log("O-C");

            //ProcessSubduction(point);

            ui.TriggerBreakpoint(Breakpoint.Subduction);
        }

/*         private void OceanicOceanicCollision(Vector3 point, Plate other) {
            //Debug.Log("O-O");

            // Base this on relative density eventually...
            if (plateNumber < other.GetPlateNumber()) {
                // We subduct
                ProcessSubduction(point);

                ui.TriggerBreakpoint(Breakpoint.AnyCollision);
                ui.TriggerBreakpoint(Breakpoint.Subduction);
            }
        } */

        // One of our perimeters has collided
        private void OceanicOceanicCollision(int v, Plate other)
        {
            //Debug.Log("O-O");

            // Base this on relative density eventually...
            if (plateNumber < other.GetPlateNumber())
            {
                // We subduct
                DeformPerimeterOceanic(v);
                RemoveFromPerimeter(v);

                ui.TriggerBreakpoint(Breakpoint.Subduction);
            }
        }

        // One of our triangles has collided
        private void OceanicOceanicCollision(int v0, int v1, int v2, Plate other)
        {
            //Debug.Log("O-O");

            // Base this on relative density eventually...
            if (plateNumber < other.GetPlateNumber())
            {
                // We subduct

                // If none of the triangle is subducted, subduct all its current perimeter points
                // If any of it already is subducted, do nothing
                if (!IsSubducting(v0) && !IsSubducting(v1) && !IsSubducting(v2))
                {
                    var subducted = new List<int>();
                    SubductIfPerimeter(v0, subducted);
                    SubductIfPerimeter(v1, subducted);
                    SubductIfPerimeter(v2, subducted);

                    foreach (var v in subducted)
                    {
                        RemoveFromPerimeter(v);
                    }

                    ui.TriggerBreakpoint(Breakpoint.Subduction);
                }
            }
        }

        private void SubductIfPerimeter(int v, List<int> subducted)
        {
            if (perimeter.Contains(v))
            {
                DeformPerimeterOceanic(v);
                subducted.Add(v);
            }            
        }

        /*         private void ProcessSubduction(Vector3 point)
                {
                    var subducted = DeformPerimeterOceanic(point);
                    //PropagateDeformationOceanic();

                    // Once a node has started to subduct, we don't want it to trigger collisions
                    foreach (var v in subducted)
                    {
                        RemoveFromPerimeter(v);
                    }
                }
         */

        /*         public List<int> DeformPerimeterOceanic(Vector3 point)
                {
                    List<int> subducted = new List<int>();
                    var scale = 1f * collisionForce.magnitude;
                    var threshold = 2.5f;

                    foreach (int v in GetExtendedPerimeter(0))
                    { // Should this be all vertices?
                      //for (int v = 0; v < mesh.vertices.Length; v++) {
                        var vertex = mesh.vertices[v];
                        var normal = mesh.normals[v];
                        var dist = (vertex - point).magnitude;

                        if (dist < threshold && !IsVertexAhead(collisionForce, vertex, point, point, 2.5f))
                        {
                            var adjustedScale = -scale / (1f + (dist));
                            //vertexNext[v] = Elevate(collisionForce, vertex, normal, adjustedScale);
                            //vertexNext[v] = Vector3.Scale(vertex, new Vector3(0.95f, 0.95f, 0.95f));
                            vertexNext[v] = vertex;
                            vertexScaling[v] = 0.9994f; // This gets applied every frame, so the difference from 1 needs to be quite small
                            vertexActions[v] = Color.white;
                            AddToCohort(v, 0);

                            subducted.Add(v);
                        }
                    }

                    return subducted;
                }
         */
        public void DeformPerimeterOceanic(int v)
        {
            vertexNext[v] = mesh.vertices[v];
            vertexScaling[v] = 0.9995f; // This gets applied every frame, so the difference from 1 needs to be quite small
            vertexActions[v] = Color.white;
            AddToCohort(v, 0);
        }

        public void PropagateDeformationOceanic()
        {
            var pushdownDiscount = 0.9f;
            var threshold = 0.005f;
            var normals = mesh.normals;
            var cohort = 0;

            while (cohort < modificationCohorts.Count)
            {
                foreach (var v in modificationCohorts[cohort])
                {
                    vertexPrev[v] = plateVertices[v];
                    var newVertex = vertexNext[v];
                    var newNormal = newVertex.normalized;

                    var q = WCS2MovementCS(collisionForce, newNormal);
                    var rotatedOld = q * plateVertices[v];
                    var rotatedNew = q * newVertex;

                    var distDown = rotatedOld.y - rotatedNew.y;

                    foreach (var n in neighbors[v])
                    {
                        if (IsInAnyCohort(n))
                        {
                            continue;
                        }

                        vertexActions[n] = Color.yellow;

                        if (distDown > threshold)
                        {
                            if (IsVertexAhead(collisionForce, plateVertices[n], newVertex, newNormal) &&
                                !IsVirtual(plateVertices[n]))
                            {
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