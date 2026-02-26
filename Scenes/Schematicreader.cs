using System.Collections.Generic;
using UnityEngine;
using System.IO;
using fNbt;

public static class SchematicReader
{
    // =========================================================
    // Wrapper de datos (solo datos)
    // =========================================================
    public sealed class VolumeData
    {
        public readonly int sizeX;
        public readonly int sizeY;
        public readonly int sizeZ;
        public readonly List<Vector3Int> voxels;

        public VolumeData(int x, int y, int z, List<Vector3Int> voxels)
        {
            sizeX = x;
            sizeY = y;
            sizeZ = z;
            this.voxels = voxels;
        }

        public VolumeData (int x, int y, int z)

        {
            sizeX = x;
            sizeY = y;
            sizeZ = z;
            voxels = new List<Vector3Int>();
        }

        public VolumeData(int pSize)

        {
            sizeX = sizeY = sizeZ = pSize;
            voxels = new List<Vector3Int>();
        }

        public void Fill()
        {
            voxels.Clear();

            int total = sizeX * sizeY * sizeZ;
            voxels.Capacity = total;

            for (int z = 0; z < sizeZ; z++)
                for (int y = 0; y < sizeY; y++)
                    for (int x = 0; x < sizeX; x++)
                        voxels.Add(new Vector3Int(x, y, z));
        }
    }

    // =========================================================
    // LECTURA COMPLETA POR RUTA
    // p
    // =========================================================

    public static VolumeData Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(path);

        var nbt = new NbtFile();
        nbt.LoadFromFile(path);

        if (!nbt.RootTag.TryGet("Schematic", out NbtCompound schematic))
            throw new IOException("Missing Schematic compound.");

        int width = GetShort(schematic, "Width");
        int height = GetShort(schematic, "Height");
        int length = GetShort(schematic, "Length");

        // -------- Blocks compound --------
        if (!schematic.TryGet("Blocks", out NbtCompound blocks))
            throw new IOException("Missing Blocks compound.");

        // -------- Palette --------
        if (!blocks.TryGet("Palette", out NbtCompound palette))
            throw new IOException("Missing Blocks/Palette.");

        // -------- Data --------
        if (!blocks.TryGet("Data", out NbtByteArray dataTag))
            throw new IOException("Missing Blocks/Data.");

        byte[] data = dataTag.Value;

        // -------- Detectar ID de aire --------
        int airId = -1;
        foreach (var tag in palette.Tags)
        {
            if (tag.Name.Contains("air") && tag is NbtInt air)
            {
                airId = air.Value;
                break;
            }
        }

        //if (airId == -1)
        //    Debug.LogWarning("Air not found in palette; assuming no air.");

        int expected = width * height * length;
        if (data.Length < expected)
            throw new IOException("Blocks/Data array too small.");

        List<Vector3Int> voxels = new();

        /*
         * Orden legacy WorldEdit:
         * index = y * width * length + z * width + x
         */
        int index = 0;
        for (int y = 0; y < height; y++)
            for (int z = 0; z < length; z++)
                for (int x = 0; x < width; x++, index++)
                {
                    byte blockId = data[index];

                    if (blockId == airId)
                        continue;

                    voxels.Add(new Vector3Int(x, y, z));
                }

        return new VolumeData (width, height, length, voxels);
    }

    // =========================================================
    // LECTURA SUBVOLUMEN POR RUTA, ORIGEN Y VOLUMEN
    // pORIGIN PUNTO COORDENADAS GLOBALES DE SCHEM, pVOLUMESIZE TAMAÑO REGULAR SUBVOLUMEN
    // =========================================================

    public static VolumeData Load(
     string pPath,
     Vector3Int pOrigin,
     int pVolumeSize
 )
    {
        // PRIMERO HACEMOS UNA LECTURA COMPLETA POR RUTA DE FICHERO

        VolumeData full = Load(pPath);

        // LA LOGICA SUBSIGUIENTE ES PARA EXTRAER SOLO LAS COORDENADAS
        // QUE ESTEN DENTRO DEL SUBVOLUMEN

        int size = pVolumeSize;

        List<Vector3Int> voxels = new List<Vector3Int>();

        for (int i = 0; i < full.voxels.Count; i++)
        {
            Vector3Int v = full.voxels[i];

            int lx = v.x - pOrigin.x;
            int ly = v.y - pOrigin.y;
            int lz = v.z - pOrigin.z;

            if (lx < 0 || ly < 0 || lz < 0)
                continue;

            if (lx >= size || ly >= size || lz >= size)
                continue;

            voxels.Add(new Vector3Int(lx, ly, lz));
        }

        return new VolumeData(
            size,
            size,
            size,
            voxels
        );
    }

    // =========================================================
    // Helpers
    // =========================================================

    static int GetShort(NbtCompound c, string name)
    {
        if (!c.TryGet(name, out NbtTag tag) || tag is not NbtShort s)
            throw new IOException($"Missing or invalid '{name}'.");

        return s.Value;
    }
}










