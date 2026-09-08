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

    static GameplayRegressionChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(RunningKey, false))
                StartSuite();
        };
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);
        SessionState.SetBool(RunningKey, true);
        EditorApplication.EnterPlaymode();
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
        cases.Enqueue(("Pausa repetida conserva tiempo y reanuda Timeline", DoublePause));
        cases.Enqueue(("Secuencia ignora botones mientras está pausada", PausedSequence));
        cases.Enqueue(("Intro no puede empezar debajo del menú de pausa", IntroPause));
        cases.Enqueue(("Pausa y opciones conservan contador del QTE", QteTimer));
        cases.Enqueue(("Respiración conserva su fase durante pausa", Breathing));
        cases.Enqueue(("Fallo y reintento vuelven a emitir el primer signal", RetrySignal));
        cases.Enqueue(("Cinco ciclos de pausa antes del primer signal", RepeatedSignal));
        for (int i = 0; i < 7; i++)
        {
            int index = i;
            cases.Enqueue(($"Signal QTE {i + 1}: pausa justo antes del marcador", () => SignalCase(index)));
        }
        cases.Enqueue(("Recorrido completo sin saltos: todos los signals y créditos", FullFlow));
        cases.Enqueue(("Secuencia: todos los botones correctos completan el QTE", CompleteSequence));
        cases.Enqueue(("Secuencia: botón incorrecto reinicia el progreso", WrongSequence));
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
        deadline = EditorApplication.timeSinceStartup + 240d;
        var driver = new GameObject("Gameplay regression driver");
        GameplayCheckDriver.OnFrame = Tick;
        driver.AddComponent<GameplayCheckDriver>();
        UnityEngine.Object.DontDestroyOnLoad(driver);
    }

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

    private static IEnumerator Reset(bool begin = true)
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

    private static IEnumerator DoublePause()
    {
        yield return Reset(); yield return Wait(0.1f);
        pause.PauseGame(); double at = director.time;
        pause.PauseGame(); yield return Wait(0.12f);
        Check(Math.Abs(director.time - at) < 0.02d, "Timeline advanced while paused");
        pause.ResumeGame(); yield return Wait(0.15f);
        Check(Time.timeScale > 0f && director.time > at + 0.03d, "Second pause erased resume state; Timeline is stuck");
    }

    private static IEnumerator PausedSequence()
    {
        yield return Reset(); manager.StartQTE(0);
        // Locate the actual sequence configuration, independently of its index.
        var qtes = Get<List<QTEConfig>>(manager, "qtes");
        int index = qtes.FindIndex(q => q.type == QTEType.ButtonSequence);
        Check(index >= 0, "No sequence QTE configured"); manager.StartQTE(index);
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

    private static IEnumerator IntroPause()
    {
        yield return Reset(false); pause.PauseGame(); manager.BeginGame(); yield return Wait(0.1f);
        bool started = Get<bool>(manager, "gameStarted");
        Check(!started || !Get<bool>(pause, "isPaused"), "Intro began under pause; resume can restore zero timeScale");
        pause.ResumeGame(); manager.BeginGame(); yield return Wait(0.1f);
        Check(Time.timeScale > 0f, "Game remains frozen after closing intro and pause");
    }

    private static IEnumerator QteTimer()
    {
        yield return Reset(); manager.StartQTE(0); yield return null;
        pause.PauseGame(); float time = Get<float>(manager, "timeRemaining");
        pause.OpenOptions(); yield return Wait(0.15f); pause.CloseOptions();
        Check(Math.Abs(Get<float>(manager, "timeRemaining") - time) < 0.01f, "Paused QTE lost time");
        pause.ResumeGame(); yield return Wait(0.1f);
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

    private static IEnumerator RepeatedSignal()
    {
        yield return Reset(); yield return Cross(markers.First(), 5);
    }

    private static IEnumerator RetrySignal()
    {
        yield return Reset(); var first = markers.First(); yield return Cross(first);
        Set(manager, "timeRemaining", 0.01f); yield return Wait(0.1f);
        Check(Get<GameObject>(manager, "gameOverPanel").activeSelf, "Timeout did not show game over");
        pause.PauseGame(); pause.ResumeGame(); manager.RetryQTE(); yield return null;
        yield return Cross(first);
        Check(manager.IsQteActive, "Retry did not reactivate QTE");
    }

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
        var sequence = (IList)Field(manager, "sequence").GetValue(manager);
        int length = sequence.Count;
        for (int i = 0; i < length; i++)
            yield return KeyPress(new[] { Key.S, Key.D, Key.A, Key.W }[Convert.ToInt32(sequence[i])]);
        Check(!manager.IsQteActive, "Correct sequence did not complete QTE");
        Check(director.state == PlayState.Playing, "Successful input did not resume cinematic");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("gameplay-success.png")); yield return null;
    }

    private static IEnumerator WrongSequence()
    {
        yield return Reset(); manager.StartQTE(0);
        var sequence = (IList)Field(manager, "sequence").GetValue(manager);
        var keys = new[] { Key.S, Key.D, Key.A, Key.W };
        yield return KeyPress(keys[Convert.ToInt32(sequence[0])]);
        Check(Get<int>(manager, "sequencePosition") == 1, "First correct input was not accepted");
        yield return KeyPress(keys[(Convert.ToInt32(sequence[1]) + 1) % 4]);
        Check(Get<int>(manager, "sequencePosition") == 0, "Wrong input did not reset sequence");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("gameplay-wrong-input.png")); yield return null;
    }

    private static IEnumerator PausedRotation()
    {
        yield return Reset();
        int index = Get<List<QTEConfig>>(manager, "qtes").FindIndex(q => q.type == QTEType.RotateLeftStick);
        manager.StartQTE(index);
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

    private static IEnumerator GameOverVideo()
    {
        yield return Reset(); manager.StartQTE(0); Set(manager, "timeRemaining", 0.01f);
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

    private static FieldInfo Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static T Get<T>(object owner, string name) => (T)Field(owner, name).GetValue(owner);
    private static void Set(object owner, string name, object value) => Field(owner, name).SetValue(owner, value);
    private static void Call(object owner, string name) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
