﻿using System;

namespace WebsiteRipper.Parsers
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public sealed class ParserAttribute : Attribute
    {
        public string MimeType { get; private set; }

        public ParserAttribute(string mimeType)
        {
            if (string.IsNullOrEmpty(mimeType)) throw new ArgumentNullException("mimeType");
            MimeType = mimeType;
        }
    }
}
