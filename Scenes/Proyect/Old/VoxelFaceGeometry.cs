using UnityEngine;

public static class VoxelFaceGeometry
{
    // =========================
    // Direcciones de las caras
    // =========================
    // El orden DEBE coincidir con el de FaceVertices
    public static readonly Vector3Int[] Directions =
    {
        new( 1, 0, 0),  // +X
        new(-1, 0, 0),  // -X
        new( 0, 1, 0),  // +Y
        new( 0,-1, 0),  // -Y
        new( 0, 0, 1),  // +Z
        new( 0, 0,-1),  // -Z
    };

    // =========================
    // Geometría por cara
    // =========================
    // 6 caras, cada una con 4 vértices CCW vistos desde fuera
    public static readonly Vector3[][] FaceVertices =
    {
        // +X
        new[]
        {
            new Vector3( 0.5f,-0.5f,-0.5f),
            new Vector3( 0.5f, 0.5f,-0.5f),
            new Vector3( 0.5f, 0.5f, 0.5f),
            new Vector3( 0.5f,-0.5f, 0.5f),
        },
        // -X
        new[]
        {
            new Vector3(-0.5f,-0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f,-0.5f),
            new Vector3(-0.5f,-0.5f,-0.5f),
        },
        // +Y
        new[]
        {
            new Vector3(-0.5f, 0.5f,-0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f),
            new Vector3( 0.5f, 0.5f, 0.5f),
            new Vector3( 0.5f, 0.5f,-0.5f),
        },
        // -Y
        new[]
        {
            new Vector3(-0.5f,-0.5f, 0.5f),
            new Vector3(-0.5f,-0.5f,-0.5f),
            new Vector3( 0.5f,-0.5f,-0.5f),
            new Vector3( 0.5f,-0.5f, 0.5f),
        },
        // +Z
        new[]
        {
            new Vector3(-0.5f,-0.5f, 0.5f),
            new Vector3( 0.5f,-0.5f, 0.5f),
            new Vector3( 0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f),
        },
        // -Z
        new[]
        {
            new Vector3( 0.5f,-0.5f,-0.5f),
            new Vector3(-0.5f,-0.5f,-0.5f),
            new Vector3(-0.5f, 0.5f,-0.5f),
            new Vector3( 0.5f, 0.5f,-0.5f),
        },
    };

    // =========================
    // Triángulos por cara
    // =========================
    public static readonly int[] FaceTriangles =
    {
        0, 1, 2,
        0, 2, 3
    };
}


