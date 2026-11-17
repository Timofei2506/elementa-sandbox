using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;


namespace Elementa.Sim
{
    /// <summary>
    /// Minimal, allocation-friendly JSON reader. Kept engine-independent so the whole
    /// simulation core can be unit-tested outside Unity and reused for save files.
    /// Produces Dictionary&lt;string,object&gt;, List&lt;object&gt;, double, string, bool or null.
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int i = 0;
            SkipWs(text, ref i);
            var v = ParseValue(text, ref i);
            return v;
        }

        static void SkipWs(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') i++;
                else if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
                { while (i < s.Length && s[i] != '\n') i++; }
                else break;
            }
        }

        static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) throw new FormatException("Unexpected end of JSON");
            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't': Expect(s, ref i, "true"); return true;
                case 'f': Expect(s, ref i, "false"); return false;
                case 'n': Expect(s, ref i, "null"); return null;
                default: return ParseNumber(s, ref i);
            }
        }

        static void Expect(string s, ref int i, string word)
        {
            if (i + word.Length > s.Length || s.Substring(i, word.Length) != word)
                throw new FormatException("Invalid literal at " + i);
            i += word.Length;
        }

        static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++; // {
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return d; }
            while (true)
            {
                SkipWs(s, ref i);
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (s[i] != ':') throw new FormatException("Expected ':' at " + i);
                i++;
                d[key] = ParseValue(s, ref i);
                SkipWs(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return d; }
                throw new FormatException("Expected ',' or '}' at " + i);
            }
        }

        static List<object> ParseArray(string s, ref int i)
        {
            var list = new List<object>();
            i++; // [
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return list; }
            while (true)
            {
                list.Add(ParseValue(s, ref i));
                SkipWs(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return list; }
                throw new FormatException("Expected ',' or ']' at " + i);
            }
        }

        static string ParseString(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException("Expected string at " + i);
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c == '\\')
                {
                    char e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                            i += 4;
                            break;
                        default: sb.Append(e); break;
                    }
                }
                else sb.Append(c);
            }
            throw new FormatException("Unterminated string");
        }

        static double ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "-+.eE0123456789".IndexOf(s[i]) >= 0) i++;
            var span = s.Substring(start, i - start);
            return double.Parse(span, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        // ---------- typed helpers ----------
        public static Dictionary<string, object> Obj(object o) => (Dictionary<string, object>)o;
        public static List<object> Arr(object o) => (List<object>)o;

        public static string Str(Dictionary<string, object> o, string k, string def = "")
            => o != null && o.TryGetValue(k, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : def;

        public static double Num(Dictionary<string, object> o, string k, double def = 0)
        {
            if (o == null || !o.TryGetValue(k, out var v) || v == null) return def;
            if (v is double d) return d;
            if (v is string str && double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var dd)) return dd;
            return def;
        }

        public static int Int(Dictionary<string, object> o, string k, int def = 0) => (int)Math.Round(Num(o, k, def));

        public static bool Bool(Dictionary<string, object> o, string k, bool def = false)
        {
            if (o == null || !o.TryGetValue(k, out var v) || v == null) return def;
            if (v is bool b) return b;
            return Str(o, k).ToLowerInvariant() is "true" or "1";
        }

        public static List<object> ArrOf(Dictionary<string, object> o, string k)
            => o != null && o.TryGetValue(k, out var v) && v is List<object> l ? l : new List<object>();
    }
}
