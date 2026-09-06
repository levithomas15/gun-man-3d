using System.Collections;
using UnityEngine;

namespace GunMan
{
    public enum FireMode { SemiAuto, FullAuto, Burst }
    public enum SoundProfile { Pistol, Smg, Rifle, Shotgun, Sniper, Launcher, Throw }

    /// <summary>
    /// A single weapon held by the player. Hitscan by default, or fires a projectile prefab.
    /// Input is driven by <see cref="WeaponHolder"/>.
    /// </summary>
    public class Weapon : MonoBehaviour
    {
        [Header("Identity")]
        public string displayName = "Weapon";
        public SoundProfile soundProfile = SoundProfile.Rifle;

        [Header("Firing")]
        public FireMode fireMode = FireMode.FullAuto;
        public float roundsPerMinute = 600f;
        public int pellets = 1;
        public float spreadDegrees = 1.2f;
        public float damage = 20f;
        public float range = 400f;
        public float impactForce = 6f;
        public int burstCount = 4;
        public float burstInterval = 0.09f;
        public bool tracers = true;

        [Header("Projectile (optional)")]
        public GameObject projectilePrefab;
        public float projectileSpeed = 45f;
        [Tooltip("Adds an upward component so thrown objects arc")]
        public float throwUpwardBias = 0.15f;

        [Header("Ammo")]
        public int magazineSize = 30;
        public int reserveAmmo = 120;
        public float reloadTime = 1.7f;
        public bool infiniteReserve = false;

        [Header("View")]
        public float zoomFov = 0f;              // > 0 enables aim zoom on right mouse button
        public float recoilKick = 0.04f;        // metres backwards
        public float recoilPitch = 1.2f;        // degrees camera kick
        public Transform muzzle;
        public ParticleSystem muzzleFlash;
        public Light muzzleLight;

        public int AmmoInMag { get; private set; }
        public int Reserve => reserveAmmo;
        public bool IsReloading { get; private set; }
        public float ReloadProgress { get; private set; }
        public int ShotsFired { get; private set; }

        WeaponHolder _holder;
        AudioSource _audio;
        float _nextFireTime;
        bool _burstRunning;
        bool _triggerWasHeld;
        LayerMask _hitMask;
        Transform _ownerRoot;
        static readonly RaycastHit[] RayBuffer = new RaycastHit[24];

        void Awake()
        {
            AmmoInMag = magazineSize;
            _hitMask = ~LayerMask.GetMask("Player", "Ignore Raycast");
            _audio = GetComponent<AudioSource>();
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.spatialBlend = 0f;
                _audio.playOnAwake = false;
            }
            if (muzzle == null) muzzle = transform;
            if (_ownerRoot == null)
            {
                // the character this weapon belongs to (shots ignore its colliders)
                var owner = GetComponentInParent<Health>();
                _ownerRoot = owner != null ? owner.transform : transform;
            }
        }

        public void Attach(WeaponHolder holder)
        {
            _holder = holder;
        }

        /// <summary>
        /// Makes this weapon belong to another character (e.g. an NPC): its colliders are ignored by the
        /// shots and the given mask replaces the default player mask.
        /// </summary>
        public void SetOwner(GameObject owner, LayerMask hitMask)
        {
            _ownerRoot = owner != null ? owner.transform : transform;
            _hitMask = hitMask;
        }

