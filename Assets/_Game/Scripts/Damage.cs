using UnityEngine;

namespace GunMan
{
    public struct DamageInfo
    {
        public float Amount;
        public Vector3 Point;
        public Vector3 Direction;
        public Vector3 Normal;
        public float Force;
        public GameObject Source;
        public bool IsExplosion;
    }

    public interface IDamageable
    {
        void TakeDamage(in DamageInfo info);
    }
}
