using System.Collections;
using UnityEngine;


public sealed class QTEStartOnTimelineImage : MonoBehaviour
{
    #region referencias

    [SerializeField] private QTEManager qteManager;
    [SerializeField, Min(0)] private int qteIndex;

    #endregion

    #region timeline

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
    #endregion
}
