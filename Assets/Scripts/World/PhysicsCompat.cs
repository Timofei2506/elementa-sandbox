using System.Reflection;
using UnityEngine;

namespace Elementa
{
    /// <summary>
    /// Unity 6 переименовала часть API Rigidbody: drag → linearDamping, angularDrag → angularDamping,
    /// velocity → linearVelocity. Прямое обращение к новым именам не компилируется на старых
    /// версиях, а к старым — даёт obsolete-ошибку (CS0619) при сборке под Unity 6.
    /// Поэтому имена определяются один раз через рефлексию: код собирается и работает на обеих ветках.
    /// Вызовы редкие (создание объекта, пару раз за кадр), поэтому цена рефлексии не заметна.
    /// </summary>
    public static class PhysicsCompat
    {
        static PropertyInfo _linearDamping, _angularDamping, _linearVelocity;
        static PropertyInfo _drag, _angularDrag, _velocity;
        static bool _resolved;

        static void Resolve()
        {
            _resolved = true;
            var t = typeof(Rigidbody);
            var flags = BindingFlags.Public | BindingFlags.Instance;
            _linearDamping = t.GetProperty("linearDamping", flags);
            _angularDamping = t.GetProperty("angularDamping", flags);
            _linearVelocity = t.GetProperty("linearVelocity", flags);
            _drag = t.GetProperty("drag", flags);
            _angularDrag = t.GetProperty("angularDrag", flags);
            _velocity = t.GetProperty("velocity", flags);
        }

        public static void SetLinearDamping(Rigidbody rb, float value)
        {
            if (rb == null) return;
            if (!_resolved) Resolve();
            if (_linearDamping != null) _linearDamping.SetValue(rb, value);
            else _drag?.SetValue(rb, value);
        }

        public static void SetAngularDamping(Rigidbody rb, float value)
        {
            if (rb == null) return;
            if (!_resolved) Resolve();
            if (_angularDamping != null) _angularDamping.SetValue(rb, value);
            else _angularDrag?.SetValue(rb, value);
        }

        public static Vector3 GetVelocity(Rigidbody rb)
        {
            if (rb == null) return Vector3.zero;
            if (!_resolved) Resolve();
            if (_linearVelocity != null && _linearVelocity.GetValue(rb) is Vector3 v6) return v6;
            if (_velocity != null && _velocity.GetValue(rb) is Vector3 v) return v;
            return Vector3.zero;
        }

        public static void SetVelocity(Rigidbody rb, Vector3 value)
        {
            if (rb == null) return;
            if (!_resolved) Resolve();
            if (_linearVelocity != null) _linearVelocity.SetValue(rb, value);
            else _velocity?.SetValue(rb, value);
        }
    }
}
