#region "copyright"

/*
    Copyright (c) 2021-2026 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using NINA.Core.Utility;
using NINA.Sequencer;
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
        Token,
    }

    /// <summary>
    /// A single autocompletion candidate along with the category it belongs to. Function entries
    /// additionally carry the argument counts and documentation published by N.I.N.A., which the
    /// popup displays and the insertion logic uses to place the caret.
    /// </summary>
    internal record CompletionItem(string Name, CompletionKind Kind) {

        /// <summary>
        /// The fewest arguments the function accepts. Zero for symbols and tokens.
        /// </summary>
        public int MinArgs { get; init; }

        /// <summary>
        /// The most arguments the function accepts. Zero for symbols and tokens, and for functions
        /// that take no arguments at all.
        /// </summary>
        public int MaxArgs { get; init; }

        public string Description { get; init; }

        public string UsageExample { get; init; }

        /// <summary>
        /// The source the entry originates from, typically the device or provider that publishes the
        /// symbol. Null when N.I.N.A. published none.
        /// </summary>
        public string Category { get; init; }

        /// <summary>
        /// A short human readable summary of the accepted argument count, shown beside the name of a
        /// function. Null for entries that do not take arguments in the first place.
        /// </summary>
        public string ArgumentHint {
            get {
                if (Kind != CompletionKind.Function) {
                    return null;
                }

                if (MaxArgs == 0) {
                    return "()";
                }

                if (MinArgs == MaxArgs) {
                    return MaxArgs == 1 ? "(1 arg)" : $"({MaxArgs} args)";
                }

                return $"({MinArgs}-{MaxArgs} args)";
            }
        }

        /// <summary>
        /// The documentation shown when hovering an entry, or null when N.I.N.A. published none.
        /// </summary>
        public string ToolTipText {
            get {
                var parts = new[] { Description, UsageExample }.Where(p => !string.IsNullOrWhiteSpace(p));

                var text = string.Join(Environment.NewLine + Environment.NewLine, parts);

                return string.IsNullOrEmpty(text) ? null : text;
            }
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Helpers for working with N.I.N.A. 3.3 symbols (constants and variables) and expressions.
    /// Shared by all Ground Station services so that expression support can be adopted uniformly.
    /// </summary>
    internal static class ExpressionUtilities {
        private const string OpenBracePlaceholder = "\uE000GS_LBRACE\uE000";
        private const string CloseBracePlaceholder = "\uE000GS_RBRACE\uE000";

        private static volatile ISymbolBroker globalSymbolBroker;

        /// <summary>
        /// The most recently observed symbol broker. User interface that has no sequence context of
        /// its own, such as the plugin options page, falls back to this so that expression previews
        /// and autocompletion remain available there. May be null until a sequence item that carries
        /// a broker has been displayed.
        /// </summary>
        internal static ISymbolBroker GlobalSymbolBroker {
            get => globalSymbolBroker;
            set => globalSymbolBroker = value;
        }

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
        /// Expands any symbol expressions contained in <paramref name="text"/> using the failed
        /// sequence entity for context. Failure triggers are not themselves sequence items, so the
        /// entity that raised the failure supplies the sequence scope that symbol lookup needs.
        /// </summary>
        internal static string ExpandForEntity(string text, ISymbolBroker symbolBroker, ISequenceEntity sequenceEntity) {
            return Expand(text, symbolBroker, sequenceEntity as ISequenceItem);
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

                    items.Add(new CompletionItem(symbol.Key, CompletionKind.Symbol) {
                        Category = symbol.Category,
                    });

                    if (symbol.Constants == null) {
                        continue;
                    }

                    foreach (var constant in symbol.Constants) {
                        if (!string.IsNullOrEmpty(constant?.Key)) {
                            items.Add(new CompletionItem($"{symbol.Key}.{constant.Key}", CompletionKind.Symbol) {
                                Category = constant.Category ?? symbol.Category,
                            });
                        }
                    }
                }

                foreach (var function in symbolBroker.GetFunctions()) {
                    if (!string.IsNullOrEmpty(function?.Key)) {
                        items.Add(new CompletionItem(function.Key, CompletionKind.Function) {
                            MinArgs = function.MinArgs,
                            MaxArgs = function.MaxArgs,
                            Description = function.Description,
                            UsageExample = function.UsageExample,
                        });
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

        /// <summary>
        /// Returns Ground Station's own $$TOKEN$$ message tokens, for use in autocompletion. Names
        /// are returned bare, without the surrounding <c>$$</c> delimiters, matching how the caret
        /// context is detected in the text box.
        /// </summary>
        /// <param name="includeFailureTokens">
        /// When false, tokens belonging to <see cref="TokenGroup.Failure"/> are omitted. These
        /// tokens only resolve in the message templates used by the Failures To... triggers.
        /// </param>
        internal static IList<CompletionItem> GetTokenCompletions(bool includeFailureTokens = false) {
            return [.. GsTokenRegistry.All
                .Where(t => includeFailureTokens || t.Group != TokenGroup.Failure)
                .Select(t => new CompletionItem(t.Name, CompletionKind.Token))
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
