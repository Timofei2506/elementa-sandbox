using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Elementa
{
    /// <summary>
    /// Аварийный экран-диагностика. Ловит первые ошибки Unity и рисует их поверх игры,
    /// чтобы чёрный экран больше никогда не оставлял вопросов «что случилось».
    /// Нажатие F3 скрывает/показывает окно, F4 копирует текст в буфер.
    /// </summary>
    public static class ErrorOverlay
    {
        static readonly List<string> _messages = new List<string>();
        static GameObject _root;
        static Text _text;
        static bool _forced;

        public const int MaxLines = 14;

        public static void Capture(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            string line = condition;
            if (string.IsNullOrEmpty(line)) line = type.ToString();
            if (line.Length > 400) line = line.Substring(0, 400) + "…";
            string where = FirstFrames(stackTrace);
            if (!string.IsNullOrEmpty(where)) line += "   ← " + where;
            _messages.Add("• " + line);
            if (_messages.Count > MaxLines) _messages.RemoveAt(0);
            Refresh();
            Show();
        }

        /// <summary>Верхние кадры стека игровых скриптов — чтобы было видно, где именно упало.</summary>
        static string FirstFrames(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace)) return "";
            var sb = new StringBuilder();
            int used = 0;
            foreach (var raw in stackTrace.Split('\n'))
            {
                var l = raw.Trim();
                if (l.Length == 0) continue;
                if (!l.Contains("Elementa")) continue;      // только наш код, без внутренностей Unity
                if (l.StartsWith("at Elementa.ErrorOverlay")) continue;
                int paren = l.IndexOf(')');
                var frame = l;
                if (paren > 0) frame = l.Substring(0, paren + 1);
                if (sb.Length > 0) sb.Append(" | ");
                sb.Append(frame);
                if (++used >= 2) break;                     // два кадра достаточно
            }
            return sb.ToString();
        }

        public static void Report(string message)
        {
            _messages.Add("• " + message);
            if (_messages.Count > MaxLines) _messages.RemoveAt(0);
            Refresh();
            Show();
        }

        /// <summary>Текущие сообщения диагностики (для копирования в буфер).</summary>
        public static IReadOnlyList<string> Messages() => _messages;

        public static void ForceShow(string reason)
        {
            _forced = true;
            Report(reason);
            Show();
        }

        /// <summary>Создаёт окно после загрузки сцены, если уже есть ошибки.</summary>
        public static void AttachToScene()
        {
            if (_messages.Count == 0 && !_forced) return;
            Show();
        }

        static void Show()
        {
            if (_root == null) Build();
            if (_root != null) _root.SetActive(true);
        }

        static void Refresh()
        {
            if (_text == null) return;
            var sb = new StringBuilder();
            sb.AppendLine("<b>ELEMENTA · диагностика</b>  (F3 — скрыть, F4 — копировать)");
            for (int i = 0; i < _messages.Count; i++) sb.AppendLine(_messages[i]);
            _text.text = sb.ToString();
        }

        static void Build()
        {
            try
            {
                var canvasGo = new GameObject("Diagnostics_Canvas");
                Object.DontDestroyOnLoad(canvasGo);
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32000;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1600, 900);
                canvasGo.AddComponent<GraphicRaycaster>();

                var panel = new GameObject("Panel", typeof(RectTransform));
                panel.transform.SetParent(canvasGo.transform, false);
                var rt = (RectTransform)panel.transform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.offsetMin = new Vector2(12f, 12f);
                rt.offsetMax = new Vector2(-12f, 250f);
                var img = panel.AddComponent<Image>();
                img.color = new Color(0.55f, 0.06f, 0.06f, 0.93f);

                var textGo = new GameObject("Text", typeof(RectTransform));
                textGo.transform.SetParent(panel.transform, false);
                var trt = (RectTransform)textGo.transform;
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(14f, 10f);
                trt.offsetMax = new Vector2(-14f, -10f);
                _text = textGo.AddComponent<Text>();
                Theme.EnsureFont();
                _text.font = Theme.Font;
                _text.fontSize = 14;
                _text.color = Color.white;
                _text.alignment = TextAnchor.UpperLeft;
                _text.horizontalOverflow = HorizontalWrapMode.Wrap;
                _text.verticalOverflow = VerticalWrapMode.Truncate;
                _text.raycastTarget = false;

                var watcher = canvasGo.AddComponent<ErrorOverlayHotkeys>();
                watcher.Target = panel;
                _root = canvasGo;
                Refresh();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ELEMENTA] Не удалось показать диагностику: " + e.Message);
            }
        }
    }

    /// <summary>F3 — скрыть диагностику, F4 — скопировать её текст в буфер обмена.</summary>
    public class ErrorOverlayHotkeys : MonoBehaviour
    {
        public GameObject Target;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3) && Target != null) Target.SetActive(!Target.activeSelf);
            if (Input.GetKeyDown(KeyCode.F4)) GUIUtility.systemCopyBuffer = BuildLog();
        }

        static string BuildLog()
        {
            var sb = new StringBuilder();
            sb.AppendLine("ELEMENTA diagnostics — Unity " + Application.unityVersion + " / " + Application.platform);
            foreach (var line in ErrorOverlayLog()) sb.AppendLine(line);
            return sb.ToString();
        }

        static IEnumerable<string> ErrorOverlayLog() => ErrorOverlay.Messages();
    }
}
