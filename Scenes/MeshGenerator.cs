using UnityEngine;

public interface MeshGenerator
{
    Mesh Generate(Chunk pChunk);
    Mesh Generate(Chunk[] pChunks);
    Mesh Generate(Chunk pChunk, Chunk[] allChunks, Vector3Int worldSize);
}

