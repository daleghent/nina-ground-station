#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using NINA.Core.Utility;
using NINA.Sequencer.Logic;
using NINA.Sequencer.SequenceItem;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DaleGhent.NINA.GroundStation.Utilities {

    /// <summary>
    /// The category a completion entry belongs to, used to group the autocompletion popup.
    /// </summary>
    internal enum CompletionKind {
        Symbol,
        Function,
    }

    /// <summary>
    /// A single autocompletion candidate along with the category it belongs to.
    /// </summary>
    internal record CompletionItem(string Name, CompletionKind Kind) {

        public override string ToString() => Name;
    }

    /// <summary>
    /// Helpers for working with N.I.N.A. 3.3 symbols (constants and variables) and expressions.
    /// Shared by all Ground Station services so that expression support can be adopted uniformly.
    /// </summary>
    internal static class ExpressionUtilities {
        private const string OpenBracePlaceholder = "\uE000GS_LBRACE\uE000";
        private const string CloseBracePlaceholder = "\uE000GS_RBRACE\uE000";

        /// <summary>
        /// Expands any symbol expressions contained in <paramref name="text"/>. Returns the original
        /// text unaltered if there is no symbol context available or if expansion fails.
        /// </summary>
        internal static string Expand(string text, ISymbolBroker symbolBroker, ISequenceItem sequenceItem) {
            if (string.IsNullOrEmpty(text) || symbolBroker == null) {
                return text;
            }

            try {
                return ExpressionExpander.Expand(text, symbolBroker, sequenceItem);
            } catch (Exception ex) {
                Logger.Warning($"Failed to expand expression: {ex.Message}");
                return text;
            }
        }

        /// <summary>
        /// Expands any symbol expressions contained in <paramref name="text"/>, honouring backslash
        /// escaped braces. A <c>\{</c> or <c>\}</c> sequence is withheld from the expander and emitted
        /// as a literal <c>{</c> or <c>}</c>, allowing payloads that legitimately contain braces (such
        /// as JSON) to pass through untouched.
        /// </summary>
        internal static string ExpandWithEscapes(string text, ISymbolBroker symbolBroker, ISequenceItem sequenceItem) {
            if (string.IsNullOrEmpty(text)) {
                return text;
            }

            if (!text.Contains(@"\{") && !text.Contains(@"\}")) {
                return Expand(text, symbolBroker, sequenceItem);
            }

            var masked = text.Replace(@"\{", OpenBracePlaceholder).Replace(@"\}", CloseBracePlaceholder);
            var expanded = Expand(masked, symbolBroker, sequenceItem);

            return expanded.Replace(OpenBracePlaceholder, "{").Replace(CloseBracePlaceholder, "}");
        }

        /// <summary>
        /// Returns the names of all symbols and functions that are usable at the current point in the
        /// sequence, for use in autocompletion. Constants belonging to a symbol are returned in their
        /// dotted form, eg. <c>MySymbol.SomeConstant</c>.
        /// </summary>
        internal static IList<string> GetCompletionNames(ISymbolBroker symbolBroker) {
            if (symbolBroker == null) {
                return [];
            }

            var names = new List<string>();

            try {
                foreach (var symbol in symbolBroker.GetSymbols()) {
                    if (symbol == null || string.IsNullOrEmpty(symbol.Key)) {
                        continue;
                    }

                    if (symbol.Type == Symbol.SymbolType.SYMBOL_HIDDEN) {
                        continue;
                    }

                    names.Add(symbol.Key);

                    if (symbol.Constants == null) {
                        continue;
                    }

                    foreach (var constant in symbol.Constants) {
                        if (!string.IsNullOrEmpty(constant?.Key)) {
                            names.Add($"{symbol.Key}.{constant.Key}");
                        }
                    }
                }

                foreach (var function in symbolBroker.GetFunctions()) {
                    if (!string.IsNullOrEmpty(function?.Key)) {
                        names.Add(function.Key);
                    }
                }
            } catch (Exception ex) {
                Logger.Warning($"Failed to enumerate symbols: {ex.Message}");
            }

            return [.. names.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase)];
        }

        /// <summary>
        /// Returns all symbols and functions that are usable at the current point in the sequence,
        /// categorized so that the autocompletion popup can present them in separate sections.
        /// Constants belonging to a symbol are returned in their dotted form, eg.
        /// <c>MySymbol.SomeConstant</c>.
        /// </summary>
        internal static IList<CompletionItem> GetCompletions(ISymbolBroker symbolBroker) {
            if (symbolBroker == null) {
                return [];
            }

            var items = new List<CompletionItem>();

            try {
                foreach (var symbol in symbolBroker.GetSymbols()) {
                    if (symbol == null || string.IsNullOrEmpty(symbol.Key)) {
                        continue;
                    }

                    if (symbol.Type == Symbol.SymbolType.SYMBOL_HIDDEN) {
                        continue;
                    }

                    items.Add(new CompletionItem(symbol.Key, CompletionKind.Symbol));

                    if (symbol.Constants == null) {
                        continue;
                    }

                    foreach (var constant in symbol.Constants) {
                        if (!string.IsNullOrEmpty(constant?.Key)) {
                            items.Add(new CompletionItem($"{symbol.Key}.{constant.Key}", CompletionKind.Symbol));
                        }
                    }
                }

                foreach (var function in symbolBroker.GetFunctions()) {
                    if (!string.IsNullOrEmpty(function?.Key)) {
                        items.Add(new CompletionItem(function.Key, CompletionKind.Function));
                    }
                }
            } catch (Exception ex) {
                Logger.Warning($"Failed to enumerate symbols: {ex.Message}");
            }

            return [.. items
                .GroupBy(i => (i.Kind, i.Name), CompletionItemKeyComparer.Instance)
                .Select(g => g.First())
                .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)];
        }

        private sealed class CompletionItemKeyComparer : IEqualityComparer<(CompletionKind Kind, string Name)> {
            internal static readonly CompletionItemKeyComparer Instance = new();

            public bool Equals((CompletionKind Kind, string Name) x, (CompletionKind Kind, string Name) y)
                => x.Kind == y.Kind && StringComparer.OrdinalIgnoreCase.Equals(x.Name, y.Name);

            public int GetHashCode((CompletionKind Kind, string Name) obj)
                => HashCode.Combine(obj.Kind, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name ?? string.Empty));
        }
    }
}
