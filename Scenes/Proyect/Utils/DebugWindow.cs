using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ventana de debug global para scripts que no heredan de MonoBehaviour.
/// Muestra un recuadro semitransparente en la esquina superior izquierda con pares clave-valor.
/// </summary>
public static class DebugWindow
{
    const string ROOT_NAME = "DebugWindowRoot";
    const float BACKGROUND_ALPHA = 0.3f;
    const string COPY_BUTTON_TEXT = "Copiar";

    static bool mInitialized;
    static DebugWindowBehaviour mBehaviour;

    // Historial de líneas: una entrada por llamada a Debug(...)
    static List<string> mPendingLines = new List<string>();
    static List<string> mShownLines = new List<string>();

    static Vector2 mScrollPosition = Vector2.zero;

    static int mRefreshSeconds;
    static float mNextRefreshTime;

    #region API PÚBLICA

    public static void Debug(string pTexto, int pValor, int pRefreshSeconds = 0)
    {
        EnsureInitialized();
        SetValue(pTexto, pValor.ToString());
        SetRefresh(pRefreshSeconds);
    }

    public static void Debug(string pTexto, float pValor, int pRefreshSeconds = 0)
    {
        EnsureInitialized();
        SetValue(pTexto, pValor.ToString("F3"));
        SetRefresh(pRefreshSeconds);
    }

    public static void Debug(string pTexto, byte pValor, int pRefreshSeconds = 0)
    {
        EnsureInitialized();
        SetValue(pTexto, pValor.ToString());
        SetRefresh(pRefreshSeconds);
    }

    public static void Debug(string pTexto, string pValor, int pRefreshSeconds = 0)
    {
        EnsureInitialized();
        string vValor = pValor == null ? "null" : pValor;
        SetValue(pTexto, vValor);
        SetRefresh(pRefreshSeconds);
    }

    public static void Clear()
    {
        EnsureInitialized();
        mPendingLines.Clear();
        mShownLines.Clear();
    }

    #endregion

    #region INICIALIZACIÓN

    static void EnsureInitialized()
    {
        if (mInitialized)
            return;

        GameObject vRoot = GameObject.Find(ROOT_NAME);
        if (vRoot == null)
        {
            vRoot = new GameObject(ROOT_NAME);
            Object.DontDestroyOnLoad(vRoot);
        }

        mBehaviour = vRoot.GetComponent<DebugWindowBehaviour>();
        if (mBehaviour == null)
            mBehaviour = vRoot.AddComponent<DebugWindowBehaviour>();

        mBehaviour.hideFlags = HideFlags.HideInHierarchy;

        mInitialized = true;
    }

    #endregion

    #region LÓGICA INTERNA

    static void SetValue(string pClave, string pValor)
    {
        string vTexto = pClave;
        if (!string.IsNullOrEmpty(pValor))
            vTexto = pClave + ": " + pValor;

        mPendingLines.Add(vTexto);
    }

    static void SetRefresh(int pSegundos)
    {
        if (pSegundos < 0)
            pSegundos = 0;

        mRefreshSeconds = pSegundos;
        if (mRefreshSeconds == 0)
        {
            CopyPendingToShown();
        }
        else
        {
            mNextRefreshTime = Time.realtimeSinceStartup + mRefreshSeconds;
        }
    }

    static void CommitIfNeeded()
    {
        if (!mInitialized)
            return;

        if (mRefreshSeconds <= 0)
        {
            CopyPendingToShown();
            return;
        }

        float vNow = Time.realtimeSinceStartup;
        if (vNow >= mNextRefreshTime)
        {
            CopyPendingToShown();
            mNextRefreshTime = vNow + mRefreshSeconds;
        }
    }

    static void CopyPendingToShown()
    {
        for (int i = 0; i < mPendingLines.Count; i++)
        {
            mShownLines.Add(mPendingLines[i]);
        }
        mPendingLines.Clear();
    }

    static void OnGUIInternal()
    {
        if (mShownLines.Count == 0)
            return;

        float vWidth = 360f;
        float vMargin = 8f;
        float vTitleHeight = 28f;
        float vMaxHeight = 300f;

        Color vOldColor = GUI.color;

        GUIStyle vTitleStyle = new GUIStyle(GUI.skin.label);
        vTitleStyle.fontSize = 28;
        vTitleStyle.fontStyle = FontStyle.Bold;

        GUIStyle vLineStyle = new GUIStyle(GUI.skin.label);
        vLineStyle.fontSize = 22;
        vLineStyle.wordWrap = true;

        float vContentWidth = vWidth - vMargin * 2f - 16f;
        int vCount = mShownLines.Count;
        float[] vHeights = new float[vCount];
        float vContentHeight = 0f;
        for (int i = 0; i < vCount; i++)
        {
            string vTextoLinea = mShownLines[i];
            vHeights[i] = vLineStyle.CalcHeight(new GUIContent(vTextoLinea), vContentWidth);
            vContentHeight += vHeights[i];
        }

        float vVisibleHeight = Mathf.Min(vTitleHeight + vContentHeight + vMargin * 2f, vMaxHeight);

        float vX = Screen.width - vWidth - vMargin;
        if (vX < vMargin)
            vX = vMargin;

        Rect vRect = new Rect(vX, 10f, vWidth, vVisibleHeight);

        // Fondo semitransparente más oscuro
        GUI.color = new Color(0f, 0f, 0f, BACKGROUND_ALPHA);
        GUI.Box(vRect, GUIContent.none);

        GUI.color = Color.white;

        Rect vTitleRect = new Rect(vRect.x + vMargin, vRect.y + vMargin, vRect.width - vMargin * 2f, vTitleHeight);
        GUI.Label(vTitleRect, "DebugWindow", vTitleStyle);

        // Botón para copiar el contenido al portapapeles
        float vButtonWidth = 70f;
        float vButtonHeight = 22f;
        Rect vButtonRect = new Rect(
            vRect.xMax - vMargin - vButtonWidth,
            vRect.y + vMargin + (vTitleHeight - vButtonHeight) * 0.5f,
            vButtonWidth,
            vButtonHeight
        );
        if (GUI.Button(vButtonRect, COPY_BUTTON_TEXT))
        {
            System.Text.StringBuilder vBuilder = new System.Text.StringBuilder();
            for (int i = 0; i < vCount; i++)
            {
                vBuilder.AppendLine(mShownLines[i]);
            }
            GUIUtility.systemCopyBuffer = vBuilder.ToString();
        }

        Rect vScrollRect = new Rect(
            vRect.x + vMargin,
            vRect.y + vMargin + vTitleHeight,
            vRect.width - vMargin * 2f,
            vRect.height - vMargin * 2f - vTitleHeight
        );

        Rect vViewRect = new Rect(0f, 0f, vContentWidth, vContentHeight);

        mScrollPosition = GUI.BeginScrollView(vScrollRect, mScrollPosition, vViewRect);

        float vYAccum = 0f;
        for (int i = 0; i < vCount; i++)
        {
            Rect vLineRect = new Rect(0f, vYAccum, vViewRect.width, vHeights[i]);
            string vTexto = mShownLines[i];
            GUI.Label(vLineRect, vTexto, vLineStyle);
            vYAccum += vHeights[i];
        }

        GUI.EndScrollView();

        GUI.color = vOldColor;
    }

    #endregion

    #region PUENTE MONOBEHAVIOUR

    class DebugWindowBehaviour : MonoBehaviour
    {
        void Update()
        {
            CommitIfNeeded();
        }

        void OnGUI()
        {
            OnGUIInternal();
        }
    }

    #endregion
}

