﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.Components
{
    internal class ComponentKeyLoweringPass : ComponentIntermediateNodePassBase, IRazorOptimizationPass
    {
        // Run after component lowering pass
        public override int Order => 50;

        protected override void ExecuteCore(RazorCodeDocument codeDocument, DocumentIntermediateNode documentNode)
        {
            if (!IsComponentDocument(documentNode))
            {
                return;
            }

            var @namespace = documentNode.FindPrimaryNamespace();
            var @class = documentNode.FindPrimaryClass();
            if (@namespace == null || @class == null)
            {
                // Nothing to do, bail. We can't function without the standard structure.
                return;
            }

            var references = documentNode.FindDescendantReferences<TagHelperPropertyIntermediateNode>();
            for (var i = 0; i < references.Count; i++)
            {
                var reference = references[i];
                var node = (TagHelperPropertyIntermediateNode)reference.Node;

                if (node.TagHelper.IsKeyTagHelper() && node.IsDirectiveAttribute)
                {
                    reference.Replace(RewriteUsage(reference.Parent, node));
                }
            }
        }

        private IntermediateNode RewriteUsage(IntermediateNode parent, TagHelperPropertyIntermediateNode node)
        {
            // If we can't get a nonempty attribute value, do nothing because there will
            // already be a diagnostic for empty values
            var keyValueToken = DetermineKeyValueToken(node);
            if (keyValueToken == null)
            {
                return node;
            }

            return new SetKeyIntermediateNode(keyValueToken);
        }

        private IntermediateToken DetermineKeyValueToken(TagHelperPropertyIntermediateNode attributeNode)
        {
            IntermediateToken foundToken = null;

            if (attributeNode.Children.Count == 1)
            {
                if (attributeNode.Children[0] is IntermediateToken token)
                {
                    foundToken = token;
                }
                else if (attributeNode.Children[0] is CSharpExpressionIntermediateNode csharpNode)
                {
                    if (csharpNode.Children.Count == 1)
                    {
                        foundToken = csharpNode.Children[0] as IntermediateToken;
                    }
                }
            }
            
            return !string.IsNullOrWhiteSpace(foundToken?.Content) ? foundToken : null;
        }
    }
}
