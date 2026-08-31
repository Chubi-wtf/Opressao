using System;
using System.Collections;
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
    [SerializeField] private bool startFirstQteWithScene;
    [SerializeField, Min(0f)] private float firstQteStartDelay = 0.15f;
    [Tooltip("Segundo de Timeline al que salta cada QTE cuando se completa. Deben coincidir con el inicio del siguiente video.")]
    [SerializeField] private List<double> successVideoTimes = new() { 6.216666666666656d, 18.483333333333334d };

    [Header("Interfaz")]
    [SerializeField] private GameObject qtePanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Text titleText;
    [SerializeField] private Text instructionText;
    [SerializeField] private Text sequenceText;
    [SerializeField] private Image progressBar;
    [Tooltip("Barra antigua. Se conserva por compatibilidad, pero el temporizador visual usa los dos cierres de abajo.")]
    [SerializeField] private Image timerBar;
    [SerializeField] private RectTransform timerContainer;
    [SerializeField] private RectTransform leftTimerClose;
    [SerializeField] private RectTransform rightTimerClose;

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

    private void Awake()
    {
        EnsureClosingTimerVisual();
        EnsureQteOverlayCanvas();
    }

    private IEnumerator Start()
    {
        SetActive(qtePanel, false);
        SetActive(gameOverPanel, false);

        // No dependemos de Play On Awake: al evaluar el cero se aplica la
        // Activation Track de la primera imagen y luego Timeline avanza.
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

        // El Signal se ubica al inicio del video que requiere QTE. Al pausar
        // Timeline ese video sigue activo y su VideoPlayer queda en loop,
        // mientras que los tramos sin Signal avanzan normalmente.
        if (timeline != null)
            timeline.Pause();

        SetActive(gameOverPanel, false);
        SetActive(qtePanel, true);

        // CinematicVideos está después de QTEPanel dentro del Canvas y sus
        // RawImages cubrían el texto. Como último hermano, el QTE se dibuja
        // sobre cualquier imagen o video de la cinemática.
        qtePanel.transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();

        if (titleText != null) titleText.text = currentQTE.title;
        PrepareInstructions();
        UpdateBars();
    }

    public bool IsQteActive => qteActive;

    /// <summary>
    /// Lo llaman los videos de Timeline al aparecer. Cada video abre el QTE
    /// siguiente, pero nunca interrumpe uno que ya esté en curso.
    /// </summary>
    public void StartNextQTE()
    {
        if (qteActive || currentIndex >= qtes.Count - 1)
            return;

        StartQTE(currentIndex + 1);
    }

    public void RetryQTE()
    {
        // El botón de derrota debe reiniciar la cinemática completa, no sólo
        // el QTE que acababa de fallar.
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
                break;

            case QTEType.ButtonSequence:
                SetText(instructionText, "Sigue el botón indicado\nMando: A / B / X / Y · Teclado: WASD o flechas");
                ConfigureSequencePromptLayout();
                int length = Mathf.Max(1, Mathf.RoundToInt(currentQTE.requiredAmount));
                for (int i = 0; i < length; i++)
                    sequence.Add((FaceButton)UnityEngine.Random.Range(0, 4));
                ShowSequence();
                break;

            case QTEType.RotateStick:
                SetText(instructionText, "Gira cualquier análogo\nTeclado: recorre WASD o flechas en círculo");
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
        int completedIndex = currentIndex;
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

        // En vez de continuar el hueco que queda en el clip de la imagen,
        // saltamos al comienzo del video asociado a este QTE.
        if (currentIndex >= 0 && currentIndex < successVideoTimes.Count)
        {
            timeline.time = successVideoTimes[currentIndex];
            timeline.Evaluate();
            timeline.Play();
            return;
        }

        timeline.Resume();
        if (timeline.state != PlayState.Playing)
            timeline.Play();
    }

    private void FailQTE()
    {
        qteActive = false;
        SetActive(qtePanel, false);
        if (timeline != null) timeline.Pause();
        SetActive(gameOverPanel, true);
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

        // A medida que se acaba el tiempo, los dos bloques avanzan hacia el centro.
        float elapsed = 1f - Mathf.Clamp01(timeRemaining / currentQTE.timeLimit);
        float closeWidth = timerContainer.rect.width * 0.5f * elapsed;
        leftTimerClose.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, closeWidth);
        rightTimerClose.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, closeWidth);
    }

    private void EnsureClosingTimerVisual()
    {
        if (qtePanel == null || (timerContainer != null && leftTimerClose != null && rightTimerClose != null))
            return;

        // Se crea por código para que la escena existente no necesite prefabs adicionales.
        timerContainer = CreateUiImage("QTE_Timer", qtePanel.transform, new Color(0f, 0f, 0f, 0.82f));
        timerContainer.anchorMin = timerContainer.anchorMax = new Vector2(0.5f, 0.5f);
        timerContainer.pivot = new Vector2(0.5f, 0.5f);
        timerContainer.anchoredPosition = new Vector2(0f, -190f);
        timerContainer.sizeDelta = new Vector2(700f, 34f);

        leftTimerClose = CreateUiImage("Cierre izquierdo", timerContainer, new Color(0.86f, 0.15f, 0.18f, 1f));
        leftTimerClose.anchorMin = new Vector2(0f, 0f);
        leftTimerClose.anchorMax = new Vector2(0f, 1f);
        leftTimerClose.pivot = new Vector2(0f, 0.5f);
        leftTimerClose.anchoredPosition = Vector2.zero;
        leftTimerClose.sizeDelta = new Vector2(0f, 0f);

        rightTimerClose = CreateUiImage("Cierre derecho", timerContainer, new Color(0.86f, 0.15f, 0.18f, 1f));
        rightTimerClose.anchorMin = new Vector2(1f, 0f);
        rightTimerClose.anchorMax = new Vector2(1f, 1f);
        rightTimerClose.pivot = new Vector2(1f, 0.5f);
        rightTimerClose.anchoredPosition = Vector2.zero;
        rightTimerClose.sizeDelta = new Vector2(0f, 0f);

        if (timerBar != null)
            timerBar.gameObject.SetActive(false);
    }

    private void EnsureQteOverlayCanvas()
    {
        if (qtePanel == null)
            return;

        // La capa de videos se activa desde Timeline y puede reconstruirse
        // después del panel. Este Canvas anidado garantiza que las
        // instrucciones queden siempre por delante de los RawImage de video.
        Canvas overlayCanvas = qtePanel.GetComponent<Canvas>();
        if (overlayCanvas == null)
            overlayCanvas = qtePanel.AddComponent<Canvas>();

        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = 100;
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
}
