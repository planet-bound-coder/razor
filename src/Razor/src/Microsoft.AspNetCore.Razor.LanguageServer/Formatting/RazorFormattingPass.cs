﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal class RazorFormattingPass : FormattingPassBase
    {
        private readonly ILogger _logger;

        public RazorFormattingPass(
            RazorDocumentMappingService documentMappingService,
            FilePathNormalizer filePathNormalizer,
            ClientNotifierServiceBase server,
            ILoggerFactory loggerFactory)
            : base(documentMappingService, filePathNormalizer, server)
        {
            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger<RazorFormattingPass>();
        }

        // Run after the C# formatter pass.
        public override int Order => DefaultOrder - 4;

        public override bool IsValidationPass => false;

        public async override Task<FormattingResult> ExecuteAsync(FormattingContext context, FormattingResult result, CancellationToken cancellationToken)
        {
            if (context.IsFormatOnType)
            {
                // We don't want to handle OnTypeFormatting here.
                return result;
            }

            // Apply previous edits if any.
            var originalText = context.SourceText;
            var changedText = originalText;
            var changedContext = context;
            if (result.Edits.Length > 0)
            {
                var changes = result.Edits.Select(e => e.AsTextChange(originalText)).ToArray();
                changedText = changedText.WithChanges(changes);
                changedContext = await context.WithTextAsync(changedText);

                cancellationToken.ThrowIfCancellationRequested();
            }

            // Format the razor bits of the file
            var syntaxTree = changedContext.CodeDocument.GetSyntaxTree();
            var edits = FormatRazor(changedContext, syntaxTree);

            // Compute the final combined set of edits
            var formattingChanges = edits.Select(e => e.AsTextChange(changedText));
            changedText = changedText.WithChanges(formattingChanges);
            var finalChanges = SourceTextDiffer.GetMinimalTextChanges(originalText, changedText, lineDiffOnly: false);
            var finalEdits = finalChanges.Select(f => f.AsTextEdit(originalText)).ToArray();

            return new FormattingResult(finalEdits);
        }

        private static IEnumerable<TextEdit> FormatRazor(FormattingContext context, RazorSyntaxTree syntaxTree)
        {
            var edits = new List<TextEdit>();
            var source = syntaxTree.Source;

            foreach (var node in syntaxTree.Root.DescendantNodes())
            {
                // Disclaimer: CSharpCodeBlockSyntax is used a _lot_ in razor so these methods are probably
                // being overly careful to only try to format syntax forms they care about.
                TryFormatCSharpBlockStructure(context, edits, source, node);
                TryFormatSingleLineDirective(context, edits, source, node);
                TryFormatBlocks(context, edits, source, node);
            }

            return edits;
        }

        private static bool TryFormatBlocks(FormattingContext context, IList<TextEdit> edits, RazorSourceDocument source, SyntaxNode node)
        {
            return TryFormatFunctionsBlock(context, edits, source, node) ||
                TryFormatCSharpExplicitTransition(context, edits, source, node) ||
                TryFormatHtmlInCSharp(context, edits, source, node) ||
                TryFormatComplexCSharpBlock(context, edits, source, node);
        }

        private static bool TryFormatFunctionsBlock(FormattingContext context, IList<TextEdit> edits, RazorSourceDocument source, SyntaxNode node)
        {
            // @functions
            // {
            // }
            if (node is CSharpCodeBlockSyntax directiveCode &&
                directiveCode.Children.Count == 1 && directiveCode.Children.First() is RazorDirectiveSyntax directive &&
                directive.Body is RazorDirectiveBodySyntax directiveBody &&
                directiveBody.Keyword.GetContent().Equals("functions"))
            {
                var cSharpCode = directiveBody.CSharpCode;
                if (!cSharpCode.Children.TryGetOpenBraceNode(out var openBrace) || !cSharpCode.Children.TryGetCloseBraceNode(out var closeBrace))
                {
                    // Don't trust ourselves in an incomplete scenario.
                    return false;
                }
                var code = cSharpCode.Children.PreviousSiblingOrSelf(closeBrace) as CSharpCodeBlockSyntax;

                var openBraceNode = openBrace;
                var codeNode = code!;
                var closeBraceNode = closeBrace;

                return FormatBlock(context, source, openBraceNode, codeNode, closeBraceNode, edits);
            }

            return false;
        }

        private static bool TryFormatCSharpExplicitTransition(FormattingContext context, IList<TextEdit> edits, RazorSourceDocument source, SyntaxNode node)
        {
            // We're looking for a code block like this:
            //
            // @{
            //     var x = 1;
            // }
            if (node is CSharpCodeBlockSyntax expliciteCode &&
                expliciteCode.Children.FirstOrDefault() is CSharpStatementSyntax statement &&
                statement.Body is CSharpStatementBodySyntax csharpStatementBody)
            {
                var openBraceNode = csharpStatementBody.OpenBrace;
                var codeNode = csharpStatementBody.CSharpCode;
                var closeBraceNode = csharpStatementBody.CloseBrace;

                return FormatBlock(context, source, openBraceNode, codeNode, closeBraceNode, edits);
            }

            return false;
        }

        private static bool TryFormatComplexCSharpBlock(FormattingContext context, IList<TextEdit> edits, RazorSourceDocument source, SyntaxNode node)
        {
            // complex situations like
            // @{
            //  void Method(){
            //      @(DateTime.Now)
            //  }
            // }
            if (node is CSharpRazorBlockSyntax csharpRazorBlock &&
                csharpRazorBlock.Parent is CSharpCodeBlockSyntax innerCodeBlock &&
                csharpRazorBlock.Parent.Parent is CSharpCodeBlockSyntax outerCodeBlock)
            {
                var codeNode = csharpRazorBlock;
                var openBraceNode = outerCodeBlock.Children.PreviousSiblingOrSelf(innerCodeBlock);
                var closeBraceNode = outerCodeBlock.Children.NextSiblingOrSelf(innerCodeBlock);

                return FormatBlock(context, source, openBraceNode, codeNode, closeBraceNode, edits);
            }

            return false;
        }

        private static bool TryFormatHtmlInCSharp(FormattingContext context, IList<TextEdit> edits, RazorSourceDocument source, SyntaxNode node)
        {
            // void Method()
            // {
            //     <div></div>
            // }
            if (node is MarkupBlockSyntax markupBlockNode &&
                markupBlockNode.Parent is CSharpCodeBlockSyntax cSharpCodeBlock)
            {
                var openBraceNode = cSharpCodeBlock.Children.PreviousSiblingOrSelf(markupBlockNode);
                var closeBraceNode = cSharpCodeBlock.Children.NextSiblingOrSelf(markupBlockNode);

                return FormatBlock(context, source, openBraceNode, markupBlockNode, closeBraceNode, edits);
            }

            return false;
        }

        private static void TryFormatCSharpBlockStructure(FormattingContext context, List<TextEdit> edits, RazorSourceDocument source, SyntaxNode node)
        {
            // We're looking for a code block like this:
            //
            // @code {
            //    var x = 1;
            // }
            //
            // The nodes will be a grandchild of a RazorDirective (the "@code") and we expect there to be
            // at least three children, being:
            // 1. Optional whitespace
            // 2. The opening brace
            // 3. The C# code
            // 4. The closing brace
            if (node is CSharpCodeBlockSyntax code &&
                node.Parent?.Parent is RazorDirectiveSyntax directive &&
                !directive.ContainsDiagnostics &&
                directive.DirectiveDescriptor?.Kind == DirectiveKind.CodeBlock)
            {
                var children = code.Children;
                if (TryGetLeadingWhitespace(children, out var whitespace))
                {
                    // For whitespace we normalize it differently depending on if its multi-line or not
                    FormatWhitespaceBetweenDirectiveAndBrace(whitespace, directive, edits, source, context);
                }
                else if (children.TryGetOpenBraceToken(out var brace))
                {
                    // If there is no whitespace at all we normalize to a single space
                    var start = brace.GetRange(source).Start;
                    var edit = new TextEdit
                    {
                        Range = new Range(start, start),
                        NewText = " "
                    };
                    edits.Add(edit);
                }
            }

            static bool TryGetLeadingWhitespace(SyntaxList<RazorSyntaxNode> children, [NotNullWhen(true)] out UnclassifiedTextLiteralSyntax? whitespace)
            {
                // If there is whitespace between the directive and the brace, it will be in the first child
                // of the 4 total children
                whitespace = null;
                if (children.Count == 4 &&
                    children[0] is UnclassifiedTextLiteralSyntax literal &&
                    literal.ContainsOnlyWhitespace())
                {
                    whitespace = literal;
                }

                return whitespace != null;
            }
        }

        private static void TryFormatSingleLineDirective(FormattingContext context, List<TextEdit> edits, RazorSourceDocument source, SyntaxNode node)
        {
            // Looking for single line directives like
            //
            // @attribute [Obsolete("old")]
            //
            // The CSharpCodeBlockSyntax covers everything from the end of "attribute" to the end of the line
            if (IsSingleLineDirective(node, out var children) ||
                IsUsingDirective(node, out children))
            {
                // Shrink any block of C# that only has whitespace down to a single space.
                // In the @attribute case above this would only be the whitespace between the directive and code
                // but for @inject its also between the type and the field name.
                foreach (var child in children)
                {
                    if (child.ContainsOnlyWhitespace(includingNewLines: false))
                    {
                        ShrinkToSingleSpace(child, edits, source);
                    }
                }
            }

            static bool IsSingleLineDirective(SyntaxNode node, [NotNullWhen(true)] out SyntaxList<SyntaxNode>? children)
            {
                if (node is CSharpCodeBlockSyntax content &&
                    node.Parent?.Parent is RazorDirectiveSyntax directive &&
                    directive.DirectiveDescriptor?.Kind == DirectiveKind.SingleLine)
                {
                    children = content.Children;
                    return true;
                }

                children = null;
                return false;
            }

            static bool IsUsingDirective(SyntaxNode node, [NotNullWhen(true)] out SyntaxList<SyntaxNode>? children)
            {
                // Using directives are weird, because the directive keyword ("using") is part of the C# statement it represents
                if (node is RazorDirectiveSyntax razorDirective &&
                    razorDirective.DirectiveDescriptor is null &&
                    razorDirective.Body is RazorDirectiveBodySyntax body &&
                    body.Keyword is CSharpStatementLiteralSyntax literal &&
                    literal.LiteralTokens.Count > 0)
                {
                    if (literal.LiteralTokens[0] is { Kind: SyntaxKind.Keyword, Content: "using" })
                    {
                        children = literal.LiteralTokens;
                        return true;
                    }
                }

                children = null;
                return false;
            }
        }

        private static void FormatWhitespaceBetweenDirectiveAndBrace(SyntaxNode node, RazorDirectiveSyntax directive, List<TextEdit> edits, RazorSourceDocument source, FormattingContext context)
        {
            if (node.ContainsOnlyWhitespace(includingNewLines: false))
            {
                ShrinkToSingleSpace(node, edits, source);
            }
            else
            {
                // If there is a newline then we want to have just one newline after the directive
                // and indent the { to match the @
                var edit = new TextEdit
                {
                    Range = node.GetRange(source),
                    NewText = context.NewLineString + context.GetIndentationString(directive.GetLinePositionSpan(source).Start.Character)
                };
                edits.Add(edit);
            }
        }

        private static void ShrinkToSingleSpace(SyntaxNode node, List<TextEdit> edits, RazorSourceDocument source)
        {
            // If there is anything other than one single space then we replace with one space between directive and brace.
            //
            // ie, "@code     {" will become "@code {"
            var edit = new TextEdit
            {
                Range = node.GetRange(source),
                NewText = " "
            };
            edits.Add(edit);
        }

        private static bool FormatBlock(FormattingContext context, RazorSourceDocument source, SyntaxNode openBraceNode, SyntaxNode codeNode, SyntaxNode closeBraceNode, IList<TextEdit> edits)
        {
            var didFormat = false;

            var openBraceRange = openBraceNode.GetRangeWithoutWhitespace(source);
            var codeRange = codeNode.GetRangeWithoutWhitespace(source);
            if (openBraceRange is not null &&
                codeRange is not null &&
                openBraceRange.End.Line == codeRange.Start.Line &&
                !RangeHasBeenModified(edits, codeRange))
            {
                var additionalIndentationLevel = GetAdditionalIndentationLevel(context, openBraceRange, openBraceNode, codeNode);
                var newText = context.NewLineString;
                if (additionalIndentationLevel > 0)
                {
                    newText += context.GetIndentationString(additionalIndentationLevel);
                }

                var edit = new TextEdit
                {
                    NewText = newText,
                    Range = new Range(openBraceRange.End, openBraceRange.End),
                };
                edits.Add(edit);
                didFormat = true;
            }

            var closeBraceRange = closeBraceNode.GetRangeWithoutWhitespace(source);
            if (codeRange is not null &&
                closeBraceRange is not null &&
                codeRange.End.Line == closeBraceRange.Start.Line &&
                !RangeHasBeenModified(edits, codeRange))
            {
                // Add a Newline between the content and the "}" if one doesn't already exist.
                var edit = new TextEdit
                {
                    NewText = context.NewLineString,
                    Range = new Range(codeRange.End, codeRange.End),
                };
                edits.Add(edit);
                didFormat = true;
            }

            return didFormat;

            static bool RangeHasBeenModified(IList<TextEdit> edits, Range range)
            {
                // Because we don't always know what kind of Razor object we're operating on we have to do this to avoid duplicate edits.
                // The other way to accomplish this would be to apply the edits after every node and function, but that's not in scope for my current work.
                var hasBeenModified = edits.Any(e => e.Range.End == range.End);

                return hasBeenModified;
            }

            static int GetAdditionalIndentationLevel(FormattingContext context, Range range, SyntaxNode openBraceNode, SyntaxNode codeNode)
            {
                if (!context.TryGetIndentationLevel(codeNode.Position, out var desiredIndentationLevel))
                {
                    // If for some reason we don't match a particular span use the indentation for the whole line
                    var indentation = context.Indentations[range.Start.Line];
                    desiredIndentationLevel = indentation.HtmlIndentationLevel + indentation.RazorIndentationLevel;
                }
                var desiredIndentationOffset = context.GetIndentationOffsetForLevel(desiredIndentationLevel);
                var currentIndentationOffset = openBraceNode.GetTrailingWhitespaceLength(context) + codeNode.GetLeadingWhitespaceLength(context);

                return desiredIndentationOffset - currentIndentationOffset;
            }
        }
    }
}
