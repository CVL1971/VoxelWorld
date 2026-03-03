using UnityEngine;
using System.Collections.Generic;

public class MeshData
{
    public List<Vector3> vertices = new List<Vector3>();
    public List<Vector3> normals = new List<Vector3>();
    public List<int> triangles = new List<int>();

    // Aquí puedes meter los colores de los que hablábamos al principio
    public List<Color32> colors = new List<Color32>();
}