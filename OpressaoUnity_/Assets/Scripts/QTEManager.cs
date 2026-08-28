using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UI;

public enum QTEType
{
    HoldButtons,
    ButtonSequence,
    RotateStick
}

[Serializable]
public class QTEConfig
{
    public string title = "Nuevo QTE";
    public QTEType type;
    [Min(1f)] public float timeLimit = 6f;
    [Min(1f)] public float requiredAmount = 4f;
    public UnityEvent onSuccess;
    public UnityEvent onFailure;
}
public class QTEManager : MonoBehaviour
{
    [Header("Timeline")]
    [SerializeField] private PlayableDirector timeline;

    [Header("Interfaz")]
    [SerializeField] private GameObject qtePanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Text titleText;
    [SerializeField] private Text instructionText;
    [SerializeField] private Text sequenceText;
    [SerializeField] private Image progressBar;
    [SerializeField] private Image timerBar;

    [Header("QTE de la cinemática")]
    [SerializeField] private List<QTEConfig> qtes = new();

    [Header("Eventos generales")]
    public UnityEvent onAllQtesCompleted;

    private QTEConfig currentQTE;
    private int currentIndex = -1;
    private float timeRemaining;
    private float progress;
    private bool qteActive;

    private readonly List<FaceButton> sequence = new();
    private int sequencePosition;
    private Vector2 previousDirection;
    private float accumulatedAngle;

    private enum FaceButton
    {
        South,
        East,
        West,
        North
    }

    private void Start()
    {
        SetActive(qtePanel, false);
        SetActive(gameOverPanel, false);
    }

    private void Update()
    {
        if (!qteActive) return;

        timeRemaining -= Time.deltaTime;

        switch (currentQTE.type)
        {
            case QTEType.HoldButtons:
                UpdateHoldQTE();
                break;
            case QTEType.ButtonSequence:
                UpdateSequenceQTE();
                break;
            case QTEType.RotateStick:
                UpdateRotateQTE();
                break;
        }

        UpdateBars();

        if (progress >= currentQTE.requiredAmount)
            CompleteQTE();
        else if (timeRemaining <= 0f)
            FailQTE();
    }

   
    public void StartQTE(int index)
    {
        if (index < 0 || index >= qtes.Count)
        {
            Debug.LogError($"No existe un QTE con índice {index}.");
            return;
        }

        currentIndex = index;
        currentQTE = qtes[index];
        timeRemaining = currentQTE.timeLimit;
        progress = 0f;
        qteActive = true;
        previousDirection = Vector2.zero;
        accumulatedAngle = 0f;

        if (timeline != null) timeline.Pause();
        SetActive(gameOverPanel, false);
        SetActive(qtePanel, true);

        if (titleText != null) titleText.text = currentQTE.title;
        PrepareInstructions();
        UpdateBars();
    }

    public void RetryQTE()
    {
        if (currentIndex >= 0) StartQTE(currentIndex);
    }

    private void PrepareInstructions()
    {
        sequence.Clear();
        sequencePosition = 0;

        switch (currentQTE.type)
        {
            case QTEType.HoldButtons:
                SetText(instructionText, "Mantén L2 + R2  /  Teclado: Q + E");
                SetText(sequenceText, "");
                break;

            case QTEType.ButtonSequence:
                SetText(instructionText, "Pulsa la secuencia  /  Teclado: WASD o flechas");
                int length = Mathf.Max(1, Mathf.RoundToInt(currentQTE.requiredAmount));
                for (int i = 0; i < length; i++)
                    sequence.Add((FaceButton)UnityEngine.Random.Range(0, 4));
                ShowSequence();
                break;

            case QTEType.RotateStick:
                SetText(instructionText, "Gira un análogo  /  Teclado: recorre WASD en círculo");
                SetText(sequenceText, "↻");
                break;
        }
    }

