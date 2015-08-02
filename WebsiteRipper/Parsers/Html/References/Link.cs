﻿using System;
using System.Linq;
using HtmlAgilityPack;

namespace WebsiteRipper.Parsers.Html.References
{
    [ReferenceAttribute("href", Kind = ReferenceKind.Hyperlink)]
    public sealed class Link : HtmlReference
    {
        static readonly string[] _externalResourceRelationships = { "icon", "pingback", "prefetch", "stylesheet" };
        static readonly string[] _skipRelationships = { "dns-prefetch" };

        static HtmlReferenceArgs FixReferenceArgs(HtmlReferenceArgs htmlReferenceArgs)
        {
            return new HtmlReferenceArgs(htmlReferenceArgs.Parser,
                GetKind(htmlReferenceArgs.Kind, htmlReferenceArgs.Node), htmlReferenceArgs.MimeType, htmlReferenceArgs.Node,
                htmlReferenceArgs.Attribute);
        }

        // TODO: Make this more generic
        static ReferenceKind GetKind(ReferenceKind defaultKind, HtmlNode node)
        {
            const char listSeparatorChar = ' ';
            var relationshipAttribute = node.Attributes["rel"];
            if (relationshipAttribute == null || string.IsNullOrEmpty(relationshipAttribute.Value)) return defaultKind;
            var relationships = relationshipAttribute.Value.Split(listSeparatorChar);
            if (relationships.Any(relationship => _externalResourceRelationships.Any(type => string.Equals(relationship, type, StringComparison.OrdinalIgnoreCase))))
                return ReferenceKind.ExternalResource;
            if (relationships.Any(relationship => _skipRelationships.Any(type => string.Equals(relationship, type, StringComparison.OrdinalIgnoreCase))))
                return ReferenceKind.Skip;
            return defaultKind;
        }

        public Link(HtmlReferenceArgs htmlReferenceArgs) : base(FixReferenceArgs(htmlReferenceArgs)) { }
    }
}
