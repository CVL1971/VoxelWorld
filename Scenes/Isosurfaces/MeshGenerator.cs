using UnityEngine;

public abstract class MeshGenerator
{
    // El contrato ahora exige devolver MeshData
    public abstract MeshData Generate(IChunk pChunk, Chunk[] allChunks, Vector3Int worldSize);
}