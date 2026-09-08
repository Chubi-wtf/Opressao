using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class QTEManager
{
    [Header("Visual feedback")]
    [SerializeField, Range(0f, 1f)] private float feedbackMotion = 0.65f;
    private readonly Color calmAccent = new Color(0.48f, 0.81f, 0.89f);
    private readonly Color successAccent = new Color(0.55f, 0.94f, 0.76f);
    private readonly Color errorAccent = new Color(1f, 0.48f, 0.39f);
    [SerializeField, HideInInspector] private CanvasGroup presentationGroup;
    [SerializeField, HideInInspector] private Image presentationAccent;
    [SerializeField, HideInInspector] private Image presentationProgress;
    [SerializeField, HideInInspector] private TextMeshProUGUI presentationStatus;
    [SerializeField, HideInInspector] private CanvasGroup outcomeGroup;
    [SerializeField, HideInInspector] private TextMeshProUGUI outcomeText;
    private float presentationStartedAt;
    private float presentationPulseUntil;
    private float outcomeStartedAt = -10f;
    private bool presentationError;
    private Vector3 promptBaseScale = Vector3.one;

    private void EnsurePolishedPresentation()
    {
        if (qtePanel == null) return;
        presentationGroup = qtePanel.GetComponent<CanvasGroup>() ?? qtePanel.AddComponent<CanvasGroup>();
        if (sequenceTmpText != null) promptBaseScale = sequenceTmpText.transform.localScale;

        if (instructionBackdrop != null)
        {
            Image backdrop = instructionBackdrop.GetComponent<Image>();
            backdrop.color = new Color(0.025f, 0.04f, 0.065f, 0.91f);
            Outline border = backdrop.GetComponent<Outline>() ?? backdrop.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(0.48f, 0.81f, 0.89f, 0.2f);
            border.effectDistance = new Vector2(1f, -1f);
            Transform existingAccent = FindDescendant(instructionBackdrop.transform, "QTE_Accent");
            if (existingAccent != null)
                presentationAccent = existingAccent.GetComponent<Image>();
            else
            {
                RectTransform accent = CreateUiImage("QTE_Accent", instructionBackdrop.transform, calmAccent);
                accent.anchorMin = new Vector2(0.12f, 1f);
                accent.anchorMax = new Vector2(0.88f, 1f);
                accent.sizeDelta = new Vector2(0f, 3f);
                presentationAccent = accent.GetComponent<Image>();
            }
        }

        Transform existingTrack = FindDescendant(qtePanel.transform, "QTE_ActionProgress");
        RectTransform track;
        if (existingTrack != null)
            track = existingTrack.GetComponent<RectTransform>();
        else
        {
            track = CreateUiImage("QTE_ActionProgress", qtePanel.transform, new Color(0.08f, 0.13f, 0.17f, 0.95f));
            track.anchorMin = track.anchorMax = new Vector2(0.5f, 0.5f);
            track.anchoredPosition = new Vector2(0f, -188f);
            track.sizeDelta = new Vector2(700f, 6f);
        }
        Transform existingFill = FindDescendant(track, "Fill");
        if (existingFill != null)
            presentationProgress = existingFill.GetComponent<Image>();
        else
        {
            RectTransform fill = CreateUiImage("Fill", track, calmAccent);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            presentationProgress = fill.GetComponent<Image>();
        }
        Transform existingStatus = FindDescendant(qtePanel.transform, "QTE_Status");
        presentationStatus = existingStatus != null ? existingStatus.GetComponent<TextMeshProUGUI>() : CreatePresentationText("QTE_Status", qtePanel.transform, 18f);
        presentationStatus.rectTransform.anchoredPosition = new Vector2(0f, -214f);
        presentationStatus.rectTransform.sizeDelta = new Vector2(1000f, 32f);

        Transform existingOutcome = FindDescendant(qtePanel.transform.parent, "QTE_Outcome");
        GameObject outcome = existingOutcome != null ? existingOutcome.gameObject : new GameObject("QTE_Outcome", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        if (existingOutcome == null) outcome.transform.SetParent(qtePanel.transform.parent, false);
        RectTransform outcomeRect = outcome.GetComponent<RectTransform>();
        outcomeRect.anchorMin = outcomeRect.anchorMax = new Vector2(0.5f, 0.78f);
        outcomeRect.sizeDelta = new Vector2(560f, 68f);
        Canvas canvas = outcome.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 102;
        outcomeGroup = outcome.GetComponent<CanvasGroup>();
        outcomeGroup.blocksRaycasts = false;
        outcomeGroup.interactable = false;
        outcomeGroup.alpha = 0f;
        Transform existingPlate = FindDescendant(outcomeRect, "Backdrop");
        if (existingPlate == null)
        {
            RectTransform plate = CreateUiImage("Backdrop", outcomeRect, new Color(0.025f, 0.04f, 0.065f, 0.94f));
            plate.anchorMin = Vector2.zero;
            plate.anchorMax = Vector2.one;
            plate.offsetMin = plate.offsetMax = Vector2.zero;
        }
        Transform existingResult = FindDescendant(outcomeRect, "Result");
        outcomeText = existingResult != null ? existingResult.GetComponent<TextMeshProUGUI>() : CreatePresentationText("Result", outcomeRect, 26f);
        outcomeText.rectTransform.sizeDelta = new Vector2(540f, 60f);
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null) return null;
        foreach (Transform child in root)
        {
            if (child.name == objectName) return child;
            Transform nested = FindDescendant(child, objectName);
            if (nested != null) return nested;
        }
        return null;
    }

#if UNITY_EDITOR
    public void ValidatePresentationInEditor()
    {
        foreach (string objectName in new[] { "QTE_Instruction_Backdrop", "QTE_Feedback", "QTE_Vignette", "QTE_ActionProgress", "QTE_Status", "QTE_Outcome" })
        {
            int count = 0;
            foreach (Transform item in qtePanel.transform.parent.GetComponentsInChildren<Transform>(true))
                if (item.name == objectName) count++;
            if (count != 1) throw new System.InvalidOperationException($"{objectName}: expected one object, found {count}.");
        }
        if (presentationGroup == null || presentationProgress == null || outcomeGroup == null)
            throw new System.InvalidOperationException("Missing presentation references.");
        Debug.Log("[QTE] Saved presentation validated: references connected and no duplicate objects.");
    }

    public void BakeQtePresentationInEditor()
    {
        EnsureClosingTimerVisual();
        EnsureQteOverlayCanvas();
        EnsureResponsiveCanvas();
        EnsureTextPresentation();
        EnsureHighQualityPrimaryText();
        EnsureInstructionBackdrop();
        EnsureFeedbackVisual();
        EnsureVignetteOverlay();
        EnsurePolishedPresentation();
        if (presentationGroup == null)
            presentationGroup = UnityEditor.Undo.AddComponent<CanvasGroup>(qtePanel);
        UnityEditor.EditorUtility.SetDirty(qtePanel);
        UnityEditor.EditorUtility.SetDirty(presentationGroup);
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

    private static TextMeshProUGUI CreatePresentationText(string label, Transform parent, float size)
    {
        GameObject item = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        item.transform.SetParent(parent, false);
        TextMeshProUGUI text = item.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.8f, 0.88f, 0.92f);
        text.raycastTarget = false;
        return text;
    }

    private void BeginPresentation()
    {
        presentationStartedAt = Time.time;
        presentationPulseUntil = 0f;
        if (presentationGroup != null) presentationGroup.alpha = 0.35f;
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
    }

    private void PulsePresentation(bool success)
    {
        presentationError = !success;
        presentationPulseUntil = Time.time + 0.28f;
    }

    private void ShowOutcome(bool success)
    {
        if (outcomeText == null) return;
        outcomeStartedAt = Time.time;
        outcomeText.text = success ? "ACCIÓN COMPLETADA" : "TIEMPO AGOTADO";
        outcomeText.color = success ? successAccent : errorAccent;
    }

    private void UpdatePresentation()
    {
        // Scaled time freezes the animations together with the pause menu.
        if (outcomeGroup != null)
        {
            float age = Time.time - outcomeStartedAt;
            outcomeGroup.alpha = Mathf.Clamp01(age / 0.12f) * Mathf.Clamp01((1.1f - age) / 0.3f);
        }
        if (!qteActive || currentQTE == null || presentationGroup == null) return;
        float entry = Mathf.Clamp01((Time.time - presentationStartedAt) / 0.2f);
        presentationGroup.alpha = Mathf.Lerp(0.35f, 1f, entry);
        float pulse = Mathf.Clamp01((presentationPulseUntil - Time.time) / 0.28f);
        Color accent = Color.Lerp(calmAccent, presentationError ? errorAccent : successAccent, pulse);
        if (presentationAccent != null) presentationAccent.color = accent;
        if (sequenceTmpText != null)
        {
            float pop = Mathf.Sin(entry * Mathf.PI) * 0.08f + pulse * 0.07f;
            sequenceTmpText.transform.localScale = promptBaseScale * (1f + pop * feedbackMotion);
            Vector2 position = sequenceText.rectTransform.anchoredPosition;
            if (presentationError)
                position.x += Mathf.Sin((Time.time - presentationPulseUntil) * 65f) * 6f * pulse * feedbackMotion;
            sequenceTmpText.rectTransform.anchoredPosition = position;
        }
        float remaining = Mathf.Clamp01(timeRemaining / currentQTE.timeLimit);
        float completed = Mathf.Clamp01(progress / currentQTE.requiredAmount);
        if (presentationProgress != null)
        {
            presentationProgress.rectTransform.anchorMax = new Vector2(completed, 1f);
            presentationProgress.color = accent;
        }
        bool urgent = remaining <= 0.25f;
        if (presentationStatus != null)
        {
            string advance = currentQTE.type == QTEType.ButtonSequence
                ? $"{sequencePosition} / {sequence.Count} pasos"
                : $"{Mathf.RoundToInt(completed * 100f)}% completado";
            
        }
        if (timerContainer != null && timerContainer.TryGetComponent(out Image timerImage))
            timerImage.color = urgent ? errorAccent : calmAccent;
        if (vignetteOverlay != null && vignetteOverlay.TryGetComponent(out Image shade))
            shade.color = Color.Lerp(new Color(0.015f, 0.025f, 0.045f, 0.14f),
                new Color(0.2f, 0.025f, 0.02f, 0.23f), urgent ? 0.7f : (presentationError ? pulse : 0f));
        SetCameraVignette(qteVignetteIntensity + (urgent ? 0.04f : 0f));
    }
}
