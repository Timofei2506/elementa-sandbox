// Заглушки только для CI-компиляции под Unity 6.
// В настоящем Unity этот файл не участвует в сборке (лежит вне Assets/),
// а нужен он потому, что reference-сборки на NuGet существуют только для
// Unity 2021.3 — без заглушек ветка #if UNITY_6000_0_OR_NEWER не проверяется
// и ошибки вроде CS0619 (PhysicMaterial → PhysicsMaterial) всплывают только
// в реальном билде.
#if UNITY_6000_0_OR_NEWER
namespace UnityEngine
{
    /// <summary>Unity 6: PhysicMaterial переименован в PhysicsMaterial.</summary>
    public class PhysicsMaterial : PhysicMaterial
    {
        public new float bounciness;
        public new float dynamicFriction;
        public new float staticFriction;
    }
}
#endif
