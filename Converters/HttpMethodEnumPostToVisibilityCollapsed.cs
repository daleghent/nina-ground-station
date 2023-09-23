#region "copyright"

/*
    Copyright 2023, Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using System;
using System.Globalization;
using System.Windows.Data;
using static DaleGhent.NINA.GroundStation.HTTP.HttpClient;

namespace DaleGhent.NINA.GroundStation.Converters {

    public class HttpMethodEnumPostToVisibilityCollapsed : IValueConverter {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            System.Windows.Visibility result = (HttpMethodEnum)value == HttpMethodEnum.POST ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}