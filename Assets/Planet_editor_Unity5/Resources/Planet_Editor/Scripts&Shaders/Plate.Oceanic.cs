using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Orogeny.Plates {
    public partial class Plate {
        private int subductionPolarityFlippingCollisionCount = 0;

        public void ClearSubductionPolarityFlippingCollisionCount()
        {
            subductionPolarityFlippingCollisionCount = 0;
        }

        private void OceanicContinentCollision(Vector3 point, Plate other)
        {
            //Debug.Log("O-C");

            //ProcessSubduction(point);
        }

        // One of our perimeters has collided
        private void OceanicOceanicCollision(int v, Plate other, int v0, int v1, int v2, SubductionTracker subductionTracker)
        {
            //Debug.Log("O-O");

            // Take the median plateRenderCount ages stored in uv2 for determining who will subduct
            var localVertices = GetValidNeighbors(v, 3);
            var localUVMedian = localVertices.Median(v => mesh.uv2[v].x);

            var otherVertices = other.GetValidNeighbors(v0, 3);
            var otherUVMedian = otherVertices.Median(v => other.GetMesh().uv2[v].x);

            if (subductionTracker.IsXSubductingUnderY(this, other, localVertices, otherVertices, localUVMedian, otherUVMedian))
            {
                // We subduct
                if (!IsSubducting(v))
                {
                    InitiateSubductionOceanic(v, other);
                    RemoveFromPerimeter(v);
                }
            }
        }

        // One of our triangles has collided
        private void OceanicOceanicCollision(int v0, int v1, int v2, Plate other, int v, SubductionTracker subductionTracker)
        {
            //Debug.Log("O-O");

            // Take the median plateRenderCount ages stored in uv2 for determining who will subduct
            var localVertices = GetValidNeighbors(v0, 3);
            var localUVMedian = localVertices.Median(v => mesh.uv2[v].x);

            var otherVertices = other.GetValidNeighbors(v, 3);
            var otherUVMedian = otherVertices.Median(v => other.GetMesh().uv2[v].x);

            if (subductionTracker.IsXSubductingUnderY(this, other, localVertices, otherVertices, localUVMedian, otherUVMedian))
            {
                // We subduct

                // If none of the triangle is subducted, subduct all its current perimeter points
                // If any of it already is subducting, do nothing
                if (!IsSubducting(v0) && !IsSubducting(v1) && !IsSubducting(v2))
                {
                    var subducted = new List<int>();
                    SubductIfPerimeter(v0, other, subducted);
                    SubductIfPerimeter(v1, other, subducted);
                    SubductIfPerimeter(v2, other, subducted);

                    foreach (var s in subducted)
                    {
                        RemoveFromPerimeter(s);
                    }
                }
            }
        }

        // One of our oceanic perimeter points has collided with continental crust
        private void OceanicContinentCollision(int v, Plate other, int v0, int v1, int v2, SubductionTracker subductionTracker)
        {
            //Debug.Log("O-C");

            // Check for subduction polarity change
            if (!subductionTracker.IsXAlreadySubductingUnderY(this, other))
            {
                subductionPolarityFlippingCollisionCount++;

                if (subductionPolarityFlippingCollisionCount > 2)
                {
                    subductionTracker.ForceXSubductUnderY(this, other);
                    var newMesh = other.TearOffSubductingVertices(this);
                    other.AddTornMesh(newMesh);

                    Debug.Log($"Subduction polarity flip! {GetPlateNumber()} < {other.GetPlateNumber()}");
                    ui.TriggerBreakpoint(Breakpoint.SubductionPolarityFlip);
                }
            }

            if (!IsSubducting(v))
            {
                InitiateSubductionOceanic(v, other);
                RemoveFromPerimeter(v);
            }
        }

        private void SubductIfPerimeter(int v, Plate other, List<int> subducted)
        {
            if (perimeter.Contains(v))
            {
                InitiateSubductionOceanic(v, other);
                subducted.Add(v);
            }
        }

        public void InitiateSubductionOceanic(int v, Plate other)
        {
            vertexNext[v] = mesh.vertices[v];
            AddToCohort(v, 0);

            var firstStep = (seaFloorMaxNormalRadius - trenchRadius) / 3;
            var secondStep = vertexSubductionState[v] == SubductionState.Adjacent ? 2 * firstStep : 3 * firstStep;

            // Apply final trenchiness, after which any height above trenchRadius should be its original excess above seaFloorMaxNormalRadius
            plateVertices[v] = vertexHomes[v] * Mathf.Max(plateVertices[v].magnitude - secondStep, trenchRadius);

            vertexSubductionState[v] = SubductionState.Initiated;

            // Apply first trenchiness to untrenched neighbors
            foreach (var n in GetValidNeighbors(v, 1))
            {
                if (vertexSubductionState[n] == SubductionState.None)
                {
                    plateVertices[n] = plateVertices[n].normalized * Mathf.Max(plateVertices[n].magnitude - firstStep, trenchRadius);
                    vertexSubductionState[n] = SubductionState.Adjacent;
                }
            }

            mesh.vertices = plateVertices;
        }
    }
}