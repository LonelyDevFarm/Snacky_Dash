using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace CheatSystem
{
    /// <summary>
    /// The shared part of the cheat system, the same in every game: the commands, grouped in tabs, and the panel that
    /// shows them (the CheatPanel prefab in Resources, opened by a <see cref="CheatButton"/> or <see cref="Open"/>).
    /// A game adds its own commands once at start, from a [RuntimeInitializeOnLoadMethod] (Snack Dash: SnackDashCheats);
    /// the System tab (FPS, time scale) is built in and always comes last.
    /// Only in the Editor and in Development Builds: anywhere else <see cref="Enabled"/> is false and nothing opens.
    /// </summary>
    public static class CheatManager
    {
        public const string SystemTab = "System";
        public const string PanelResource = "CheatPanel";

        public static bool Enabled =>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            true;
#else
            false;
#endif

        private static readonly List<CheatCommand> _commands = new List<CheatCommand>();
        private static readonly List<string> _tabs = new List<string>();
        private static CheatPanel _panel;
        private static bool _builtIns;

        public static IReadOnlyList<CheatCommand> Commands => _commands;

        /// <summary>Tabs in the order they were first used; System last.</summary>
        public static IReadOnlyList<string> Tabs => _tabs;

        public static bool IsOpen => _panel != null;

        /// <summary>Commands were added or removed (an open panel rebuilds).</summary>
        public static event Action Changed;

        /// <summary>The panel opened / closed (a game can stop its input meanwhile).</summary>
        public static event Action Opened, Closed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _commands.Clear();
            _tabs.Clear();
            _panel = null;
            _builtIns = false;
            Changed = null;
            Opened = null;
            Closed = null;
        }

        // ------------------------------------------------------------------ adding commands

        /// <summary>A line of live text (state, numbers).</summary>
        public static CheatCommand Label(string tab, Func<string> text) =>
            Add(new CheatCommand { Tab = tab, Kind = CheatKind.Label, Label = "", Text = text });

        /// <summary>A full-width button.</summary>
        public static CheatCommand Button(string tab, string label, Action click) =>
            Add(new CheatCommand { Tab = tab, Kind = CheatKind.Button, Label = label, Click = click });

        /// <summary>A label with several small buttons beside it ("Coins: +1K +10K +100K").</summary>
        public static CheatCommand Buttons(string tab, string label, params (string text, Action click)[] choices) =>
            Add(new CheatCommand { Tab = tab, Kind = CheatKind.Buttons, Label = label, Choices = choices });

        /// <summary>A text field and a button that runs <paramref name="submit"/> with the typed text.</summary>
        public static CheatCommand Input(string tab, string label, string buttonText, Action<string> submit, Func<string> defaultText = null) =>
            Add(new CheatCommand { Tab = tab, Kind = CheatKind.Input, Label = label, ButtonText = buttonText, Submit = submit, Default = defaultText });

        /// <summary>An ON / OFF switch.</summary>
        public static CheatCommand Toggle(string tab, string label, Func<bool> get, Action<bool> set) =>
            Add(new CheatCommand { Tab = tab, Kind = CheatKind.Toggle, Label = label, Get = get, Set = set });

        /// <summary>Takes every command of <paramref name="tab"/> out (a game that registers per scene).</summary>
        public static void RemoveTab(string tab)
        {
            _commands.RemoveAll(c => c.Tab == tab);
            _tabs.Remove(tab);
            Changed?.Invoke();
        }

        private static CheatCommand Add(CheatCommand command)
        {
            _commands.Add(command);
            if (!_tabs.Contains(command.Tab))
            {
                int system = _tabs.IndexOf(SystemTab);
                if (system >= 0 && command.Tab != SystemTab) _tabs.Insert(system, command.Tab); // System stays last
                else _tabs.Add(command.Tab);
            }
            Changed?.Invoke();
            return command;
        }

        /// <summary>Runs a command's action and logs it; an exception is logged, not thrown into the UI.</summary>
        public static void Run(string what, Action action)
        {
            Debug.Log("[Cheat] " + what);
            try { action?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        // ------------------------------------------------------------------ the panel

        /// <summary>Opens the panel (<paramref name="prefab"/>, or Resources/CheatPanel). Nothing if it is open already.</summary>
        public static void Open(CheatPanel prefab = null)
        {
            if (!Enabled || IsOpen) return;
            if (prefab == null) prefab = Resources.Load<CheatPanel>(PanelResource);
            if (prefab == null) { Debug.LogWarning("[Cheat] No CheatPanel prefab (Tools/Cheat System/Create Cheat Panel Prefab)."); return; }
            AddBuiltIns();
            EnsureEventSystem();
            _panel = Object.Instantiate(prefab);
            _panel.name = prefab.name;
            Opened?.Invoke();
        }

        public static void Close()
        {
            if (_panel != null) Object.Destroy(_panel.gameObject); // OnDestroy calls PanelDestroyed
        }

        public static void Toggle(CheatPanel prefab = null)
        {
            if (IsOpen) Close();
            else Open(prefab);
        }

        /// <summary>The panel went away (closed, or its scene unloaded).</summary>
        internal static void PanelDestroyed(CheatPanel panel)
        {
            if (panel != _panel) return;
            _panel = null;
            Closed?.Invoke();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || Object.FindObjectOfType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        // ------------------------------------------------------------------ System tab

        private static readonly float[] TimeScales = { 0f, 0.25f, 0.5f, 1f, 2f, 4f };

        private static void AddBuiltIns()
        {
            if (_builtIns) return;
            _builtIns = true;
            Toggle(SystemTab, "FPS counter", () => CheatFps.Shown, on => CheatFps.Shown = on);
            Label(SystemTab, () => $"Time scale: x{Time.timeScale:0.##}");
            var choices = new (string, Action)[TimeScales.Length];
            for (int i = 0; i < TimeScales.Length; i++)
            {
                float scale = TimeScales[i];
                choices[i] = ("x" + scale.ToString("0.##"), () => Time.timeScale = scale);
            }
            Buttons(SystemTab, "Speed", choices);
            Label(SystemTab, () => $"{Screen.width}x{Screen.height}  safe {Screen.safeArea.width:0}x{Screen.safeArea.height:0}  {Application.version}");
            Button(SystemTab, "Measure 5 s (perf)", () => { Close(); CheatPerf.Measure(5f); });
            Label(SystemTab, () => CheatPerf.LastReport);
        }
    }
}
