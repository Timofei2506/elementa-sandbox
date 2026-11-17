using Elementa.Sim;
using UnityEngine;

namespace Elementa
{
    /// <summary>Armour plate component: material + thickness, used by the penetration model.</summary>
    public class ArmorPlate : MonoBehaviour
    {
        public MaterialDef Material;
        public float ThicknessMM = 10f;

        public static ArmorPlate Attach(SandboxObject obj, float thicknessMM)
        {
            var plate = obj.gameObject.AddComponent<ArmorPlate>();
            plate.Material = obj.Material;
            plate.ThicknessMM = thicknessMM;
            return plate;
        }
    }
}
