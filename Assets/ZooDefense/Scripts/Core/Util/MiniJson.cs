using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ZooDefense.Core
{
    /// <summary>
    /// Small JSON reader / writer for service responses whose shape is not known in advance (Remote Config values,
    /// leaderboard metadata, Cloud Code results). Objects become Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;,
    /// numbers double, plus string, bool and null. JsonUtility cannot read free-form objects, hence this.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var p = new Parser(json);
            try
            {
                var v = p.Value();
                p.SkipWs();
                return p.AtEnd ? v : null;
            }
            catch (FormatException) { return null; }
            catch (IndexOutOfRangeException) { return null; }
            catch (ArgumentOutOfRangeException) { return null; }
        }

        public static Dictionary<string, object> Object(string json) { return Parse(json) as Dictionary<string, object>; }

        // ------------------------------------------------------------------ typed access helpers

        public static Dictionary<string, object> Obj(object o, string key)
        {
            var d = o as Dictionary<string, object>;
            object v;
            return d != null && d.TryGetValue(key, out v) ? v as Dictionary<string, object> : null;
        }

        public static List<object> Arr(object o, string key)
        {
            var d = o as Dictionary<string, object>;
            object v;
            return d != null && d.TryGetValue(key, out v) ? v as List<object> : null;
        }

        public static string Str(object o, string key, string fallback = null)
        {
            var d = o as Dictionary<string, object>;
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return fallback;
            if (v is string) return (string)v;
            if (v is double) return ((double)v).ToString(CultureInfo.InvariantCulture);
            if (v is bool) return (bool)v ? "true" : "false";
            return fallback;
        }

        public static double Num(object o, string key, double fallback = 0)
        {
            var d = o as Dictionary<string, object>;
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return fallback;
            if (v is double) return (double)v;
            if (v is bool) return (bool)v ? 1 : 0;
            double x;
            if (v is string && double.TryParse((string)v, NumberStyles.Float, CultureInfo.InvariantCulture, out x)) return x;
            return fallback;
        }

        public static int Int(object o, string key, int fallback = 0)
        {
            double v = Num(o, key, double.NaN);
            if (double.IsNaN(v) || double.IsInfinity(v)) return fallback;
            return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, Math.Round(v)));
        }

        public static bool Bool(object o, string key, bool fallback = false)
        {
            var d = o as Dictionary<string, object>;
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return fallback;
            if (v is bool) return (bool)v;
            if (v is double) return (double)v != 0;
            if (v is string) { var s = ((string)v).Trim().ToLowerInvariant(); if (s == "true" || s == "1") return true; if (s == "false" || s == "0") return false; }
            return fallback;
        }

        // ------------------------------------------------------------------ writer

        public static string Serialize(object value)
        {
            var sb = new StringBuilder();
            Write(sb, value);
            return sb.ToString();
        }

        static void Write(StringBuilder sb, object v)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is string) { WriteString(sb, (string)v); return; }
            if (v is bool) { sb.Append((bool)v ? "true" : "false"); return; }
            if (v is double || v is float || v is decimal)
            {
                double d = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                if (double.IsNaN(d) || double.IsInfinity(d)) sb.Append("0");
                else sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            if (v is int || v is long || v is short || v is byte || v is uint || v is ulong) { sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture)); return; }
            var dict = v as IDictionary<string, object>;
            if (dict != null)
            {
                sb.Append('{');
                bool first = true;
                foreach (var kv in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(sb, kv.Key);
                    sb.Append(':');
                    Write(sb, kv.Value);
                }
                sb.Append('}');
                return;
            }
            var list = v as System.Collections.IEnumerable;
            if (list != null)
            {
                sb.Append('[');
                bool first = true;
                foreach (var x in list)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    Write(sb, x);
                }
                sb.Append(']');
                return;
            }
            WriteString(sb, v.ToString());
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ------------------------------------------------------------------ reader

        sealed class Parser
        {
            readonly string _s;
            int _i;
            int _depth;

            public Parser(string s) { _s = s; }

            public bool AtEnd { get { return _i >= _s.Length; } }

            public void SkipWs() { while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++; }

            public object Value()
            {
                SkipWs();
                if (_i >= _s.Length) throw new FormatException("end");
                char c = _s[_i];
                switch (c)
                {
                    case '{': return ObjectValue();
                    case '[': return ArrayValue();
                    case '"': return StringValue();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return NumberValue();
                        throw new FormatException("unexpected " + c);
                }
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0) throw new FormatException("expected " + word);
                _i += word.Length;
            }

            Dictionary<string, object> ObjectValue()
            {
                if (++_depth > 64) throw new FormatException("too deep");
                var d = new Dictionary<string, object>();
                _i++;
                SkipWs();
                if (_s[_i] == '}') { _i++; _depth--; return d; }
                while (true)
                {
                    SkipWs();
                    if (_s[_i] != '"') throw new FormatException("key");
                    string key = StringValue();
                    SkipWs();
                    if (_s[_i++] != ':') throw new FormatException(":");
                    d[key] = Value();
                    SkipWs();
                    char c = _s[_i++];
                    if (c == '}') break;
                    if (c != ',') throw new FormatException(",");
                }
                _depth--;
                return d;
            }

            List<object> ArrayValue()
            {
                if (++_depth > 64) throw new FormatException("too deep");
                var list = new List<object>();
                _i++;
                SkipWs();
                if (_s[_i] == ']') { _i++; _depth--; return list; }
                while (true)
                {
                    list.Add(Value());
                    SkipWs();
                    char c = _s[_i++];
                    if (c == ']') break;
                    if (c != ',') throw new FormatException(",");
                }
                _depth--;
                return list;
            }

            string StringValue()
            {
                _i++;   // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    char c = _s[_i++];
                    if (c == '"') break;
                    if (c != '\\') { sb.Append(c); continue; }
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _i += 4;
                            break;
                        default: throw new FormatException("escape");
                    }
                }
                return sb.ToString();
            }

            object NumberValue()
            {
                int start = _i;
                if (_s[_i] == '-') _i++;
                while (_i < _s.Length && "0123456789+-.eE".IndexOf(_s[_i]) >= 0) _i++;
                double d;
                if (!double.TryParse(_s.Substring(start, _i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) throw new FormatException("number");
                return d;
            }
        }
    }
}
