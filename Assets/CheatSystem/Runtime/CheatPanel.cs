using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CheatSystem
{
    /// <summary>
    /// Script of the CheatPanel prefab: a tab button per tab (Window/Tabs) and the chosen tab's commands as rows in
    /// Window/Scroll/Viewport/Content, each copied from the hidden Templates (Tab, Label, Button, Buttons, Input,
    /// Toggle). Parts are found by name: restyle the prefab freely, but keep the names and parents.
    /// Labels and toggles refresh four times a second; the rows are rebuilt after a command runs (it may show or hide
    /// others), when a row's visibility changes, or when the command list changes.
    /// </summary>
    public class CheatPanel : MonoBehaviour
    {
        private const float RefreshInterval = 0.25f, ConfirmSeconds = 2f;
        private static readonly Color ToggleOn = new Color(0.30f, 0.85f, 0.35f), ToggleOff = new Color(0.65f, 0.65f, 0.65f);

        private static string _lastTab; // opens again on the same tab

        private RectTransform _tabs, _content, _templates;
        private string _tab;
        private readonly List<Action> _refreshers = new List<Action>();
        private readonly List<CheatCommand> _shown = new List<CheatCommand>();
        private float _nextRefresh;
        private bool _dirty = true;

        private void Awake()
        {
            _tabs = (RectTransform)transform.Find("Window/Tabs");
            _content = (RectTransform)transform.Find("Window/Scroll/Viewport/Content");
            _templates = (RectTransform)transform.Find("Templates");
            _templates.gameObject.SetActive(false);
            OnClick(transform.Find("Window/Header/Close"), CheatManager.Close);
            OnClick(transform.Find("Dim"), CheatManager.Close);
            CheatManager.Changed += MarkDirty;
        }

        private void OnDestroy()
        {
            CheatManager.Changed -= MarkDirty;
            CheatManager.PanelDestroyed(this);
        }

        private void MarkDirty() => _dirty = true;

        private void Update()
        {
            if (!_dirty && Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshInterval;
            if (_dirty || VisibilityChanged()) Rebuild();
            foreach (var refresh in _refreshers) refresh();
        }

        private bool VisibilityChanged()
        {
            int shown = 0;
            foreach (var command in CheatManager.Commands)
            {
                if (command.Tab != _tab || !command.IsVisible) continue;
                if (shown >= _shown.Count || _shown[shown] != command) return true;
                shown++;
            }
            return shown != _shown.Count;
        }

        // ------------------------------------------------------------------ building

        private void Rebuild()
        {
            _dirty = false;
            var tabs = CheatManager.Tabs;
            if (_tab == null || !Contains(tabs, _tab)) _tab = Contains(tabs, _lastTab) ? _lastTab : tabs.Count > 0 ? tabs[0] : null;

            Clear(_tabs);
            foreach (var tab in tabs)
            {
                var button = Clone("Tab", _tabs);
                SetText(button, "Text", tab);
                var selected = button.Find("Selected");
                if (selected != null) selected.gameObject.SetActive(tab == _tab);
                string chosen = tab;
                OnClick(button, () => { _tab = _lastTab = chosen; _dirty = true; });
            }

            Clear(_content);
            _refreshers.Clear();
            _shown.Clear();
            foreach (var command in CheatManager.Commands)
            {
                if (command.Tab != _tab || !command.IsVisible) continue;
                _shown.Add(command);
                switch (command.Kind)
                {
                    case CheatKind.Label: BuildLabel(command); break;
                    case CheatKind.Button: BuildButton(command); break;
                    case CheatKind.Buttons: BuildButtons(command); break;
                    case CheatKind.Input: BuildInput(command); break;
                    case CheatKind.Toggle: BuildToggle(command); break;
                }
            }
        }

        private void BuildLabel(CheatCommand command)
        {
            var text = Clone("Label", _content).GetComponentInChildren<TMP_Text>();
            _refreshers.Add(() => text.text = Safe(command.Text));
        }

        private void BuildButton(CheatCommand command)
        {
            var row = Clone("Button", _content);
            var text = row.GetComponentInChildren<TMP_Text>();
            text.text = command.Label;
            float armedUntil = 0f;
            OnClick(row, () =>
            {
                if (command.NeedsConfirm && Time.unscaledTime > armedUntil)
                {
                    armedUntil = Time.unscaledTime + ConfirmSeconds;
                    text.text = "Sure? Tap again: " + command.Label;
                    return;
                }
                armedUntil = 0f;
                text.text = command.Label;
                Execute(command.Label, command.Click);
            });
            if (command.NeedsConfirm) _refreshers.Add(() => { if (armedUntil > 0f && Time.unscaledTime > armedUntil) { armedUntil = 0f; text.text = command.Label; } });
        }

        private void BuildButtons(CheatCommand command)
        {
            var row = Clone("Buttons", _content);
            SetLabel(row, command.Label);
            var line = row.Find("Row");
            var template = line.GetChild(0);
            foreach (var (label, click) in command.Choices)
            {
                var button = Instantiate(template.gameObject, line).transform;
                button.name = label;
                button.GetComponentInChildren<TMP_Text>().text = label;
                OnClick(button, () => Execute($"{command.Label} {label}", click));
            }
            Destroy(template.gameObject);
        }

        private void BuildInput(CheatCommand command)
        {
            var row = Clone("Input", _content);
            SetLabel(row, command.Label);
            var field = row.Find("Field").GetComponent<TMP_InputField>();
            field.contentType = command.IsNumeric ? TMP_InputField.ContentType.IntegerNumber : TMP_InputField.ContentType.Standard;
            field.text = Safe(command.Default);
            var go = row.Find("Go");
            go.GetComponentInChildren<TMP_Text>().text = command.ButtonText;
            OnClick(go, () => Execute($"{command.Label}: {field.text}", () => command.Submit(field.text)));
        }

        private void BuildToggle(CheatCommand command)
        {
            var row = Clone("Toggle", _content);
            SetLabel(row, command.Label);
            var state = row.Find("State").GetComponent<TMP_Text>();
            _refreshers.Add(() =>
            {
                bool on = SafeBool(command.Get);
                state.text = on ? "ON" : "OFF";
                state.color = on ? ToggleOn : ToggleOff;
            });
            OnClick(row, () => Execute(command.Label, () => command.Set(!SafeBool(command.Get))));
        }

        /// <summary>Runs a command, then rebuilds (it may have changed what shows).</summary>
        private void Execute(string what, Action action)
        {
            CheatManager.Run(what, action);
            _dirty = true;
        }

        // ------------------------------------------------------------------ helpers

        private RectTransform Clone(string template, Transform parent)
        {
            var source = _templates.Find(template);
            var row = Instantiate(source.gameObject, parent);
            row.name = template;
            row.SetActive(true);
            return (RectTransform)row.transform;
        }

        private static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private static void SetLabel(Transform row, string label)
        {
            var part = row.Find("Label");
            if (part == null) return;
            part.gameObject.SetActive(!string.IsNullOrEmpty(label));
            part.GetComponent<TMP_Text>().text = label;
        }

        private static void SetText(Transform row, string path, string text)
        {
            var part = row.Find(path);
            if (part != null) part.GetComponent<TMP_Text>().text = text;
        }

        private static void OnClick(Transform target, Action action)
        {
            if (target == null) return;
            var button = target.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(() => action());
        }

        private static bool Contains(IReadOnlyList<string> list, string value)
        {
            if (value == null) return false;
            foreach (var item in list) if (item == value) return true;
            return false;
        }

        private static string Safe(Func<string> text)
        {
            try { return text?.Invoke() ?? ""; }
            catch (Exception e) { return "(" + e.GetType().Name + ")"; }
        }

        private static bool SafeBool(Func<bool> get)
        {
            try { return get != null && get(); }
            catch { return false; }
        }
    }
}
