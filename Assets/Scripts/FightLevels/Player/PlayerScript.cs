using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Jogador do Fight Club.
// Ataque:  combo de 3 socos (J/Quadrado) · uppercut que lança (K/Triângulo, com cooldown)
// Defesa:  defender (L/R1) · parry no momento certo · esquiva com invencibilidade (Shift/Círculo)
//          parry ou esquiva perfeita => CONTRA-ATAQUE: o próximo golpe faz dano a dobrar e parte a defesa
// Extra:   fúria (Espaço/L1) · kits de primeiros socorros (H/Cruz)
public class PlayerScript : MonoBehaviour
{
    public Transform spr;
    public Rigidbody2D rb;
    public Animator anim;
    public GameObject AttackArea;
    public int life = 100;
    [SerializeField] private GameObject popUpPrefab;
    [SerializeField] private Image lifebar;
    [SerializeField] private GameObject particles;
    [SerializeField] private AudioSource radioSource;
    [SerializeField] private GruntScript hurtSound;
    [SerializeField] private GameObject cadaver;
    public float speed = 12f;

    [Header("Defesa")]
    [SerializeField] private float parryWindow = 0.22f;
    [SerializeField] private float blockChip = 0.15f;
    [SerializeField] private float invulnerableAfterHit = 0.45f;
    [SerializeField] private float counterWindow = 1.5f;

    [Header("Esquiva")]
    [SerializeField] private float dodgeSpeed = 24f;
    [SerializeField] private float dodgeDuration = 0.2f;
    [SerializeField] private float dodgeInvulnerability = 0.3f;

    [Header("Cooldowns")]
    [SerializeField] private float uppercutCooldown = 2.5f;
    [SerializeField] private float dodgeCooldown = 1f;
    [SerializeField] private float comboCooldown = 0.5f;

    public static PlayerScript Current { get; private set; }
    public int BeatenEnemies { get; set; }
    public bool IsDead { get; private set; }
    public int MaxLife => RunManager.MaxHealth;

    // Para o HUD: 0 = pronto, 1 = acabou de ser usado
    public float UppercutCooldown01 => Remaining01(uppercutReadyAt, uppercutCooldown);
    public float DodgeCooldown01 => Remaining01(dodgeReadyAt, dodgeCooldown);
    public float ComboCooldown01 => Remaining01(comboReadyAt, comboCooldown);
    public bool CounterReady => Time.time < counterUntil;
    public float LastDeniedAt { get; private set; } = -10f;

    private enum State { Free, Attacking, Blocking, Dodging, Hurt, Healing, Dead }

    private class Attack
    {
        public string anim;
        public float duration, activeStart, activeEnd, cancelFrom;
        public int damage;
        public float knockback, lunge, hitStop, shake;
        public bool launch, finisher;
    }

    // Tempos em segundos. "cancelFrom" = a partir de quando o golpe seguinte do combo pode sair.
    private static readonly Attack[] Combo =
    {
        new Attack { anim = "PunchPlr", duration = 0.30f, activeStart = 0.07f, activeEnd = 0.15f, cancelFrom = 0.13f, damage = 9, knockback = 3f, lunge = 4f, hitStop = 0.05f, shake = 0.05f },
        new Attack { anim = "PunchPlrRight", duration = 0.30f, activeStart = 0.07f, activeEnd = 0.15f, cancelFrom = 0.13f, damage = 9, knockback = 3f, lunge = 4f, hitStop = 0.05f, shake = 0.05f },
        new Attack { anim = "DownwardsPlayer", duration = 0.44f, activeStart = 0.13f, activeEnd = 0.23f, cancelFrom = 0.44f, damage = 15, knockback = 14f, lunge = 7f, hitStop = 0.09f, shake = 0.22f, finisher = true },
    };
    private static readonly Attack Uppercut = new Attack
    {
        anim = "UppercutPlr", duration = 0.48f, activeStart = 0.14f, activeEnd = 0.26f, cancelFrom = 0.40f,
        damage = 14, knockback = 4f, lunge = 5f, hitStop = 0.1f, shake = 0.25f, launch = true
    };

