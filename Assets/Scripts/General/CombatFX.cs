using UnityEngine;

// Pequenos efeitos de combate gerados por código:
//   Sparks - faíscas no impacto de um soco
//   Dust   - pó ao cair no chão / ao esquivar
//   Ghost  - "fantasma" do sprite (rasto da esquiva)
public static class CombatFX
{
    private static ParticleSystem sparks;
    private static ParticleSystem dust;
    private static Material material;

    private static Material Mat
    {
        get
        {
            if (material == null) material = Resources.Load<Material>("FX/FXParticle");
            return material;
        }
    }

    public static void Sparks(Vector3 position, float direction, Color color, int count = 10, float speed = 12f)
    {
        ParticleSystem ps = Get(ref sparks, "FX_Sparks", 2.5f);
        var p = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(-55f, 55f) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle) * direction, Mathf.Sin(angle) + 0.25f);
            p.position = position + (Vector3)(Random.insideUnitCircle * 0.15f);
            p.velocity = dir * Random.Range(speed * 0.5f, speed);
            p.startSize = Random.Range(0.1f, 0.22f);
            p.startLifetime = Random.Range(0.12f, 0.3f);
            p.startColor = Color.Lerp(color, Color.white, Random.value * 0.5f);
            ps.Emit(p, 1);
        }
    }

    public static void Dust(Vector3 position, int count = 8, float spread = 1f)
    {
        ParticleSystem ps = Get(ref dust, "FX_Dust", -0.3f);
        var p = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            p.position = position + new Vector3(Random.Range(-0.4f, 0.4f) * spread, 0f, 0f);
            p.velocity = new Vector2(side * Random.Range(1.5f, 4.5f) * spread, Random.Range(0.3f, 1.8f));
            p.startSize = Random.Range(0.18f, 0.4f);
            p.startLifetime = Random.Range(0.3f, 0.55f);
            p.startColor = new Color(0.62f, 0.58f, 0.52f, 0.7f);
            ps.Emit(p, 1);
        }
    }

    public static void Ghost(SpriteRenderer source, Color tint, float lifetime = 0.22f)
    {
        if (source == null || source.sprite == null) return;
        GameObject go = new GameObject("FX_Ghost");
        go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        go.transform.localScale = source.transform.lossyScale;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = source.sprite;
        sr.flipX = source.flipX;
        sr.sharedMaterial = Mat != null ? Mat : source.sharedMaterial;
        sr.sortingLayerID = source.sortingLayerID;
        sr.sortingOrder = source.sortingOrder - 1;
        sr.color = tint;
        go.AddComponent<FadeAndDie>().Init(lifetime);
    }

    private static ParticleSystem Get(ref ParticleSystem ps, string name, float gravity)
    {
        if (ps != null) return ps;
        GameObject go = new GameObject(name);
        ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true; // fica sempre a simular; as partículas só nascem via Emit
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = gravity * 0.01f; // a gravidade do projeto é -706: escalar para valores normais
        main.maxParticles = 400;
        main.startSpeed = 0f;

        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = Mat;
        renderer.sortingOrder = 60;
        ps.Play();
        return ps;
    }

    private class FadeAndDie : MonoBehaviour
    {
        private float lifetime, t;
        private SpriteRenderer sr;
        private Color start;

        public void Init(float life)
        {
            lifetime = life;
            sr = GetComponent<SpriteRenderer>();
            start = sr.color;
        }

        private void Update()
        {
            t += Time.deltaTime;
            Color c = start;
            c.a = start.a * (1f - t / lifetime);
            sr.color = c;
            if (t >= lifetime) Destroy(gameObject);
        }
    }
}
