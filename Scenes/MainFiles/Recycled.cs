using UnityEngine;

public class Recycled
{
    private const float WARP_SCALE = 0.002f;

    //original
    //public static float GetGeneratedHeight(float x, float z)
    //{
    //    // 1. CONTINENTES (Estructura base)
    //    float C = Mathf.PerlinNoise(x * BASE_SCALE, z * BASE_SCALE);

    //    // 2. DOMAIN WARPING (Para que las montañas no parezcan nubes de Perlin)
    //    Vector2 p = Warp(x, z);

    //    // M: Montañas (Ridged Noise para crestas afiladas)
    //    float noiseM = Mathf.PerlinNoise(p.x * 0.01f, p.y * 0.01f);
    //    float M = 1.0f - Mathf.Abs((noiseM * 2.0f) - 1.0f);
    //    float mountain = M * M;

    //    // D: Detalle fino (Roca y suelo)
    //    float D = (Mathf.PerlinNoise(p.x * 0.08f, p.y * 0.08f) * 2.0f) - 1.0f;

    //    // 3. MEZCLA LÓGICA (La personalidad geológica)
    //    float baseLayer = SmoothStep(-0.2f, 0.6f, (C * 2.0f) - 1.0f);

    //    // Las montañas solo crecen en los continentes
    //    mountain *= (baseLayer * baseLayer);
    //    float valley = baseLayer * (1.0f - mountain);

    //    // Resultado final en metros (Y)
    //    float h = (baseLayer * 40.0f) + (mountain * 120.0f) + (valley * 25.0f) + (D * 5.0f * baseLayer);

    //    return h;
    //}

   

    //public static float GetGeneratedHeight(float x, float z)
    //{
    //    const float SCALE = 0.05f;   // frecuencia espacial
    //    const float AMPLITUDE = 40f; // altura máxima

    //    // OFFSET para centrar el terreno en el cubo
    //    const float CENTER_OFFSET = 256f; // ajusta según el tamaño del chunk

    //    float sx = Mathf.Sin(x * SCALE);
    //    float sz = Mathf.Sin(z * SCALE);

    //    float height = (sx + sz) * 0.5f;

    //    float result = height * AMPLITUDE + CENTER_OFFSET;

    //    DebugWindow.Debug("Y", result);

    //    return result;
    //}

    /// <summary>
    /// Rellena el Chunk usando el nuevo gradiente de distancia.
    /// Optimizado con detección de límites de altura para descartar aire/sólido rápidamente.
    /// </summary>
    /// 

