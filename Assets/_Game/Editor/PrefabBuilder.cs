using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

namespace GunMan.EditorTools
{
    /// <summary>Builds weapon, projectile, player, NPC, target and pickup prefabs.</summary>
    public static class PrefabBuilder
    {
        public class WeaponDef
        {
            public string file, name;
            public SoundProfile sound = SoundProfile.Rifle;
            public FireMode mode = FireMode.FullAuto;
            public float rpm = 600, spread = 1f, damage = 20, force = 6, reload = 1.8f, zoom = 0, kick = 0.04f, pitch = 1.2f;
            public int pellets = 1, mag = 30, reserve = 120, burst = 4;
            public float length = 0.8f;              // desired model length in metres
            public string projectile;                // prefab name (built separately)
            public float projSpeed = 40, upBias = 0f;
            public bool tracers = true;
            // orientation tweaks (applied after auto-alignment)
            public Vector3 extraRotation = Vector3.zero;
            public Vector3 extraOffset = Vector3.zero;
        }

        public static readonly WeaponDef[] Weapons =
        {
            new WeaponDef { file = "pew", name = "Pistole", sound = SoundProfile.Pistol, mode = FireMode.SemiAuto, rpm = 420, spread = 0.9f, damage = 24, force = 5, mag = 12, reserve = 96, reload = 1.2f, length = 0.3f, kick = 0.035f, pitch = 1.4f },
            new WeaponDef { file = "mac10", name = "MAC-10", sound = SoundProfile.Smg, mode = FireMode.FullAuto, rpm = 950, spread = 2.4f, damage = 12, force = 4, mag = 32, reserve = 160, reload = 1.6f, length = 0.42f, kick = 0.025f, pitch = 0.7f, extraOffset = new Vector3(0f, -0.02f, 0.1f) },
            new WeaponDef { file = "ak47", name = "AK-47", sound = SoundProfile.Rifle, mode = FireMode.FullAuto, rpm = 600, spread = 1.4f, damage = 27, force = 7, mag = 30, reserve = 120, reload = 1.9f, length = 0.9f, kick = 0.045f, pitch = 1.3f },
            new WeaponDef { file = "shotgun", name = "Schrotflinte", sound = SoundProfile.Shotgun, mode = FireMode.SemiAuto, rpm = 72, pellets = 9, spread = 5f, damage = 13, force = 9, mag = 6, reserve = 36, reload = 2.4f, length = 1.0f, kick = 0.09f, pitch = 3f },
            new WeaponDef { file = "awp", name = "AWP Sniper", sound = SoundProfile.Sniper, mode = FireMode.SemiAuto, rpm = 42, spread = 0.03f, damage = 150, force = 14, mag = 5, reserve = 30, reload = 2.8f, zoom = 14, length = 1.25f, kick = 0.1f, pitch = 3.5f },
            new WeaponDef { file = "rocketlaucher", name = "Raketenwerfer", sound = SoundProfile.Launcher, mode = FireMode.SemiAuto, rpm = 40, mag = 1, reserve = 8, reload = 2.4f, length = 1.05f, projectile = "Proj_Rocket", projSpeed = 42, kick = 0.12f, pitch = 4f, tracers = false },
            new WeaponDef { file = "quadrocket", name = "Quad-Rakete", sound = SoundProfile.Launcher, mode = FireMode.Burst, rpm = 30, burst = 4, mag = 4, reserve = 16, reload = 3.2f, length = 0.7f, projectile = "Proj_MiniRocket", projSpeed = 36, kick = 0.08f, pitch = 2.5f, tracers = false, extraOffset = new Vector3(0.06f, -0.06f, 0.15f) },
            new WeaponDef { file = "nade_low", name = "Granate", sound = SoundProfile.Throw, mode = FireMode.SemiAuto, rpm = 70, mag = 1, reserve = 12, reload = 0.8f, length = 0.13f, projectile = "Proj_Grenade", projSpeed = 17, upBias = 0.35f, kick = 0.02f, pitch = 0.5f, tracers = false, extraOffset = new Vector3(-0.04f, 0.1f, -0.08f) },
            new WeaponDef { file = "ak47variant", name = "AK-47 Custom", sound = SoundProfile.Rifle, mode = FireMode.FullAuto, rpm = 680, spread = 1.1f, damage = 30, force = 8, mag = 30, reserve = 90, reload = 1.8f, length = 0.8f, kick = 0.045f, pitch = 1.2f, extraOffset = new Vector3(0.02f, -0.03f, 0.08f) },
        };

