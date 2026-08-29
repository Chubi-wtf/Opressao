using System.Collections;
using UnityEngine;

/// <summary>
/// Añádalo al mismo objeto que activa una Activation Track. Cuando la imagen
/// aparece, arranca el QTE indicado y el QTEManager pausa el Timeline.
/// </summary>
public sealed class QTEStartOnTimelineImage : MonoBehaviour
{
    [SerializeField] private QTEManager qteManager;
    [SerializeField, Min(0)] private int qteIndex;

    private IEnumerator Start()
    {
        // Espera un frame: así QTEManager ya ejecutó Start y no ocultará el panel
        // después de que lo activemos.
        yield return null;

        if (qteManager == null)
            qteManager = FindFirstObjectByType<QTEManager>();

        if (qteManager == null)
        {
            Debug.LogError("No se encontró QTEManager para iniciar el QTE de Timeline.", this);
            yield break;
        }

        qteManager.StartQTE(qteIndex);
    }
}
