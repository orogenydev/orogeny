using System.Collections.Generic;
using UnityEngine;

public interface IProjection {
    (int, int) GetSize(int startWidth, int startHeight);
    List<Vector2> GetMapCoordinates(float lonRad, float latRad, int width, int height);
    (bool, float, float) GetGeodeticCoordinates(int i, int j, int width, int height);
    string GetName();
}