        /// <summary>Rotates/scales a raw model so its longest axis points along +Z with the given length, and centres it.</summary>
        public static GameObject FitModel(GameObject modelAsset, Transform parent, float targetLength, Vector3 extraRotation, out Bounds localBounds)
        {
            var holder = new GameObject("Model");
            holder.transform.SetParent(parent, false);
            var inst = BuildUtil.Instantiate(modelAsset, holder.transform);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            Vector3 baseScale = modelAsset.transform.localScale; // FBX roots may carry a unit scale (e.g. 100)

            // figure out the longest axis of the model in the holder's local space
            var b = BuildUtil.WorldBounds(inst);
            var size = holder.transform.InverseTransformVector(b.size);
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            Quaternion rot = Quaternion.identity;
            if (size.x >= size.y && size.x >= size.z) rot = Quaternion.Euler(0f, -90f, 0f);      // x -> z
            else if (size.y >= size.x && size.y >= size.z) rot = Quaternion.Euler(90f, 0f, 0f);   // y -> z
            inst.transform.localRotation = rot;

            // guns are taller than they are wide: if the model lies flat, roll it upright around the barrel axis
            b = BuildUtil.WorldBounds(inst);
            if (b.size.x > b.size.y * 1.3f) inst.transform.localRotation = Quaternion.Euler(0f, 0f, 90f) * rot;
            inst.transform.localRotation = Quaternion.Euler(extraRotation) * inst.transform.localRotation;

            b = BuildUtil.WorldBounds(inst);
            float longest = Mathf.Max(b.size.x, b.size.y, b.size.z);
            float scale = longest > 0.0001f ? targetLength / longest : 1f;
            inst.transform.localScale = baseScale * scale;
            b = BuildUtil.WorldBounds(inst);
            inst.transform.position += holder.transform.position - b.center;
            b = BuildUtil.WorldBounds(inst);
            localBounds = new Bounds(holder.transform.InverseTransformPoint(b.center), holder.transform.InverseTransformVector(b.size));
            Debug.Log($"[GunMan] FitModel {modelAsset.name}: raw={size} baseScale={baseScale} scale={scale:F3} final={localBounds.size}");
            return holder;
        }

        // ------------------------------------------------------------------ projectiles

        public static Dictionary<string, GameObject> BuildProjectiles(FxBuilder.FxAssets fx)
        {
            var result = new Dictionary<string, GameObject>();

            // Rocket: uses the rocket sub-mesh of the launcher model.
            result["Proj_Rocket"] = BuildRocket(fx, "Proj_Rocket", "rocketlaucher", "rocketbullet_low", 0.45f, 5.5f, 130f, 20f);
            result["Proj_MiniRocket"] = BuildRocket(fx, "Proj_MiniRocket", "quadrocket", null, 0.3f, 3.5f, 60f, 12f);

            // Grenade
            var nade = new GameObject("Proj_Grenade");
            var nadeModel = BuildUtil.LoadModel(BuildUtil.GunsFbx, "nade_low");
            FitModel(nadeModel, nade.transform, 0.14f, Vector3.zero, out var nb);
            var col = nade.AddComponent<SphereCollider>();
            col.radius = 0.07f;
            var mat = new PhysicsMaterial("Grenade") { bounciness = 0.35f, dynamicFriction = 0.6f, staticFriction = 0.6f };
            try
            {
                string pmPath = $"{BuildUtil.PrefabDir}/PM_Grenade.physicsMaterial";
                if (AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(pmPath) == null) AssetDatabase.CreateAsset(mat, pmPath);
                col.material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(pmPath) ?? mat;
            }
            catch (System.Exception e) { Debug.LogWarning($"[GunMan] physics material asset failed: {e.Message}"); }
            var rb = nade.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.angularDamping = 0.5f;
            var proj = nade.AddComponent<Projectile>();
            proj.explosionRadius = 6f;
            proj.explosionDamage = 140f;
            proj.explosionForce = 22f;
            proj.explodeOnImpact = false;
            proj.fuseSeconds = 2.6f;
            proj.useGravity = true;
            result["Proj_Grenade"] = BuildUtil.SavePrefab(nade, "Proj_Grenade");
            return result;
        }

