using System.Collections.Generic;
using TMPro;
using UnityEngine;

// IA dos lutadores do Fight Club.
// - Só 1 ou 2 inimigos atacam de cada vez (os outros esperam à volta e vão rodando).
// - Antes de cada soco aparece um aviso por cima da cabeça e o inimigo brilha:
//     "!"  laranja = dá para defender / fazer parry
//     "!!" vermelho = IMPARÁVEL: defender não chega, tens de esquivar
// - Alguns encadeiam 2-3 socos seguidos (cada um com o seu aviso curto).
// - Depois de atacar ficam expostos uns instantes (janela para castigar).
// - Três tipos: Brawler (combos de 2), Heavy (lento, imparáveis, aguenta golpes), Swift (rápido, rajadas de 3, defende muito).
public class EnemyScript : MonoBehaviour
{
    public Rigidbody2D rb;
    public float speed = 5f;
    public Transform spr;
    public int damage = 10;
    [SerializeField] private GameObject popUpPrefab;
    [SerializeField] private AudioSource punchsound;
    [SerializeField] private GameObject particles;
    [SerializeField] private AudioSource radioSource;
    [SerializeField] private GruntScript hurtSound;
    public RuntimeAnimatorController whiteAnimator;
    public RuntimeAnimatorController blackAnimator;
    public RuntimeAnimatorController asianAnimator;
    [SerializeField] private GameObject JaneBody;
    [SerializeField] private GameObject TyroneBody;
    [SerializeField] private GameObject WangBody;

    public enum Archetype { Brawler, Heavy, Swift }

    public struct HitInfo
    {
        public int damage;
        public float knockback;
        public float direction;
        public bool launch;
        public bool finisher;
        public bool guardBreak;
    }

    private class Stats
    {
        public int hp, damage, guardHits, maxExtraHits;
        public float moveSpeed, windup, followupWindup, recover, range, blockChance, knockbackResist;
        public float comboChance, unblockableChance, unblockableWindup, unblockableDamage;
        public bool armoredWindup;
    }

    private static readonly Dictionary<Archetype, Stats> ArchetypeStats = new Dictionary<Archetype, Stats>
    {
        { Archetype.Brawler, new Stats {
            hp = 55, moveSpeed = 6f, damage = 12, windup = 0.42f, followupWindup = 0.24f, recover = 0.5f, range = 2.9f,
            blockChance = 0.2f, guardHits = 3, knockbackResist = 1f, comboChance = 0.45f, maxExtraHits = 1 } },
        { Archetype.Heavy, new Stats {
            hp = 95, moveSpeed = 4.3f, damage = 20, windup = 0.65f, followupWindup = 0.4f, recover = 0.75f, range = 3.2f,
            blockChance = 0.08f, guardHits = 4, knockbackResist = 0.4f, armoredWindup = true,
            unblockableChance = 0.4f, unblockableWindup = 0.85f, unblockableDamage = 1.4f } },
        { Archetype.Swift, new Stats {
            hp = 40, moveSpeed = 8f, damage = 8, windup = 0.3f, followupWindup = 0.18f, recover = 0.4f, range = 2.7f,
            blockChance = 0.5f, guardHits = 2, knockbackResist = 1.2f, comboChance = 0.6f, maxExtraHits = 2 } },
    };

    private static readonly Color BlockableColor = new Color(1f, 0.6f, 0.15f);
    private static readonly Color UnblockableColor = new Color(1f, 0.15f, 0.15f);

    public static readonly List<EnemyScript> All = new List<EnemyScript>();
    private static readonly List<EnemyScript> attackers = new List<EnemyScript>();
    public static int MaxAttackers = 1;
    public static float AggressionScale = 1f; // < 1 = atacam mais vezes

    private enum State { Approach, Wait, Engage, Telegraph, Strike, Recover, Hitstun, Airborne, Knockdown, Staggered, Blocking, Dead }

