using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

// Runs the saved Game scene in Play Mode, using its actual managers and Timeline.
// Test-only time positioning shortens the wait before a marker; notifications are
// delivered by normal Timeline playback, never by calling OnNotify ourselves.
[InitializeOnLoad]
public static class GameplayRegressionChecks
{
    #region referencias

    private const string RunningKey = "Opressao.GameplayChecks.Running";
    private static readonly List<string> results = new();
    private static readonly Queue<(string name, Func<IEnumerator> run)> cases = new();
    private static readonly Stack<IEnumerator> stack = new();
    private static string currentCase;
    private static int failed;
    private static int lastFrame = -1;
    private static double deadline;
    private static Keyboard keyboard;
    private static Gamepad gamepad;
    private static QTEManager manager;
    private static PauseMenuController pause;
    private static PlayableDirector director;
    private static List<(double time, int index)> markers;
    private static bool expectingEndingExit;

    #endregion

    #region inicio

    static GameplayRegressionChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(RunningKey, false))
                StartSuite();
            if (state == PlayModeStateChange.ExitingPlayMode && expectingEndingExit)
            {
                expectingEndingExit = false;
                bool black = manager != null && Get<UnityEngine.UI.Image>(manager, "endingFade").color.a >= 1f;
                results.Add((black ? "PASS: " : "FAIL: ") + "Créditos: fundido completo y salida automática de Play Mode");
                if (!black) failed++;
                Finish();
            }
        };
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);
        SessionState.SetBool(RunningKey, true);
        EditorApplication.EnterPlaymode();
    }

    public static void RunStartupOnly()
    {
        SessionState.SetBool("Opressao.StartupOnly", true);
        Run();
    }

    public static void RunMenuAndEndingOnly()
    {
        SessionState.SetBool("Opressao.MenuAndEndingOnly", true);
        Run();
    }

    private static void StartSuite()
    {
        keyboard = InputSystem.AddDevice<Keyboard>();
        gamepad = InputSystem.AddDevice<Gamepad>();
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        Application.runInBackground = true;
        results.Clear(); cases.Clear(); stack.Clear(); failed = 0;
        if (SessionState.GetBool("Opressao.StartupOnly", false))
        {
            SessionState.SetBool("Opressao.StartupOnly", false);
            cases.Enqueue(("Inicio: primer fotograma, avance y pausa del video", VideoStartup));
            deadline = EditorApplication.timeSinceStartup + 45d;
            var startupDriver = new GameObject("Startup regression driver");
            GameplayCheckDriver.OnFrame = Tick;
            startupDriver.AddComponent<GameplayCheckDriver>();
            UnityEngine.Object.DontDestroyOnLoad(startupDriver);
            return;
        }
        cases.Enqueue(("Pausa repetida conserva tiempo y reanuda Timeline", DoublePause));
        cases.Enqueue(("Pausa durante preparación del video respeta el menú", PauseDuringStartup));
        cases.Enqueue(("Botones reales: opciones, volver y reanudar recuperan el video", ResumeViaButtons));
        cases.Enqueue(("Secuencia ignora botones mientras está pausada", PausedSequence));
        cases.Enqueue(("Inicio directo oculta la introducción y reproduce Timeline", DirectStart));
        cases.Enqueue(("Pausa y opciones conservan contador del QTE", QteTimer));
        cases.Enqueue(("Respiración conserva su fase durante pausa", Breathing));
        cases.Enqueue(("Fallo y reintento vuelven a emitir el primer signal", RetrySignal));
        cases.Enqueue(("Cinco ciclos de pausa antes del primer signal", RepeatedSignal));
        cases.Enqueue(("Signals configurados: pausa justo antes de cada marcador", ConfiguredSignals));
        cases.Enqueue(("Recorrido completo sin saltos: todos los signals y créditos", FullFlow));
        cases.Enqueue(("Secuencia: todos los botones correctos completan el QTE", CompleteSequence));
        cases.Enqueue(("Secuencia: botón incorrecto reinicia el progreso", WrongSequence));
        cases.Enqueue(("Final: L3 y R3 sostenidos; gatillos no cuentan", FinalStickHold));
        cases.Enqueue(("Mando: giro durante pausa no suma progreso", PausedRotation));
        cases.Enqueue(("Mando: Start abre y cierra la pausa", ControllerPause));
        cases.Enqueue(("Teclado: Escape abre y cierra la pausa", KeyboardPause));
        cases.Enqueue(("Debug: F10 no completa el QTE estando en pausa", PausedDebug));
        cases.Enqueue(("Reintentar directamente desde pausa recupera los signals", RetryWhilePaused));
        cases.Enqueue(("Game over: cerrar pausa no vuelve a reproducir el video", GameOverVideo));
        foreach (float speed in new[] { 0.5f, 1f, 2f })
        {
            float value = speed;
            cases.Enqueue(($"Signals con velocidad {speed} y pausa repetida", () => VariableSpeed(value)));
        }
        cases.Enqueue(("Créditos: lectura, fundido y cierre", Ending));
        if (SessionState.GetBool("Opressao.MenuAndEndingOnly", false))
        {
            SessionState.SetBool("Opressao.MenuAndEndingOnly", false);
            cases.Clear();
            cases.Enqueue(("Botones reales: opciones, volver y reanudar recuperan el video", ResumeViaButtons));
            cases.Enqueue(("Créditos: lectura, fundido y cierre", Ending));
        }
        deadline = EditorApplication.timeSinceStartup + 360d;
        var driver = new GameObject("Gameplay regression driver");
        GameplayCheckDriver.OnFrame = Tick;
        driver.AddComponent<GameplayCheckDriver>();
        UnityEngine.Object.DontDestroyOnLoad(driver);
    }

    #endregion

    #region ejecucion

    public static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        InputSystem.Update();
        if (EditorApplication.timeSinceStartup > deadline)
        {
            results.Add("FAIL: suite timeout"); failed++; Finish(); return;
        }
        if (stack.Count == 0)
        {
            if (currentCase != null) { results.Add("PASS: " + currentCase); currentCase = null; }
            if (cases.Count == 0) { Finish(); return; }
            var next = cases.Dequeue(); currentCase = next.name;
            stack.Push(next.run());
        }
        try
        {
            // Nested iterators are advanced on separate frames, as in a coroutine.
            IEnumerator top = stack.Peek();
            if (!top.MoveNext()) stack.Pop();
            else if (top.Current is IEnumerator nested) stack.Push(nested);
        }
        catch (Exception error)
        {
            string message = "FAIL: " + currentCase + " — " + error.GetBaseException().Message;
            results.Add(message); Debug.LogWarning(message + "\n" + error);
            failed++; currentCase = null; stack.Clear();
        }
    }

    private static void Finish()
    {
        SessionState.SetBool(RunningKey, false);
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        if (gamepad != null) InputSystem.RemoveDevice(gamepad);
        Time.timeScale = 1f; AudioListener.pause = false;
        string report = string.Join("\n", results) + $"\nTOTAL: {results.Count} / FAILURES: {failed}\n";
        File.WriteAllText(Path.GetFullPath("gameplay-checks.txt"), report);
        Debug.Log(report);
        EditorApplication.Exit(failed == 0 ? 0 : 1);
    }

    #endregion

    #region utilidades

    private static IEnumerator Reset(bool begin = true, bool waitForStartup = true)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        InputSystem.QueueStateEvent(gamepad, new GamepadState());
        Time.timeScale = 1f; AudioListener.pause = false;
        SceneManager.LoadScene("Game");
        yield return null; yield return null; yield return null;
        manager = UnityEngine.Object.FindFirstObjectByType<QTEManager>();
        Check(manager != null, "Game scene has no QTEManager");
        pause = UnityEngine.Object.FindFirstObjectByType<PauseMenuController>();
        Check(UnityEngine.Object.FindObjectsByType<PauseMenuController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
            "Multiple pause controllers listen to the same gameplay input");
        director = Get<PlayableDirector>(manager, "timeline");
        var receiver = manager.GetComponent<QTESignalReceiver>();
        var signals = Get<List<SignalAsset>>(receiver, "qteSignals");
        var timeline = (TimelineAsset)director.playableAsset;
        markers = timeline.GetOutputTracks().Where(t => !t.mutedInHierarchy)
            .SelectMany(t => t.GetMarkers()).OfType<SignalEmitter>()
            .Where(s => s.asset != null && signals.Contains(s.asset))
            .Select(s => (s.time, signals.IndexOf(s.asset))).OrderBy(s => s.time).ToList();
        if (begin) manager.BeginGame();
        yield return null;
        if (waitForStartup)
            yield return Until(() => !Get<bool>(manager, "cinematicStarting"), "Video startup did not finish", 12f);
    }

    private static IEnumerator Wait(float seconds)
    {
        double until = Time.realtimeSinceStartupAsDouble + seconds;
        while (Time.realtimeSinceStartupAsDouble < until) yield return null;
    }

    private static IEnumerator Until(Func<bool> condition, string error, float timeout = 2f)
    {
        double until = Time.realtimeSinceStartupAsDouble + timeout;
        while (!condition() && Time.realtimeSinceStartupAsDouble < until) yield return null;
        Check(condition(), error);
    }

    #endregion

    #region pausa

    private static IEnumerator DoublePause()
    {
        yield return Reset(); yield return Wait(0.1f);
        pause.PauseGame(); double at = director.time;
        pause.PauseGame(); yield return Wait(0.12f);
        Check(Math.Abs(director.time - at) < 0.02d, "Timeline advanced while paused");
        pause.ResumeGame(); yield return Wait(0.15f);
        Check(Time.timeScale > 0f && director.time > at + 0.03d, "Second pause erased resume state; Timeline is stuck");
    }

    private static IEnumerator PauseDuringStartup()
    {
        yield return Reset(waitForStartup: false);
        // Reproduce preparation completing underneath an open menu deterministically.
        manager.StopAllCoroutines();
        director.Pause(); TimelineVideoPlayerBehaviour.PauseAll();
        Set(manager, "cinematicStarting", true);
        pause.PauseGame();
        manager.StartCoroutine((IEnumerator)manager.GetType().GetMethod("StartTimelineWhenVideoIsReady",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null));
        yield return Wait(0.4f);
        Check(director.state != PlayState.Playing, "Preparation resumed Timeline underneath pause");
        Check(!UnityEngine.Object.FindFirstObjectByType<UnityEngine.Video.VideoPlayer>().isPlaying,
            "Preparation resumed video underneath pause");
        pause.ResumeGame();
        yield return Until(() => director.state == PlayState.Playing, "Startup did not recover after resume", 12f);
    }

    private static IEnumerator ResumeViaButtons()
    {
        yield return Reset();
        var video = UnityEngine.Object.FindFirstObjectByType<UnityEngine.Video.VideoPlayer>();
        for (int i = 0; i < 3; i++)
        {
            pause.PauseGame();
            var panel = Get<GameObject>(pause, "pausePanel");
            var buttons = panel.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            buttons.First(b => b.name == "Boton Opciones").onClick.Invoke();
            Check(Get<GameObject>(pause, "optionsPanel").activeSelf, "Options button did not open options");
            Get<GameObject>(pause, "optionsPanel").GetComponentsInChildren<UnityEngine.UI.Button>(true)
                .First().onClick.Invoke();
            Check(panel.activeSelf, "Back button did not return to pause");
            double at = director.time;
            buttons.First(b => b.name == "Boton Resumen").onClick.Invoke();
            yield return Wait(0.12f);
            Check(!pause.IsPaused && Time.timeScale > 0f, "Resume button left game paused");
            Check(director.time > at && video.isPlaying, "Resume button left Timeline or video frozen");
        }
    }

    private static IEnumerator Ending()
    {
        yield return Reset();
        Set(manager, "creditsDisplayDuration", 0.5f);
        Set(manager, "creditsFadeDuration", 0.6f);
        pause.PauseGame();
        Call(manager, "ShowCredits");
        Check(manager.IsEnding && !pause.IsPaused, "Ending did not dismiss the pause menu");
        pause.PauseGame();
        Check(!pause.IsPaused, "Pause interrupted the ending");
        var fade = Get<UnityEngine.UI.Image>(manager, "endingFade");
        Check(fade != null && fade.color.a == 0f, "Credits were covered before reading time");
        Call(manager, "ShowCredits");
        Check(Get<UnityEngine.UI.Image>(manager, "endingFade") == fade, "Ending started twice");
        // The ending must finish even if another system freezes scaled time.
        Time.timeScale = 0f;
        yield return Wait(0.65f);
        Check(fade.color.a > 0f && fade.color.a < 1f, "Ending did not fade gradually in real time");
        expectingEndingExit = true;
        yield return Wait(2f);
        expectingEndingExit = false;
        Check(false, "Ending never exited Play Mode");
    }

    private static IEnumerator PausedSequence()
    {
        yield return Reset(); manager.StartQTE(0);
        // Locate the actual sequence configuration, independently of its index.
        var qtes = Get<List<QTEConfig>>(manager, "qtes");
        int index = qtes.FindIndex(q => q.type == QTEType.ButtonSequence);
        Check(index >= 0, "No sequence QTE configured"); manager.StartQTE(index);
        yield return WaitForQteInput();
        var sequence = (IList)Field(manager, "sequence").GetValue(manager);
        int button = Convert.ToInt32(sequence[0]);
        Key key = new[] { Key.S, Key.D, Key.A, Key.W }[button];
        pause.PauseGame();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        yield return Wait(0.1f);
        Check(keyboard[key].isPressed, "Virtual keyboard input was not delivered by the test environment");
        Check(Get<int>(manager, "sequencePosition") == 0, "Gameplay accepted a sequence button through the pause menu");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        pause.ResumeGame(); yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return Wait(0.08f);
        Check(Get<int>(manager, "sequencePosition") == 1, "Sequence input did not recover after resume");
    }

    private static IEnumerator DirectStart()
    {
        yield return Reset(false);
        Check(Get<bool>(manager, "gameStarted"), "Game did not start automatically");
        Check(director.state == PlayState.Playing, "Timeline did not start automatically");
        GameObject intro = GameObject.Find("PanelIntroInstrucciones");
        Check(intro == null || !intro.activeInHierarchy, "Intro panel is still visible");
    }

    private static IEnumerator VideoStartup()
    {
        yield return Reset(false);
        var video = UnityEngine.Object.FindFirstObjectByType<UnityEngine.Video.VideoPlayer>();
        Check(video != null, "No active VideoPlayer at startup");
        for (int i = 0; i < 30; i++)
        {
            Debug.Log($"[StartupCheck] timeline={director.time:F3}, video={video.time:F3}, frame={video.frame}, prepared={video.isPrepared}, playing={video.isPlaying}, qte={manager.IsQteActive}, scale={Time.timeScale}");
            Check(video.time < 5d, "Video started at a future timestamp");
            if (manager.IsQteActive)
            {
                Check(video.isPrepared && video.frame >= 0, "QTE paused before the video displayed its first frame");
                double pausedTime = video.time;
                yield return Wait(0.3f);
                Check(Math.Abs(video.time - pausedTime) < 0.15d, "Video kept advancing during the QTE");
                yield break;
            }
            yield return Wait(0.2f);
        }
        Check(false, "Startup did not reach the first QTE within six seconds");
    }

    #endregion

    #region qte

    private static IEnumerator QteTimer()
    {
        yield return Reset(); manager.StartQTE(0); yield return null;
        float fullTime = Get<float>(manager, "timeRemaining");
        var sequence = (IList)Field(manager, "sequence").GetValue(manager);
        yield return KeyPress(new[] { Key.S, Key.D, Key.A, Key.W }[Convert.ToInt32(sequence[0])]);
        yield return Wait(0.3f);
        Check(Get<float>(manager, "timeRemaining") == fullTime, "Reading time consumed the QTE timer");
        Check(Get<int>(manager, "sequencePosition") == 0, "Input was accepted during reading time");
        float reading = Get<float>(manager, "readingTimeRemaining");
        pause.PauseGame(); float time = Get<float>(manager, "timeRemaining");
        pause.OpenOptions(); yield return Wait(0.15f); pause.CloseOptions();
        Check(Math.Abs(Get<float>(manager, "timeRemaining") - time) < 0.01f, "Paused QTE lost time");
        Check(Get<float>(manager, "readingTimeRemaining") == reading, "Pause consumed reading time");
        pause.ResumeGame(); yield return Wait(reading + 0.1f);
        Check(Get<float>(manager, "timeRemaining") < time, "QTE timer did not resume");
        Check(director.state != PlayState.Playing, "Timeline resumed underneath an unfinished QTE");
    }

    private static IEnumerator Breathing()
    {
        yield return Reset();
        int index = Get<List<QTEConfig>>(manager, "qtes").FindIndex(q => q.continuous);
        Check(index >= 0, "No continuous breathing QTE"); manager.StartQTE(index);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q, Key.E)); yield return Wait(0.15f);
        Check(keyboard.qKey.isPressed && keyboard.eKey.isPressed, "Virtual breathing keys were not delivered");
        pause.PauseGame(); float progress = Get<float>(manager, "backgroundBreathingProgress");
        yield return Wait(0.15f);
        Check(Math.Abs(Get<float>(manager, "backgroundBreathingProgress") - progress) < 0.01f, "Breathing advanced while paused");
        pause.ResumeGame(); yield return Wait(0.1f);
        Check(Get<float>(manager, "backgroundBreathingProgress") > progress, "Breathing did not resume");
    }

    #endregion

    #region signals

    private static IEnumerator Cross((double time, int index) marker, int pauses = 1)
    {
        director.time = Math.Max(0d, marker.time - 0.3d); director.Evaluate();
        for (int i = 0; i < pauses; i++)
        {
            pause.PauseGame(); yield return Wait(0.035f); pause.ResumeGame(); yield return null;
        }
        yield return Until(() => Get<int>(manager, "currentIndex") == marker.index,
            $"Signal for QTE {marker.index + 1} did not arrive at {marker.time:0.###}s");
    }

    private static IEnumerator SignalCase(int index)
    {
        yield return Reset();
        Check(markers.Any(m => m.index == index), $"No active Timeline signal is configured for QTE {index + 1}");
        yield return Cross(markers.First(m => m.index == index));
    }

    private static IEnumerator ConfiguredSignals()
    {
        yield return Reset();
        int[] configured = markers.Select(m => m.index).Distinct().ToArray();
        Check(configured.Length > 0, "Timeline has no configured QTE signals");
        foreach (int index in configured) yield return SignalCase(index);
    }

    private static IEnumerator RepeatedSignal()
    {
        yield return Reset(); yield return Cross(markers.First(), 5);
    }

    private static IEnumerator RetrySignal()
    {
        yield return Reset(); var first = markers.First(); yield return Cross(first);
        yield return WaitForQteInput();
        Set(manager, "timeRemaining", 0.01f); yield return Wait(0.1f);
        Check(Get<GameObject>(manager, "gameOverPanel").activeSelf, "Timeout did not show game over");
        pause.PauseGame(); pause.ResumeGame(); manager.RetryQTE(); yield return null;
        yield return Cross(first);
        Check(manager.IsQteActive, "Retry did not reactivate QTE");
    }

    #endregion

    #region recorrido

    private static IEnumerator FullFlow()
    {
        yield return Reset();
        // Debug completion isolates the Timeline transitions from player dexterity.
        // Signals themselves must still be emitted by ordinary playback.
        var visited = new List<int>();
        var qtes = Get<List<QTEConfig>>(manager, "qtes");
        var expected = markers.Where(m => !qtes[m.index].continuous).Select(m => m.index).ToArray();
        double until = Time.realtimeSinceStartupAsDouble + director.duration + 20d;
        while (Time.realtimeSinceStartupAsDouble < until)
        {
            int index = Get<int>(manager, "currentIndex");
            if (manager.IsQteActive)
            {
                visited.Add(index);
                pause.PauseGame(); yield return Wait(0.03f); pause.ResumeGame();
                Call(manager, "CompleteQTE");
            }
            if (Get<GameObject>(manager, "creditsPanel")?.activeSelf == true) break;
            yield return null;
        }
        Check(visited.SequenceEqual(expected), $"Signals mismatch. Expected [{string.Join(",", expected)}], got [{string.Join(",", visited)}]");
        Check(Get<GameObject>(manager, "creditsPanel").activeSelf, "Credits did not appear after final QTE and Timeline ending");
    }

    #endregion

    #region controles

    private static IEnumerator KeyPress(Key key)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        yield return Wait(0.05f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return Wait(0.05f);
    }

    private static IEnumerator CompleteSequence()
    {
        yield return Reset(); manager.StartQTE(0);
        yield return WaitForQteInput();
        var sequence = (IList)Field(manager, "sequence").GetValue(manager);
        int length = sequence.Count;
        for (int i = 0; i < length; i++)
            yield return KeyPress(new[] { Key.S, Key.D, Key.A, Key.W }[Convert.ToInt32(sequence[i])]);
        Check(!manager.IsQteActive, "Correct sequence did not complete QTE");
        Check(director.state == PlayState.Playing, "Successful input did not resume cinematic");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("gameplay-success.png")); yield return null;
    }

    private static IEnumerator FinalStickHold()
    {
        yield return Reset();
        var configs = Get<List<QTEConfig>>(manager, "qtes");
        int index = configs.FindIndex(q => q.type == QTEType.HoldSticks);
        Check(index == configs.Count - 1, "Final QTE must use stick presses");
        Check(configs[index].requiredAmount == 5f, "Final hold must last five seconds");
        Check(configs[1].type == QTEType.HoldButtons, "Common breathing must retain triggers");
        manager.StartQTE(index);
        InputSystem.QueueStateEvent(gamepad, new GamepadState { leftTrigger = 1f, rightTrigger = 1f });
        yield return WaitForQteInput();
        yield return Wait(0.1f);
        Check(Get<float>(manager, "progress") == 0f, "Triggers advanced final hold");
        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.LeftStick)
            .WithButton(GamepadButton.RightStick));
        yield return Wait(0.2f);
        Check(Get<float>(manager, "progress") > 0f, "Stick presses did not advance final hold");
        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.LeftStick));
        yield return Wait(0.1f);
        Check(Get<float>(manager, "progress") == 0f, "Releasing one stick must reset the hold");
        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.LeftStick)
            .WithButton(GamepadButton.RightStick));
        yield return Wait(5.2f);
        Check(!manager.IsQteActive, "Five-second hold did not complete final QTE");
    }

    private static IEnumerator WrongSequence()
    {
        yield return Reset(); manager.StartQTE(0);
        yield return WaitForQteInput();
        var sequence = (IList)Field(manager, "sequence").GetValue(manager);
        var keys = new[] { Key.S, Key.D, Key.A, Key.W };
        yield return KeyPress(keys[Convert.ToInt32(sequence[0])]);
        Check(Get<int>(manager, "sequencePosition") == 1, "First correct input was not accepted");
        yield return KeyPress(keys[(Convert.ToInt32(sequence[1]) + 1) % 4]);
        Check(Get<int>(manager, "sequencePosition") == 0, "Wrong input did not reset sequence");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("gameplay-wrong-input.png")); yield return null;
    }

    #endregion

    #region pausa

    private static IEnumerator PausedRotation()
    {
        yield return Reset();
        int index = Get<List<QTEConfig>>(manager, "qtes").FindIndex(q => q.type == QTEType.RotateLeftStick);
        manager.StartQTE(index);
        yield return WaitForQteInput();
        InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.right });
        yield return Wait(0.06f); pause.PauseGame();
        float before = Get<float>(manager, "progress");
        foreach (Vector2 direction in new[] { Vector2.up, Vector2.left, Vector2.down, Vector2.right })
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = direction });
            yield return Wait(0.06f);
        }
        Check(Math.Abs(Get<float>(manager, "progress") - before) < 0.001f, "Paused stick rotations advanced the door");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("gameplay-paused.png")); yield return null;
    }

    private static IEnumerator ControllerPause()
    {
        yield return Reset();
        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.Start)); yield return Wait(0.06f);
        Check(Get<bool>(pause, "isPaused"), "Start did not pause");
        InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return Wait(0.06f);
        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.Start)); yield return Wait(0.06f);
        Check(!Get<bool>(pause, "isPaused") && Time.timeScale > 0f, "Start did not resume");
    }

    private static IEnumerator KeyboardPause()
    {
        yield return Reset(); yield return KeyPress(Key.Escape);
        Check(Get<bool>(pause, "isPaused"), "Escape did not pause");
        yield return KeyPress(Key.Escape);
        Check(!Get<bool>(pause, "isPaused") && Time.timeScale > 0f, "Escape did not resume");
    }

    private static IEnumerator PausedDebug()
    {
        yield return Reset(); manager.StartQTE(0); pause.PauseGame();
        yield return KeyPress(Key.F10);
        Check(manager.IsQteActive, "Debug key completed QTE underneath pause");
    }

    private static IEnumerator RetryWhilePaused()
    {
        yield return Reset(); manager.StartQTE(0); pause.PauseGame(); manager.RetryQTE();
        yield return null;
        Check(!Get<bool>(pause, "isPaused") && Time.timeScale > 0f, "Retry left game paused");
        yield return Cross(markers.First());
    }

    #endregion

    #region videos

    private static IEnumerator GameOverVideo()
    {
        yield return Reset(); manager.StartQTE(0); Set(manager, "timeRemaining", 0.01f);
        yield return WaitForQteInput();
        yield return Wait(0.1f); pause.PauseGame(); pause.ResumeGame(); yield return null;
        foreach (var player in UnityEngine.Object.FindObjectsByType<TimelineVideoPlayerBehaviour>(FindObjectsSortMode.None))
            Check(!Get<bool>(player, "playWhenPrepared"), "Pause resume requested video playback behind game over");
    }

    private static IEnumerator VariableSpeed(float speed)
    {
        yield return Reset(); Time.timeScale = speed;
        yield return Cross(markers.First(), 3);
        Check(Math.Abs(Time.timeScale - speed) < 0.001f, "Pause changed the previous game speed");
    }

    #endregion

    #region utilidades

    private static IEnumerator WaitForQteInput()
    {
        while (Get<float>(manager, "readingTimeRemaining") > 0f)
            yield return null;
        yield return null;
    }

    private static FieldInfo Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static T Get<T>(object owner, string name) => (T)Field(owner, name).GetValue(owner);
    private static void Set(object owner, string name, object value) => Field(owner, name).SetValue(owner, value);
    private static void Call(object owner, string name) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    #endregion
}