        static GameObject BuildRocket(FxBuilder.FxAssets fx, string name, string modelFile, string childName, float length, float radius, float damage, float force)
        {
            var root = new GameObject(name);
            var modelAsset = BuildUtil.LoadModel(BuildUtil.GunsFbx, modelFile);
            GameObject visualSource = modelAsset;
            if (childName != null)
            {
                var child = modelAsset.transform.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == childName);
                if (child != null) visualSource = child.gameObject;
            }
            if (visualSource == modelAsset && childName != null)
                Debug.LogWarning($"[GunMan] rocket child {childName} not found in {modelFile}");

            if (visualSource != modelAsset)
            {
                // copy just the sub-mesh
                var holder = new GameObject("Model");
                holder.transform.SetParent(root.transform, false);
                var copy = Object.Instantiate(visualSource, holder.transform);
                copy.name = childName;
                copy.transform.localPosition = Vector3.zero;
                copy.transform.localRotation = Quaternion.identity;
                // keep the world scale the sub-mesh had inside the model (root unit scale etc.)
                copy.transform.localScale = visualSource.transform.lossyScale;
                Vector3 rocketBase = copy.transform.localScale;
                var b = BuildUtil.WorldBounds(copy);
                var size = b.size;
                Quaternion rot = Quaternion.identity;
                if (size.x >= size.y && size.x >= size.z) rot = Quaternion.Euler(0f, -90f, 0f);
                else if (size.y >= size.x && size.y >= size.z) rot = Quaternion.Euler(90f, 0f, 0f);
                copy.transform.localRotation = rot;
                b = BuildUtil.WorldBounds(copy);
                float longest = Mathf.Max(b.size.x, b.size.y, b.size.z);
                copy.transform.localScale = rocketBase * (length / Mathf.Max(0.0001f, longest));
                b = BuildUtil.WorldBounds(copy);
                copy.transform.position += root.transform.position - b.center;
            }
            else
            {
                // fallback: simple capsule
                var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.DestroyImmediate(cap.GetComponent<Collider>());
                cap.transform.SetParent(root.transform, false);
                cap.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                cap.transform.localScale = new Vector3(0.08f, length * 0.5f, 0.08f);
                cap.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Guns/rocketlaucher_rocketmat.mat");
            }

            var col = root.AddComponent<CapsuleCollider>();
            col.direction = 2;
            col.radius = 0.05f;
            col.height = length;
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.useGravity = false;
            var proj = root.AddComponent<Projectile>();
            proj.explosionRadius = radius;
            proj.explosionDamage = damage;
            proj.explosionForce = force;
            proj.explodeOnImpact = true;
            proj.fuseSeconds = 0f;
            proj.useGravity = false;