        /// <summary>Raycast that skips every collider under <paramref name="ignoreRoot"/>.</summary>
        public static bool RaycastIgnoring(Vector3 origin, Vector3 direction, float range, LayerMask mask, Transform ignoreRoot, out RaycastHit hit)
        {
            int n = Physics.RaycastNonAlloc(origin, direction, RayBuffer, range, mask, QueryTriggerInteraction.Ignore);
            hit = default;
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = RayBuffer[i];
                if (h.distance >= best) continue;
                if (ignoreRoot != null && h.collider.transform.IsChildOf(ignoreRoot)) continue;
                best = h.distance;
                hit = h;
            }
            return best < float.MaxValue;
        }

        public void OnEquipped()
        {
            _triggerWasHeld = true; // avoid firing immediately when switching with the trigger held
            if (muzzleLight != null) muzzleLight.enabled = false;
        }

        void OnDisable()
        {
            // coroutines die with the object: never get stuck in a reload / burst
            IsReloading = false;
            ReloadProgress = 0f;
            _burstRunning = false;
            if (muzzleLight != null) muzzleLight.enabled = false;
        }

        public void OnUnequipped()
        {
            StopAllCoroutines();
            IsReloading = false;
            _burstRunning = false;
            if (muzzleLight != null) muzzleLight.enabled = false;
        }

        /// <summary>Called every frame by the holder while this weapon is active.</summary>
        public void Tick(bool triggerHeld, bool reloadPressed, Camera cam)
        {
            if (reloadPressed) TryReload();

            bool triggerPressed = triggerHeld && !_triggerWasHeld;
            _triggerWasHeld = triggerHeld;

            if (IsReloading || _burstRunning) return;

            bool wantsFire = fireMode switch
            {
                FireMode.FullAuto => triggerHeld,
                _ => triggerPressed,
            };
            if (!wantsFire) return;
            if (Time.time < _nextFireTime) return;

            if (AmmoInMag <= 0)
            {
                if (triggerPressed)
                {
                    _audio.PlayOneShot(ProceduralAudio.Click("dry", 1200f, 0.05f), 0.6f);
                    TryReload();
                }
                return;
            }

            float interval = 60f / Mathf.Max(1f, roundsPerMinute);
            _nextFireTime = Time.time + interval;

            if (fireMode == FireMode.Burst) StartCoroutine(BurstRoutine(cam));
            else FireOnce(cam);
        }

        IEnumerator BurstRoutine(Camera cam)
        {
            _burstRunning = true;
            for (int i = 0; i < burstCount && AmmoInMag > 0; i++)
            {
                FireOnce(cam);
                yield return new WaitForSeconds(burstInterval);
            }
            _burstRunning = false;
        }

        /// <summary>Fires one shot aimed from the camera centre (player).</summary>
        public void FireOnce(Camera cam)
        {
            if (cam != null) Fire(cam.transform.position, cam.transform.forward, cam.transform);
            else Fire(muzzle.position, muzzle.forward, muzzle);
        }

        /// <summary>
        /// Fires one shot from <paramref name="origin"/> along <paramref name="forward"/>.
        /// Visuals (tracer, flash, projectile) start at the muzzle. Used by the player (camera ray) and NPCs (muzzle ray).
        /// </summary>
        public void Fire(Vector3 origin, Vector3 forward, Transform basis)
        {
            AmmoInMag--;
            ShotsFired++;
            if (basis == null) basis = muzzle;

            if (projectilePrefab != null)
            {
                Vector3 target = origin + forward * range;
                if (RaycastIgnoring(origin, forward, range, _hitMask, _ownerRoot, out var aimHit))
                    target = aimHit.point;
                Vector3 dir = (target - muzzle.position).normalized;
                dir = (dir + Vector3.up * throwUpwardBias).normalized;
                var go = Instantiate(projectilePrefab, muzzle.position, Quaternion.LookRotation(dir));
                var proj = go.GetComponent<Projectile>();
                if (proj != null) proj.Launch(dir * projectileSpeed, gameObject);
                else if (go.TryGetComponent<Rigidbody>(out var rb)) rb.linearVelocity = dir * projectileSpeed;
            }
            else
            {
                for (int p = 0; p < Mathf.Max(1, pellets); p++)
                {
                    Vector3 dir = ApplySpread(forward, spreadDegrees, basis);
                    Vector3 end = origin + dir * range;
                    if (RaycastIgnoring(origin, dir, range, _hitMask, _ownerRoot, out var hit))
                    {
                        end = hit.point;
                        var dmg = new DamageInfo
                        {
                            Amount = damage,
                            Point = hit.point,
                            Direction = dir,
                            Normal = hit.normal,
                            Force = impactForce,
                            Source = gameObject,
                            IsExplosion = false,
                        };
                        var target = hit.collider.GetComponentInParent<IDamageable>();
                        if (target != null) target.TakeDamage(dmg);
                        else if (hit.rigidbody != null) hit.rigidbody.AddForceAtPosition(dir * impactForce, hit.point, ForceMode.Impulse);
                        bool hitCharacter = target is Health th && (th.GetComponent<PlayerController>() != null || th.GetComponent<NpcCharacter>() != null);
                        FxLibrary.Impact(hit.point, hit.normal, hit.collider, !hitCharacter && (target == null || hit.rigidbody == null));
                        if (target != null) _holder?.RegisterHit(target is Health h && h.IsDead);
                    }
                    if (tracers) FxLibrary.Tracer(muzzle.position, end);
                }
            }

            PlayShotSound();
            if (muzzleFlash != null) muzzleFlash.Play(true);
            if (muzzleLight != null && gameObject.activeInHierarchy) StartCoroutine(FlashLight());
            _holder?.ApplyRecoil(recoilKick, recoilPitch);

            if (AmmoInMag <= 0) TryReload();
        }

        IEnumerator FlashLight()
        {
            muzzleLight.enabled = true;
            yield return new WaitForSeconds(0.045f);
            if (muzzleLight != null) muzzleLight.enabled = false;
        }

        static Vector3 ApplySpread(Vector3 forward, float degrees, Transform basis)
        {
            if (degrees <= 0f) return forward;
            Vector2 r = Random.insideUnitCircle * degrees;
            return (Quaternion.AngleAxis(r.x, basis.up) * Quaternion.AngleAxis(r.y, basis.right) * forward).normalized;
        }

        void PlayShotSound()
        {
            AudioClip clip = soundProfile switch
            {
                SoundProfile.Pistol => ProceduralAudio.Gunshot("pistol", 0.22f, 0.45f, 0.9f, 0.7f),
                SoundProfile.Smg => ProceduralAudio.Gunshot("smg", 0.16f, 0.5f, 0.7f, 0.9f),
                SoundProfile.Rifle => ProceduralAudio.Gunshot("rifle", 0.3f, 0.35f, 1.1f, 0.6f),
                SoundProfile.Shotgun => ProceduralAudio.Gunshot("shotgun", 0.45f, 0.2f, 1.5f, 0.5f),
                SoundProfile.Sniper => ProceduralAudio.Gunshot("sniper", 0.6f, 0.25f, 1.6f, 0.45f),
                SoundProfile.Launcher => ProceduralAudio.Gunshot("launcher", 0.5f, 0.12f, 1.2f, 0.5f),
                SoundProfile.Throw => ProceduralAudio.Click("throw", 400f, 0.08f),
                _ => ProceduralAudio.Gunshot("rifle"),
            };
            _audio.pitch = Random.Range(0.94f, 1.06f);
            _audio.PlayOneShot(clip, soundProfile == SoundProfile.Throw ? 0.4f : 0.8f);
        }

        public void TryReload()
        {
            if (IsReloading) return;
            if (AmmoInMag >= magazineSize) return;
            if (reserveAmmo <= 0 && !infiniteReserve) return;
            StartCoroutine(ReloadRoutine());
        }

        IEnumerator ReloadRoutine()
        {
            IsReloading = true;
            ReloadProgress = 0f;
            _audio.PlayOneShot(ProceduralAudio.Click("reload-out", 900f, 0.07f), 0.5f);
            float t = 0f;
            while (t < reloadTime)
            {
                t += Time.deltaTime;
                ReloadProgress = t / reloadTime;
                yield return null;
            }
            int needed = magazineSize - AmmoInMag;
            int taken = infiniteReserve ? needed : Mathf.Min(needed, reserveAmmo);
            if (!infiniteReserve) reserveAmmo -= taken;
            AmmoInMag += taken;
            _audio.PlayOneShot(ProceduralAudio.Click("reload-in", 1500f, 0.06f), 0.5f);
            IsReloading = false;
            ReloadProgress = 0f;
        }

        public void AddReserveAmmo(int amount)
        {
            reserveAmmo += amount;
        }

        public void RefillFull()
        {
            reserveAmmo = Mathf.Max(reserveAmmo, magazineSize * 4);
            AmmoInMag = magazineSize;
        }
    }
}
