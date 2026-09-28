#region "copyright"

/*
    Copyright (c) 2021-2026 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using System;

namespace DaleGhent.NINA.GroundStation.Utilities {

    /// <summary>
    /// The equipment or data family a token belongs to. Device families are blanked out wholesale
    /// when the corresponding device is not connected.
    /// </summary>
    internal enum TokenGroup {
        General,
        Target,
        Camera,
        Dome,
        FilterWheel,
        FlatDevice,
        Focuser,
        Mount,
        Rotator,
        Safety,
        Weather,
        Failure,
    }

    /// <summary>
    /// The definition of a single Ground Station <c>$$TOKEN$$</c>. <see cref="Name"/> is the bare
    /// token name without the surrounding <c>$$</c> delimiters.
    /// </summary>
    /// <param name="Name">The token name, eg. <c>TARGET_NAME</c>.</param>
    /// <param name="Group">The family the token belongs to.</param>
    /// <param name="Description">A short human readable explanation of what the token yields.</param>
    /// <param name="Resolve">Produces the token's value for a given resolution context.</param>
    internal sealed record GsToken(string Name, TokenGroup Group, string Description, Func<TokenContext, string> Resolve) {

        /// <summary>
        /// True when a N.I.N.A. 3.3 symbol supersedes this token. The token remains fully supported
        /// for compatibility; <see cref="ReplacementHint"/> names the preferred alternative.
        /// </summary>
        internal bool IsDeprecated { get; init; }

        /// <summary>
        /// The symbol that should be preferred over this token, or a note explaining why no symbol
        /// is equivalent. Null when the token has no symbol counterpart worth mentioning.
        /// </summary>
        internal string ReplacementHint { get; init; }

        /// <summary>
        /// True when the token takes an inline argument and therefore cannot be resolved by simple
        /// name lookup. Dynamic tokens are expanded by their own handler before the name sweep runs,
        /// and <see cref="Resolve"/> is never invoked for them.
        /// </summary>
        internal bool IsDynamic { get; init; }

        /// <summary>
        /// The text inserted by autocompletion in place of the bare token name, used by tokens that
        /// take an argument. Null when the name itself is the whole token.
        /// </summary>
        internal string CompletionTemplate { get; init; }

        /// <summary>
        /// Where the caret is placed, relative to the start of the inserted completion, after this
        /// token has been committed. Null to use the default of placing it past the token.
        /// </summary>
        internal int? CompletionCaretOffset { get; init; }

        /// <summary>
        /// The token as it appears in message text, including the <c>$$</c> delimiters.
        /// </summary>
        internal string Delimited => $"$${Name}$$";
    }
}
