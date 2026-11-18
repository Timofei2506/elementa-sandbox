// Test-only shim. Provides the subset of the UnityEngine API used by the
// engine-independent simulation core so it can run under plain .NET in CI.
// Never compiled inside Unity (lives outside Assets/).
using System;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 down => new Vector3(0, -1, 0);
        public static Vector3 left => new Vector3(-1, 0, 0);
        public static Vector3 right => new Vector3(1, 0, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public static Vector3 back => new Vector3(0, 0, -1);
        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t = Mathf.Clamp01(t); return a + (b - a) * t; }
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) => a + (b - a) * t;
        public static Vector3 MoveTowards(Vector3 a, Vector3 b, float d)
        { var v = b - a; float m = v.magnitude; return m <= d || m < 1e-5f ? b : a + v / m * d; }
        public static Vector3 ClampMagnitude(Vector3 v, float max) { float m = v.magnitude; return m > max ? v / m * max : v; }
        public static Vector3 ProjectOnPlane(Vector3 v, Vector3 n) => v - n * Dot(v, n) / Dot(n, n);
        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator *(float d, Vector3 a) => a * d;
        public static Vector3 operator /(Vector3 a, float d) => new Vector3(a.x / d, a.y / d, a.z / d);
        public override string ToString() => $"({x:F2}, {y:F2}, {z:F2})";
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float d) => new Vector2(a.x * d, a.y * d);
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1);
        public static Color black => new Color(0, 0, 0);
        public static Color clear => new Color(0, 0, 0, 0);
        public static Color red => new Color(1, 0, 0);
        public static Color green => new Color(0, 1, 0);
        public static Color blue => new Color(0, 0, 1);
        public static Color yellow => new Color(1, 1, 0);
        public static Color cyan => new Color(0, 1, 1);
        public static Color magenta => new Color(1, 0, 1);
        public static Color gray => new Color(0.5f, 0.5f, 0.5f);
        public static Color Lerp(Color x, Color y, float t)
        { t = Mathf.Clamp01(t); return new Color(x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t, x.b + (y.b - x.b) * t, x.a + (y.a - x.a) * t); }
        public static Color operator *(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);
        public static Color operator *(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
        public static Color operator +(Color a, Color b) => new Color(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a);
        public static implicit operator Color32(Color c) => new Color32((byte)(Mathf.Clamp01(c.r) * 255), (byte)(Mathf.Clamp01(c.g) * 255), (byte)(Mathf.Clamp01(c.b) * 255), (byte)(Mathf.Clamp01(c.a) * 255));
        public override string ToString() => $"RGBA({r:F2}, {g:F2}, {b:F2}, {a:F2})";
    }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static implicit operator Color(Color32 c) => new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);
    }

    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 180f / PI;
        public const float Epsilon = 1e-5f;
        public const float Infinity = float.PositiveInfinity;
        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        public static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
        public static float Clamp01(float v) => v < 0 ? 0 : (v > 1 ? 1 : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
        public static float InverseLerp(float a, float b, float v) => Math.Abs(b - a) < Epsilon ? 0 : Clamp01((v - a) / (b - a));
        public static float MoveTowards(float a, float b, float d) => Math.Abs(b - a) <= d ? b : a + Math.Sign(b - a) * d;
        public static float Abs(float v) => Math.Abs(v);
        public static int Abs(int v) => Math.Abs(v);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static int Min(int a, int b) => Math.Min(a, b);
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float Exp(float v) => (float)Math.Exp(v);
        public static float Log(float v) => (float)Math.Log(v);
        public static float Log10(float v) => (float)Math.Log10(v);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Tan(float v) => (float)Math.Tan(v);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Floor(float v) => (float)Math.Floor(v);
        public static float Ceil(float v) => (float)Math.Ceiling(v);
        public static float Round(float v) => (float)Math.Round(v);
        public static int FloorToInt(float v) => (int)Math.Floor(v);
        public static int CeilToInt(float v) => (int)Math.Ceiling(v);
        public static int RoundToInt(float v) => (int)Math.Round(v);
        public static float Sign(float v) => v >= 0 ? 1f : -1f;
        public static float SmoothStep(float a, float b, float t) { t = Clamp01(t); t = t * t * (3f - 2f * t); return a + (b - a) * t; }
        public static float Repeat(float t, float len) => t - Floor(t / len) * len;
        public static float PingPong(float t, float len) { t = Repeat(t, len * 2); return len - Math.Abs(t - len); }
        public static bool Approximately(float a, float b) => Math.Abs(b - a) < Math.Max(1e-6f * Math.Max(Math.Abs(a), Math.Abs(b)), Epsilon * 8);
    }

    public static class Random
    {
        static System.Random _rng = new System.Random(12345);
        public static void InitState(int seed) => _rng = new System.Random(seed);
        public static float value => (float)_rng.NextDouble();
        public static float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
        public static int Range(int min, int maxExclusive) => _rng.Next(min, maxExclusive);
        public static Vector3 insideUnitSphere
        {
            get
            {
                double u = _rng.NextDouble(), v = _rng.NextDouble(), w = _rng.NextDouble();
                double theta = 2 * Math.PI * u, phi = Math.Acos(2 * v - 1), r = Math.Pow(w, 1.0 / 3.0);
                return new Vector3((float)(r * Math.Sin(phi) * Math.Cos(theta)), (float)(r * Math.Sin(phi) * Math.Sin(theta)), (float)(r * Math.Cos(phi)));
            }
        }
        public static Vector2 insideUnitCircle => new Vector2(Range(-1f, 1f), Range(-1f, 1f));
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new Quaternion(0, 0, 0, 1);
        public static Quaternion Euler(float x, float y, float z)
        {
            float cx = Mathf.Cos(x * Mathf.Deg2Rad * 0.5f), sx = Mathf.Sin(x * Mathf.Deg2Rad * 0.5f);
            float cy = Mathf.Cos(y * Mathf.Deg2Rad * 0.5f), sy = Mathf.Sin(y * Mathf.Deg2Rad * 0.5f);
            float cz = Mathf.Cos(z * Mathf.Deg2Rad * 0.5f), sz = Mathf.Sin(z * Mathf.Deg2Rad * 0.5f);
            return new Quaternion(
                sx * cy * cz - cx * sy * sz,
                cx * sy * cz + sx * cy * sz,
                cx * cy * sz - sx * sy * cz,
                cx * cy * cz + sx * sy * sz);
        }
        public static Quaternion LookRotation(Vector3 forward) => identity;
        public static Vector3 operator *(Quaternion q, Vector3 v) => v;
    }
}
