using UnityEngine;
using UnityEditor; // Solo funciona en el Editor
using System;

public class Limpiador : MonoBehaviour
{
    // Esto crea un botón en el menú superior de Unity
    [MenuItem("Herramientas/Limpiar RAM Forzado")]
    public static void LimpiarAhora()
    {
        Debug.Log("--- Iniciando Purga de Memoria ---");

        // 1. Matar referencias de C#
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // 2. Matar assets de Unity huérfanos
        EditorUtility.UnloadUnusedAssetsImmediate();

        Debug.Log("--- Purga Completa. Comprueba el Administrador de Tareas ---");
    }
}