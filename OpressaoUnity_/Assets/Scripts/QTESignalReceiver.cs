using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

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

        int qteIndex = qteSignals.IndexOf(signal.asset);
        if (qteIndex >= 0)
            qteManager.StartQTE(qteIndex);
    }
}
