using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Elementa
{
    /// <summary>
    /// Точка входа. Всё создаётся в рантайме, поэтому достаточно пустой сцены.
    /// Каждый шаг обёрнут в try/catch: если одна система упала, остальные всё равно
    /// поднимутся, а ошибка будет видна на экране (см. <see cref="ErrorOverlay"/>),
    /// а не превратится в чёрный экран без объяснений.
    /// </summary>
    public static class Boot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BeforeSceneLoad()
        {
            if (UnityEngine.Object.FindAnyObjectByType<Game>() != null) return;

            Application.logMessageReceived += ErrorOverlay.Capture;
            Debug.Log("[ELEMENTA] Старт. Unity " + Application.unityVersion + ", " + Application.platform);

            Physics.defaultSolverIterations = 8;
            Physics.defaultSolverVelocityIterations = 2;
            Physics.sleepThreshold = 0.05f;
            Physics.bounceThreshold = 1.2f;

            Step("EventSystem", EnsureEventSystem);
            Step("Game", () =>
            {
                var go = new GameObject("ELEMENTA");
                go.AddComponent<Game>();
            });
        }

        static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            UnityEngine.Object.DontDestroyOnLoad(es);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AfterSceneLoad()
        {
            var game = Game.I;
            if (game == null)
            {
                Debug.LogError("[ELEMENTA] Game не создался — показываю аварийный экран.");
                ErrorOverlay.ForceShow("Game не был создан на этапе BeforeSceneLoad.");
                return;
            }
            if (game.World != null) return;

            // 1. камера — самое важное, без неё ничего не видно
            Step("CameraRig", () =>
            {
                var rig = CameraRig.Create();
                game.CameraRig = rig;
                rig.gameObject.AddComponent<ScreenShake>();
            });
            EnsureCameraFallback();

            // 2. мир: свет, земля, задник, арена
            Step("World", () =>
            {
                var worldGo = new GameObject("SandboxWorld");
                var world = worldGo.AddComponent<SandboxWorld>();
                game.World = world;
                world.Init();
            });

            // 3. пост-обработка (не критична)
            Step("PostFX", () => { if (game.CameraRig != null) PostFX.Attach(game.CameraRig.Cam); });

            // 4. интерфейс — самый заметный элемент, ошибку из него показываем явно
            try
            {
                var ui = UIRoot.Create();
                game.UI = ui;
                Step("Vignette", () => PostFX.AddVignette(ui.Canvas));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogError("[ELEMENTA] Интерфейс не собрался: " + e.Message);
                ErrorOverlay.Report("Интерфейс: " + e.Message);
            }

            Step("Audio", () => AudioSystem.Create(game.World != null ? game.World.transform : null));
            Step("Physics world", () =>
            {
                if (game.World != null)
                {
                    game.World.gameObject.AddComponent<ThermalSystem>();
                    PlayerController.Create(game.World);
                }
            });

            Step("Effects", () =>
            {
                if (game.World != null && game.World.GetComponentInChildren<EffectsSystem>() == null)
                {
                    var fx = new GameObject("EffectsSystem");
                    fx.transform.SetParent(game.World.transform, false);
                    fx.AddComponent<EffectsSystem>();
                }
            });

            Step("Сцена", () =>
            {
                if (game.World != null) game.World.BuildStarterScene();
            });

            Step("Вид", () =>
            {
                if (game.CameraRig == null) return;
                game.CameraRig.ResetView();
                if (game.IsMobile)
                {
                    game.CameraRig.OrthoSize = 14f;
                    game.UI?.Log("Телефон: один палец — двигать сцену, два пальца — зум.");
                }
            });

            if (game.CameraRig != null && game.CameraRig.Cam != null)
                Debug.Log("[ELEMENTA] Готово. Объектов: " + (game.World != null ? game.World.Objects.Count : 0)
                          + ", orthoSize " + game.CameraRig.OrthoSize);
            ErrorOverlay.AttachToScene();
        }

        /// <summary>Выполняет шаг и логирует ошибку, не ломая остальную инициализацию.</summary>
        static void Step(string name, Action action)
        {
            try { action(); }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogError("[ELEMENTA] Шаг \"" + name + "\" упал: " + e.Message);
                ErrorOverlay.Report(name + ": " + e.Message);
            }
        }

        /// <summary>Если камера пропала (например, сцена без MainCamera), создаём принудительно.</summary>
        static void EnsureCameraFallback()
        {
            if (Camera.main != null) return;
            Debug.LogWarning("[ELEMENTA] Camera.main не найдена — создаю резервную.");
            var go = new GameObject("FallbackCamera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
            cam.transform.position = new Vector3(0f, 4f, -20f);
            go.AddComponent<AudioListener>();
        }
    }
}
