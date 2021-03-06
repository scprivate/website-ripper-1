﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using WebsiteRipper.Downloaders;
using WebsiteRipper.Extensions;
using WebsiteRipper.Properties;

namespace WebsiteRipper
{
    public sealed class DefaultExtensions : IEnumerable<MimeType>
    {
        static readonly string _rootPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);

        static readonly CultureInfo _language = new CultureInfo("en-US");
        internal static CultureInfo Language { get { return _language; } }

        internal const int Timeout = 30000;

        const string DefaultExtensionsFile = "default.extensions";
        static readonly string _defaultExtensionsPath = Path.Combine(_rootPath, DefaultExtensionsFile);

        static readonly string _extensionRegexClass = string.Format(@"{0}(?:\.{0})?", string.Format(@"[^\s\.,{0}]+", Regex.Escape(string.Join(string.Empty, new[] { '\f', '\n', '\r', '\t', '\v', '\x85' }.Union(Path.GetInvalidFileNameChars())))));
        internal static string ExtensionRegexClass { get { return _extensionRegexClass; } }

        static readonly Lazy<DefaultExtensions> _emptyLazy = new Lazy<DefaultExtensions>(() => new DefaultExtensions(Enumerable.Empty<MimeType>()));
        internal static DefaultExtensions Empty { get { return _emptyLazy.Value; } }

        static DefaultExtensions _all = null;
        static readonly Lazy<DefaultExtensions> _allLazy = new Lazy<DefaultExtensions>(() => Load(_defaultExtensionsPath));
        public static DefaultExtensions All { get { return _all ?? _allLazy.Value; } }

        public static void Update(bool skipIanaMimeTypes)
        {
            if (_ianaLazy.IsValueCreated) throw new InvalidOperationException("Iana mime type file already read.");
            _skipIanaMimeTypes = skipIanaMimeTypes;
            var hardCodedDefaultExtensions = new[]
            {
                new MimeType("application", "xml").SetExtensions(new[] { ".xml", ".xsd", ".xsl", ".xslt" }),
                new MimeType("text", "xml").SetExtensions(new[] { ".xml", ".xsd", ".xsl", ".xslt" }),
                new MimeType("application", "xslt+xml").SetExtensions(new[] { ".xsl", ".xslt" }),
                new MimeType("text", "xsl").SetExtensions(new[] { ".xsl", ".xslt" }),
                new MimeType("application", "x-javascript").SetExtensions(new[] { ".js" })
            };
            var defaultExtensions = Iana.OuterJoin(hardCodedDefaultExtensions, mimeType => mimeType.ToString(), MimeTypeResultSelector, null, StringComparer.OrdinalIgnoreCase)
                .OuterJoin(Apache, mimeType => mimeType.ToString(), MimeTypeResultSelector, null, StringComparer.OrdinalIgnoreCase);
            _all = new DefaultExtensions(defaultExtensions).Save(_defaultExtensionsPath);
        }

        static MimeType MimeTypeResultSelector(MimeType outerMimeType, MimeType innerMimeType)
        {
            if (innerMimeType == null) return outerMimeType;
            if (outerMimeType == null) return innerMimeType;
            if (innerMimeType.Extensions == null) return outerMimeType;
            if (outerMimeType.Extensions == null) return innerMimeType;
            return outerMimeType.SetExtensions(outerMimeType.Extensions.Union(innerMimeType.Extensions, StringComparer.OrdinalIgnoreCase));
        }

        static DefaultExtensions GetDefaultExtensions(string file, string uri, Func<Uri, DateTime?, DefaultExtensions> factory, bool createOnly = false)
        {
            return GetDefaultExtensions(file, new Uri(uri), factory, createOnly);
        }

        static DefaultExtensions GetDefaultExtensions(string file, Uri uri, Func<Uri, DateTime?, DefaultExtensions> factory, bool createOnly = false)
        {
            var path = Path.Combine(_rootPath, file);
            DateTime? lastModified = null;
            if (File.Exists(path))
            {
                var defaultExtensions = Load(path);
                if (createOnly) return defaultExtensions;
                using (var download = Downloader.Create(uri, Timeout, Language.GetPreferredLanguages()))
                {
                    download.SendRequest();
                    if (download.LastModified <= defaultExtensions.LastModified) return defaultExtensions;
                    lastModified = download.LastModified;
                }
            }
            return factory(uri, lastModified).Save(path);
        }

        static bool _skipIanaMimeTypes = false;

        static readonly Lazy<DefaultExtensions> _ianaLazy = new Lazy<DefaultExtensions>(() =>
        {
            const string ianaMimeTypesFile = "iana.mime.types";
            return GetDefaultExtensions(ianaMimeTypesFile, Settings.Default.IanaMediaTypesUri, (uri, lastModified) =>
            {
                var allBackup = _all;
                _all = Empty;
                try
                {
                    return DefaultExtensionsRipper.GetIanaDefaultExtensions(uri);
                }
                finally
                {
                    _all = allBackup;
                }
            }, _skipIanaMimeTypes);
        });
        internal static DefaultExtensions Iana { get { return _ianaLazy.Value; } }

