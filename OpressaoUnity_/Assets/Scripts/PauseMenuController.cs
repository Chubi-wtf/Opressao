using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class PauseMenuController : MonoBehaviour
{
    private const string PausePanelName = "PanelPausa";
    private const string OptionsPanelName = "PanelOpcionesPausa";

    private GameObject pausePanel;
    private GameObject optionsPanel;
    private readonly List<PlayableDirector> pausedDirectors = new();
    private readonly List<RaycastResult> uiRaycastResults = new();
    private bool isPaused;
    private bool qteWasActive;
    private float previousTimeScale = 1f;
    private bool previousAudioPause;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;

    [SerializeField, Min(100f)] private float pauseCursorSpeed = 1100f;

    public static void EnsureOn(GameObject owner)
    {
        if (owner != null && owner.GetComponent<PauseMenuController>() == null)
            owner.AddComponent<PauseMenuController>();
    }

    private void Start()
    {
        ResolvePanels();
        ConfigureButtons();
        SetPanelActive(pausePanel, false);
        SetPanelActive(optionsPanel, false);
    }

    private void Update()
    {
        if (isPaused)
        {
            MoveCursorWithLeftStick();
            TryClickUiWithCross();
        }

        bool startPressed = Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
        bool escapePressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        if (!startPressed && !escapePressed)
            return;

        if (!isPaused)
            PauseGame();
        else if (optionsPanel != null && optionsPanel.activeSelf)
            CloseOptions();
        else
            ResumeGame();
    }

    public void PauseGame()
    {
        ResolvePanels();
        if (pausePanel == null)
            return;

        isPaused = true;
        previousTimeScale = Time.timeScale;
        previousAudioPause = AudioListener.pause;
        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;
        qteWasActive = GetComponent<QTEManager>() != null && GetComponent<QTEManager>().IsQteActive;

        pausedDirectors.Clear();
        foreach (PlayableDirector director in FindObjectsByType<PlayableDirector>(FindObjectsSortMode.None))
        {
            if (director.state == PlayState.Playing)
            {
                pausedDirectors.Add(director);
                director.Pause();
            }
        }

        TimelineVideoPlayerBehaviour.PauseAll();
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetPanelActive(pausePanel, true);
        SetPanelActive(optionsPanel, false);
    }

    public void ResumeGame()
    {
        if (!isPaused)
            return;

        SetPanelActive(optionsPanel, false);
        SetPanelActive(pausePanel, false);
        Time.timeScale = previousTimeScale;
        AudioListener.pause = previousAudioPause;
        Cursor.lockState = previousCursorLockMode;
        Cursor.visible = previousCursorVisible;

        if (!qteWasActive)
        {
            foreach (PlayableDirector director in pausedDirectors)
            {
                if (director != null)
                    director.Play();
            }

            TimelineVideoPlayerBehaviour.ResumeAll();
        }

        pausedDirectors.Clear();
        isPaused = false;
    }

    private void MoveCursorWithLeftStick()
    {
        if (Gamepad.current == null || Mouse.current == null)
            return;

        Vector2 stick = Gamepad.current.leftStick.ReadValue();
        if (stick.sqrMagnitude < 0.16f)
            return;

        Vector2 cursorPosition = Mouse.current.position.ReadValue();
        Vector2 movement = stick * (pauseCursorSpeed * Time.unscaledDeltaTime);
        Vector2 targetPosition = cursorPosition + new Vector2(movement.x, movement.y);
        targetPosition.x = Mathf.Clamp(targetPosition.x, 0f, Screen.width);
        targetPosition.y = Mathf.Clamp(targetPosition.y, 0f, Screen.height);
        Mouse.current.WarpCursorPosition(targetPosition);
    }

    private void TryClickUiWithCross()
    {
        if (Gamepad.current == null || !Gamepad.current.buttonSouth.wasPressedThisFrame)
            return;

        if (EventSystem.current == null || Mouse.current == null)
        {
            Debug.LogWarning("[Pause] PS4 X was pressed, but no EventSystem or mouse device is available for the UI click.");
            return;
        }

        PointerEventData pointer = new(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue(),
            button = PointerEventData.InputButton.Left
        };

        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointer, uiRaycastResults);
        if (uiRaycastResults.Count == 0)
        {
            Debug.Log("[Pause] PS4 X click found no UI element under the cursor.");
            return;
        }

        GameObject target = uiRaycastResults[0].gameObject;
        ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerClickHandler);
        Debug.Log($"[Pause] PS4 X sent a left click to {target.name}.");
    }

    public void OpenOptions()
    {
        if (!isPaused)
            PauseGame();

        SetPanelActive(pausePanel, false);
        SetPanelActive(optionsPanel, true);
    }

    public void CloseOptions()
    {
        if (!isPaused)
            return;

        SetPanelActive(optionsPanel, false);
        SetPanelActive(pausePanel, true);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void ReturnToMenu()
    {
        Debug.Log("[Pause] Returning to Main Menu.");
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene("Main Menu");
    }

    private void ResolvePanels()
    {
        pausePanel ??= FindSceneObject(PausePanelName);
        optionsPanel ??= FindSceneObject(OptionsPanelName);
    }

    private void ConfigureButtons()
    {
        ConfigurePauseButtons();
        ConfigureOptionsButtons();
    }

    private void ConfigurePauseButtons()
    {
        if (pausePanel == null)
            return;

        Button[] buttons = pausePanel.GetComponentsInChildren<Button>(true);
        HashSet<Button> assigned = new();
        foreach (Button button in buttons)
        {
            string name = button.name.ToLowerInvariant();
            if (Contains(name, "opcion", "option"))
                Assign(button, OpenOptions, assigned);
            else if (Contains(name, "reanudar", "continuar", "resume", "volver", "back"))
                Assign(button, ResumeGame, assigned);
            else if (Contains(name, "menu", "menú", "inicio", "home"))
                Assign(button, ReturnToMenu, assigned);
            else if (Contains(name, "salir", "quit", "exit"))
                Assign(button, QuitGame, assigned);
        }

        if (buttons.Length > 0 && assigned.Count == 0)
            Assign(buttons[0], ResumeGame, assigned);
        if (buttons.Length > 1 && !assigned.Contains(buttons[1]))
            Assign(buttons[1], OpenOptions, assigned);
        if (buttons.Length > 2 && assigned.Count < 3)
            Assign(buttons[2], ReturnToMenu, assigned);
        if (buttons.Length > 3 && assigned.Count < 4)
            Assign(buttons[3], QuitGame, assigned);
    }

    private void ConfigureOptionsButtons()
    {
        if (optionsPanel == null)
            return;

        Button[] buttons = optionsPanel.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            string name = button.name.ToLowerInvariant();
            if (Contains(name, "volver", "regresar", "back", "return"))
            {
                button.onClick.AddListener(CloseOptions);
                return;
            }
        }

        if (buttons.Length > 0)
            buttons[0].onClick.AddListener(CloseOptions);
    }

    private static void Assign(Button button, UnityEngine.Events.UnityAction action, HashSet<Button> assigned)
    {
        if (button == null || assigned.Contains(button))
            return;

        button.onClick.AddListener(action);
        assigned.Add(button);
    }

    private static bool Contains(string value, params string[] words)
    {
        foreach (string word in words)
        {
            if (value.Contains(word, StringComparison.Ordinal))
                return true;
        }

        return false;
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

    private static void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }
}
