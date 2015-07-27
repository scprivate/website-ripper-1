﻿using HtmlAgilityPack;

namespace WebsiteRipper.Parsers.Html.References
{
    [HtmlReference("src")]
    //[HtmlReference("srcset")] // TODO: Comma-separated list indicating a set of possible images
    public sealed class Source : HtmlReference
    {
        public Source(Parser parser, ReferenceKind kind, HtmlNode node, HtmlAttribute attribute) : base(parser, kind, node, attribute) { }
    }
}
