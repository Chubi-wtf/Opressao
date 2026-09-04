using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class IntroInstructionsController : MonoBehaviour
{
    private const string IntroPanelName = "PanelIntroInstrucciones";

    private GameObject introPanel;
    private Button startButton;
    private QTEManager qteManager;
    private bool listenerRegistered;

    public bool HasIntroPanel => introPanel != null;

    public static IntroInstructionsController EnsureOn(GameObject owner)
    {
        IntroInstructionsController controller = owner.GetComponent<IntroInstructionsController>();
        return controller != null ? controller : owner.AddComponent<IntroInstructionsController>();
    }

    private void Awake()
    {
        qteManager = GetComponent<QTEManager>();
        ResolvePanel();
        Debug.Log($"[Intro] Awake. Panel found: {introPanel != null}; JUGAR button found: {startButton != null}.");
        ShowIntro();
    }

    private void Update()
    {
        if (introPanel == null || !introPanel.activeInHierarchy)
            return;

        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            Debug.Log("[Intro] PS4 X pressed: starting the game.");
            StartGame();
        }
    }

    public void ShowIntro()
    {
        ResolvePanel();
        if (introPanel == null)
        {
            Debug.LogWarning("[Intro] PanelIntroInstrucciones was not found in the loaded scene.");
            return;
        }

        introPanel.SetActive(true);
        if (startButton != null && !listenerRegistered)
        {
            startButton.onClick.AddListener(StartGame);
            listenerRegistered = true;
            Debug.Log("[Intro] JUGAR button listener registered.");
        }
    }

    public void HideIntro()
    {
        if (introPanel != null)
            introPanel.SetActive(false);
    }

    public void StartGame()
    {
        qteManager ??= GetComponent<QTEManager>();
        if (qteManager != null)
        {
            Debug.Log("[Intro] JUGAR requested BeginGame.");
            qteManager.BeginGame();
        }
        else
        {
            Debug.LogError("[Intro] QTEManager is missing; the game cannot start.");
        }
    }

    private void ResolvePanel()
    {
        if (introPanel == null)
            introPanel = FindSceneObject(IntroPanelName);

        if (introPanel != null && startButton == null)
            startButton = introPanel.GetComponentInChildren<Button>(true);
    }

    private static GameObject FindSceneObject(string objectName)
    {
        foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (candidate.name == objectName && candidate.scene.IsValid() && candidate.scene.isLoaded)
                return candidate;
        }

        return null;
    }
}
