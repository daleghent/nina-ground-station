#region "copyright"

/*
    Copyright (c) 2026 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using System.Windows;
using System.Windows.Controls;

namespace DaleGhent.NINA.GroundStation.Controls {

    /// <summary>
    /// A <see cref="TextBox"/> that does not let its content dictate its desired width when it is in
    /// single line mode.
    /// <para>
    /// A stock <see cref="TextBox"/> measures its content with an unbounded width whenever a
    /// horizontal scrollbar is enabled, so long text inflates the desired width and drags the
    /// containing window wider with it. Reporting a zero content width lets the layout system fall
    /// back to <see cref="FrameworkElement.Width"/>/<see cref="FrameworkElement.MinWidth"/> and the
    /// space granted by the parent, which in turn lets the horizontal scrollbar do its job.
    /// </para>
    /// </summary>
    public class GsTextBox : TextBox {

        public GsTextBox() {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            TextWrapping = TextWrapping.NoWrap;

            // WPF keys implicit styles on the exact runtime type, so the theme's TextBox style would
            // otherwise be skipped for this derived type. Using a resource reference rather than a
            // one-off lookup keeps the control in step when the active theme changes. A Style set
            // explicitly in XAML is applied after construction and still takes precedence.
            SetResourceReference(StyleProperty, typeof(TextBox));
        }

        protected override Size MeasureOverride(Size constraint) {
            var desired = base.MeasureOverride(constraint);

            if (AcceptsReturn) {
                return desired;
            }

            // The framework clamps this against Width/MinWidth/MaxWidth, so the box still renders at
            // its intended size - it simply stops asking for room to fit all of its text.
            return new Size(0, desired.Height);
        }
    }
}
