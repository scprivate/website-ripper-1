﻿using HtmlAgilityPack;

namespace WebsiteRipper.Parsers.Html.References
{
    [ReferenceAttribute("src")]
    public sealed class Input : HtmlReference
    {
        public Input(Parser parser, ReferenceKind kind, HtmlNode node, HtmlAttribute attribute) : base(parser, kind, node, attribute) { }
    }
}
