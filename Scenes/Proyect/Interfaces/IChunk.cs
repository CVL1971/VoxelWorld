using UnityEngine;

public interface IChunk
{
    int mSize { get; }

    Vector3Int WorldOrigin { get; }

    float GetDensity(int x, int y, int z);

    float ISO_LEVEL { get; }

    ArrayPool.DCache DCache { get; }
    
}