    private State state = State.Approach;
    private float stateTime;
    private Stats stats;
    private Archetype archetype;
    private bool configured;
    private int life;
    private float damageScale = 1f;
    private int guardCount;
    private float nextAttackAt;
    private float waitOffset;
    private float nextWaitShuffle;
    private bool grounded;
    private float knockdownTime;
    private float staggerTime;
    private int hitsLeft;
    private float currentWindup;
    private bool currentUnblockable;
    private bool strikeLanded;
    private float punchLength = 0.33f;
    private Animator anim;

    // Duração visível do soco (a animação inteira toca neste tempo) e quando o braço "chega".
    private const float JabDuration = 0.38f;
    private const float HeavyStrikeDuration = 0.6f;
    private const float StrikeImpactAt = 0.4f; // fração da animação em que o golpe acerta
    private float StrikeDuration => currentUnblockable ? HeavyStrikeDuration : JabDuration;
    private SpriteRenderer sprite;
    private GameObject body;
    private string currentAnim;
    private float flashUntil;
    private string idleAnim, walkAnim, punchAnim, blockAnim, hitAnim, juggleAnim;
    private TextMeshPro warning;

    public bool IsDead => state == State.Dead;
    private PlayerScript Player => PlayerScript.Current;

    // Chamado pelo FightDirector logo a seguir ao Instantiate.
    public void Setup(Archetype type, float scale)
    {
        archetype = type;
        damageScale = scale;
        configured = true;
    }

    private void Awake()
    {
        anim = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();
    }

    private void OnEnable() => All.Add(this);

    private void OnDisable()
    {
        All.Remove(this);
        ReleaseToken();
    }

    private void Start()
    {
        if (!configured) archetype = (Archetype)Random.Range(0, 3);
        stats = ArchetypeStats[archetype];
        life = stats.hp;

        switch (archetype)
        {
            case Archetype.Brawler: anim.runtimeAnimatorController = whiteAnimator; body = JaneBody; break;
            case Archetype.Heavy: anim.runtimeAnimatorController = blackAnimator; body = TyroneBody; break;
            case Archetype.Swift: anim.runtimeAnimatorController = asianAnimator; body = WangBody; break;
        }
        foreach (AnimationClip clip in anim.runtimeAnimatorController.animationClips)
        {
            string n = clip.name;
            if (n.Contains("Idle")) idleAnim = n;
            else if (n.Contains("Walk")) walkAnim = n;
            else if (n.Contains("Punch")) { punchAnim = n; punchLength = clip.length; }
            else if (n.Contains("Block")) blockAnim = n;
            else if (n.Contains("Juggle")) juggleAnim = n;
            else if (n.Contains("Hit")) hitAnim = n;
        }
        if (archetype == Archetype.Heavy) transform.localScale *= 1.08f;

        CreateWarning();
        nextAttackAt = Time.time + Random.Range(0.4f, 1.0f);
        waitOffset = Random.Range(0f, 1.5f);
        PlayAnim(walkAnim);
    }

