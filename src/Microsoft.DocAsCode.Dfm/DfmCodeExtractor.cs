﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.Dfm
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    using Microsoft.DocAsCode.Common;
    using Microsoft.DocAsCode.Plugins;

    internal class DfmCodeExtractor
    {
        private static readonly string RemoveIndentSpacesRegexString = @"^[ \t]{{1,{0}}}";

        [Obsolete]
        public DfmExtractCodeResult ExtractFencesCode(DfmFencesToken token, string fencesPath)
            => ExtractFencesCode(token, fencesPath, null);

        public DfmExtractCodeResult ExtractFencesCode(DfmFencesToken token, string fencesPath, IDfmFencesBlockPathQueryOption pathQueryOption)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            if (string.IsNullOrEmpty(fencesPath))
            {
                throw new ArgumentNullException(nameof(fencesPath));
            }

            using (new LoggerPhaseScope("Extract Dfm Code From Path"))
            {
                var fencesCode = EnvironmentContext.FileAbstractLayer.ReadAllLines(fencesPath);

                return ExtractFencesCode(token, fencesCode, pathQueryOption);
            }
        }

        public DfmExtractCodeResult ExtractFencesCode(DfmFencesToken token, string[] fencesCode, IDfmFencesBlockPathQueryOption pathQueryOption)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            if (fencesCode == null)
            {
                throw new ArgumentNullException(nameof(fencesCode));
            }

            using (new LoggerPhaseScope("Extract Dfm Code"))
            {
                if (pathQueryOption == null)
                {
                    // Add the full file when no query option is given
                    return new DfmExtractCodeResult
                    {
                        IsSuccessful = true,
                        CodeLines = Dedent(fencesCode),
                    };
                }

                if (!pathQueryOption.ValidateAndPrepare(fencesCode, token))
                {
                    Logger.LogWarning(GenerateErrorMessage(token, pathQueryOption), line: token.SourceInfo.LineNumber.ToString(), code: WarningCodes.Markdown.InvalidCodeSnippet);
                    return new DfmExtractCodeResult { IsSuccessful = false, ErrorMessage = pathQueryOption.ErrorMessage, CodeLines = fencesCode };
                }
                if (!string.IsNullOrEmpty(pathQueryOption.ErrorMessage))
                {
                    Logger.LogWarning(GenerateErrorMessage(token, pathQueryOption), line: token.SourceInfo.LineNumber.ToString());
                }

                var includedLines = pathQueryOption.GetQueryLines(fencesCode).ToList();

                if (!pathQueryOption.ValidateHighlightLinesAndDedentLength(includedLines.Count))
                {
                    Logger.LogWarning(GenerateErrorMessage(token, pathQueryOption), line: token.SourceInfo.LineNumber.ToString());
                }

                return new DfmExtractCodeResult
                {
                    IsSuccessful = true,
                    ErrorMessage = pathQueryOption.ErrorMessage,
                    CodeLines = Dedent(includedLines, pathQueryOption.DedentLength)
                };
            }
        }

        private static string[] Dedent(IEnumerable<string> lines, int? dedentLength = null)
        {
            var length = dedentLength ??
                               (from line in lines
                                where !string.IsNullOrWhiteSpace(line)
                                select (int?)DfmCodeExtractorHelper.GetIndentLength(line)).Min() ?? 0;
            var normalizedLines = (length == 0 ? lines : lines.Select(s => Regex.Replace(s, string.Format(RemoveIndentSpacesRegexString, length), string.Empty))).ToArray();
            return normalizedLines;
        }

        private static string GenerateErrorMessage(DfmFencesToken token, IDfmFencesBlockPathQueryOption option)
        {
            return $"{option.ErrorMessage} when resolving \"{token.SourceInfo.Markdown.Trim()}\"";
        }
    }
}