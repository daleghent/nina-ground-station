#region "copyright"

/*
    Copyright © 2016 - 2026 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Sequencer.Logic;
using NINA.Sequencer.SequenceItem;
using System.Windows;
using System.Windows.Controls;

namespace DaleGhent.GroundStation.Utilities {
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

        public static new readonly DependencyProperty VerticalAlignmentProperty =
            DependencyProperty.Register(nameof(VerticalAlignment), typeof(VerticalAlignment), typeof(GsExprTextBoxControl),
                new PropertyMetadata(VerticalAlignment.Top));

        public static new readonly DependencyProperty HorizontalAlignmentProperty =
            DependencyProperty.Register(nameof(HorizontalAlignment), typeof(HorizontalAlignment), typeof(GsExprTextBoxControl),
                new PropertyMetadata(HorizontalAlignment.Left));

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

        public new VerticalAlignment VerticalAlignment {
            get => (VerticalAlignment)GetValue(VerticalAlignmentProperty);
            set => SetValue(VerticalAlignmentProperty, value);
        }

        public new HorizontalAlignment HorizontalAlignment {
            get => (HorizontalAlignment)GetValue(HorizontalAlignmentProperty);
            set => SetValue(HorizontalAlignmentProperty, value);
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var control = (GsExprTextBoxControl)d;
            control.UpdateProcessedText();
        }

        private void UpdateProcessedText() {
            if (DataContext is not ISequenceItem sequenceItem) {
                ProcessedText = Text;
                return;
            }

            ProcessedText = ExpressionExpander.Expand(Text, sequenceItem.SymbolBroker, sequenceItem.Parent);
        }
    }
}