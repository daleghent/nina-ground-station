#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DaleGhent.NINA.GroundStation.Utilities {

    public class SelectTextOnFocus : DependencyObject {

        public static readonly DependencyProperty ActiveProperty = DependencyProperty.RegisterAttached(
            "Active",
            typeof(bool),
            typeof(SelectTextOnFocus),
            new PropertyMetadata(false, ActivePropertyChanged));

        private static void ActivePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is TextBox) {
                TextBox textBox = d as TextBox;
                if ((e.NewValue as bool?).GetValueOrDefault(false)) {
                    textBox.GotKeyboardFocus += OnKeyboardFocusSelectText;
                    textBox.PreviewMouseLeftButtonDown += OnMouseLeftButtonDown;
                } else {
                    textBox.GotKeyboardFocus -= OnKeyboardFocusSelectText;
                    textBox.PreviewMouseLeftButtonDown -= OnMouseLeftButtonDown;
                }
            }
        }

        private static void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            DependencyObject dependencyObject = GetParentFromVisualTree(e.OriginalSource);

            if (dependencyObject == null) {
                return;
            }

            var textBox = (TextBox)dependencyObject;
            if (!textBox.IsKeyboardFocusWithin) {
                textBox.Focus();
                e.Handled = true;
            }
        }

        private static DependencyObject GetParentFromVisualTree(object source) {
            DependencyObject parent = source as UIElement;
            while (parent != null && parent is not TextBox) {
                parent = VisualTreeHelper.GetParent(parent);
            }

            return parent;
        }

        private static void OnKeyboardFocusSelectText(object sender, KeyboardFocusChangedEventArgs e) {
            if (e.OriginalSource is TextBox textBox) {
                textBox.SelectAll();
            }
        }

        [AttachedPropertyBrowsableForChildren(IncludeDescendants = false)]
        [AttachedPropertyBrowsableForType(typeof(TextBox))]
        public static bool GetActive(DependencyObject @object) {
            return (bool)@object.GetValue(ActiveProperty);
        }

        public static void SetActive(DependencyObject @object, bool value) {
            @object.SetValue(ActiveProperty, value);
        }
    }
}