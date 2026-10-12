using System;
using System.Collections.Generic;

namespace CheatSystem
{
    public enum CheatKind { Label, Button, Buttons, Input, Toggle }

    /// <summary>
    /// One row of the cheat panel, made through <see cref="CheatManager"/> (Label, Button, Buttons, Input, Toggle).
    /// The fluent calls narrow it down: <c>.When(() => inLevel)</c> shows it only then, <c>.Confirmed()</c> asks for a
    /// second tap, <c>.Numeric()</c> opens the number keyboard.
    /// </summary>
    public sealed class CheatCommand
    {
        public string Tab { get; internal set; }
        public CheatKind Kind { get; internal set; }
        public string Label { get; internal set; }

        /// <summary>Label: the live text, read a few times a second.</summary>
        public Func<string> Text { get; internal set; }

        /// <summary>Button: what it does.</summary>
        public Action Click { get; internal set; }

        /// <summary>Buttons: several small buttons on one row.</summary>
        public IReadOnlyList<(string text, Action click)> Choices { get; internal set; }

        /// <summary>Input: what it does with the typed text, the button's text and the text the field starts with.</summary>
        public Action<string> Submit { get; internal set; }
        public string ButtonText { get; internal set; }
        public Func<string> Default { get; internal set; }

        /// <summary>Toggle: reads and sets the state.</summary>
        public Func<bool> Get { get; internal set; }
        public Action<bool> Set { get; internal set; }

        public Func<bool> Visible { get; private set; }
        public bool NeedsConfirm { get; private set; }
        public bool IsNumeric { get; private set; }

        public bool IsVisible
        {
            get
            {
                try { return Visible == null || Visible(); }
                catch { return false; }
            }
        }

        /// <summary>Shows the row only while <paramref name="visible"/> is true (checked a few times a second).</summary>
        public CheatCommand When(Func<bool> visible) { Visible = visible; return this; }

        /// <summary>Button: the first tap only arms it ("Sure?"), a second tap within 2 s runs it.</summary>
        public CheatCommand Confirmed() { NeedsConfirm = true; return this; }

        /// <summary>Input: numbers only.</summary>
        public CheatCommand Numeric() { IsNumeric = true; return this; }
    }
}