    private State state = State.Free;
    private Attack currentAttack;
    private float stateTime;
    private int comboIndex;
    private float lastAttackEnd = -10f;
    private float attackBufferedAt = -10f;
    private bool heavyBuffered;
    private float blockStartedAt = -10f;
    private float blockReleasedAt = -10f;
    private float invulnerableUntil;
    private float dodgeInvulnerableUntil;
    private float uppercutReadyAt;
    private float dodgeReadyAt;
    private float comboReadyAt;
    private float counterUntil;
    private float hurtDuration = 0.28f;
    private float dodgeDir;
    private float nextGhostAt;
    private readonly HashSet<EnemyScript> hitThisSwing = new HashSet<EnemyScript>();
    private readonly Dictionary<string, float> clipLengths = new Dictionary<string, float>();
    private string currentAnim;
    private SpriteRenderer sprite;
    private BoxCollider2D hitBox;
    private RageScript fury;
    private int enemyMask;

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

    private bool FacingRight => Mathf.Abs(transform.eulerAngles.y) < 90f;
    private float Facing => FacingRight ? 1f : -1f;

    private void Awake()
    {
        Current = this;
        sprite = GetComponent<SpriteRenderer>();
        fury = GetComponent<RageScript>();
        hitBox = AttackArea != null ? AttackArea.GetComponent<BoxCollider2D>() : null;
        if (AttackArea != null) AttackArea.SetActive(false);
        enemyMask = LayerMask.GetMask("Enemy");
        foreach (AnimationClip clip in anim.runtimeAnimatorController.animationClips)
            clipLengths[clip.name] = clip.length;
    }

