using System.Collections.Generic;
using UnityEngine;

// Objetos em primeiro plano (ex.: colunas da cave): quando o jogador passa por trás,
// abre-se uma "janela" à volta dele onde a coluna fica semitransparente, com uma
// transição suave na borda. O resto da coluna fica normal.
// O efeito é feito no shader "Mayhem/2D/Sprite-Lit-SeeThrough" (mantém a luz 2D).
[RequireComponent(typeof(SpriteRenderer))]
public class OccluderFade : MonoBehaviour
{
    private static Material seeThroughMaterial;
    private SpriteRenderer sr;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (seeThroughMaterial == null) seeThroughMaterial = Resources.Load<Material>("FX/SpriteLitSeeThrough");
        if (seeThroughMaterial != null) sr.sharedMaterial = seeThroughMaterial;
        SeeThroughController.Register(sr);
    }

    private void OnDestroy()
    {
        SeeThroughController.Unregister(sr);
    }

    // Liga o efeito a todos os sprites com parallax que ficam à frente dos lutadores.
    public static void AttachToForegroundParallax(int fighterSortingOrder)
    {
        foreach (Parallax p in Object.FindObjectsByType<Parallax>(FindObjectsSortMode.None))
        {
            SpriteRenderer r = p.GetComponent<SpriteRenderer>();
            if (r != null && r.sortingOrder > fighterSortingOrder && p.GetComponent<OccluderFade>() == null)
                p.gameObject.AddComponent<OccluderFade>();
        }
    }
}

// Envia para o shader a posição do jogador e quanto a janela está aberta.
public class SeeThroughController : MonoBehaviour
{
    private const float Radius = 2.6f;       // raio da janela
    private const float Softness = 1.6f;     // largura da transição suave na borda
    private const float MinAlpha = 0.25f;    // opacidade da coluna no centro
    private const float Darken = 0.6f;       // escurecimento no centro
    private const float OpenSpeed = 4f;
    private const float PixelsPerUnit = 10f; // grelha de píxeis dos sprites do jogo (PPU 10)

    private static readonly int PosId = Shader.PropertyToID("_SeeThroughPos");
    private static readonly int ParamsId = Shader.PropertyToID("_SeeThroughParams");
    private static SeeThroughController instance;
    private static readonly List<SpriteRenderer> occluders = new List<SpriteRenderer>();

    private float open;
    private Vector2 center;
    private PlayerScript cachedPlayer;
    private Collider2D cachedCollider;

    public static void Register(SpriteRenderer occluder)
    {
        if (!occluders.Contains(occluder)) occluders.Add(occluder);
        if (instance == null) instance = new GameObject("SeeThroughController").AddComponent<SeeThroughController>();
    }

    public static void Unregister(SpriteRenderer occluder) => occluders.Remove(occluder);

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        Shader.SetGlobalVector(PosId, Vector4.zero);
    }

    private void LateUpdate()
    {
        PlayerScript player = PlayerScript.Current;
        bool behind = false;
        if (player != null)
        {
            if (player != cachedPlayer)
            {
                cachedPlayer = player;
                cachedCollider = player.GetComponent<Collider2D>();
            }
            Bounds pb = cachedCollider.bounds;
            center = new Vector2(pb.center.x, pb.center.y + 0.3f);
            foreach (SpriteRenderer o in occluders)
            {
                if (o == null) continue;
                Bounds ob = o.bounds;
                // considera "atrás" um pouco antes de tocar, para a janela já estar a abrir
                if (pb.max.x > ob.min.x - 1f && pb.min.x < ob.max.x + 1f && pb.max.y > ob.min.y && pb.min.y < ob.max.y)
                {
                    behind = true;
                    break;
                }
            }
        }

        open = Mathf.MoveTowards(open, behind ? 1f : 0f, OpenSpeed * Time.unscaledDeltaTime);
        float eased = open * open * (3f - 2f * open); // smoothstep na abertura também
        // centro alinhado à grelha de píxeis, para o padrão de dithering não "escorregar"
        Vector2 snapped = new Vector2(Mathf.Round(center.x * PixelsPerUnit), Mathf.Round(center.y * PixelsPerUnit)) / PixelsPerUnit;
        Shader.SetGlobalVector(PosId, new Vector4(snapped.x, snapped.y, eased, PixelsPerUnit));
        Shader.SetGlobalVector(ParamsId, new Vector4(Radius * Mathf.Lerp(0.6f, 1f, eased), Softness, MinAlpha, Darken));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        occluders.Clear();
        instance = null;
    }
}
