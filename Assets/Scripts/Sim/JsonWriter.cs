using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Elementa.Sim
{
    /// <summary>Tiny JSON writer used for save files and telemetry.</summary>
    public class JsonWriter
    {
        readonly StringBuilder _sb = new StringBuilder();
        readonly Stack<bool> _first = new Stack<bool>();
        bool _needComma;

        public JsonWriter BeginObject() { Sep(); _sb.Append('{'); _first.Push(true); _needComma = false; return this; }
        public JsonWriter EndObject() { _sb.Append('}'); _first.Pop(); _needComma = true; return this; }
        public JsonWriter BeginArray() { Sep(); _sb.Append('['); _first.Push(true); _needComma = false; return this; }
        public JsonWriter EndArray() { _sb.Append(']'); _first.Pop(); _needComma = true; return this; }

        public JsonWriter Key(string key)
        {
            Sep();
            _sb.Append('"').Append(Escape(key)).Append("\":");
            _needComma = false;
            return this;
        }

        public JsonWriter Value(string v) { Sep(); _sb.Append(v == null ? "null" : "\"" + Escape(v) + "\""); _needComma = true; return this; }
        public JsonWriter Value(float v) { Sep(); _sb.Append(v.ToString("R", CultureInfo.InvariantCulture)); _needComma = true; return this; }
        public JsonWriter Value(double v) { Sep(); _sb.Append(v.ToString("R", CultureInfo.InvariantCulture)); _needComma = true; return this; }
        public JsonWriter Value(int v) { Sep(); _sb.Append(v.ToString(CultureInfo.InvariantCulture)); _needComma = true; return this; }
        public JsonWriter Value(bool v) { Sep(); _sb.Append(v ? "true" : "false"); _needComma = true; return this; }

        public JsonWriter Prop(string key, string v) => Key(key).Value(v);
        public JsonWriter Prop(string key, float v) => Key(key).Value(v);
        public JsonWriter Prop(string key, int v) => Key(key).Value(v);
        public JsonWriter Prop(string key, bool v) => Key(key).Value(v);

        void Sep()
        {
            if (_first.Count > 0 && _first.Peek()) { _first.Pop(); _first.Push(false); _needComma = false; return; }
            if (_needComma) _sb.Append(',');
        }

        static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");

        public override string ToString() => _sb.ToString();
    }
}
