using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UI;
using TMPro;

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
    [SerializeField] private bool startFirstQteWithScene;
    [SerializeField, Min(0f)] private float firstQteStartDelay = 0.15f;
    [SerializeField] private List<double> successVideoTimes = new() { 6.216666666666656d, 18.483333333333334d };

    [Header("UI")]
    [SerializeField] private GameObject qtePanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Text titleText;
    [SerializeField] private Text instructionText;
    [SerializeField] private Text sequenceText;
    [SerializeField] private TMP_Text gameOverText;
    [SerializeField] private Image progressBar;
    [SerializeField] private Image timerBar;
    [SerializeField] private RectTransform timerContainer;
    [SerializeField] private RectTransform leftTimerClose;
    [SerializeField] private RectTransform rightTimerClose;

    [Header("Cinematic QTEs")]
    [SerializeField] private List<QTEConfig> qtes = new();

    [Header("General Events")]
    public UnityEvent onAllQtesCompleted;

    [Header("Diagnostics")]
    [SerializeField] private bool debugQteFlow = true;

    private QTEConfig currentQTE;
    private int currentIndex = -1;
    private float timeRemaining;
    private float progress;
    private bool qteActive;
    private Text feedbackText;
    private float feedbackExpiresAt;
    private bool holdInputWasCorrect;

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

    private void Awake()
    {
        EnsureClosingTimerVisual();
        EnsureQteOverlayCanvas();
        EnsureGameOverPresentation();
        EnsureFeedbackVisual();
    }

    private IEnumerator Start()
    {
        SetActive(qtePanel, false);
        SetActive(gameOverPanel, false);

        if (timeline != null)
        {
            timeline.time = 0d;
            timeline.Evaluate();
            timeline.Play();
        }

        if (!startFirstQteWithScene || qtes.Count == 0)
            yield break;

        if (firstQteStartDelay > 0f)
            yield return new WaitForSeconds(firstQteStartDelay);

        StartQTE(0);
    }

    private void Update()
    {
        if (!qteActive) return;

        if (feedbackText != null && feedbackText.gameObject.activeSelf &&
            Time.unscaledTime > feedbackExpiresAt)
            feedbackText.gameObject.SetActive(false);

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
        holdInputWasCorrect = false;

        Trace($"Inicio QTE {index + 1}/{qtes.Count}: {currentQTE.title}. Límite: {currentQTE.timeLimit:0.##} s.");

        if (timeline != null)
            timeline.Pause();

        SetActive(gameOverPanel, false);
        SetActive(qtePanel, true);

        qtePanel.transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();

        if (titleText != null) titleText.text = currentQTE.title;
        PrepareInstructions();
        UpdateBars();
    }

    public bool IsQteActive => qteActive;

    public void StartNextQTE()
    {
        if (qteActive)
        {
            Trace("Signal recibido mientras un QTE seguía activo; se ignora.");
            return;
        }

        if (currentIndex >= qtes.Count - 1)
        {
            Trace("Signal recibido, pero ya no quedan QTE configurados.");
            return;
        }

        Trace($"Signal recibido: se abrirá el QTE {currentIndex + 2}.");
        StartQTE(currentIndex + 1);
    }

    public void RetryQTE()
    {
        qteActive = false;
        currentQTE = null;
        currentIndex = -1;
        timeRemaining = 0f;
        progress = 0f;
        sequence.Clear();
        sequencePosition = 0;
        previousDirection = Vector2.zero;
        accumulatedAngle = 0f;

        SetActive(qtePanel, false);
        SetActive(gameOverPanel, false);

        if (timeline == null)
            return;

        timeline.Stop();
        timeline.time = 0d;
        timeline.Evaluate();
        timeline.Play();
    }

    private void PrepareInstructions()
    {
        sequence.Clear();
        sequencePosition = 0;

        switch (currentQTE.type)
        {
            case QTEType.HoldButtons:
                SetText(instructionText, "Mantén L2 + R2\nTeclado: Q + E");
                SetText(sequenceText, "");
                ShowFeedback("MANTÉN AMBOS BOTONES", new Color(1f, 0.82f, 0.18f), 10f);
                break;

            case QTEType.ButtonSequence:
                SetText(instructionText, "Sigue el botón indicado\nMando: A / B / X / Y · Teclado: WASD o flechas");
                ConfigureSequencePromptLayout();
                int length = Mathf.Max(1, Mathf.RoundToInt(currentQTE.requiredAmount));
                for (int i = 0; i < length; i++)
                    sequence.Add((FaceButton)UnityEngine.Random.Range(0, 4));
                ShowSequence();
                ShowFeedback("", Color.white, 0f);
                break;

            case QTEType.RotateStick:
                SetText(instructionText, "Gira cualquier análogo en ambos sentidos\nTeclado: A → S → D → W → A, o al revés");
                SetText(sequenceText, "↻");
                ShowFeedback("COMPLETA UN GIRO", new Color(1f, 0.82f, 0.18f), 10f);
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
        {
            progress += Time.deltaTime;
            ShowFeedback("BOTONES CORRECTOS", new Color(0.25f, 1f, 0.42f), 0.2f);
            holdInputWasCorrect = true;
        }
        else
        {
            progress = Mathf.Max(0f, progress - Time.deltaTime * 0.5f);
            if (holdInputWasCorrect)
                ShowFeedback("FALTAN BOTONES", new Color(1f, 0.38f, 0.32f), 1f);
            holdInputWasCorrect = false;
        }
    }

    private void UpdateSequenceQTE()
    {
        FaceButton? pressed = ReadFaceButton();
        if (pressed == null) return;

        if (pressed == sequence[sequencePosition])
        {
            sequencePosition++;
            progress = sequencePosition;
            ShowFeedback("CORRECTO", new Color(0.25f, 1f, 0.42f), 0.5f);

            if (sequencePosition < sequence.Count)
                ShowSequence();
        }
        else
        {
            sequencePosition = 0;
            progress = 0f;
            ShowFeedback("✕ SECUENCIA REINICIADA", new Color(1f, 0.38f, 0.32f), 0.8f);
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

            if (angle >= 12f && angle <= 135f)
            {
                accumulatedAngle += angle;
                progress = accumulatedAngle / 360f;
                ShowFeedback("GIRO CORRECTO", new Color(0.25f, 1f, 0.42f), 0.35f);
            }
        }

        previousDirection = direction;
    }

    private void CompleteQTE()
    {
        int completedIndex = currentIndex;
        Trace($"QTE {completedIndex + 1} completado correctamente.");
        qteActive = false;
        SetActive(qtePanel, false);
        currentQTE.onSuccess?.Invoke();

        ContinueAtNextVideo();

        if (completedIndex == qtes.Count - 1) onAllQtesCompleted?.Invoke();
    }

    private void ContinueAtNextVideo()
    {
        if (timeline == null)
            return;

        if (currentIndex >= 0 && currentIndex < successVideoTimes.Count)
        {
            timeline.time = successVideoTimes[currentIndex];
            timeline.Evaluate();
            timeline.Play();
            Trace($"Timeline salta a {successVideoTimes[currentIndex]:0.###} s para el siguiente video.");
            return;
        }

        timeline.Resume();
        if (timeline.state != PlayState.Playing)
            timeline.Play();
    }

    private void FailQTE()
    {
        Trace($"QTE {currentIndex + 1} fallado por tiempo.");
        qteActive = false;
        SetActive(qtePanel, false);
        if (timeline != null) timeline.Pause();
        SetActive(gameOverPanel, true);
        EnsureGameOverPresentation();
        GameObject visibleGameOverTitle = GameObject.Find("GameOverText");
        if (visibleGameOverTitle != null && visibleGameOverTitle.TryGetComponent(out TMP_Text visibleText))
            visibleText.text = "QTE FALLIDO";
        gameOverPanel.transform.SetAsLastSibling();
        currentQTE.onFailure?.Invoke();
    }

    private void UpdateBars()
    {
        if (progressBar != null)
            progressBar.fillAmount = Mathf.Clamp01(progress / currentQTE.requiredAmount);

        if (timerBar != null)
            timerBar.fillAmount = Mathf.Clamp01(timeRemaining / currentQTE.timeLimit);

        if (timerContainer == null || leftTimerClose == null || rightTimerClose == null)
            return;

        float elapsed = 1f - Mathf.Clamp01(timeRemaining / currentQTE.timeLimit);
        float closeWidth = timerContainer.rect.width * 0.5f * elapsed;
        leftTimerClose.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, closeWidth);
        rightTimerClose.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, closeWidth);
    }

    private void EnsureClosingTimerVisual()
    {
        if (qtePanel == null)
            return;

        bool validTimer = timerContainer != null &&
                          leftTimerClose != null &&
                          rightTimerClose != null &&
                          leftTimerClose != rightTimerClose &&
                          leftTimerClose != timerContainer &&
                          rightTimerClose != timerContainer &&
                          leftTimerClose.IsChildOf(timerContainer) &&
                          rightTimerClose.IsChildOf(timerContainer);

        if (validTimer)
            return;

        timerContainer = null;
        leftTimerClose = null;
        rightTimerClose = null;

        timerContainer = CreateUiImage("QTE_Timer", qtePanel.transform, new Color(0.86f, 0.15f, 0.18f, 1f));
        timerContainer.anchorMin = timerContainer.anchorMax = new Vector2(0.5f, 0.5f);
        timerContainer.pivot = new Vector2(0.5f, 0.5f);
        timerContainer.anchoredPosition = new Vector2(0f, -330f);
        timerContainer.sizeDelta = new Vector2(700f, 34f);

        leftTimerClose = CreateUiImage("Cierre izquierdo", timerContainer, new Color(0f, 0f, 0f, 0.82f));
        leftTimerClose.anchorMin = new Vector2(0f, 0f);
        leftTimerClose.anchorMax = new Vector2(0f, 1f);
        leftTimerClose.pivot = new Vector2(0f, 0.5f);
        leftTimerClose.anchoredPosition = Vector2.zero;
        leftTimerClose.sizeDelta = new Vector2(0f, 0f);

        rightTimerClose = CreateUiImage("Cierre derecho", timerContainer, new Color(0f, 0f, 0f, 0.82f));
        rightTimerClose.anchorMin = new Vector2(1f, 0f);
        rightTimerClose.anchorMax = new Vector2(1f, 1f);
        rightTimerClose.pivot = new Vector2(1f, 0.5f);
        rightTimerClose.anchoredPosition = Vector2.zero;
        rightTimerClose.sizeDelta = new Vector2(0f, 0f);

        if (timerBar != null)
            timerBar.gameObject.SetActive(false);

        foreach (Slider legacySlider in qtePanel.GetComponentsInChildren<Slider>(true))
            legacySlider.gameObject.SetActive(false);
    }

    private void EnsureQteOverlayCanvas()
    {
        if (qtePanel == null)
            return;

        Canvas overlayCanvas = qtePanel.GetComponent<Canvas>();
        if (overlayCanvas == null)
            overlayCanvas = qtePanel.AddComponent<Canvas>();

        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = 100;
    }

    private void EnsureGameOverPresentation()
    {
        if (gameOverPanel == null)
            return;

        Transform title = gameOverPanel.transform.Find("GameOverText");
        if (title != null)
            gameOverText = title.GetComponent<TMP_Text>();

        if (gameOverText == null)
            gameOverText = gameOverPanel.GetComponentInChildren<TMP_Text>(true);

        if (gameOverText != null)
            gameOverText.text = "QTE FALLIDO";
    }

    private void EnsureFeedbackVisual()
    {
        if (qtePanel == null || feedbackText != null)
            return;

        GameObject item = new GameObject("QTE_Feedback", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        item.transform.SetParent(qtePanel.transform, false);
        feedbackText = item.GetComponent<Text>();
        feedbackText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        feedbackText.fontSize = 26;
        feedbackText.fontStyle = FontStyle.Bold;
        feedbackText.alignment = TextAnchor.MiddleCenter;
        feedbackText.raycastTarget = false;

        RectTransform rect = feedbackText.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -250f);
        rect.sizeDelta = new Vector2(700f, 48f);
        item.SetActive(false);
    }

    private void ShowFeedback(string message, Color color, float duration)
    {
        if (feedbackText == null)
            return;

        if (string.IsNullOrEmpty(message))
        {
            feedbackText.gameObject.SetActive(false);
            return;
        }

        feedbackText.text = message;
        feedbackText.color = color;
        feedbackExpiresAt = Time.unscaledTime + duration;
        feedbackText.gameObject.SetActive(true);
    }

    private static RectTransform CreateUiImage(string objectName, Transform parent, Color color)
    {
        var item = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Image image = item.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private void ShowSequence()
    {
        if (sequencePosition >= sequence.Count) return;
        SetText(sequenceText, $"PULSA AHORA:\n{ButtonName(sequence[sequencePosition])}");
    }

    private void ConfigureSequencePromptLayout()
    {
        if (sequenceText == null)
            return;

        RectTransform prompt = sequenceText.rectTransform;
        prompt.anchorMin = prompt.anchorMax = new Vector2(0.5f, 0.5f);
        prompt.pivot = new Vector2(0.5f, 0.5f);
        prompt.anchoredPosition = new Vector2(0f, -20f);
        prompt.sizeDelta = new Vector2(760f, 120f);
        sequenceText.alignment = TextAnchor.MiddleCenter;
        sequenceText.fontSize = 32;
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
            Vector2 stick = left.sqrMagnitude >= right.sqrMagnitude ? left : right;

            if (stick.sqrMagnitude >= 0.65f * 0.65f)
                return stick;
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
        FaceButton.South => "A (mando)  /  S o ↓",
        FaceButton.East => "B (mando)  /  D o →",
        FaceButton.West => "X (mando)  /  A o ←",
        FaceButton.North => "Y (mando)  /  W o ↑",
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

    private void Trace(string message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugQteFlow)
            Debug.Log($"[QTE] {message}", this);
#endif
    }
}
