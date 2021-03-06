﻿using System.Collections.Generic;
using System.Linq;
using System.Xml;
using WebsiteRipper.Helpers;

namespace WebsiteRipper.Parsers.Xml.XsiReferences
{
    [ReferenceElement(Any = true, Namespace = XmlParser.XsiNamespace, QualifiedAttributes = true)]
    [ReferenceAttribute("schemaLocation", ParserType = typeof(SchemaLocationParser))]
    [ReferenceAttribute("noNamespaceSchemaLocation")]
    public sealed class Any : XmlReference
    {
        sealed class SchemaLocationParser : ReferenceValueParser
        {
            public override IEnumerable<string> GetUriStrings(string value)
            {
                // "schemaLocation" attribute contains space-separated pairs of "namespaceUri schemaUri", only "schemaUri" values are kept here.
                return Helper.SplitSpaceSeparatedTokens(value).Where((_, i) => i % 2 == 1);
            }
        }

        public Any(ReferenceArgs<XmlElement, XmlAttribute> referenceArgs) : base(referenceArgs) { }
    }
}
