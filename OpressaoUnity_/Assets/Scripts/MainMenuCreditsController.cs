using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class MainMenuCreditsController : MonoBehaviour
{
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject creditsPanel;
    [SerializeField, Min(100f)] private float gamepadCursorSpeed = 1100f;

    private readonly List<RaycastResult> uiRaycastResults = new();

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        MoveCursorWithLeftStick();
        TryClickUiWithCross();
    }

    public void ShowCredits()
    {
        if (menuPanel != null)
            menuPanel.SetActive(false);
        if (creditsPanel != null)
            creditsPanel.SetActive(true);
    }

    public void HideCredits()
    {
        if (creditsPanel != null)
            creditsPanel.SetActive(false);
        if (menuPanel != null)
            menuPanel.SetActive(true);
    }

    public void Configure(GameObject menu, GameObject credits)
    {
        menuPanel = menu;
        creditsPanel = credits;
    }

    private void MoveCursorWithLeftStick()
    {
        if (Gamepad.current == null || Mouse.current == null)
            return;

        Vector2 stick = Gamepad.current.leftStick.ReadValue();
        if (stick.sqrMagnitude < 0.16f)
            return;

        Vector2 cursorPosition = Mouse.current.position.ReadValue();
        Vector2 movement = stick * (gamepadCursorSpeed * Time.unscaledDeltaTime);
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
            Debug.LogWarning("[Main Menu] PS4 X was pressed, but UI click support is unavailable.");
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
            return;

        GameObject target = uiRaycastResults[0].gameObject;
        ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerClickHandler);
        Debug.Log($"[Main Menu] PS4 X sent a left click to {target.name}.");
    }
}
