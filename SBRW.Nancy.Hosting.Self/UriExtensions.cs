using System;
using System.Collections.Generic;
using System.Text;

namespace SBRW.Nancy.Hosting.Self
{
    /// <summary>
    /// Extension methods for working with <see cref="Uri"/> instances.
    /// </summary>
    public static class UriExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool IsCaseInsensitiveBaseOf(this Uri source, Uri value)
        {
            UriComponents uriComponents = source.Host == "localhost" ? (UriComponents.Port | UriComponents.Scheme) : (UriComponents.HostAndPort | UriComponents.Scheme);
            if (Uri.Compare(source, value, uriComponents, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) != 0)
            {
                return false;
            }

            string[] sourceSegments = source.Segments;
            string[] valueSegments = value.Segments;

            return sourceSegments.ZipCompare(valueSegments, (s1, s2) => s1.Length == 0 || SegmentEquals(s1, s2));
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="appBaseUri"></param>
        /// <param name="fullUri"></param>
        /// <returns></returns>
        public static string MakeAppLocalPath(this Uri appBaseUri, Uri fullUri)
        {
            return string.Concat("/", appBaseUri.Segments.ZipFill(fullUri.Segments, (x, y) => x != null && SegmentEquals(x, y) ? null : y).Join());
        }

        private static string AppendSlashIfNeeded(string segment)
        {
            if (!segment.EndsWith("/"))
            {
                segment = string.Concat(segment, "/");
            }

            return segment;
        }

        private static bool SegmentEquals(string segment1, string segment2)
        {
            return String.Equals(AppendSlashIfNeeded(segment1), AppendSlashIfNeeded(segment2), StringComparison.OrdinalIgnoreCase);
        }

        private static bool ZipCompare(this IEnumerable<string> source1, IEnumerable<string> source2, Func<string, string, bool> comparison)
        {
            using (var enumerator1 = source1.GetEnumerator())
            {
                using (var enumerator2 = source2.GetEnumerator())
                {
                    bool has1 = enumerator1.MoveNext();
                    bool has2 = enumerator2.MoveNext();

                    while (has1 || has2)
                    {
                        string current1 = has1 ? enumerator1.Current : "";
                        string current2 = has2 ? enumerator2.Current : "";

                        if (!comparison(current1, current2))
                        {
                            return false;
                        }

                        if (has1)
                        {
                            has1 = enumerator1.MoveNext();
                        }

                        if (has2)
                        {
                            has2 = enumerator2.MoveNext();
                        }
                    }

                }
            }

            return true;
        }

        private static IEnumerable<string> ZipFill(this IEnumerable<string> source1, IEnumerable<string> source2, Func<string, string, string> selector)
        {
            using (var enumerator1 = source1.GetEnumerator())
            {
                using (var enumerator2 = source2.GetEnumerator())
                {
                    bool has1 = enumerator1.MoveNext();
                    bool has2 = enumerator2.MoveNext();

                    while (has1 || has2)
                    {
                        string value1 = has1 ? enumerator1.Current : null;
                        string value2 = has2 ? enumerator2.Current : null;
                        string value = selector(value1, value2);

                        if (value != null)
                        {
                            yield return value;
                        }

                        if (has1)
                        {
                            has1 = enumerator1.MoveNext();
                        }

                        if (has2)
                        {
                            has2 = enumerator2.MoveNext();
                        }
                    }

                }
            }
        }

        private static string Join(this IEnumerable<string> source)
        {
            StringBuilder builder = new StringBuilder();

            foreach (string value in source)
            {
                builder.Append(value);
            }

            return builder.ToString();
        }
    }
}