            // trail
            var trail = FxBuilder.AddParticles(root, "Trail", fx.smoke, main =>
            {
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.85f, 0.85f, 0.6f));
            }, burst: 0, coneAngle: 8f, sizeGrow: 3f, fadeOut: true, loop: true, rateOverDistance: 12f);
            trail.transform.localPosition = new Vector3(0f, 0f, -length * 0.5f);
            trail.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var flame = FxBuilder.AddParticles(root, "Flame", fx.additive, main =>
            {
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.15f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.25f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.4f), new Color(1f, 0.5f, 0.15f));
            }, burst: 60, coneAngle: 10f, fadeOut: true, loop: true, worldSpace: false);
            flame.transform.localPosition = new Vector3(0f, 0f, -length * 0.5f);
            flame.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var light = new GameObject("Glow").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.transform.localPosition = new Vector3(0f, 0f, -length * 0.4f);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.65f, 0.3f);
            light.intensity = 3f;
            light.range = 5f;
            proj.trail = trail;
            return BuildUtil.SavePrefab(root, name);
        }

        // ------------------------------------------------------------------ weapons

        public static List<GameObject> BuildWeapons(FxBuilder.FxAssets fx, Dictionary<string, GameObject> projectiles)
        {
            var list = new List<GameObject>();
            foreach (var def in Weapons)
            {
                var asset = BuildUtil.LoadModel(BuildUtil.GunsFbx, def.file);
                if (asset == null) continue;
                var root = new GameObject($"W_{def.file}");
                var holder = FitModel(asset, root.transform, def.length, def.extraRotation, out var lb);
                holder.transform.localPosition += def.extraOffset;
                // place so the grip area is roughly at the holder origin: shift model forward so its back sits near origin
                holder.transform.localPosition += new Vector3(0f, 0f, lb.extents.z * 0.35f);

                var muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(root.transform, false);
                var wb = BuildUtil.WorldBounds(holder);
                var localMax = root.transform.InverseTransformPoint(new Vector3(wb.center.x, wb.max.y, wb.max.z));
                muzzle.localPosition = new Vector3(0f, localMax.y - lb.size.y * 0.18f, localMax.z);

                var (flash, light) = FxBuilder.AddMuzzleFlash(muzzle, fx, def.projectile != null ? 0.45f : Mathf.Lerp(0.18f, 0.35f, def.length));

                var w = root.AddComponent<Weapon>();
                w.displayName = def.name;
                w.soundProfile = def.sound;
                w.fireMode = def.mode;
                w.roundsPerMinute = def.rpm;
                w.pellets = def.pellets;
                w.spreadDegrees = def.spread;
                w.damage = def.damage;
                w.impactForce = def.force;
                w.burstCount = def.burst;
                w.tracers = def.tracers;
                w.magazineSize = def.mag;
                w.reserveAmmo = def.reserve;
                w.reloadTime = def.reload;
                w.zoomFov = def.zoom;
                w.recoilKick = def.kick;
                w.recoilPitch = def.pitch;
                w.muzzle = muzzle;
                w.muzzleFlash = flash;
                w.muzzleLight = light;
                w.projectileSpeed = def.projSpeed;
                w.throwUpwardBias = def.upBias;
                if (def.projectile != null && projectiles.TryGetValue(def.projectile, out var pp)) w.projectilePrefab = pp;

                var audio = root.AddComponent<AudioSource>();
                audio.spatialBlend = 0f;
                audio.playOnAwake = false;

                // weapon model must not cast shadows onto the world in a weird way / render behind walls: keep simple
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                list.Add(BuildUtil.SavePrefab(root, $"W_{def.file}"));
            }
            return list;
        }

        // ------------------------------------------------------------------ player

        public static GameObject BuildPlayer(List<GameObject> weaponPrefabs)
        {
            int playerLayer = BuildUtil.EnsureLayer("Player");
            var root = new GameObject("Player");
            root.layer = playerLayer;
            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.4f;
            var pc = root.AddComponent<PlayerController>();
            var health = root.AddComponent<Health>();
            health.maxHealth = 100f;
            health.respawnDelay = -1f;           // PlayerController handles death + respawn
            health.applyImpactForceToRigidbody = false;
            health.regenPerSecond = 6f;
            health.regenDelay = 6f;

            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            pc.cameraPivot = pivot.transform;

            var camGo = new GameObject("PlayerCamera");
            camGo.transform.SetParent(pivot.transform, false);
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 72f;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 1500f;
            cam.cullingMask = ~(1 << playerLayer);
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraShake>();

            var holderGo = new GameObject("WeaponHolder");
            holderGo.transform.SetParent(camGo.transform, false);
            holderGo.transform.localPosition = new Vector3(0.22f, -0.16f, 0.40f);
            var holder = holderGo.AddComponent<WeaponHolder>();
            holder.playerCamera = cam;
            holder.player = pc;
            holderGo.AddComponent<AudioSource>().spatialBlend = 0f;

            for (int i = 0; i < weaponPrefabs.Count; i++)
            {
                var inst = BuildUtil.Instantiate(weaponPrefabs[i], holderGo.transform);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.SetActive(i == 0); // WeaponHolder activates the current weapon at runtime
            }
            return BuildUtil.SavePrefab(root, "Player");
        }

        // ------------------------------------------------------------------ npc

        /// <summary>How an NPC uses a weapon from the weapon table (tuned to be beatable).</summary>
        public class NpcWeaponDef
        {
            public string file;
            public float preferredRange = 12f, spread = 4f, damageFactor = 0.5f;
            public int burst = 3;
            public Vector2 pause = new Vector2(0.9f, 1.8f);
            /// <summary>Extra rotation (degrees) of the weapon around the hand's grip axes, tuned per model.</summary>
            public Vector3 gripRotation = Vector3.zero;
            public Vector3 gripOffset = Vector3.zero;
        }

        public static readonly NpcWeaponDef[] NpcWeapons =
        {
            new NpcWeaponDef { file = "pew", preferredRange = 10f, spread = 3.5f, damageFactor = 0.45f, burst = 2, pause = new Vector2(1.0f, 1.8f) },
            new NpcWeaponDef { file = "mac10", preferredRange = 8f, spread = 6.5f, damageFactor = 0.35f, burst = 6, pause = new Vector2(1.4f, 2.4f) },
            new NpcWeaponDef { file = "ak47", preferredRange = 15f, spread = 4.5f, damageFactor = 0.35f, burst = 3, pause = new Vector2(1.2f, 2.2f) },
            new NpcWeaponDef { file = "shotgun", preferredRange = 6f, spread = 6f, damageFactor = 0.25f, burst = 1, pause = new Vector2(2.0f, 3.0f) },
            new NpcWeaponDef { file = "awp", preferredRange = 28f, spread = 1.2f, damageFactor = 0.3f, burst = 1, pause = new Vector2(3.0f, 4.5f) },
        };

        public class NpcPrefabs
        {
            public GameObject unarmed;
            public List<GameObject> armed = new List<GameObject>();
        }

        static AnimatorController BuildNpcAnimator()
        {
            GunManBootstrap.EnsureFolder(BuildUtil.AnimDir);
            var clips = AssetDatabase.LoadAllAssetRepresentationsAtPath(BuildUtil.UalFbx).OfType<AnimationClip>().ToDictionary(c => c.name, c => c);
            AnimationClip Clip(string n)
            {
                if (clips.TryGetValue(n, out var c)) return c;
                Debug.LogWarning($"[GunMan] animation clip {n} missing");
                return null;
            }

            string ctrlPath = $"{BuildUtil.AnimDir}/NpcController.controller";
            AssetDatabase.DeleteAsset(ctrlPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Revive", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Aiming", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Reload", AnimatorControllerParameterType.Trigger);
            var sm = ctrl.layers[0].stateMachine;

            var locomotion = ctrl.CreateBlendTreeInController("Locomotion", out var tree);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(Clip("Idle_Loop"), 0f);
            tree.AddChild(Clip("Walk_Loop"), 0.5f);
            tree.AddChild(Clip("Jog_Fwd_Loop"), 1f);
            sm.defaultState = locomotion;

            var hit = sm.AddState("Hit");
            hit.motion = Clip("Hit_Chest");
            var toHit = sm.AddAnyStateTransition(hit);
            toHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            toHit.canTransitionToSelf = false;
            toHit.duration = 0.08f;
            toHit.hasExitTime = false;
            var hitBack = hit.AddTransition(locomotion);
            hitBack.hasExitTime = true;
            hitBack.exitTime = 0.75f;
            hitBack.duration = 0.15f;

            var death = sm.AddState("Death");
            death.motion = Clip("Death01");
            var toDeath = sm.AddAnyStateTransition(death);
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Die");
            toDeath.canTransitionToSelf = false;
            toDeath.duration = 0.05f;
            toDeath.hasExitTime = false;
            var revive = death.AddTransition(locomotion);
            revive.AddCondition(AnimatorConditionMode.If, 0f, "Revive");
            revive.hasExitTime = false;
            revive.duration = 0.1f;

            // ---- upper body layer: pistol poses for armed NPCs (weight is set at runtime)
            string maskPath = $"{BuildUtil.AnimDir}/UpperBody.mask";
            AssetDatabase.DeleteAsset(maskPath);
            var mask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            foreach (var part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers })
                mask.SetHumanoidBodyPartActive(part, true);
            AssetDatabase.CreateAsset(mask, maskPath);

            var armsSm = new AnimatorStateMachine { name = "Arms", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(armsSm, ctrl);
            var armsLayer = new AnimatorControllerLayer
            {
                name = "Arms",
                avatarMask = mask,
                defaultWeight = 0f,
                blendingMode = AnimatorLayerBlendingMode.Override,
                stateMachine = armsSm,
            };
            ctrl.AddLayer(armsLayer);

            var relaxed = armsSm.AddState("PistolIdle");
            relaxed.motion = Clip("Pistol_Idle_Loop");
            var aim = armsSm.AddState("PistolAim");
            aim.motion = Clip("Pistol_Aim_Neutral");
            armsSm.defaultState = relaxed;
            var toAim = relaxed.AddTransition(aim);
            toAim.AddCondition(AnimatorConditionMode.If, 0f, "Aiming");
            toAim.hasExitTime = false;
            toAim.duration = 0.2f;
            var toRelaxed = aim.AddTransition(relaxed);
            toRelaxed.AddCondition(AnimatorConditionMode.IfNot, 0f, "Aiming");
            toRelaxed.hasExitTime = false;
            toRelaxed.duration = 0.3f;

            var shoot = armsSm.AddState("PistolShoot");
            shoot.motion = Clip("Pistol_Shoot");
            var toShoot = armsSm.AddAnyStateTransition(shoot);
            toShoot.AddCondition(AnimatorConditionMode.If, 0f, "Shoot");
            toShoot.canTransitionToSelf = true;
            toShoot.duration = 0.03f;
            toShoot.hasExitTime = false;
            var shootBack = shoot.AddTransition(aim);
            shootBack.hasExitTime = true;
            shootBack.exitTime = 0.6f;
            shootBack.duration = 0.1f;

            var reload = armsSm.AddState("PistolReload");
            reload.motion = Clip("Pistol_Reload");
            var toReload = armsSm.AddAnyStateTransition(reload);
            toReload.AddCondition(AnimatorConditionMode.If, 0f, "Reload");
            toReload.canTransitionToSelf = false;
            toReload.duration = 0.1f;
            toReload.hasExitTime = false;
            var reloadBack = reload.AddTransition(aim);
            reloadBack.hasExitTime = true;
            reloadBack.exitTime = 0.9f;
            reloadBack.duration = 0.15f;

            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        /// <summary>Builds the unarmed NPC prefab plus one armed variant per <see cref="NpcWeapons"/> entry.</summary>
        public static AnimationClip LoadUalClip(string name) =>
            AssetDatabase.LoadAllAssetRepresentationsAtPath(BuildUtil.UalFbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);

        public static NpcPrefabs BuildNpcs(List<GameObject> weaponPrefabs)
        {
            var ctrl = BuildNpcAnimator();
            var result = new NpcPrefabs { unarmed = BuildNpc(ctrl, null, null) };
            foreach (var def in NpcWeapons)
            {
                var weapon = weaponPrefabs.FirstOrDefault(w => w.name == $"W_{def.file}");
                if (weapon == null)
                {
                    Debug.LogWarning($"[GunMan] NPC weapon W_{def.file} not found");
                    continue;
                }
                result.armed.Add(BuildNpc(ctrl, weapon, def));
            }
            return result;
        }

        static GameObject BuildNpc(AnimatorController ctrl, GameObject weaponPrefab, NpcWeaponDef weaponDef)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(BuildUtil.UalFbx);
            string prefabName = weaponPrefab != null ? $"NPC_{weaponDef.file}" : "NPC";

            var root = new GameObject(prefabName);
            var col = root.AddComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.radius = 0.32f;
            col.center = new Vector3(0f, 0.9f, 0f);
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.speed = 1.6f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            var health = root.AddComponent<Health>();
            health.maxHealth = 100f;
            health.respawnDelay = 5f;
            health.applyImpactForceToRigidbody = false;
            var npc = root.AddComponent<NpcCharacter>();

            var inst = BuildUtil.Instantiate(model, root.transform, "Model");
            inst.transform.localPosition = Vector3.zero;
            var b = BuildUtil.WorldBounds(inst);
            float h = b.size.y;
            if (h > 0.01f && (h < 1.4f || h > 2.3f))
            {
                float s = 1.8f / h;
                inst.transform.localScale = model.transform.localScale * s;
                Debug.Log($"[GunMan] NPC model height {h:F2} -> scaled by {s:F2}");
            }
            b = BuildUtil.WorldBounds(inst);
            inst.transform.localPosition = new Vector3(0f, -b.min.y, 0f);

            var animator = inst.GetComponent<Animator>();
            if (animator == null) animator = inst.AddComponent<Animator>();
            animator.runtimeAnimatorController = ctrl;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            npc.animator = animator;
            npc.tintRenderers = inst.GetComponentsInChildren<Renderer>();
            foreach (var r in npc.tintRenderers)
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;

            if (weaponPrefab != null)
            {
                var weapon = AttachWeaponToHand(inst, weaponPrefab, weaponDef, LoadUalClip("Pistol_Aim_Neutral"));
                npc.weapon = weapon;
                npc.role = NpcRole.Soldier;
                npc.preferredRange = weaponDef.preferredRange;
                npc.burstShots = weaponDef.burst;
                npc.burstPauseRange = weaponDef.pause;
                health.respawnDelay = 12f;
            }

            return BuildUtil.SavePrefab(root, prefabName);
        }

        /// <summary>
        /// Parents a weapon prefab to the right hand bone. The model is posed with the aiming clip (edit-mode sampling),
        /// the weapon is aligned with the character's forward axis / world up in that pose and placed in the palm;
        /// the resulting hand-local offset is what gets saved. Falls back to a bind-pose heuristic if sampling fails.
        /// </summary>
        static Weapon AttachWeaponToHand(GameObject model, GameObject weaponPrefab, NpcWeaponDef def, AnimationClip aimClip)
        {
            var bones = model.GetComponentsInChildren<Transform>(true);
            Transform Bone(string n) => bones.FirstOrDefault(t => t.name == n);
            var hand = Bone("hand_r");
            var middle = Bone("middle_01_r") ?? Bone("index_01_r");
            var index = Bone("index_01_r");
            var pinky = Bone("pinky_01_r");
            if (hand == null)
            {
                Debug.LogWarning("[GunMan] hand_r bone not found, weapon attached to model root");
                hand = model.transform;
            }

            Vector3 bindHandPos = hand.position;
            bool sampled = false;
            if (aimClip != null && model.GetComponent<Animator>() != null)
            {
                try
                {
                    AnimationMode.StartAnimationMode();
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(model, aimClip, Mathf.Min(0.2f, aimClip.length * 0.5f));
                    AnimationMode.EndSampling();
                    sampled = Vector3.Distance(bindHandPos, hand.position) > 0.05f;
                    if (!sampled) Debug.LogWarning($"[GunMan] aim clip sampling did not move hand_r ({bindHandPos} -> {hand.position})");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[GunMan] aim clip sampling failed: {e.Message}");
                }
            }

            Vector3 fingers = middle != null ? (middle.position - hand.position).normalized : hand.up;
            Vector3 palm = index != null && pinky != null
                ? Vector3.Cross((pinky.position - hand.position).normalized, (index.position - hand.position).normalized).normalized
                : -hand.forward;

            Quaternion worldRot;
            if (sampled)
            {
                // aiming pose: barrel along the character's view direction, top up
                worldRot = Quaternion.LookRotation(model.transform.forward, Vector3.up);
            }
            else
            {
                // bind pose (T-pose, palm down): barrel along the metacarpals, top towards the thumb side
                Vector3 up = Vector3.Cross(fingers, -palm).normalized;
                worldRot = Quaternion.LookRotation(fingers, up);
            }
            worldRot *= Quaternion.Euler(def.gripRotation);
            Vector3 worldPos = hand.position + fingers * 0.05f + palm * 0.03f + worldRot * def.gripOffset;

            // hand-local placement (valid in every pose since the weapon is a child of the bone)
            Vector3 localPos = hand.InverseTransformPoint(worldPos);
            Quaternion localRot = Quaternion.Inverse(hand.rotation) * worldRot;
            var ls = hand.lossyScale;
            Debug.Log($"[GunMan] NPC weapon {weaponPrefab.name}: sampled={sampled} fingers={fingers} palm={palm} localPos={localPos} localRot={localRot.eulerAngles}");

            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();

            var inst = BuildUtil.Instantiate(weaponPrefab, hand);
            inst.name = weaponPrefab.name;
            inst.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
            inst.transform.localRotation = localRot;
            inst.transform.localPosition = localPos;

            var weapon = inst.GetComponent<Weapon>();
            weapon.damage *= def.damageFactor;
            weapon.spreadDegrees = def.spread;
            weapon.infiniteReserve = true;
            weapon.zoomFov = 0f;
            var audio = inst.GetComponent<AudioSource>();
            if (audio != null)
            {
                audio.spatialBlend = 1f;
                audio.minDistance = 3f;
                audio.maxDistance = 70f;
                audio.rolloffMode = AudioRolloffMode.Linear;
            }
            return weapon;
        }

        /// <summary>Poses a scene NPC with the aiming clip for previews. Call <see cref="AnimationMode.StopAnimationMode"/> afterwards.</summary>
        public static bool SampleAimPose(NpcCharacter npc)
        {
            var clip = LoadUalClip("Pistol_Aim_Neutral");
            if (npc == null || npc.animator == null || clip == null) return false;
            if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(npc.animator.gameObject, clip, Mathf.Min(0.2f, clip.length * 0.5f));
            AnimationMode.EndSampling();
            return true;
        }

        // ------------------------------------------------------------------ target

        public static GameObject BuildTarget()
        {
            var root = new GameObject("Target");
            var pivot = new GameObject("Pivot");
            pivot.transform.SetParent(root.transform, false);

            // post
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Post";
            post.transform.SetParent(pivot.transform, false);
            post.transform.localScale = new Vector3(0.08f, 0.55f, 0.08f);
            post.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            post.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Village/MI_WoodTrim.mat");

            // plate with rings (separate discs)
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Village/MI_WoodTrim_Wear.mat");
            var ringMats = new[]
            {
                BuildUtil.CreateMaterial($"{BuildUtil.FxMatDir}/Target_White.mat", "Universal Render Pipeline/Lit", m => { m.SetColor("_BaseColor", new Color(0.95f, 0.95f, 0.9f)); m.SetFloat("_Smoothness", 0.3f); }),
                BuildUtil.CreateMaterial($"{BuildUtil.FxMatDir}/Target_Red.mat", "Universal Render Pipeline/Lit", m => { m.SetColor("_BaseColor", new Color(0.85f, 0.12f, 0.1f)); m.SetFloat("_Smoothness", 0.3f); }),
            };
            float[] radii = { 0.45f, 0.34f, 0.23f, 0.12f };
            for (int i = 0; i < radii.Length; i++)
            {
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = $"Ring{i}";
                disc.transform.SetParent(pivot.transform, false);
                disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                disc.transform.localScale = new Vector3(radii[i] * 2f, 0.015f, radii[i] * 2f);
                disc.transform.localPosition = new Vector3(0f, 1.55f, -0.015f * i);
                disc.GetComponent<MeshRenderer>().sharedMaterial = ringMats[i % 2];
                if (i > 0) Object.DestroyImmediate(disc.GetComponent<Collider>());
            }
            var health = root.AddComponent<Health>();
            health.maxHealth = 30f;
            health.respawnDelay = 4f;
            var t = root.AddComponent<PopupTarget>();
            t.pivot = pivot.transform;
            return BuildUtil.SavePrefab(root, "Target");
        }

        // ------------------------------------------------------------------ ammo

        public static GameObject BuildAmmoPickup()
        {
            var root = new GameObject("AmmoPickup");
            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.9f;
            trigger.center = new Vector3(0f, 0.6f, 0f);
            var visualHolder = new GameObject("Visual");
            visualHolder.transform.SetParent(root.transform, false);
            var model = BuildUtil.LoadModel(BuildUtil.GunsFbx, "ammobox_low");
            var inst = BuildUtil.Instantiate(model, visualHolder.transform);
            var b = BuildUtil.WorldBounds(inst);
            float longest = Mathf.Max(b.size.x, b.size.y, b.size.z);
            inst.transform.localScale = model.transform.localScale * (0.5f / Mathf.Max(0.0001f, longest));
            b = BuildUtil.WorldBounds(inst);
            inst.transform.position += visualHolder.transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
            var pickup = root.AddComponent<AmmoPickup>();
            pickup.visual = visualHolder.transform;
            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            glow.type = LightType.Point;
            glow.color = new Color(0.4f, 0.9f, 0.5f);
            glow.intensity = 2f;
            glow.range = 3f;
            return BuildUtil.SavePrefab(root, "AmmoPickup");
        }

        // ------------------------------------------------------------------ crate

        public static GameObject BuildCrate()
        {
            var model = BuildUtil.LoadModel(BuildUtil.VillageFbx, "Prop_Crate");
            var root = new GameObject("Crate");
            var inst = BuildUtil.Instantiate(model, root.transform);
            var b = BuildUtil.WorldBounds(inst);
            inst.transform.position += root.transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
            foreach (var mc in inst.GetComponentsInChildren<MeshCollider>()) mc.convex = true;
            b = BuildUtil.WorldBounds(inst);
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 25f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var health = root.AddComponent<Health>();
            health.maxHealth = 80f;
            health.respawnDelay = 25f;
            return BuildUtil.SavePrefab(root, "Crate");
        }
    }
}
