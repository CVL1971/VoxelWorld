using System.Collections.Generic;
using UnityEngine;

public static class VoxelArrayPool
{
    // El "Libro de Cuentas" único
    private static readonly Dictionary<int, Stack<VoxelData[]>> _pools = new Dictionary<int, Stack<VoxelData[]>>();

    // El "Pomo de la puerta". Es static para que sea la misma llave para todos los cores.
    private static readonly object mLock = new object();

    public static VoxelData[] Get(int pSideSize)
    {
        // Calculamos el volumen total para usarlo como clave única
        int vTotalSize = pSideSize * pSideSize * pSideSize;

        // Intentamos entrar al almacén
        lock (mLock)
        {
            // A partir de aquí, solo UN hilo puede estar ejecutando este código
            if (!_pools.ContainsKey(vTotalSize))
            {
                _pools[vTotalSize] = new Stack<VoxelData[]>();
            }

            if (_pools[vTotalSize].Count > 0)
            {
                return _pools[vTotalSize].Pop();
            }
        } // Aquí el hilo sale y suelta la llave automáticamente

        // Si llegamos aquí es porque no había arrays. Creamos uno nuevo.
        // Se hace fuera del lock para que la creación (lenta) no bloquee a otros hilos.
        return new VoxelData[vTotalSize];
    }

    public static void Return(VoxelData[] pArray)
    {
        if (pArray == null) return;

        int vTotalSize = pArray.Length;

        lock (mLock)
        {
            if (!_pools.ContainsKey(vTotalSize))
            {
                _pools[vTotalSize] = new Stack<VoxelData[]>();
            }

            _pools[vTotalSize].Push(pArray);
        }
    }
}