﻿#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using System;
using System.Globalization;
using System.Windows.Data;

namespace DaleGhent.NINA.GroundStation.Converters {

    public class EmptyStringToEmptyText : IValueConverter {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return string.IsNullOrEmpty((string)value) ? "< No description >" : value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}