    // "!" por cima da cabeça durante o aviso de ataque
    private void CreateWarning()
    {
        GameObject go = new GameObject("Warning");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 2.9f, 0f);
        warning = go.AddComponent<TextMeshPro>();
        warning.font = UIFactory.Font;
        warning.fontSize = 18f;
        warning.alignment = TextAlignmentOptions.Center;
        warning.rectTransform.sizeDelta = new Vector2(4f, 2f);
        warning.sortingOrder = 50;
        warning.outlineWidth = 0.3f;
        warning.outlineColor = Color.black;
        go.SetActive(false);
    }

    // ---------------- Ciclo principal ----------------

    private void Update()
    {
        if (state == State.Dead) return;
        stateTime += Time.deltaTime;
        PlayerScript player = Player;

        if (player == null || player.IsDead)
        {
            ReleaseToken();
            if (state != State.Airborne && state != State.Knockdown) Idle();
            UpdateVisuals();
            return;
        }

        float dx = player.transform.position.x - transform.position.x;
        float dist = Mathf.Abs(dx);

        switch (state)
        {
            case State.Approach:
            case State.Wait:
                FacePlayer(dx);
                if (Time.time >= nextAttackAt && TryTakeToken())
                {
                    SetState(State.Engage);
                    break;
                }
                MoveToSlot(player, dx, dist);
                break;

            case State.Engage:
                FacePlayer(dx);
                if (dist <= stats.range * 0.85f)
                {
                    hitsLeft = Random.value < stats.comboChance ? Random.Range(1, stats.maxExtraHits + 1) : 0;
                    bool unblockable = Random.value < stats.unblockableChance;
                    StartTelegraph(unblockable ? stats.unblockableWindup : stats.windup, unblockable);
                }
                else
                {
                    MoveTowardsX(player.transform.position.x - Mathf.Sign(dx) * stats.range * 0.75f, stats.moveSpeed * 1.2f);
                    if (stateTime > 2.5f) FinishAttackCycle(); // desistiu de chegar lá
                }
                break;

            case State.Telegraph:
                Brake();
                FacePlayer(dx); // segue o jogador enquanto prepara: não dá para simplesmente passar por trás
                if (stateTime >= currentWindup) DoStrike(player, dx, dist);
                break;

            case State.Strike:
                rb.linearVelocity = new Vector2(Mathf.MoveTowards(rb.linearVelocity.x, 0f, 40f * Time.deltaTime), rb.linearVelocity.y);
                // o dano só entra quando o braço está esticado (não no primeiro frame)
                if (!strikeLanded && stateTime >= StrikeDuration * StrikeImpactAt)
                {
                    strikeLanded = true;
                    LandStrike(player, player.transform.position.x - transform.position.x);
                }
                if (stateTime >= StrikeDuration)
                {
                    if (hitsLeft > 0)
                    {
                        hitsLeft--;
                        StartTelegraph(stats.followupWindup, false);
                    }
                    else
                    {
                        SetState(State.Recover);
                        PlayAnim(idleAnim);
                    }
                }
                break;

            case State.Recover:
                Brake();
                if (stateTime >= stats.recover) FinishAttackCycle();
                break;

            case State.Hitstun:
                rb.linearVelocity = new Vector2(Mathf.MoveTowards(rb.linearVelocity.x, 0f, 45f * Time.deltaTime), rb.linearVelocity.y);
                if (stateTime >= 0.36f) BackToNeutral();
                break;

            case State.Staggered:
                Brake();
                if (stateTime >= staggerTime) BackToNeutral();
                break;

            case State.Blocking:
                rb.linearVelocity = new Vector2(Mathf.MoveTowards(rb.linearVelocity.x, 0f, 40f * Time.deltaTime), rb.linearVelocity.y);
                if (stateTime >= 0.3f)
                {
                    // contra-ataque rápido depois de defender
                    if (Random.value < 0.35f && TryTakeToken()) { hitsLeft = 0; StartTelegraph(stats.followupWindup + 0.1f, false); }
                    else BackToNeutral();
                }
                break;

            case State.Airborne:
                if (stateTime > 0.15f && grounded && rb.linearVelocity.y <= 0.1f)
                {
                    StartKnockdown(0.75f);
                    CombatFX.Dust(Feet, 12, 1.3f);
                    CameraFollow.Shake(0.12f, 0.12f);
                }
                break;

            case State.Knockdown:
                Brake();
                if (stateTime >= knockdownTime) BackToNeutral();
                break;
        }

        UpdateVisuals();
    }

    // ---------------- Posicionamento ----------------

    // Os que não estão a atacar ficam a uma distância "de espera", espaçados entre si, e vão mexendo-se um pouco.
    private void MoveToSlot(PlayerScript player, float dx, float dist)
    {
        float side = dx > 0 ? -1f : 1f; // de que lado do jogador estou
        int slot = 0;
        foreach (EnemyScript other in All)
        {
            if (other == this || other.IsDead || attackers.Contains(other)) continue;
            float otherSide = other.transform.position.x < player.transform.position.x ? -1f : 1f;
            if (otherSide != side) continue;
            if (Mathf.Abs(other.transform.position.x - player.transform.position.x) < dist) slot++;
        }

        if (Time.time >= nextWaitShuffle)
        {
            waitOffset = Random.Range(0f, 1.4f);
            nextWaitShuffle = Time.time + Random.Range(1.0f, 2.0f);
        }

        float desired = 4.3f + slot * 1.8f + waitOffset;
        float targetX = player.transform.position.x + side * desired;
        if (Mathf.Abs(transform.position.x - targetX) > 0.35f)
        {
            state = State.Approach;
            MoveTowardsX(targetX, stats.moveSpeed * (dist > 9f ? 1.3f : 0.75f));
        }
        else
        {
            state = State.Wait;
            Brake();
        }
    }

    private void MoveTowardsX(float targetX, float moveSpeed)
    {
        float dir = Mathf.Sign(targetX - transform.position.x);
        float vx = Mathf.MoveTowards(rb.linearVelocity.x, dir * moveSpeed, 50f * Time.deltaTime);
        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
        PlayAnim(walkAnim);
    }

    private void Brake()
    {
        rb.linearVelocity = new Vector2(Mathf.MoveTowards(rb.linearVelocity.x, 0f, 60f * Time.deltaTime), rb.linearVelocity.y);
        if (state == State.Wait) PlayAnim(idleAnim);
    }

    private void Idle()
    {
        Brake();
        PlayAnim(idleAnim);
    }

    // Ponto do corpo onde o soco acerta (do lado de onde veio o golpe).
    private Vector3 HitPoint(float hitDirection) => transform.position + new Vector3(-hitDirection * 0.6f, 0.8f, 0f);

    private Collider2D bodyCollider;
    private Collider2D BodyCollider => bodyCollider != null ? bodyCollider : (bodyCollider = GetComponent<Collider2D>());

    private Vector3 Feet
    {
        get
        {
            Collider2D col = BodyCollider;
            return col != null ? new Vector3(transform.position.x, col.bounds.min.y + 0.1f, 0f) : transform.position;
        }
    }

    // O sprite dos inimigos olha para a esquerda por defeito.
    private void FacePlayer(float dx)
    {
        if (Mathf.Abs(dx) < 0.1f) return;
        transform.rotation = dx > 0 ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
    }

    private float Facing => Mathf.Abs(transform.eulerAngles.y) > 90f ? 1f : -1f;

    // ---------------- Ataque ----------------

    private bool TryTakeToken()
    {
        if (attackers.Contains(this)) return true;
        attackers.RemoveAll(e => e == null || e.IsDead);
        if (attackers.Count >= MaxAttackers) return false;
        attackers.Add(this);
        return true;
    }

    private void ReleaseToken() => attackers.Remove(this);

    private void StartTelegraph(float windup, bool unblockable)
    {
        currentWindup = windup;
        currentUnblockable = unblockable;
        SetState(State.Telegraph);
        // pose de "preparar o soco" congelada durante o aviso
        PlayAnim(punchAnim, true, 0f);
    }

    private void DoStrike(PlayerScript player, float dx, float dist)
    {
        SetState(State.Strike);
        strikeLanded = false;
        // a animação de soco toca inteira durante o golpe: mais lenta no golpe imparável
        PlayAnim(punchAnim, true, punchLength / StrikeDuration);
        rb.linearVelocity = new Vector2(Facing * (currentUnblockable ? 8f : 5f), rb.linearVelocity.y);
    }

    private void LandStrike(PlayerScript player, float dx)
    {
        float dist = Mathf.Abs(dx);
        punchsound.Play();

        bool inFront = Mathf.Sign(dx) == Facing;
        float dy = Mathf.Abs(player.transform.position.y - transform.position.y);
        float reach = stats.range + (currentUnblockable ? 1f : 0.5f);
        if (inFront && dist <= reach && dy < 2.5f)
        {
            float mult = currentUnblockable ? stats.unblockableDamage : 1f;
            player.ReceiveHit(this, Mathf.RoundToInt(stats.damage * damageScale * mult), currentUnblockable);
        }
        if (currentUnblockable) CameraFollow.Shake(0.2f, 0.15f);
    }

    private void FinishAttackCycle()
    {
        ReleaseToken();
        nextAttackAt = Time.time + Random.Range(0.6f, 1.4f) * AggressionScale;
        BackToNeutral();
    }

    private void BackToNeutral()
    {
        anim.speed = 1f;
        hitsLeft = 0;
        SetState(State.Approach);
        PlayAnim(idleAnim);
    }

    // ---------------- Levar porrada ----------------

    // Devolve true se o golpe fez dano (false se foi defendido ou ignorado).
    public bool TakeHit(HitInfo hit)
    {
        if (state == State.Dead || state == State.Knockdown) return false;

        bool attackerInFront = Mathf.Sign(-hit.direction) == Facing;
        bool canBlock = state == State.Approach || state == State.Wait || state == State.Blocking || state == State.Engage || state == State.Recover;
        if (canBlock && attackerInFront && !hit.guardBreak && Random.value < stats.blockChance)
        {
            Block(hit);
            return false;
        }

        bool staggered = state == State.Staggered;
        int dmg = staggered ? Mathf.RoundToInt(hit.damage * 1.5f) : hit.damage;
        if (state == State.Airborne) dmg = Mathf.Max(1, Mathf.RoundToInt(dmg * 0.8f));
        life -= dmg;
        guardCount = 0;
        flashUntil = Time.time + 0.08f;
        ShowPopUp(dmg.ToString(), staggered ? UIFactory.Gold : Color.white);
        CombatFX.Sparks(HitPoint(hit.direction), hit.direction, new Color(1f, 0.75f, 0.35f), hit.finisher || hit.launch ? 14 : 8);
        Instantiate(particles, transform.position, transform.rotation).AddComponent<AutoDestroy>();
        punchsound.Play();

        if (life <= 0)
        {
            Die(hit);
            return true;
        }
        radioSource.PlayOneShot(hurtSound.GruntSound);

        if (hit.launch || state == State.Airborne)
        {
            Launch(hit);
            return true;
        }

        // o Heavy aguenta o golpe sem recuar enquanto prepara o soco dele
        if (stats.armoredWindup && (state == State.Telegraph || state == State.Strike) && !hit.guardBreak)
        {
            rb.linearVelocity = new Vector2(hit.direction * hit.knockback * 0.2f, rb.linearVelocity.y);
            return true;
        }

        ReleaseToken();
        hitsLeft = 0;
        if (state == State.Telegraph) nextAttackAt = Time.time + 0.5f; // interrompido: tenta outra vez daqui a pouco
        rb.linearVelocity = new Vector2(hit.direction * hit.knockback * stats.knockbackResist, rb.linearVelocity.y);
        if (hit.finisher)
        {
            StartKnockdown(0.6f);
        }
        else if (!staggered)
        {
            SetState(State.Hitstun);
            PlayAnim(hitAnim, true);
        }
        return true;
    }

    private void Block(HitInfo hit)
    {
        guardCount++;
        rb.linearVelocity = new Vector2(hit.direction * 4f, rb.linearVelocity.y);
        ShowPopUp("BLOCK", new Color(0.6f, 0.7f, 1f));
        CombatFX.Sparks(HitPoint(hit.direction), hit.direction, new Color(0.55f, 0.7f, 1f), 5, 7f);
        if (guardCount >= stats.guardHits)
        {
            guardCount = 0;
            Stagger(1.1f); // defesa partida
            return;
        }
        SetState(State.Blocking);
        PlayAnim(blockAnim, true);
    }

    // Parry / esquiva perfeita do jogador ou defesa partida: fica tonto e leva mais dano.
    public void Stagger(float duration)
    {
        if (state == State.Dead) return;
        ReleaseToken();
        hitsLeft = 0;
        staggerTime = duration;
        SetState(State.Staggered);
        PlayAnim(hitAnim, true, 0.3f);
        rb.linearVelocity = new Vector2(-Facing * 5f, rb.linearVelocity.y);
        nextAttackAt = Time.time + duration + 0.6f;
    }

    private void Launch(HitInfo hit)
    {
        ReleaseToken();
        hitsLeft = 0;
        SetState(State.Airborne);
        grounded = false;
        float up = hit.launch ? 22f : 13f; // os socos no ar mantêm-no lá em cima (juggle)
        rb.linearVelocity = new Vector2(hit.direction * (hit.launch ? 3f : 1.5f), Mathf.Max(rb.linearVelocity.y, up));
        PlayAnim(juggleAnim, true);
    }

    private void StartKnockdown(float duration)
    {
        SetState(State.Knockdown);
        knockdownTime = duration;
        PlayAnim(hitAnim, true, 0.4f);
    }

    private void Die(HitInfo hit)
    {
        state = State.Dead;
        ReleaseToken();
        radioSource.PlayOneShot(hurtSound.DeathSound);
        GameObject corpse = Instantiate(body, transform.position, transform.rotation);
        Rigidbody2D corpseRb = corpse.GetComponent<Rigidbody2D>();
        if (corpseRb != null) corpseRb.linearVelocity = new Vector2(hit.direction * 10f, 0f);

        RunManager.Instance.HitStop(0.12f);
        CombatFX.Sparks(HitPoint(hit.direction), hit.direction, new Color(1f, 0.5f, 0.3f), 20, 16f);
        CombatFX.Dust(Feet, 10, 1.2f);
        CameraFollow.Punch(0.15f);
        CameraFollow.Shake(0.3f, 0.25f);
        if (Player != null)
        {
            Player.BeatenEnemies++;
            RageScript fury = Player.GetComponent<RageScript>();
            if (fury != null) fury.AddFury(8);
        }
        if (FightDirector.Instance != null) FightDirector.Instance.EnemyDied(this);
        Destroy(gameObject);
    }

    // ---------------- Física / visuais ----------------

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground")) grounded = true;
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground")) grounded = false;
    }

    private void SetState(State s)
    {
        state = s;
        stateTime = 0f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        All.Clear();
        attackers.Clear();
        MaxAttackers = 1;
        AggressionScale = 1f;
    }

    private void PlayAnim(string animName, bool restart = false, float animSpeed = 1f)
    {
        if (string.IsNullOrEmpty(animName)) return;
        anim.speed = animSpeed;
        if (!restart && currentAnim == animName) return;
        currentAnim = animName;
        anim.Play(animName, 0, 0f);
    }

    private void UpdateVisuals()
    {
        Color c = Color.white;
        bool telegraph = state == State.Telegraph;
        if (telegraph)
        {
            Color warn = currentUnblockable ? UnblockableColor : BlockableColor;
            float blink = Mathf.PingPong(stateTime * (currentUnblockable ? 14f : 10f), 1f);
            c = Color.Lerp(Color.white, warn, 0.45f + 0.55f * blink);
        }
        else if (state == State.Staggered)
        {
            c = new Color(0.65f, 0.75f, 1f);
        }
        if (Time.time < flashUntil) c = new Color(1f, 0.45f, 0.45f);
        sprite.color = c;

        if (warning != null)
        {
            warning.gameObject.SetActive(telegraph);
            if (telegraph)
            {
                warning.text = currentUnblockable ? "!!" : "!";
                warning.color = currentUnblockable ? UnblockableColor : BlockableColor;
                // o "!" cresce à medida que o soco se aproxima
                float t = Mathf.Clamp01(stateTime / Mathf.Max(0.01f, currentWindup));
                float s = Mathf.Lerp(0.7f, 1.25f, t);
                warning.transform.localScale = new Vector3(s, s, 1f);
                // mantém o texto legível mesmo quando o inimigo está virado
                warning.transform.rotation = Quaternion.identity;
            }
        }
    }

    private void ShowPopUp(string text, Color color)
    {
        if (popUpPrefab == null) return;
        GameObject popUp = Instantiate(popUpPrefab, transform.position + Vector3.up, Quaternion.identity);
        TMP_Text label = popUp.GetComponentInChildren<TMP_Text>();
        label.text = text;
        label.color = color;
    }
}
