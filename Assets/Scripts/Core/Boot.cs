using UnityEngine;
using UnityEngine.EventSystems;

namespace Elementa
{
    /// <summary>
    /// Entry point. Creates every system at runtime, so an empty scene is enough:
    /// open the project, press Play, and the sandbox boots.
    /// </summary>
    public static class Boot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BeforeSceneLoad()
        {
            if (Object.FindAnyObjectByType<Game>() != null) return;

            Physics.defaultSolverIterations = 8;
            Physics.defaultSolverVelocityIterations = 2;
            Physics.sleepThreshold = 0.05f;
            Physics.bounceThreshold = 1.2f;

            EnsureEventSystem();
            var game = new GameObject("ELEMENTA").AddComponent<Game>();
        }

        static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(es);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AfterSceneLoad()
        {
            var game = Game.I;
            if (game == null) return;
            if (game.World != null) return;

            var rig = CameraRig.Create();
            game.CameraRig = rig;
            rig.gameObject.AddComponent<ScreenShake>();

            var worldGo = new GameObject("SandboxWorld");
            var world = worldGo.AddComponent<SandboxWorld>();
            game.World = world;
            world.Init();

            PostFX.Attach(rig.Cam);

            var ui = UIRoot.Create();
            PostFX.AddVignette(ui.Canvas);
            game.UI = ui;

            AudioSystem.Create(world.transform);
            worldGo.AddComponent<ThermalSystem>();
            PlayerController.Create(world);
            world.BuildStarterScene();

            if (game.IsMobile)
            {
                rig.Distance = 20f;
                ui.Log("Мобильная раскладка: один палец — вращение камеры, два пальца — зум.");
            }
        }
    }
}
