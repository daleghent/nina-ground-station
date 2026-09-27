#region "copyright"

/*
    Copyright (c) 2021-2026 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Utilities;
using NINA.Sequencer.Logic;
using NINA.Sequencer.SequenceItem;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DaleGhent.NINA.GroundStation.Controls {
    public partial class GsExprTextBoxControl : UserControl {
        public GsExprTextBoxControl() {
            InitializeComponent();
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(GsExprTextBoxControl),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(GsExprTextBoxControl),
                new PropertyMetadata(null));

        public static readonly DependencyProperty ProcessedTextProperty =
            DependencyProperty.Register(nameof(ProcessedText), typeof(string), typeof(GsExprTextBoxControl),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty SymbolBrokerProperty =
            DependencyProperty.Register(nameof(SymbolBroker), typeof(ISymbolBroker), typeof(GsExprTextBoxControl),
                new PropertyMetadata(null, OnSymbolContextChanged));

        public static readonly DependencyProperty SequenceContextProperty =
            DependencyProperty.Register(nameof(SequenceContext), typeof(ISequenceItem), typeof(GsExprTextBoxControl),
                new PropertyMetadata(null, OnSymbolContextChanged));

        public static readonly DependencyProperty AcceptsReturnProperty =
            DependencyProperty.Register(nameof(AcceptsReturn), typeof(bool), typeof(GsExprTextBoxControl),
                new PropertyMetadata(false));

        public static readonly DependencyProperty AcceptsTabProperty =
            DependencyProperty.Register(nameof(AcceptsTab), typeof(bool), typeof(GsExprTextBoxControl),
                new PropertyMetadata(false));

        public static readonly DependencyProperty TextAlignmentProperty =
            DependencyProperty.Register(nameof(TextAlignment), typeof(TextAlignment), typeof(GsExprTextBoxControl),
                new PropertyMetadata(TextAlignment.Left));

        public static readonly DependencyProperty TextWrappingProperty =
            DependencyProperty.Register(nameof(TextWrapping), typeof(TextWrapping), typeof(GsExprTextBoxControl),
                new PropertyMetadata(TextWrapping.NoWrap));

        public static readonly DependencyProperty VerticalScrollBarVisibilityProperty =
            DependencyProperty.Register(nameof(VerticalScrollBarVisibility), typeof(ScrollBarVisibility), typeof(GsExprTextBoxControl),
                new PropertyMetadata(ScrollBarVisibility.Auto));

        public static readonly DependencyProperty HorizontalScrollBarVisibilityProperty =
            DependencyProperty.Register(nameof(HorizontalScrollBarVisibility), typeof(ScrollBarVisibility), typeof(GsExprTextBoxControl),
                new PropertyMetadata(ScrollBarVisibility.Auto));

        public string Text {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string Label {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public string ProcessedText {
            get => (string)GetValue(ProcessedTextProperty);
            private set => SetValue(ProcessedTextProperty, value);
        }

        public ISymbolBroker SymbolBroker {
            get => (ISymbolBroker)GetValue(SymbolBrokerProperty);
            set => SetValue(SymbolBrokerProperty, value);
        }

        public ISequenceItem SequenceContext {
            get => (ISequenceItem)GetValue(SequenceContextProperty);
            set => SetValue(SequenceContextProperty, value);
        }

        public bool AcceptsReturn {
            get => (bool)GetValue(AcceptsReturnProperty);
            set => SetValue(AcceptsReturnProperty, value);
        }

        public bool AcceptsTab {
            get => (bool)GetValue(AcceptsTabProperty);
            set => SetValue(AcceptsTabProperty, value);
        }

        public TextAlignment TextAlignment {
            get => (TextAlignment)GetValue(TextAlignmentProperty);
            set => SetValue(TextAlignmentProperty, value);
        }

        public TextWrapping TextWrapping {
            get => (TextWrapping)GetValue(TextWrappingProperty);
            set => SetValue(TextWrappingProperty, value);
        }

        public ScrollBarVisibility VerticalScrollBarVisibility {
            get => (ScrollBarVisibility)GetValue(VerticalScrollBarVisibilityProperty);
            set => SetValue(VerticalScrollBarVisibilityProperty, value);
        }

        public ScrollBarVisibility HorizontalScrollBarVisibility {
            get => (ScrollBarVisibility)GetValue(HorizontalScrollBarVisibilityProperty);
            set => SetValue(HorizontalScrollBarVisibilityProperty, value);
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var control = (GsExprTextBoxControl)d;
            control.UpdateProcessedText();
        }

        private static void OnSymbolContextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var control = (GsExprTextBoxControl)d;

            if (e.NewValue is ISymbolBroker broker) {
                ExpressionUtilities.GlobalSymbolBroker = broker;
            }

            control.UpdateProcessedText();
        }

        private ISymbolBroker EffectiveSymbolBroker {
            get {
                var broker = SymbolBroker ?? (DataContext as ISequenceItem)?.SymbolBroker;

                if (broker != null) {
                    ExpressionUtilities.GlobalSymbolBroker = broker;
                    return broker;
                }

                return ExpressionUtilities.GlobalSymbolBroker;
            }
        }

        private ISequenceItem EffectiveSequenceContext
            => SequenceContext ?? DataContext as ISequenceItem;

        private void UpdateProcessedText() {
            ProcessedText = ExpressionUtilities.Expand(Text, EffectiveSymbolBroker, EffectiveSequenceContext);
        }

        #region Symbol autocompletion

        // Matches the identifier being typed immediately to the left of the caret.
        [GeneratedRegex(@"[A-Za-z_][A-Za-z0-9_\.]*$")]
        private static partial Regex CompletionTokenRegex();

        private bool suppressCompletion;

        private void OnTextBoxTextChanged(object sender, TextChangedEventArgs e) {
            if (suppressCompletion) {
                return;
            }

            ShowCompletions();
        }

        private void OnTextBoxLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) {
            HideCompletions();
        }

        private void OnCompletionListMouseUp(object sender, MouseButtonEventArgs e) {
            if (sender is not ListBox list || list.SelectedIndex < 0) {
                return;
            }

            // The click already moved the selection within the clicked list; clear the other one so
            // that the commit picks up the intended entry.
            SelectInList(list, list.SelectedIndex);
            CommitCompletion();
        }

        private void OnTextBoxPreviewKeyDown(object sender, KeyEventArgs e) {
            if (!PART_CompletionPopup.IsOpen) {
                if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Control) {
                    ShowCompletions(force: true);
                    e.Handled = true;
                }

                return;
            }

            switch (e.Key) {
                case Key.Down:
                    MoveCompletionSelection(1);
                    e.Handled = true;
                    break;

                case Key.Up:
                    MoveCompletionSelection(-1);
                    e.Handled = true;
                    break;

                case Key.Enter:
                case Key.Tab:
                    CommitCompletion();
                    e.Handled = true;
                    break;

                case Key.Escape:
                    HideCompletions();
                    e.Handled = true;
                    break;
            }
        }

        private void MoveCompletionSelection(int delta) {
            var lists = VisibleCompletionLists;

            if (lists.Length == 0) {
                return;
            }

            var total = lists.Sum(l => l.Items.Count);

            // Flatten the visible sections into a single ordered sequence so that moving past the end
            // of one section continues into the next, wrapping around at both ends.
            var flatIndex = 0;
            var offset = 0;

            foreach (var list in lists) {
                if (list.SelectedIndex >= 0) {
                    flatIndex = offset + list.SelectedIndex;
                    break;
                }

                offset += list.Items.Count;
            }

            var target = flatIndex + delta;
            target = target < 0 ? total - 1 : target % total;

            foreach (var list in lists) {
                if (target < list.Items.Count) {
                    SelectInList(list, target);
                    return;
                }

                target -= list.Items.Count;
            }
        }

        private void ShowCompletions(bool force = false) {
            var broker = EffectiveSymbolBroker;

            if (broker == null || !IsCaretInsideExpression(out var prefix)) {
                HideCompletions();
                return;
            }

            if (string.IsNullOrEmpty(prefix) && !force) {
                HideCompletions();
                return;
            }

            var matches = ExpressionUtilities.GetCompletions(broker)
                .Where(i => i.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var symbols = matches.Where(i => i.Kind == CompletionKind.Symbol).ToList();
            var functions = matches.Where(i => i.Kind == CompletionKind.Function).ToList();

            if (symbols.Count == 0 && functions.Count == 0) {
                HideCompletions();
                return;
            }

            PART_SymbolsList.ItemsSource = symbols.Count > 0 ? symbols : null;
            PART_FunctionsList.ItemsSource = functions.Count > 0 ? functions : null;

            SetSectionVisibility(PART_SymbolsHeader, PART_SymbolsList, symbols.Count > 0);
            SetSectionVisibility(PART_FunctionsHeader, PART_FunctionsList, functions.Count > 0);

            SelectInList(symbols.Count > 0 ? PART_SymbolsList : PART_FunctionsList, 0);

            PositionPopupAtCaret();

            PART_CompletionPopup.IsOpen = true;
        }

        /// <summary>
        /// Anchors the completion popup to the caret rather than the bottom edge of the text box.
        /// The rectangle returned by <see cref="TextBoxBase"/> is already expressed in the coordinate
        /// space of the placement target, so it can be used as-is. If the geometry is not yet
        /// available the rectangle is cleared, which falls back to placement against the full control.
        /// </summary>
        private void PositionPopupAtCaret() {
            var text = PART_TextBox.Text ?? string.Empty;
            var caret = Math.Clamp(PART_TextBox.CaretIndex, 0, text.Length);

            var rect = PART_TextBox.GetRectFromCharacterIndex(caret);

            PART_CompletionPopup.PlacementRectangle = IsValidCaretRect(rect) ? rect : Rect.Empty;
        }

        private static bool IsValidCaretRect(Rect rect) {
            if (rect.IsEmpty) {
                return false;
            }

            return !double.IsNaN(rect.X) && !double.IsInfinity(rect.X)
                && !double.IsNaN(rect.Y) && !double.IsInfinity(rect.Y)
                && !double.IsNaN(rect.Height) && !double.IsInfinity(rect.Height);
        }

        private static void SetSectionVisibility(UIElement header, UIElement list, bool visible) {
            var visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            header.Visibility = visibility;
            list.Visibility = visibility;
        }

        /// <summary>
        /// Moves the selection to <paramref name="index"/> of <paramref name="list"/>, clearing the
        /// selection of the other list so that only a single entry is ever highlighted.
        /// </summary>
        private void SelectInList(ListBox list, int index) {
            var other = ReferenceEquals(list, PART_SymbolsList) ? PART_FunctionsList : PART_SymbolsList;
            other.SelectedIndex = -1;

            list.SelectedIndex = index;

            if (list.SelectedItem != null) {
                list.ScrollIntoView(list.SelectedItem);
            }
        }

        private CompletionItem SelectedCompletion
            => PART_SymbolsList.SelectedItem as CompletionItem ?? PART_FunctionsList.SelectedItem as CompletionItem;

        /// <summary>
        /// The visible sections in display order, used for selection traversal across both lists.
        /// </summary>
        private ListBox[] VisibleCompletionLists
            => new[] { PART_SymbolsList, PART_FunctionsList }
                .Where(l => l.Visibility == Visibility.Visible && l.Items.Count > 0)
                .ToArray();

        private void HideCompletions() {
            PART_CompletionPopup.IsOpen = false;
            PART_CompletionPopup.PlacementRectangle = Rect.Empty;

            PART_SymbolsList.SelectedIndex = -1;
            PART_FunctionsList.SelectedIndex = -1;
            PART_SymbolsList.ItemsSource = null;
            PART_FunctionsList.ItemsSource = null;
        }

        /// <summary>
        /// Determines whether the caret sits inside an unterminated <c>{ }</c> expression and, if so,
        /// returns the partial identifier that precedes the caret.
        /// </summary>
        private bool IsCaretInsideExpression(out string prefix) {
            prefix = string.Empty;

            var caret = PART_TextBox.CaretIndex;
            var text = PART_TextBox.Text ?? string.Empty;

            if (caret < 1 || caret > text.Length) {
                return false;
            }

            var head = text[..caret];
            var open = head.LastIndexOf('{');

            if (open < 0 || head.LastIndexOf('}') > open) {
                return false;
            }

            var match = CompletionTokenRegex().Match(head);
            prefix = match.Success ? match.Value : string.Empty;

            return true;
        }

        private void CommitCompletion() {
            if (SelectedCompletion is not CompletionItem selected) {
                HideCompletions();
                return;
            }

            if (!IsCaretInsideExpression(out var prefix)) {
                HideCompletions();
                return;
            }

            var caret = PART_TextBox.CaretIndex;
            var start = caret - prefix.Length;
            var text = PART_TextBox.Text ?? string.Empty;
            var tail = text[caret..];

            // Always close the expression. If the text immediately following the caret already
            // terminates it, skip over that brace instead of inserting a duplicate one.
            var existingBrace = tail.Length > 0 && tail[0] == '}';
            var closingBrace = existingBrace ? string.Empty : "}";

            string insertion;
            int caretOffset;

            if (selected.Kind == CompletionKind.Function) {
                // Functions take arguments, so park the caret between the parentheses.
                insertion = $"{selected.Name}()" + closingBrace;
                caretOffset = selected.Name.Length + 1;
            } else {
                insertion = selected.Name + closingBrace;
                caretOffset = selected.Name.Length + 1;
            }

            suppressCompletion = true;

            try {
                PART_TextBox.Text = string.Concat(text[..start], insertion, tail);
                PART_TextBox.CaretIndex = start + caretOffset;
            } finally {
                suppressCompletion = false;
            }

            HideCompletions();
        }

        #endregion Symbol autocompletion
    }
}