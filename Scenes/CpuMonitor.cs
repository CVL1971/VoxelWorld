using UnityEngine;
using System;
using System.Diagnostics;
using System.Threading;

public class CPUCoreProfiler : MonoBehaviour
{
    private int mProcessorCount;
    private float mTotalUsage;
    private float[] mThreadLoads;

    // Variables para el cálculo de tiempo de CPU
    private TimeSpan mLastProcessorTime;
    private DateTime mLastSampleTime;

    [Header("Configuración")]
    [Range(0.1f, 2.0f)] public float UpdateInterval = 0.5f; // Velocidad de actualización humana
    private float mTimer;

    private GUIStyle mLabelStyle;
    private GUIStyle mBarStyle;
    private Texture2D mBarTex;

    void Start()
    {
        mProcessorCount = SystemInfo.processorCount;
        mThreadLoads = new float[mProcessorCount];
        mLastProcessorTime = Process.GetCurrentProcess().TotalProcessorTime;
        mLastSampleTime = DateTime.Now;

        // Estilos de UI
        mBarTex = new Texture2D(1, 1);
        mBarTex.SetPixel(0, 0, Color.white);
        mBarTex.Apply();
    }

    void Update()
    {
        mTimer += Time.deltaTime;
        if (mTimer >= UpdateInterval)
        {
            CalculateUsage();
            mTimer = 0;
        }
    }

    private void CalculateUsage()
    {
        // 1. Cálculo de uso de CPU Real del Proceso
        DateTime currentTime = DateTime.Now;
        TimeSpan currentProcessorTime = Process.GetCurrentProcess().TotalProcessorTime;

        double deltaWallTime = (currentTime - mLastSampleTime).TotalMilliseconds;
        double deltaCpuTime = (currentProcessorTime - mLastProcessorTime).TotalMilliseconds;

        // El uso total se divide por el número de núcleos para obtener el % real
        mTotalUsage = (float)((deltaCpuTime / (deltaWallTime * mProcessorCount)) * 100f);

        // 2. Monitor de hilos del ThreadPool
        // Esto nos dice cuántos hilos están ocupados realmente por tu sistema de mallas
        ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxIOThreads);
        ThreadPool.GetAvailableThreads(out int availWorkerThreads, out int availIOThreads);

        int busyThreads = maxWorkerThreads - availWorkerThreads;

        // 3. Distribuir la carga visualmente en las barras
        for (int i = 0; i < mProcessorCount; i++)
        {
            // Simulamos la distribución: si hay 4 hilos ocupados, las primeras 4 barras se llenan
            float targetLoad = (i < busyThreads) ? UnityEngine.Random.Range(80f, 100f) : UnityEngine.Random.Range(0f, 5f);

            // Si el proceso general está muy alto, añadimos ruido de fondo a todos
            targetLoad = Mathf.Max(targetLoad, mTotalUsage * UnityEngine.Random.Range(0.8f, 1.2f));

            // Suavizado para que no salte bruscamente
            mThreadLoads[i] = Mathf.Lerp(mThreadLoads[i], Mathf.Clamp(targetLoad, 0, 100), 0.5f);
        }

        mLastProcessorTime = currentProcessorTime;
        mLastSampleTime = currentTime;
    }

    void OnGUI()
    {
        if (mLabelStyle == null)
        {
            mLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        }

        float width = 300;
        float height = 20 + (mProcessorCount * 15) + 40;

        GUI.Box(new Rect(10, 10, width, height), "SDF Engine - CPU Monitor");

        GUI.Label(new Rect(20, 35, 250, 20), $"Uso Total APP: {mTotalUsage:F1}%", mLabelStyle);

        for (int i = 0; i < mProcessorCount; i++)
        {
            float load = mThreadLoads[i];
            Rect barArea = new Rect(20, 60 + (i * 15), 260, 12);

            // Fondo de la barra
            GUI.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            GUI.DrawTexture(barArea, mBarTex);

            // Color de la barra según carga (Verde -> Rojo)
            GUI.color = Color.Lerp(Color.green, Color.red, load / 100f);
            GUI.DrawTexture(new Rect(barArea.x, barArea.y, (load / 100f) * barArea.width, barArea.height), mBarTex);

            GUI.color = Color.white;
            if (i == 0) GUI.Label(new Rect(barArea.xMax + 5, barArea.y - 2, 50, 15), "Core " + i, GUI.skin.label);
        }
    }
}