    static float GetHeightRaw(float x, float z)
{
    float C = Mathf.PerlinNoise(x * 0.0006f, z * 0.0006f);

    Vector2 p = Warp(x, z);

    float r = Mathf.PerlinNoise(p.x * 0.006f, p.y * 0.006f);
    float ridged = 1.0f - Mathf.Abs(r * 2.0f - 1.0f);
    ridged = ridged * ridged;

    float macro = Mathf.PerlinNoise(x * 0.0015f, z * 0.0015f);
    macro = Mathf.Pow(macro, 3.0f);

    float cliffs = Mathf.PerlinNoise(p.x * 0.02f, p.y * 0.02f);
    cliffs = Mathf.Floor(cliffs * 5.0f) / 5.0f;

    float detail = Mathf.PerlinNoise(p.x * 0.1f, p.y * 0.1f) * 2.0f - 1.0f;

    float baseLayer = SmoothStep(-0.2f, 0.7f, (C * 2.0f) - 1.0f);

    float h =
        baseLayer * 120.0f +
        macro * 600.0f +
        ridged * 900.0f * baseLayer +
        cliffs * 200.0f +
        detail * 30.0f * baseLayer;

    return h;
}

//original 2
//public static float GetGeneratedHeight(float x, float z)
//{
//    // --- CONTINENTES (SIN WARP) ---
//    float C = Mathf.PerlinNoise(x * 0.0008f, z * 0.0008f);

//    // --- WARP SOLO PARA DETALLE Y MONTAÑAS ---
//    Vector2 p = Warp(x, z);

//    // M: Montañas (ridged)
//    float noiseM = Mathf.PerlinNoise(p.x * 0.01f, p.y * 0.01f);
//    float M = 1.0f - Mathf.Abs((noiseM * 2.0f) - 1.0f);

//    // D: detalle fino
//    float D = (Mathf.PerlinNoise(p.x * 0.08f, p.y * 0.08f) * 2.0f) - 1.0f;

//    // --- MEZCLA ---
//    float baseLayer = SmoothStep(-0.2f, 0.6f, (C * 2.0f) - 1.0f);

//    float mountain = M * M;
//    mountain *= baseLayer * baseLayer;

//    float valley = baseLayer * (1.0f - mountain);

//    float h =
//         baseLayer * 40.0f +
//         mountain * 120.0f +
//         valley * 25.0f +
//         D * 5.0f * baseLayer; // evita ruido en océanos

//    return h;
//}

//cuencas
//public static float GetGeneratedHeight(float x, float z)
//{
//    Vector2 p = Warp(x, z);

//    // CONTINENTES (~150 km)
//    float continents = Mathf.PerlinNoise(x * 0.00004f, z * 0.00004f);
//    continents = SmoothStep(0.35f, 0.65f, continents);

//    // MACRO RELIEVE (~20 km)
//    float macro = Mathf.PerlinNoise(p.x * 0.0004f, p.y * 0.0004f);
//    macro = Mathf.Pow(macro, 2.5f);

//    // GRANDES CORDILLERAS (~6 km)
//    float ridged = Mathf.PerlinNoise(p.x * 0.0015f, p.y * 0.0015f);
//    ridged = 1f - Mathf.Abs(ridged * 2f - 1f);
//    ridged *= ridged;

//    // MONTAÑAS (~2 km)
//    float mountains = Mathf.PerlinNoise(p.x * 0.004f, p.y * 0.004f);
//    mountains = Mathf.Pow(mountains, 1.5f);

//    // COLINAS (~300 m)
//    float hills = Mathf.PerlinNoise(p.x * 0.02f, p.y * 0.02f);

//    // DETALLE (~60 m)
//    float detail = Mathf.PerlinNoise(p.x * 0.1f, p.y * 0.1f) * 2f - 1f;

//    float h =
//        continents * 150f +
//        macro * 1200f * continents +
//        ridged * 800f * continents +
//        mountains * 250f +
//        hills * 60f +
//        detail * 8f;

//    return h;
//}



//alta frecuencia
//public static float GetGeneratedHeight(float x, float z)
//{
//    // ====================================================
//    // CONTROL MANUAL DEL BLUR (simula LOD)
//    // ====================================================

//    const float TEST_STEP = 256f;   // cámbialo manualmente para probar

//    // --- CONTINENTES ---
//    float C = Mathf.PerlinNoise(x * 0.0006f, z * 0.0006f);

//    // --- DOMAIN WARP ---
//    Vector2 p = Warp(x, z);

//    // --- MONTAÑAS RIDGED ---
//    float r = Mathf.PerlinNoise(p.x * 0.006f, p.y * 0.006f);
//    float ridged = 1.0f - Mathf.Abs(r * 2.0f - 1.0f);
//    ridged = ridged * ridged;

//    // --- MACRO CORDILLERAS ---
//    float macro = Mathf.PerlinNoise(x * 0.0015f, z * 0.0015f);
//    macro = Mathf.Pow(macro, 3.0f);

//    // --- ACANTILADOS ---
//    float cliffs = Mathf.PerlinNoise(p.x * 0.02f, p.y * 0.02f);
//    cliffs = Mathf.Floor(cliffs * 5.0f) / 5.0f;

//    // --- DETALLE ---
//    float detail = Mathf.PerlinNoise(p.x * 0.1f, p.y * 0.1f) * 2.0f - 1.0f;

//    // --- BASE CONTINENTAL ---
//    float baseLayer = SmoothStep(-0.2f, 0.7f, (C * 2.0f) - 1.0f);

//    float h =
//        baseLayer * 120.0f +
//        macro * 600.0f +
//        ridged * 900.0f * baseLayer +
//        cliffs * 200.0f +
//        detail * 30.0f * baseLayer;

//    // ====================================================
//    // BLUR SIMULADO
//    // ====================================================

//    float BLUR_STRENGTH = 1f;

//    // radio del filtro mucho mayor que el step
//    float radius = TEST_STEP * 10f;

//    // factor de mezcla independiente del step
//    float blurFactor = Mathf.Clamp01(0.6f * BLUR_STRENGTH);

//    if (blurFactor > 0.001f)
//    {
//        float h1 = GetHeightRaw(x + radius, z);
//        float h2 = GetHeightRaw(x - radius, z);
//        float h3 = GetHeightRaw(x, z + radius);
//        float h4 = GetHeightRaw(x, z - radius);

//        float blurred = (h + h1 + h2 + h3 + h4) * 0.2f;

//        h = Mathf.Lerp(h, blurred, blurFactor);
//    }

//    return h;
//}
//bosque de abetos
//public static float GetGeneratedHeight(float x, float z)
//{
//    // --- CONTINENTES ---
//    float C = Mathf.PerlinNoise(x * 0.0006f, z * 0.0006f);

//    // --- DOMAIN WARP ---
//    Vector2 p = Warp(x, z);

//    // --- MONTAÑAS RIDGED ---
//    float r = Mathf.PerlinNoise(p.x * 0.006f, p.y * 0.006f);
//    float ridged = 1.0f - Mathf.Abs(r * 2.0f - 1.0f);
//    ridged = ridged * ridged;

//    // --- MACRO CORDILLERAS ---
//    float macro = Mathf.PerlinNoise(x * 0.0015f, z * 0.0015f);
//    macro = Mathf.Pow(macro, 3.0f);

//    // --- ACANTILADOS / TERRACING ---
//    float cliffs = Mathf.PerlinNoise(p.x * 0.02f, p.y * 0.02f);
//    cliffs = Mathf.Floor(cliffs * 5.0f) / 5.0f;

//    // --- DETALLE ---
//    float detail = Mathf.PerlinNoise(p.x * 0.1f, p.y * 0.1f) * 2.0f - 1.0f;

//    // --- BASE CONTINENTAL ---
//    float baseLayer = SmoothStep(-0.2f, 0.7f, (C * 2.0f) - 1.0f);

//    // --- ALTURAS ---
//    float h =
//        baseLayer * 120.0f +        // altura base continente
//        macro * 600.0f +            // grandes cordilleras
//        ridged * 900.0f * baseLayer + // picos escarpados
//        cliffs * 200.0f +           // acantilados
//        detail * 30.0f * baseLayer; // detalle

//    return h;
//}

/// <summary>
/// Inyecta un mapa de alturas 2D como un volumen SDF coherente.
/// </summary>

static Vector2 WarpFractal(float x, float z)
{
    float w1 = (Mathf.PerlinNoise(x * 0.002f, z * 0.002f) - 0.5f) * 120f;
    float w2 = (Mathf.PerlinNoise(x * 0.01f, z * 0.01f) - 0.5f) * 40f;

    return new Vector2(x + w1 + w2, z + w1 + w2);
}

static float RidgedMF(float x, float z)
{
    float sum = 0;
    float amp = 1;
    float freq = 0.004f;

    for (int i = 0; i < 5; i++)
    {
        float n = Mathf.PerlinNoise(x * freq, z * freq);
        n = 1f - Mathf.Abs(n * 2f - 1f);
        n *= n;

        sum += n * amp;

        amp *= 0.5f;
        freq *= 2f;
    }

    return sum;
}

static float ValleyMask(float x, float z)
{
    float v = Mathf.PerlinNoise(x * 0.003f, z * 0.003f);
    v = Mathf.Pow(1f - v, 4f);
    return v;
}

static float FlowMask(float x, float z)
{
    float n = Mathf.PerlinNoise(x * 0.004f, z * 0.004f);
    n = Mathf.Pow(1f - n, 3f);
    return n;
}

//public static float GetGeneratedHeight(float x, float z)
//{
//    // 1. campo tectónico
//    float tectonic =
//        Mathf.PerlinNoise(x * 0.0007f, z * 0.0007f);

//    // 2. distancia a cresta
//    float d = Mathf.Abs(tectonic - 0.5f);

//    // 3. perfil de montaña
//    float mountain =
//        Mathf.Exp(-d * 14f) * 420f;

//    // 4. warp para romper líneas rectas
//    float warpX =
//        (Mathf.PerlinNoise(x * 0.002f, z * 0.002f) - 0.5f) * 300f;

//    float warpZ =
//        (Mathf.PerlinNoise(x * 0.002f + 200f, z * 0.002f + 200f) - 0.5f) * 300f;

//    float px = x + warpX;
//    float pz = z + warpZ;

//    // 5. erosión falsa
//    float erosion =
//        Mathf.PerlinNoise(px * 0.0025f, pz * 0.0025f);

//    mountain *= 1f - erosion * 0.6f;

//    // 6. detalle
//    float detail =
//        Mathf.PerlinNoise(px * 0.04f, pz * 0.04f) * 12f;

//    return mountain + detail;
//}

static Vector2 FlowDir(float x, float z)
{
    float e = 0.001f;

    float n1 = Mathf.PerlinNoise(x * 0.001f, z * 0.001f);
    float nx = Mathf.PerlinNoise((x + e) * 0.001f, z * 0.001f);
    float nz = Mathf.PerlinNoise(x * 0.001f, (z + e) * 0.001f);

    float dx = nx - n1;
    float dz = nz - n1;

    return new Vector2(dx, dz).normalized;
}

//static float FlowAccum(float x, float z)
//{
//    float acc = 0f;

//    float px = x;
//    float pz = z;

//    for (int i = 0; i < 6; i++)
//    {
//        Vector2 d = FlowDir(px, pz);

//        px += d.x * 200f;
//        pz += d.y * 200f;

//        acc += 1f;
//    }

//    return acc;
//}


//alien total con crateres
//public static float GetGeneratedHeight(float x, float z)
//{
//    // 1. DISTORSIÓN DE DOMINIO (Indispensable para eliminar el patrón de "rejilla")
//    // Usamos un ruido de muy baja frecuencia para "curvar" el espacio
//    float warp = Mathf.PerlinNoise(x * 0.0001f, z * 0.0001f) * 500f;
//    float wx = x + warp;
//    float wz = z + warp;

//    // 2. CONTINENTALNESS (Voronoi-like)
//    // Queremos grandes masas de tierra y grandes vacíos, no una mezcla constante
//    float baseNoise = Mathf.PerlinNoise(wx * 0.0002f, wz * 0.0002f);
//    float continent = Mathf.SmoothStep(0.3f, 0.6f, baseNoise);

//    // 3. MONTAÑAS CON CRESTAS AFILADAS (Ridged)
//    // Solo permitimos montañas en el centro de los continentes
//    float mNoise = Mathf.PerlinNoise(wx * 0.0012f, wz * 0.0012f);
//    float mountains = 1.0f - Mathf.Abs(mNoise * 2.0f - 1.0f);
//    mountains = Mathf.Pow(mountains, 3.0f) * 600f * continent;

//    // 4. EL "TAJO" DE EROSIÓN (Flow analítico)
//    // Usamos tu FlowAccum pero para "restar" de forma agresiva
//    float flow = FlowAccum(x, z);
//    float erosion = Mathf.SmoothStep(0.5f, 0.9f, flow) * 200f * continent;

//    // 5. TERRACING (Para que las laderas no sean rampas lisas)
//    // Esto crea esos "escalones" que mencionábamos para el estilo Gran Cañón
//    float hRaw = mountains - erosion;
//    float terracing = Mathf.Floor(hRaw * 0.1f) * 2.0f; // Escalones cada 10 metros
//    float hFinal = Mathf.Lerp(hRaw, hRaw + terracing, 0.3f);

//    return hFinal + (continent * 20f); // Elevación mínima del continente
//}
// Devuelve el valor del ruido en .x y las derivadas parciales en .y, .z
public static Vector3 NoiseDeriv(float x, float z)
{
    float epsilon = 0.01f;
    float v0 = Mathf.PerlinNoise(x, z);
    float vx = Mathf.PerlinNoise(x + epsilon, z);
    float vz = Mathf.PerlinNoise(x, z + epsilon);
    return new Vector3(v0, (vx - v0) / epsilon, (vz - v0) / epsilon);
}

public static float GetGeneratedHeight(float x, float z)
{
    // 1. ESCALA MACRO TECTÓNICA
    // Usamos una escala mucho más grande para que no parezcan burbujas
    float xMacro = x * 0.0001f;
    float zMacro = z * 0.0001f;

    // 2. DOMAIN WARPING DE SEGUNDO ORDEN (Indispensable)
    // Esto es lo que rompe la apariencia de "ruido" y crea formas geológicas
    float qx = Mathf.PerlinNoise(xMacro, zMacro);
    float qz = Mathf.PerlinNoise(xMacro + 5.2f, zMacro + 1.3f);

    // Deformamos las coordenadas para las montañas basándonos en el ruido macro
    float rx = Mathf.PerlinNoise(xMacro + 4.0f * qx + 1.7f, zMacro + 4.0f * qz + 9.2f);
    float rz = Mathf.PerlinNoise(xMacro + 4.0f * qx + 8.3f, zMacro + 4.0f * qz + 2.8f);

    // 3. RIDGED NOISE "AFILADO" (No es el Perlin estándar)
    // Esto crea las aristas que ves en los Alpes, no las lomas de tu imagen
    float mountainNoise = Mathf.PerlinNoise(x * 0.001f + rx, z * 0.001f + rz);
    float mountains = 1.0f - Mathf.Abs(mountainNoise * 2.0f - 1.0f);
    mountains = Mathf.Pow(mountains, 4.0f); // Cuanto más alto el exponente, más real la cima

    // 4. EROSIÓN ANALÍTICA (Usando tu FlowAccum mejorado)
    // En lugar de ser un adorno, el Flow debe ser un "cuchillo" que corta la montaña
    float flow = FlowAccum(x, z);
    float riverCanyon = Mathf.SmoothStep(0.5f, 0.95f, flow) * 350f;

    // 5. BASE CONTINENTAL (Realmente plana)
    float baseContinental = Mathf.PerlinNoise(x * 0.0002f, z * 0.0002f);
    float mask = Mathf.SmoothStep(0.4f, 0.6f, baseContinental);

    // RESULTADO FINAL: Elevamos montañas y tallamos cañones
    // Multiplicamos montañas por la máscara para tener llanuras reales entre cordilleras
    float h = (mask * 50f) + (mountains * 600f * mask) - riverCanyon;

    return h;
}

public static float FlowAccum(float x, float z)
{
    Vector2 p = new Vector2(x * 0.0006f, z * 0.0006f);
    Vector2 d = Vector2.zero; // Acumulador de derivadas
    float f = 0.0f;
    float w = 0.5f; // Peso inicial

    // Iteramos octavas (FBM) sumando las derivadas
    for (int i = 0; i < 4; i++)
    {
        Vector3 n = NoiseDeriv(p.x, p.y);
        d += new Vector2(n.y, n.z); // Sumamos la pendiente
                                    // El truco: el valor del ruido se ve afectado por la pendiente acumulada
        f += w * n.x / (1.0f + d.sqrMagnitude);
        p *= 2.0f;
        w *= 0.5f;
    }

    // El flow es inversamente proporcional a la magnitud de la pendiente acumulada
    // Zonas planas al final de pendientes largas = Mucho flujo (Valles)
    return f;
}







//public static float GetGeneratedHeight(float x, float z)
//{
//    // -----------------------------
//    // CONTINENTALNESS
//    // -----------------------------
//    float continental = Mathf.PerlinNoise(x * 0.00045f, z * 0.00045f);
//    continental = Mathf.SmoothStep(0.25f, 0.8f, continental);

//    // -----------------------------
//    // RIDGE STRUCTURE
//    // genera cordilleras largas
//    // -----------------------------
//    float ridge = Mathf.PerlinNoise(x * 0.0015f, z * 0.0015f);
//    ridge = 1f - Mathf.Abs(ridge * 2f - 1f);
//    ridge = Mathf.Pow(ridge, 3f);

//    // -----------------------------
//    // PEAK / VALLEY MODIFIER
//    // rompe la uniformidad
//    // -----------------------------
//    float peaks = Mathf.PerlinNoise(x * 0.003f, z * 0.003f);
//    peaks = peaks * 2f - 1f;

//    // -----------------------------
//    // EROSION MASK
//    // simula cuencas y drenaje
//    // -----------------------------
//    float erosion = Mathf.PerlinNoise(x * 0.002f, z * 0.002f);
//    erosion = Mathf.Pow(erosion, 2f);

//    // -----------------------------
//    // DOMAIN WARP (ligero)
//    // rompe simetría
//    // -----------------------------
//    float warpX = (Mathf.PerlinNoise(x * 0.004f, z * 0.004f) - 0.5f) * 120f;
//    float warpZ = (Mathf.PerlinNoise(x * 0.004f + 100f, z * 0.004f + 100f) - 0.5f) * 120f;

//    float px = x + warpX;
//    float pz = z + warpZ;

//    // -----------------------------
//    // DETAIL TERRAIN
//    // micro relieve
//    // -----------------------------
//    float detail = Mathf.PerlinNoise(px * 0.05f, pz * 0.05f);
//    detail = detail * 2f - 1f;

//    // -----------------------------
//    // MOUNTAIN SHAPE
//    // -----------------------------
//    float mountain = ridge * 260f;

//    // picos más pronunciados
//    mountain *= 0.6f + peaks * 0.4f;

//    // erosión
//    mountain *= (1f - erosion * 0.7f);

//    // -----------------------------
//    // HEIGHT FINAL
//    // -----------------------------
//    float height =
//        continental * 40f +
//        mountain +
//        detail * 8f * continental;

//    float cliffs = Mathf.Pow(ridge, 4f) * 120f;
//    height += cliffs;

//    return height;
//}

static float RidgeMask(float x, float z)
{
    float r = Mathf.PerlinNoise(x * 0.0015f, z * 0.0015f);
    r = Mathf.Pow(r, 4f);
    return r;
}

static Vector2 Warp(float x, float z)
{
    float wx = Mathf.PerlinNoise(x * WARP_SCALE, z * WARP_SCALE);
    float wz = Mathf.PerlinNoise(x * WARP_SCALE + 53.1f, z * WARP_SCALE + 17.7f);
    return new Vector2(x + (wx * 2 - 1) * 80f, z + (wz * 2 - 1) * 80f);
}

 static float SmoothStep(float edge0, float edge1, float x)
{
    float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
    return t * t * (3.0f - 2.0f * t);
}

}
