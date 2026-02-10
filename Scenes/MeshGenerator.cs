using UnityEngine;

public abstract class MeshGenerator
{
    // El contrato ahora exige devolver MeshData
    public abstract MeshData Generate(Chunk pChunk, Chunk[] allChunks, Vector3Int worldSize);
}