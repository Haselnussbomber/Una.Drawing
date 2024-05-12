﻿/* Una.Drawing                                                 ____ ___
 *   A declarative drawing library for FFXIV.                 |    |   \____ _____        ____                _
 *                                                            |    |   /    \\__  \      |    \ ___ ___ _ _ _|_|___ ___
 * By Una. Licensed under AGPL-3.                             |    |  |   |  \/ __ \_    |  |  |  _| .'| | | | |   | . |
 * https://github.com/una-xiv/drawing                         |______/|___|  (____  / [] |____/|_| |__,|_____|_|_|_|_  |
 * ----------------------------------------------------------------------- \/ --- \/ ----------------------------- |__*/

using System;
using System.Collections.Generic;

namespace Una.Drawing;

internal static class QuerySelectorParser
{
    internal static List<QuerySelector> Parse(string query)
    {
        List<QuerySelectorToken> tokens = QuerySelectorTokenizer.Tokenize(query);
        List<QuerySelector>      result = [];

        QuerySelector root  = new();
        QuerySelector scope = root;

        result.Add(root);

        foreach (QuerySelectorToken token in tokens) {
            switch (token.Type) {
                case QuerySelectorTokenType.Identifier:
                    if (scope.Identifier != null) {
                        scope = scope.NestedChild = new() { Identifier = token.Value };
                        continue;
                    }

                    scope.Identifier = token.Value;
                    continue;
                case QuerySelectorTokenType.Class: {
                    if (!scope.ClassList.Contains(token.Value)) {
                        scope.ClassList.Add(token.Value);
                    }
                    continue;
                }
                case QuerySelectorTokenType.Child:
                    scope = scope.DirectChild = new();
                    continue;
                case QuerySelectorTokenType.DeepChild:
                    scope = scope.NestedChild = new();
                    break;
                case QuerySelectorTokenType.TagList:
                    scope.TagList.Add(token.Value);
                    break;
                case QuerySelectorTokenType.Separator:
                    scope = new();
                    result.Add(scope);
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Invalid token: [{token.Type}]");
            }
        }

        return result;
    }
}
