#region "copyright"

/*
    Copyright (c) 2021-2026 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.MetadataClient;
using NINA.Astrometry.Interfaces;
using NINA.Sequencer;
using System;
using System.Globalization;

namespace DaleGhent.NINA.GroundStation.Utilities {

    /// <summary>
    /// The ambient state that a <see cref="GsToken"/> resolver draws its value from. A single
    /// instance is built per resolution pass so that time-dependent tokens all report the same
    /// instant and so that the deep sky object lookup is performed at most once.
    /// </summary>
    internal sealed class TokenContext {
        private IDeepSkyObject target;
        private bool targetResolved;

        internal TokenContext(ISequenceEntity sequenceItem, IMetadata metadata, FailedItem failedItem, bool urlEncode) {
            SequenceItem = sequenceItem;
            Metadata = metadata;
            FailedItem = failedItem;
            UrlEncode = urlEncode;

            Culture = metadata?.CultureInfo ?? CultureInfo.InvariantCulture;

            Now = DateTime.Now;
            NowUtc = Now.ToUniversalTime();
        }

        internal ISequenceEntity SequenceItem { get; }

        internal IMetadata Metadata { get; }

        internal FailedItem FailedItem { get; }

        internal CultureInfo Culture { get; }

        internal bool UrlEncode { get; }

        internal DateTime Now { get; }

        internal DateTime NowUtc { get; }

        /// <summary>
        /// The deep sky object that the sequence item belongs to, or null when there is no sequence
        /// context or no enclosing target container. Resolved lazily and cached, including the null
        /// result, so that repeated token lookups do not rewalk the sequence tree.
        /// </summary>
        internal IDeepSkyObject Target {
            get {
                if (!targetResolved) {
                    target = SequenceItem?.Parent != null ? Utilities.FindDsoInfo(SequenceItem.Parent) : null;
                    targetResolved = true;
                }

                return target;
            }
        }

        /// <summary>
        /// Applies URL encoding to <paramref name="value"/> when the resolution pass requested it.
        /// </summary>
        internal string Encode(string value) => Utilities.DoUrlEncode(UrlEncode, value);
    }
}
