//using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;

namespace Goke.Core.Extensions
{
    public static class StringExtensions
    {
        //
        const string SPECIAL = "@#$%&+=?<>!/";
        const string ALPHABETH = "ABCDEFGHIJKLMNPQRSTUVWXYZ";
        const string LOWERALPHABETH = "abcdefghkmnpqswxyz";
        const string NUMBER = "0123456789";
        static Random random = new();

        public static readonly Regex RegexStripHtml = new("<[^>]*>", RegexOptions.Compiled);

        extension(string str)
        {
            public bool IsNumeric()
            {
                return Decimal.TryParse(str, out var d);
            }

            public bool IsPoint()
            {
                return str == ".";
            }

            public bool IsNegation()
            {
                return str == "+/-";
            }
            public bool IsOperator()
            {
                return str == "+" || str == "-" || str == "*" || str == "/" || str == "=";
            }

            public string ToSentence(string separator = "", bool removeSeparator = true, string fill = " ")
            {
                if (string.IsNullOrWhiteSpace(str))
                    return str;

                string r = string.Empty;
                //int c = 0;
                for (int i = 0; i < str.Length; i++)
                {
                    var q = str[i];
                    if ((char.IsUpper(q) || separator == q.ToString()) && i > 0 && char.IsLower(str[(i - 1)]))
                    {
                        r = string.Format("{0}{1}{2}", r, fill, q);
                        //c++;
                    }
                    else
                    {
                        r = string.Format("{0}{1}", r, q);
                    }
                }

                //
                for (int i = 0; i < r.Length; i++)
                {
                    var q = r[i];
                    if (char.IsLower(q))
                    {
                        if (i > 1)
                        {
                            var j = i - 1;
                            r = string.Format("{0}{1}{2}", r.Substring(0, j), fill, r[j..]);
                        }
                        break;
                    }
                }
                if (removeSeparator && separator != "")
                {
                    r = r.Replace(separator, "");
                }

                return r;
            }

            public string ToTitleCase(string separator = " ")
            {
                if (string.IsNullOrWhiteSpace(str))
                    return str;

                string r = string.Empty;

                var words = str.Split(separator, StringSplitOptions.RemoveEmptyEntries);
                foreach (var w in words)
                {
                    r += " " + w.Substring(0, 1).ToUpper() + w[1..];
                }

                return r.TrimStart();
            }

            public string ToCamelCase()
            {
                if (string.IsNullOrWhiteSpace(str))
                    return str;
                // CGPAText, GodExcellent
                var j = 0;
                for (int i = 0; i < str.Count(); i++)
                {
                    var q = str[i];
                    if (char.IsLower(q))
                    {
                        j = i;
                        if (i > 1)
                            j = i - 1;

                        break;
                    }
                }

                var l = str.Substring(0, j);
                var r = str[j..];
                return l.ToLower() + r;

            }


            public string ToPlural()
            {
                var len = str.Length;
                var w = str.ToLower();
                string s;
                if (System.String.Compare(w, "person", System.StringComparison.Ordinal) == 0)
                {
                    s = str.Substring(0, 1) + "eople";
                    return s;
                }
                if (w.EndsWith("staff"))
                {
                    return str + "s";
                }
                if (System.String.Compare(w, "staff", System.StringComparison.Ordinal) == 0)
                {
                    return str + "s";
                }

                var l = w.Last();
                switch (l)
                {
                    case 'f': s = str.Substring(0, (len - 1)) + "ves"; break;
                    case 'h':
                    case 'o':
                    case 's':
                    case 'x': s = str + "es"; break;
                    case 'y': s = str.Substring(0, (len - 1)) + "ies"; break;
                    default:
                        s = str + "s"; break;
                }
                return s;
            }

            public string ToDisplayName()
            {
                var name = str;
                if (str.EndsWith("Id"))
                {
                    name = str.Remove(str.Length - 2);
                }
                ;

                return ToSentence(name);
            }

            public int IndexOfNth(string value, int nth = 0)
            {
                if (nth < 0)
                    throw new ArgumentException("Can not find a negative index of substring in string. Must start with 0");

                int offset = str.IndexOf(value);
                for (int i = 0; i < nth; i++)
                {
                    if (offset == -1) return -1;
                    offset = str.IndexOf(value, offset + 1);
                }

                return offset;
            }

            public string ToThumb()
            {
                if (str.IndexOf('/') < 1) return str;

                var first = str.Substring(0, str.LastIndexOf('/'));
                var second = str.Substring(str.LastIndexOf('/'));

                return $"{first}/thumbs{second}";
            }

            public string Capitalize()
            {
                if (string.IsNullOrEmpty(str))
                    return string.Empty;
                char[] a = str.ToCharArray();
                a[0] = char.ToUpper(a[0]);
                return new string(a);
            }

            //public string MdToHtml()
            //{
            //    var mpl = new MarkdownPipelineBuilder()
            //        .UsePipeTables()
            //        .UseAdvancedExtensions()
            //        .Build();

            //    return Markdown.ToHtml(str, mpl);
            //}

            public bool Contains(string toCheck, StringComparison comp)
            {
                return str.IndexOf(toCheck, comp) >= 0;
            }


            public string StripHtml()
            {
                return string.IsNullOrWhiteSpace(str) ? string.Empty : RegexStripHtml.Replace(str, string.Empty).Trim();
            }

