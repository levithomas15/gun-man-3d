using UnityEditor;
using UnityEngine;

namespace GunMan.EditorTools
{
    /// <summary>Creates particle prefabs + materials for impacts, explosions, muzzle flashes and the FxLibrary prefab.</summary>
    public static class FxBuilder
    {
        public class FxAssets
        {
            public Material additive, smoke, tracer, bulletHole, rocketFlame;
            public GameObject impactPrefab, explosionPrefab, bulletHolePrefab, fxLibraryPrefab;
        }

        public static FxAssets Build()
        {
            GunManBootstrap.EnsureFolder(BuildUtil.FxMatDir);
            var tex = BuildUtil.DefaultParticleTexture();
            var fx = new FxAssets();

            fx.additive = BuildUtil.CreateMaterial($"{BuildUtil.FxMatDir}/FX_Additive.mat", "Universal Render Pipeline/Particles/Unlit", m =>
            {
                m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", Color.white);
                BuildUtil.SetupParticleTransparent(m, true);
            });
            fx.smoke = BuildUtil.CreateMaterial($"{BuildUtil.FxMatDir}/FX_Smoke.mat", "Universal Render Pipeline/Particles/Unlit", m =>
            {
                m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", new Color(0.6f, 0.6f, 0.6f, 0.6f));
                BuildUtil.SetupParticleTransparent(m, false);
            });
            fx.rocketFlame = fx.additive;
            fx.tracer = BuildUtil.CreateMaterial($"{BuildUtil.FxMatDir}/FX_Tracer.mat", "Universal Render Pipeline/Particles/Unlit", m =>
            {
                m.SetColor("_BaseColor", new Color(1f, 0.85f, 0.45f, 0.9f));
                BuildUtil.SetupParticleTransparent(m, true);
            });
            fx.bulletHole = BuildUtil.CreateMaterial($"{BuildUtil.FxMatDir}/FX_BulletHole.mat", "Universal Render Pipeline/Unlit", m =>
            {
                m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", new Color(0.05f, 0.04f, 0.03f, 0.9f));
                GunManBootstrap.SetTransparent(m);
                m.renderQueue = 3001;
            });

            fx.impactPrefab = BuildImpact(fx);
            fx.explosionPrefab = BuildExplosion(fx);
            fx.bulletHolePrefab = BuildBulletHole(fx);

            var lib = new GameObject("FxLibrary");
            var comp = lib.AddComponent<FxLibrary>();
            comp.impactPrefab = fx.impactPrefab;
            comp.explosionPrefab = fx.explosionPrefab;
            comp.bulletHolePrefab = fx.bulletHolePrefab;
            comp.tracerMaterial = fx.tracer;
            fx.fxLibraryPrefab = BuildUtil.SavePrefab(lib, "FxLibrary");
            return fx;
        }

        static GameObject BuildImpact(FxAssets fx)
        {
            var root = new GameObject("FX_Impact");
            var sparks = AddParticles(root, "Sparks", fx.additive, main =>
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 7f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.55f, 0.2f));
                main.gravityModifier = 1.2f;
            }, burst: 14, coneAngle: 35f);
            var dust = AddParticles(root, "Dust", fx.smoke, main =>
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.65f, 0.6f, 0.5f));
                main.gravityModifier = -0.05f;
            }, burst: 5, coneAngle: 40f, sizeGrow: 2.2f, fadeOut: true);
            return BuildUtil.SavePrefab(root, "FX_Impact");
        }

        static GameObject BuildExplosion(FxAssets fx)
        {
            var root = new GameObject("FX_Explosion");
            AddParticles(root, "Fire", fx.additive, main =>
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 7f);
                main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.3f), new Color(1f, 0.4f, 0.1f));
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }, burst: 36, coneAngle: 180f, sizeGrow: 1.6f, fadeOut: true, sphere: true);
            AddParticles(root, "Smoke", fx.smoke, main =>
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.25f, 0.23f, 0.22f, 0.75f), new Color(0.5f, 0.48f, 0.45f, 0.6f));
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.gravityModifier = -0.08f;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }, burst: 26, coneAngle: 180f, sizeGrow: 2.4f, fadeOut: true, sphere: true, startDelay: 0.05f);
            AddParticles(root, "Sparks", fx.additive, main =>
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(9f, 22f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.5f), new Color(1f, 0.6f, 0.2f));
                main.gravityModifier = 1.5f;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }, burst: 45, coneAngle: 180f, sphere: true, stretched: true);
            return BuildUtil.SavePrefab(root, "FX_Explosion");
        }

        static GameObject BuildBulletHole(FxAssets fx)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "FX_BulletHole";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.localScale = Vector3.one * 0.09f;
            var r = quad.GetComponent<MeshRenderer>();
            r.sharedMaterial = fx.bulletHole;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return BuildUtil.SavePrefab(quad, "FX_BulletHole");
        }

        public static ParticleSystem AddParticles(GameObject parent, string name, Material mat, System.Action<ParticleSystem.MainModule> setupMain,
            int burst = 10, float coneAngle = 25f, float sizeGrow = 1f, bool fadeOut = false, bool sphere = false, bool stretched = false,
            float startDelay = 0f, bool loop = false, float rateOverDistance = 0f, bool worldSpace = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = loop;
            main.playOnAwake = true;
            main.duration = 1f;
            main.startDelay = startDelay;
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.maxParticles = 500;
            setupMain?.Invoke(main);

            var emission = ps.emission;
            emission.enabled = true;
            if (loop || rateOverDistance > 0f)
            {
                emission.rateOverTime = loop && rateOverDistance <= 0f ? burst : 0f;
                emission.rateOverDistance = rateOverDistance;
            }
            else
            {
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });
            }

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = sphere ? ParticleSystemShapeType.Sphere : ParticleSystemShapeType.Cone;
            shape.radius = sphere ? 0.15f : 0.02f;
            shape.angle = coneAngle;

            if (sizeGrow != 1f)
            {
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, sizeGrow));
            }
            if (fadeOut)
            {
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
                col.color = new ParticleSystem.MinMaxGradient(g);
            }

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.renderMode = stretched ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (stretched) { renderer.velocityScale = 0.06f; renderer.lengthScale = 2.5f; }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        /// <summary>Muzzle flash particles + light, parented to a muzzle transform.</summary>
        public static (ParticleSystem flash, Light light) AddMuzzleFlash(Transform muzzle, FxAssets fx, float size)
        {
            var holder = new GameObject("MuzzleFlash");
            holder.transform.SetParent(muzzle, false);
            var flash = AddParticles(holder, "Flash", fx.additive, main =>
            {
                main.playOnAwake = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.04f, 0.07f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1f);
                main.startSize = new ParticleSystem.MinMaxCurve(size * 0.8f, size * 1.4f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.6f), new Color(1f, 0.7f, 0.3f));
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            }, burst: 3, coneAngle: 15f, worldSpace: false);
            var smoke = AddParticles(holder, "Smoke", fx.smoke, main =>
            {
                main.playOnAwake = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(size * 0.4f, size * 0.7f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.8f, 0.8f, 0.35f));
            }, burst: 3, coneAngle: 12f, sizeGrow: 2.5f, fadeOut: true);
            smoke.transform.SetParent(flash.transform, false); // Play(withChildren) on flash triggers smoke

            var lightGo = new GameObject("MuzzleLight");
            lightGo.transform.SetParent(muzzle, false);
            lightGo.transform.localPosition = new Vector3(0f, 0f, 0.1f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.75f, 0.4f);
            light.intensity = 5f;
            light.range = 4f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            return (flash, light);
        }
    }
}
