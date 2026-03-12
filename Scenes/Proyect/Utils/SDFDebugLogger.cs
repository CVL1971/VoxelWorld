using System.IO;
using System.Threading;

/// <summary>
/// Utilidad de depuración para volcar muestras SDF a un fichero desde hilos de trabajo.
/// No usa ninguna API de Unity; es segura para multihilo.
/// </summary>
public static class SDFDebugLogger
{
    // Carpeta centralizada para logs de depuración
    const string LOG_PATH = @"e:\Unity\VoxelWorld\Logs\SDFDebug.log";
    const int MAX_LINES = 2048;

    static int mLinesRemaining = MAX_LINES;
    static readonly object mLock = new object();

    public static void LogSample(string pLine)
    {
        // Limitar el número total de líneas para evitar ficheros enormes.
        if (Interlocked.Decrement(ref mLinesRemaining) < 0)
            return;

        try
        {
            lock (mLock)
            {
                File.AppendAllText(LOG_PATH, pLine + "\n");
            }
        }
        catch
        {
            // Silenciar errores de IO en modo debug.
        }
    }

    public static void Reset()
    {
        try
        {
            lock (mLock)
            {
                if (File.Exists(LOG_PATH))
                {
                    File.Delete(LOG_PATH);
                }
                mLinesRemaining = MAX_LINES;
            }
        }
        catch
        {
            // Ignorar errores de borrado.
        }
    }
}

