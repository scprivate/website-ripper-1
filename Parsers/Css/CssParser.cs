﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ExCSS;

namespace WebsiteRipper.Parsers.Css
{
    [Parser("text/css")]
    public sealed class CssParser : Parser
    {
        public override string DefaultFile { get { return "style.css"; } }

        StyleSheet _styleSheet;
        Encoding _encoding;

        protected override void Load(string path)
        {
            using (var reader = new StreamReader(path, true))
            {
                var parser = new ExCSS.Parser();
                _styleSheet = parser.Parse(reader.ReadToEnd());
                _encoding = reader.CurrentEncoding;
            }
        }

        protected override IEnumerable<Reference> GetReferences()
        {
            return _styleSheet.ImportDirectives.Select(importRule => (Reference)new ImportRuleReference(this, importRule))
                .Concat(GetPrimitiveTerms(_styleSheet.StyleRules
                    .SelectMany(styleRule => styleRule.Declarations)
                    .Select(declaration => declaration.Term))
                    .Where(primitiveTerm => primitiveTerm.PrimitiveType == UnitType.Uri)
                    .Select(primitiveTerm => (Reference)new PrimitiveTermReference(this, primitiveTerm)));
        }

        static IEnumerable<PrimitiveTerm> GetPrimitiveTerms(IEnumerable<Term> terms)
        {
            foreach (var term in terms)
            {
                var primitiveTerm = term as PrimitiveTerm;
                if (primitiveTerm != null)
                    yield return primitiveTerm;
                else
                {
                    var termList = term as TermList;
                    if (termList == null) continue;
                    foreach (var subTerm in GetPrimitiveTerms(termList)) yield return subTerm;
                }
            }
        }

        protected override void Save(string path)
        {
            using (var writer = new StreamWriter(path, false, _encoding))
            {
                writer.WriteLine(_styleSheet.ToString(true));
            }
        }
    }
}