    private void Start()
    {
        life = Mathf.Clamp(RunManager.Instance.Health, 1, MaxLife);
        UpdateLifeBar();
        PlayAnim("IdlePlr");
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    private void Update()
    {
        if (IsDead) return;
        stateTime += Time.deltaTime;
        var input = GameInput.Player;
        Vector2 move = input.Move.ReadValue<Vector2>();

        if (input.Attack.WasPressedThisFrame()) { attackBufferedAt = Time.time; heavyBuffered = move.y > 0.5f; }
        if (input.Heavy.WasPressedThisFrame()) { attackBufferedAt = Time.time; heavyBuffered = true; }
        bool attackQueued = Time.time - attackBufferedAt < 0.22f;
        bool dodgePressed = input.Dodge.WasPressedThisFrame();

        if (input.Fury.WasPressedThisFrame() && fury != null) fury.TryActivate();

        switch (state)
        {
            case State.Free:
                if (dodgePressed && TryDodge(move)) break;
                if (input.Defend.IsPressed()) { StartBlock(); break; }
                if (attackQueued && TryStartAttack(move)) break;
                if (input.Heal.WasPressedThisFrame() && TryHeal()) break;
                Locomotion(move);
                break;

            case State.Attacking:
                UpdateAttack();
                // dá para cancelar a recuperação de um golpe com esquiva
                if (dodgePressed && stateTime > currentAttack.activeEnd && TryDodge(move)) break;
                if (stateTime >= currentAttack.cancelFrom && attackQueued && CanChain() && TryStartAttack(move)) break;
                if (stateTime >= currentAttack.duration) EndAttack();
                break;

            case State.Blocking:
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                if (move.x > 0.5f) Face(true); else if (move.x < -0.5f) Face(false);
                if (dodgePressed && TryDodge(move)) break;
                if (!input.Defend.IsPressed())
                {
                    blockReleasedAt = Time.time;
                    ToFree();
                }
                break;

            case State.Dodging:
                if (Time.time >= nextGhostAt)
                {
                    nextGhostAt = Time.time + 0.035f;
                    CombatFX.Ghost(sprite, new Color(0.45f, 0.75f, 1f, 0.55f));
                }
                rb.linearVelocity = new Vector2(dodgeDir * dodgeSpeed * (1f - stateTime / dodgeDuration * 0.5f), rb.linearVelocity.y);
                if (stateTime >= dodgeDuration)
                {
                    rb.linearVelocity = new Vector2(dodgeDir * 4f, rb.linearVelocity.y);
                    ToFree();
                }
                break;

            case State.Hurt:
                rb.linearVelocity = new Vector2(Mathf.MoveTowards(rb.linearVelocity.x, 0f, 60f * Time.deltaTime), rb.linearVelocity.y);
                if (stateTime >= hurtDuration) ToFree();
                break;

            case State.Healing:
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                if (stateTime >= 0.45f) ToFree();
                break;
        }

        UpdateTint();
        PostFXDirector.SetHealth01((float)life / MaxLife);
    }

    // ---------------- Movimento ----------------

    private void Locomotion(Vector2 move)
    {
        float target = move.x * speed * (fury != null ? fury.SpeedMultiplier : 1f);
        float vx = Mathf.MoveTowards(rb.linearVelocity.x, target, 120f * Time.deltaTime);
        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
        if (move.x > 0.1f) Face(true);
        else if (move.x < -0.1f) Face(false);
        PlayAnim(Mathf.Abs(move.x) > 0.1f ? "WalkPlr" : "IdlePlr");
    }

    private void Face(bool right)
    {
        transform.rotation = right ? Quaternion.identity : Quaternion.Euler(0, 180, 0);
    }

    // Esquiva: sem direção = recua (afasta-se do que tens à frente).
    private bool TryDodge(Vector2 move)
    {
        if (Time.time < dodgeReadyAt)
        {
            LastDeniedAt = Time.time;
            return false;
        }
        dodgeDir = Mathf.Abs(move.x) > 0.3f ? Mathf.Sign(move.x) : -Facing;
        dodgeReadyAt = Time.time + dodgeCooldown;
        dodgeInvulnerableUntil = Time.time + dodgeInvulnerability;
        attackBufferedAt = -10f;
        SetState(State.Dodging);
        nextGhostAt = 0f;
        CombatFX.Dust(Feet, 7);
        PlayAnim("WalkPlr", true, 2.5f);
        return true;
    }

    // ---------------- Ataques ----------------

    private bool CanChain()
    {
        return currentAttack != Uppercut && (heavyBuffered || comboIndex < Combo.Length);
    }

    private bool TryStartAttack(Vector2 move)
    {
        Attack attack;
        if (heavyBuffered)
        {
            if (Time.time < uppercutReadyAt)
            {
                // uppercut em cooldown: não faz nada (o HUD pisca)
                LastDeniedAt = Time.time;
                attackBufferedAt = -10f;
                heavyBuffered = false;
                return false;
            }
            attack = Uppercut;
            uppercutReadyAt = Time.time + uppercutCooldown;
            comboIndex = 0;
        }
        else
        {
            if (Time.time < comboReadyAt) return false; // pausa depois do golpe final
            if (state != State.Attacking && Time.time - lastAttackEnd > 0.35f) comboIndex = 0;
            if (comboIndex >= Combo.Length) comboIndex = 0;
            attack = Combo[comboIndex];
            comboIndex++;
        }

        attackBufferedAt = -10f;
        heavyBuffered = false;
        if (move.x > 0.3f) Face(true); else if (move.x < -0.3f) Face(false);

        currentAttack = attack;
        hitThisSwing.Clear();
        SetState(State.Attacking);
        PlayAnim(attack.anim, true, ClipLength(attack.anim) / attack.duration);
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        return true;
    }

    private void UpdateAttack()
    {
        Attack a = currentAttack;
        // pequeno avanço no início do golpe: dá sensação de peso e fecha distâncias
        float vx = stateTime < a.activeEnd ? a.lunge * Facing : 0f;
        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);

        if (stateTime >= a.activeStart && stateTime <= a.activeEnd) DoHitCheck(a);
    }

    private void EndAttack()
    {
        lastAttackEnd = Time.time;
        if (currentAttack.finisher)
        {
            comboIndex = 0;
            comboReadyAt = Time.time + comboCooldown;
        }
        if (currentAttack == Uppercut) comboIndex = 0;
        ToFree();
    }