    private void UpdateHoldQTE()
    {
        bool keyboard = Keyboard.current != null &&
                        Keyboard.current.qKey.isPressed &&
                        Keyboard.current.eKey.isPressed;

        bool controller = Gamepad.current != null &&
                          Gamepad.current.leftTrigger.ReadValue() > 0.65f &&
                          Gamepad.current.rightTrigger.ReadValue() > 0.65f;

        if (keyboard || controller)
            progress += Time.deltaTime;
        else
            progress = Mathf.Max(0f, progress - Time.deltaTime * 0.5f);
    }

    private void UpdateSequenceQTE()
    {
        FaceButton? pressed = ReadFaceButton();
        if (pressed == null) return;

        if (pressed == sequence[sequencePosition])
        {
            sequencePosition++;
            progress = sequencePosition;

            if (sequencePosition < sequence.Count)
                ShowSequence();
        }
        else
        {
            sequencePosition = 0;
            progress = 0f;
            ShowSequence();
        }
    }

    private void UpdateRotateQTE()
    {
        Vector2 direction = ReadDirection();

        if (direction.magnitude < 0.65f)
        {
            previousDirection = Vector2.zero;
            return;
        }

        if (previousDirection.magnitude >= 0.65f)
        {
            float angle = Mathf.Abs(Vector2.SignedAngle(previousDirection, direction));

            if (angle < 100f)
            {
                accumulatedAngle += angle;
                progress = accumulatedAngle / 360f;
            }
        }

        previousDirection = direction;
    }

    private void CompleteQTE()
    {
        qteActive = false;
        SetActive(qtePanel, false);
        currentQTE.onSuccess?.Invoke();

        if (timeline != null) timeline.Resume();
        if (currentIndex == qtes.Count - 1) onAllQtesCompleted?.Invoke();
    }

    private void FailQTE()
    {
        qteActive = false;
        SetActive(qtePanel, false);
        SetActive(gameOverPanel, true);
        currentQTE.onFailure?.Invoke();
    }

    private void UpdateBars()
    {
        if (progressBar != null)
            progressBar.fillAmount = Mathf.Clamp01(progress / currentQTE.requiredAmount);

        if (timerBar != null)
            timerBar.fillAmount = Mathf.Clamp01(timeRemaining / currentQTE.timeLimit);
    }

    private void ShowSequence()
    {
        if (sequencePosition >= sequence.Count) return;
        SetText(sequenceText, ButtonName(sequence[sequencePosition]));
    }

    private static FaceButton? ReadFaceButton()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame) return FaceButton.South;
            if (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame) return FaceButton.East;
            if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame) return FaceButton.West;
            if (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame) return FaceButton.North;
        }

        if (Gamepad.current != null)
        {
            if (Gamepad.current.buttonSouth.wasPressedThisFrame) return FaceButton.South;
            if (Gamepad.current.buttonEast.wasPressedThisFrame) return FaceButton.East;
            if (Gamepad.current.buttonWest.wasPressedThisFrame) return FaceButton.West;
            if (Gamepad.current.buttonNorth.wasPressedThisFrame) return FaceButton.North;
        }

        return null;
    }

    private static Vector2 ReadDirection()
    {
        if (Gamepad.current != null)
        {
            Vector2 left = Gamepad.current.leftStick.ReadValue();
            Vector2 right = Gamepad.current.rightStick.ReadValue();
            return left.sqrMagnitude >= right.sqrMagnitude ? left : right;
        }

        if (Keyboard.current == null) return Vector2.zero;

        Vector2 direction = Vector2.zero;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) direction.y++;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) direction.y--;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) direction.x--;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) direction.x++;
        return direction.normalized;
    }

    private static string ButtonName(FaceButton button) => button switch
    {
        FaceButton.South => "A  /  S  /  ↓",
        FaceButton.East => "B  /  D  /  →",
        FaceButton.West => "X  /  A  /  ←",
        FaceButton.North => "Y  /  W  /  ↑",
        _ => "?"
    };

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null) target.SetActive(value);
    }

    private static void SetText(Text target, string value)
    {
        if (target != null) target.text = value;
    }
}
