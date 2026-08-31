using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>Recibe los marcadores de Signal Track y abre el QTE asociado.</summary>
public sealed class QTESignalReceiver : MonoBehaviour, INotificationReceiver
{
    [SerializeField] private QTEManager qteManager;
    [SerializeField] private List<SignalAsset> qteSignals = new();

    public void Configure(QTEManager manager, List<SignalAsset> signals)
    {
        qteManager = manager;
        qteSignals = signals;
    }

    public void OnNotify(Playable origin, INotification notification, object context)
    {
        if (notification is not SignalEmitter signal || qteManager == null || qteManager.IsQteActive)
            return;

        // Los Signals se colocan antes de cada video siguiente. No reinician
        // el QTE anterior: abren exactamente el próximo de la secuencia.
        if (qteSignals.Contains(signal.asset))
        {
            qteManager.StartNextQTE();
        }
    }
}