    private void DoHitCheck(Attack a)
    {
        Vector2 center;
        Vector2 size;
        if (hitBox != null)
        {
            center = hitBox.transform.TransformPoint(hitBox.offset);
            size = hitBox.size;
        }
        else
        {
            center = (Vector2)transform.position + new Vector2(1.1f * Facing, 0f);
            size = new Vector2(2.4f, 4f);
        }

        bool anyHit = false;
        bool anyDamage = false;
        bool counter = CounterReady;
        foreach (Collider2D col in Physics2D.OverlapBoxAll(center, size, 0f, enemyMask))
        {
            EnemyScript enemy = col.GetComponentInParent<EnemyScript>();
            if (enemy == null || hitThisSwing.Contains(enemy)) continue;
            hitThisSwing.Add(enemy);

            float mult = (fury != null ? fury.DamageMultiplier : 1f) * (counter ? 2f : 1f);
            var info = new EnemyScript.HitInfo
            {
                damage = Mathf.RoundToInt(a.damage * mult),
                knockback = a.knockback * (counter ? 1.5f : 1f),
                direction = Facing,
                launch = a.launch,
                finisher = a.finisher,
                guardBreak = a.launch || counter || (fury != null && fury.IsActive),
            };
            if (enemy.TakeHit(info))
            {
                anyDamage = true;
                if (fury != null) fury.AddFury(counter ? 10 : 4);
            }
            anyHit = true;
        }

        if (anyHit)
        {
            if (counter && anyDamage)
            {
                counterUntil = 0f;
                ShowPopUp("COUNTER!", UIFactory.Gold);
                PostFXDirector.Flash(0.6f);
                CameraFollow.Punch(0.4f);
            }
            RunManager.Instance.HitStop(anyDamage ? a.hitStop * (counter ? 1.6f : 1f) : 0.03f);
            CameraFollow.Shake(anyDamage ? a.shake * (counter ? 1.8f : 1f) : 0.04f, 0.15f);
            if (anyDamage && (a.finisher || a.launch)) CameraFollow.Punch(0.2f);
        }
    }

    // ---------------- Defesa / dano recebido ----------------

    private void StartBlock()
    {
        // só conta como parry se não estiveres a martelar o botão
        blockStartedAt = Time.time - blockReleasedAt > 0.3f ? Time.time : -10f;
        SetState(State.Blocking);
        PlayAnim("BlockPlr");
    }

    public bool CanBeHit => !IsDead && Time.time >= invulnerableUntil;

    // Chamado pelo inimigo quando o soco dele chega.
    // unblockable = golpe com aviso vermelho: defender não chega, tens de esquivar.
    public void ReceiveHit(EnemyScript attacker, int damage, bool unblockable)
    {
        if (IsDead) return;
        float dirToAttacker = Mathf.Sign(attacker.transform.position.x - transform.position.x);

        // esquiva no momento certo = esquiva perfeita
        if (Time.time < dodgeInvulnerableUntil)
        {
            GrantCounter("PERFECT DODGE!");
            PostFXDirector.Flash(0.7f);
            CameraFollow.Punch(0.25f);
            attacker.Stagger(0.6f);
            return;
        }
        if (!CanBeHit) return;

        bool facingAttacker = dirToAttacker == Facing;
        if (state == State.Blocking && facingAttacker)
        {
            if (unblockable)
            {
                ShowPopUp("GUARD BROKEN", new Color(1f, 0.3f, 0.3f));
                TakeFullHit(damage, dirToAttacker, 0.55f);
                return;
            }
            if (Time.time - blockStartedAt <= parryWindow)
            {
                attacker.Stagger(1.2f);
                GrantCounter("PARRY!");
                PostFXDirector.Flash(1f);
                CameraFollow.Punch(0.35f);
                CombatFX.Sparks(transform.position + new Vector3(dirToAttacker * 1.1f, 0.8f, 0f), dirToAttacker, UIFactory.Gold, 16, 14f);
                if (fury != null) fury.AddFury(15);
                RunManager.Instance.HitStop(0.12f);
                CameraFollow.Shake(0.25f, 0.2f);
                return;
            }

            int chip = Mathf.Max(1, Mathf.RoundToInt(damage * blockChip));
            ApplyDamage(chip, Color.gray);
            rb.linearVelocity = new Vector2(-dirToAttacker * 6f, rb.linearVelocity.y);
            if (fury != null) fury.AddFury(3);
            RunManager.Instance.HitStop(0.04f);
            PostFXDirector.Hit(0.3f);
            CombatFX.Sparks(transform.position + new Vector3(dirToAttacker * 1.1f, 0.8f, 0f), dirToAttacker, new Color(0.6f, 0.75f, 1f), 6, 8f);
            return;
        }

        TakeFullHit(damage, dirToAttacker, 0.28f);
    }

