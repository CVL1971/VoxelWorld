using UnityEngine;
using System.Collections.Generic;

public class VoxelMeshGenerator : MeshGenerator
{
    public Mesh Generate(Chunk pChunk)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        int size = pChunk.mSize;

        for (int z = 0; z < size; z++)
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    if (!pChunk.IsSolid(x, y, z))
                        continue;

                    Vector3 basePos = new Vector3(
                        pChunk.mWorldOrigin.x + x,
                        pChunk.mWorldOrigin.y + y,
                        pChunk.mWorldOrigin.z + z
                    );

                    for (int face = 0; face < 6; face++)
                    {
                        Vector3Int dir = VoxelFaceGeometry.Directions[face];

                        if (pChunk.SafeIsSolid(x + dir.x, y + dir.y, z + dir.z))

                            continue;

                        int vOffset = vertices.Count;

                        Vector3[] faceVertices = VoxelFaceGeometry.FaceVertices[face];
                        for (int i = 0; i < faceVertices.Length; i++)
                        {
                            vertices.Add(faceVertices[i] + basePos);
                        }

                        int[] faceTriangles = VoxelFaceGeometry.FaceTriangles;
                        for (int i = 0; i < faceTriangles.Length; i++)
                        {
                            triangles.Add(vOffset + faceTriangles[i]);
                        }


                    }
                }

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    public Mesh Generate(Chunk[] pChunk)
    {
        return null;
    }

    public Mesh Generate(Chunk pChunk, Chunk[] allChunks, Vector3Int worldSize)
    {
        return null;
    }

}

