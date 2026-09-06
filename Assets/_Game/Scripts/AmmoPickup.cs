using System.Collections;
using UnityEngine;

namespace GunMan
{
    /// <summary>Refills all weapons when the player walks into it. Reappears after a cooldown.</summary>
    public class AmmoPickup : MonoBehaviour
    {
        public float respawnSeconds = 20f;
        public float spinSpeed = 60f;
        public Transform visual;

        bool _taken;

        void Update()
        {
            if (visual != null && !_taken)
            {
                visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
                visual.localPosition = new Vector3(0f, 0.15f + Mathf.Sin(Time.time * 2f) * 0.08f, 0f);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (_taken) return;
            var holder = other.GetComponentInChildren<WeaponHolder>();
            if (holder == null) return;
            holder.RefillAllAmmo();
            FxLibrary.PlayAt(ProceduralAudio.Click("pickup", 1100f, 0.12f), transform.position, 0.7f, 1f, 10f);
            StartCoroutine(Cooldown());
        }

        IEnumerator Cooldown()
        {
            _taken = true;
            if (visual != null) visual.gameObject.SetActive(false);
            yield return new WaitForSeconds(respawnSeconds);
            if (visual != null) visual.gameObject.SetActive(true);
            _taken = false;
        }
    }
}