    private void GrantCounter(string label)
    {
        counterUntil = Time.time + counterWindow;
        ShowPopUp(label, UIFactory.Gold);
    }

    private void TakeFullHit(int damage, float dirToAttacker, float stun)
    {
        ApplyDamage(damage, Color.white);
        if (IsDead) return;

        Instantiate(particles, transform.position, transform.rotation).AddComponent<AutoDestroy>();
        radioSource.PlayOneShot(hurtSound.GruntSound);
        invulnerableUntil = Time.time + invulnerableAfterHit;
        hurtDuration = stun;
        rb.linearVelocity = new Vector2(-dirToAttacker * 9f, rb.linearVelocity.y);
        SetState(State.Hurt);
        PlayAnim("HurtPlr", true);
        RunManager.Instance.HitStop(0.07f);
        PostFXDirector.Hit();
        CameraFollow.Shake(0.18f, 0.18f);
    }

    private void ApplyDamage(int value, Color color)
    {
        life = Mathf.Max(0, life - value);
        ShowPopUp(value.ToString(), color);
        UpdateLifeBar();
        if (life <= 0) Die();
    }

    public void RestoreHealth(int amount)
    {
        if (IsDead || life >= MaxLife) return;
        life = Mathf.Min(MaxLife, life + amount);
        UpdateLifeBar();
        ShowPopUp("+" + amount, new Color(0.4f, 1f, 0.5f));
    }

    private bool TryHeal()
    {
        if (life >= MaxLife || !RunManager.Instance.UseFirstAid()) return false;
        life = Mathf.Min(MaxLife, life + RunManager.FirstAidHeal);
        UpdateLifeBar();
        ShowPopUp("+" + RunManager.FirstAidHeal, new Color(0.4f, 1f, 0.5f));
        SetState(State.Healing);
        PlayAnim("BlockPlr");
        return true;
    }

    private void Die()
    {
        IsDead = true;
        state = State.Dead;
        radioSource.PlayOneShot(hurtSound.DeathSound);
        GameObject body = Instantiate(cadaver, transform.position, transform.rotation);
        Rigidbody2D bodyRb = body.GetComponent<Rigidbody2D>();
        if (bodyRb != null) bodyRb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        CameraFollow.Shake(0.4f, 0.4f);
        if (FightDirector.Instance != null) FightDirector.Instance.PlayerDied();
        Destroy(gameObject);
    }

    // ---------------- Utilidades ----------------

    private static float Remaining01(float readyAt, float cooldown)
    {
        return cooldown <= 0f ? 0f : Mathf.Clamp01((readyAt - Time.time) / cooldown);
    }

    private void ToFree()
    {
        SetState(State.Free);
        PlayAnim("IdlePlr");
    }

    private void SetState(State s)
    {
        state = s;
        stateTime = 0f;
    }

    private float ClipLength(string clip)
    {
        return clipLengths.TryGetValue(clip, out float len) ? len : 0.4f;
    }

    private void PlayAnim(string stateName, bool restart = false, float animSpeed = 1f)
    {
        anim.speed = animSpeed;
        if (!restart && currentAnim == stateName) return;
        currentAnim = stateName;
        anim.Play(stateName, 0, 0f);
    }

    private void UpdateTint()
    {
        if (sprite == null) return;
        Color c = Color.white;
        if (state == State.Healing) c = new Color(0.6f, 1f, 0.6f);
        else if (state == State.Dodging) c = new Color(0.7f, 0.85f, 1f, 0.55f);
        else if (CounterReady) c = Color.Lerp(Color.white, UIFactory.Gold, 0.5f + 0.5f * Mathf.Sin(Time.time * 25f));
        else if (fury != null && fury.IsActive) c = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.3f), 0.5f + 0.5f * Mathf.Sin(Time.time * 18f));
        // pisca durante a invulnerabilidade depois de levar um soco
        if (Time.time < invulnerableUntil && Mathf.Repeat(Time.time * 16f, 1f) > 0.5f) c.a = 0.35f;
        sprite.color = c;
    }

    private void UpdateLifeBar()
    {
        if (lifebar != null) lifebar.fillAmount = Mathf.Clamp01((float)life / MaxLife);
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
