// Заглушки только для CI-компиляции под Unity 6.
// В настоящем Unity этот файл не участвует в сборке (лежит вне Assets/).
// Reference-сборки на NuGet существуют только для Unity 2021.3, поэтому без этих
// заглушек ветки #if UNITY_6000_0_OR_NEWER вообще не проверялись, и ошибки вроде
// CS0619 (PhysicMaterial → PhysicsMaterial) всплывали только в реальном билде.
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

    /// <summary>Unity 6: RigidBody.drag / angularDrag → linearDamping / angularDamping, velocity → linearVelocity.</summary>
    public static class Unity6RigidbodyCompat
    {
        public static float GetLinearDamping(this Rigidbody rb) => rb.drag;
        public static void SetLinearDamping(this Rigidbody rb, float v) => rb.drag = v;
        public static float GetAngularDamping(this Rigidbody rb) => rb.angularDrag;
        public static void SetAngularDamping(this Rigidbody rb, float v) => rb.angularDrag = v;
        public static Vector3 GetLinearVelocity(this Rigidbody rb) => rb.velocity;
        public static void SetLinearVelocity(this Rigidbody rb, Vector3 v) => rb.velocity = v;
    }
}
#endif