            /// <summary>
            /// Should extract title (file name) from file path or Url
            /// </summary>
            /// <param name="str">c:\foo\test.png</param>
            /// <returns>test.png</returns>
            public string ExtractTitle()
            {
                if (str.Contains("\\"))
                {
                    return string.IsNullOrWhiteSpace(str) ? string.Empty : str.Substring(str.LastIndexOf("\\")).Replace("\\", "");
                }
                else if (str.Contains("/"))
                {
                    return string.IsNullOrWhiteSpace(str) ? string.Empty : str.Substring(str.LastIndexOf("/")).Replace("/", "");
                }
                else
                {
                    return str;
                }
            }

            /// <summary>
            /// Converts title to valid URL slug
            /// </summary>
            /// <returns>Slug</returns>
            public string ToSlug()
            {
                str = str.ToLowerInvariant();
                str = str.Trim('-', '_');

                if (string.IsNullOrEmpty((string)str))
                    return string.Empty;

                var bytes = Encoding.GetEncoding("utf-8").GetBytes((string)str);
                str = Encoding.UTF8.GetString(bytes);

                str = Regex.Replace((string)str, @"\s", "-", RegexOptions.Compiled);

                str = Regex.Replace((string)str, @"([-_]){2,}", "$1", RegexOptions.Compiled);

                str = RemoveIllegalCharacters(str);

                return str;
            }

            //public string Hash()
            //{
            //    var bytes = KeyDerivation.Pbkdf2(
            //              password: str,
            //              salt: Encoding.UTF8.GetBytes(str),
            //              prf: KeyDerivationPrf.HMACSHA512,
            //              iterationCount: 10000,
            //              numBytesRequested: 256 / 8);

            //    return Convert.ToBase64String(bytes);
            //}

            public string ReplaceIgnoreCase(string replacement)
            {
                string result = Regex.Replace(
                    str,
                    Regex.Escape(str),
                    replacement.Replace("$", "$$"),
                    RegexOptions.IgnoreCase
                );
                return result;
            }

            #region Helper Methods

            public string RemoveIllegalCharacters()
            {
                if (string.IsNullOrEmpty(str))
                {
                    return str;
                }

                string[] chars = [
                    ":", "/", "?", "!", "#", "[", "]", "{", "}", "@", "*", ".", ",",
                "\"","&", "'", "~", "$"
                ];

                foreach (var ch in chars)
                {
                    str = str.Replace(ch, string.Empty);
                }

                str = str.Replace("–", "-");
                str = str.Replace(" ", "-");

                str = str.RemoveUnicodePunctuation();
                str = str.RemoveDiacritics();
                str = str.RemoveExtraHyphen();

                return System.Web.HttpUtility.HtmlEncode(str).Replace("%", string.Empty);
            }

            public string RemoveUnicodePunctuation()
            {
                var normalized = str.Normalize(NormalizationForm.FormD);
                var sb = new StringBuilder();

                foreach (var c in
                    normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.InitialQuotePunctuation &&
                                          CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.FinalQuotePunctuation))
                {
                    sb.Append(c);
                }

                return sb.ToString();
            }

            public string RemoveDiacritics()
            {
                var normalized = str.Normalize(NormalizationForm.FormD);
                var sb = new StringBuilder();

                foreach (var c in
                    normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
                {
                    sb.Append(c);
                }

                return sb.ToString();
            }

            public string RemoveExtraHyphen()
            {
                if (str.Contains("--"))
                {
                    str = str.Replace("--", "-");
                    return RemoveExtraHyphen(str);
                }

                return str;
            }

            public string SanitizePath()
            {
                if (string.IsNullOrWhiteSpace(str))
                    return string.Empty;

                str = str.Replace("%2E", ".").Replace("%2F", "/");

                if (str.Contains("..") || str.Contains("//"))
                    throw new ApplicationException("Invalid directory path");

                return str;
            }

            public string SanitizeFileName()
            {
                str = str.SanitizePath();

                //TODO: add filename specific validation here

                return str;
            }

            #endregion

            public string InsertLowerAlphabeth()
            {
                var k = random.Next(LOWERALPHABETH.Length);
                var s = LOWERALPHABETH.ElementAt(k).ToString();
                k = random.Next(str.Length);
                str = str.Insert(k, s);
                return str;
            }

            public string InsertUpperAlphabeth()
            {
                int k = random.Next(ALPHABETH.Length);
                var s = ALPHABETH.ElementAt(k).ToString();
                k = random.Next(str.Length);
                str = str.Insert(k, s);
                return str;
            }

            public string InsertSpecialCharacter()
            {
                int k = random.Next(SPECIAL.Length);
                var s = SPECIAL.ElementAt(k).ToString();
                k = random.Next(1, str.Length);
                str = str.Insert(k, s);
                return str;
            }

            public string InsertNumber()
            {
                int k = random.Next(NUMBER.Length);
                var s = SPECIAL.ElementAt(k).ToString();
                k = random.Next(1, str.Length);
                str = str.Insert(k, s);
                return str;
            }

        }

        extension(string)
        {
            public static string GenerateDigits(int digits = 10)
            {
                // Generate a random digits.
                int multipleOf3 = (digits / 3) + (digits % 3 == 0 ? 0 : 1);

                // Generate a random 4-byte array
                var b = new byte[multipleOf3];

                // Fill the byte array with random bytes
                random.NextBytes(b);

                // generate a (4*3) 12-digit pin using the random bytes
                // Convert the byte array to a string representation of the pin
                var strDigits = string.Join("", b.Select(s => s.ToString("000")));

                return strDigits[..digits];
            }

            public static string GeneratePin(int digits = 9)
            {
                return StringBuilder.GeneratePin(digits);
            }   
            
            public static string GeneratePassword(int numChars = 12, int lowers =0, int uppers=1, int specials=1, int digits=1)
            {
                return StringBuilder.GeneratePassword(numChars, lowers, uppers, specials, digits);
            }
    }
}
}