        static readonly Lazy<DefaultExtensions> _apacheLazy = new Lazy<DefaultExtensions>(() =>
        {
            const string mimeTypesFile = "apache.mime.types";
            return GetDefaultExtensions(mimeTypesFile, Settings.Default.ApacheMimeTypesUri, (uri, lastModified) =>
            {
                // Parse mime types from Apache project's web site
                var mimeTypesPath = Path.GetTempFileName();
                try
                {
                    var webClient = new WebClient();
                    webClient.DownloadFile(uri, mimeTypesPath);
                    var apache = lastModified.HasValue ? Load(mimeTypesPath, lastModified.Value) : Load(mimeTypesPath);
                    return apache;
                }
                finally
                {
                    File.Delete(mimeTypesPath);
                }
            });
        });
        internal static DefaultExtensions Apache { get { return _apacheLazy.Value; } }

        readonly Dictionary<string, MimeType> _defaultExtensions;

        public DateTime LastModified { get; private set; }

        DefaultExtensions(IEnumerable<MimeType> defaultExtensions) : this(defaultExtensions, DateTime.Now) { }

        internal DefaultExtensions(IEnumerable<MimeType> defaultExtensions, DateTime lastModified)
        {
            _defaultExtensions = defaultExtensions.ToDictionary(mimeType => mimeType.ToString(), mimeType => mimeType, StringComparer.OrdinalIgnoreCase);
            LastModified = lastModified;
        }

        const string LastModifiedFormat = "# Last modified: {0}";
        static readonly Lazy<Regex> _defaultExtensionsRegexLazy = new Lazy<Regex>(() => new Regex(
            string.Format(@"^(?:{0}|(?:\s*#\s*)?(?<type>\S+)/(?<subtype>\S+)(?:\s+(?<extensions>{1})\b)*)\r?$", string.Format(LastModifiedFormat, @"(?<date>\S+)"), ExtensionRegexClass),
            RegexOptions.Multiline | RegexOptions.Compiled));

        static DefaultExtensions Load(string path)
        {
            return Load(path, DateTime.MinValue);
        }

        static DefaultExtensions Load(string path, DateTime defaultLastModified)
        {
            if (path == null) throw new ArgumentNullException("path");
            using (var streamReader = new StreamReader(path, Encoding.Default))
            {
                var matches = _defaultExtensionsRegexLazy.Value.Matches(streamReader.ReadToEnd());
                var lastModifiedGroup = matches.Cast<Match>().Select(match => match.Groups["date"]).SingleOrDefault(group => group.Success);
                DateTime lastModified;
                if (lastModifiedGroup == null || !DateTime.TryParseExact(lastModifiedGroup.Value, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out lastModified))
                    lastModified = defaultLastModified;
                var defaultExtensions = matches.Cast<Match>()
                    .Where(match => !match.Groups["date"].Success)
                    .Select(match =>
                    {
                        var mimeType = new MimeType(match.Groups["type"].Value.ToLowerInvariant(), match.Groups["subtype"].Value.ToLowerInvariant());
                        var captures = match.Groups["extensions"].Captures;
                        return captures.Count != 0 ?
                            mimeType.SetExtensions(captures.Cast<Capture>().Select(capture => string.Format(".{0}", capture.Value.ToLowerInvariant()))) :
                            mimeType;
                    });
                return new DefaultExtensions(defaultExtensions, lastModified);
            }
        }

        DefaultExtensions Save(string path)
        {
            if (path == null) throw new ArgumentNullException("path");
            const int mimeTypeWidth = 48;
            const int tabulationWidth = 8;
            using (var streamWriter = new StreamWriter(path, false, Encoding.Default))
            {
                streamWriter.WriteLine(LastModifiedFormat, LastModified.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                streamWriter.WriteLine("#");
                streamWriter.WriteLine("# MIME type (lowercased)\t\t\tExtensions");
                streamWriter.WriteLine("# ============================================\t==========");
                foreach (var mimeType in this.OrderBy(mimeType => mimeType.ToString(), StringComparer.OrdinalIgnoreCase))
                {
                    var mimeTypeName = mimeType.ToString();
                    if (mimeType.Extensions != null)
                    {
                        streamWriter.WriteLine("{0}{1}{2}", mimeTypeName,
                            new string('\t', mimeTypeName.Length < mimeTypeWidth ? (mimeTypeWidth - mimeTypeName.Length) / tabulationWidth + 1 : 1),
                            string.Join(" ", mimeType.Extensions.Select(extension => extension.Substring(1))));
                    }
                    else
                        streamWriter.WriteLine("# {0}", mimeTypeName);
                }
            }
            return this;
        }

        public IEnumerator<MimeType> GetEnumerator() { return _defaultExtensions.Values.GetEnumerator(); }

        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

        //public MimeType this[string mimeTypeName]
        //{
        //    get
        //    {
        //        if (mimeTypeName == null) throw new ArgumentNullException("mimeTypeName");
        //        return _defaultExtensions[mimeTypeName];
        //    }
        //}

        public bool TryGetDefaultExtension(string mimeTypeName, out string defaultExtension)
        {
            if (mimeTypeName == null) throw new ArgumentNullException("mimeTypeName");
            MimeType mimeType;
            if (!_defaultExtensions.TryGetValue(mimeTypeName, out mimeType) || mimeType.Extensions == null || !mimeType.Extensions.Any())
            {
                defaultExtension = null;
                return false;
            }
            defaultExtension = mimeType.Extensions.First();
            return true;
        }

        public IEnumerable<string> GetOtherExtensions(string mimeTypeName)
        {
            if (mimeTypeName == null) throw new ArgumentNullException("mimeTypeName");
            MimeType mimeType;
            return _defaultExtensions.TryGetValue(mimeTypeName, out mimeType) && mimeType.Extensions != null && mimeType.Extensions.Any() ?
                mimeType.Extensions.Skip(1) : Enumerable.Empty<string>();
        }
    }
}
