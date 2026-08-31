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
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);

        if (qteManager == null)
            qteManager = FindFirstObjectByType<QTEManager>();

        if (qteManager == null)
        {
            Debug.LogError("No se encontró QTEManager para iniciar el QTE de Timeline.", this);
            yield break;
        }

        if (!qteManager.IsQteActive)
            qteManager.StartQTE(qteIndex);
    }
}
