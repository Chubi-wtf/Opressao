#if UNITY_EDITOR
using UnityEngine;

// Test-only bridge: execute virtual inputs once per actual player frame.
[DefaultExecutionOrder(-1000)]
public sealed class GameplayCheckDriver : MonoBehaviour
{
    public static System.Action OnFrame;
    private void Update() => OnFrame?.Invoke();
}
#endif
