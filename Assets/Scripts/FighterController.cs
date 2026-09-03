using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador principal do lutador.
/// Gerencia a Máquina de Estados Finitos (FSM), coordena movimentação, animações,
/// sistema de combate com Hitboxes/Hurtboxes, Hitstop (frame freeze), gestão de saúde
/// e animação Standing Up ao solicitar rematch / reinício de combate.
/// </summary>
[RequireComponent(typeof(FighterMovement))]
[RequireComponent(typeof(HealthSystem))]
[DisallowMultipleComponent]
public class FighterController : MonoBehaviour
{
    [Header("Components")]
    [Tooltip("Referência opcional ao Animator. Se não atribuído, busca no GameObject ou nós filhos.")]
    [SerializeField] private Animator animator;

    [Header("Combat Hitboxes")]
    [Tooltip("Lista de Hitboxes presentes nos membros de ataque deste lutador.")]
    [SerializeField] private Hitbox[] hitboxes;

    [Header("Combat Stats")]
    [Tooltip("Dano padrão desferido por golpes básicos.")]
    [SerializeField, Min(0f)] private float defaultAttackDamage = 15f;

    [Tooltip("Força padrão de knockback aplicada ao atingir o oponente.")]
    [SerializeField, Min(0f)] private float defaultKnockbackForce = 4.0f;

    [Tooltip("Duração total do golpe (ativação + recovery) antes de voltar ao estado Neutro.")]
    [SerializeField, Min(0.05f)] private float attackDuration = 0.85f;

    [Tooltip("Duração padrão do congelamento por dano (Hit Stun) quando não especificado pelo golpe.")]
    [SerializeField, Min(0.05f)] private float defaultHitStunDuration = 0.55f;

    [Tooltip("Duração padrão do congelamento de quadros no impacto (Hitstop / Frame Freeze).")]
    [SerializeField, Range(0.02f, 0.2f)] private float defaultHitstopDuration = 0.08f;

    [Header("Input Actions")]
    [Tooltip("Ação de ataque do novo Input System. Opcional: cria fallback automático se nulo.")]
    [SerializeField] private InputActionReference attackActionReference;

    [Header("Animation State / Trigger Names")]
    [SerializeField] private string neutralAnimName = "Idle";
    [SerializeField] private string attackAnimName = "Attack";
    [SerializeField] private string hitStunAnimName = "HitStun";
    [SerializeField] private string knockoutAnimName = "Dying";
    [SerializeField] private string standingUpAnimName = "StandingUp";
    [SerializeField] private string turn180AnimName = "Turn180";

    [Header("Debug")]
    [SerializeField] private bool showOnScreenControls = true;
    [SerializeField] private string currentStateDebug;

    // Componentes e referências em cache
    private FighterMovement movement;
    private HealthSystem healthSystem;
    private InputAction runtimeAttackAction;
    private Coroutine hitstopCoroutine;
    private Coroutine standingUpCoroutine;
    private readonly Dictionary<HitboxLimb, Hitbox> hitboxMap = new Dictionary<HitboxLimb, Hitbox>();

    // Edge-detection para inputs de hardware
    private bool wasSpaceHeld;
    private bool wasJHeld;
    private bool wasEnterHeld;
    private bool wasMouseHeld;
    private string lastDetectedInput = "Nenhum";

    // Hashes numéricos de animação
    public int NeutralAnimHash { get; private set; }
    public int AttackAnimHash { get; private set; }
    public int HitStunAnimHash { get; private set; }
    public int KnockoutAnimHash { get; private set; }
    public int StandingUpAnimHash { get; private set; }
    public int Turn180AnimHash { get; private set; }

    // Instâncias cacheadas dos estados FSM (Zero GC em transições)
    public NeutralState NeutralState { get; private set; }
    public AttackState AttackState { get; private set; }
    public HitStunState HitStunState { get; private set; }
    public KnockoutState KnockoutState { get; private set; }

    // Estado ativo
    public IFighterState CurrentState { get; private set; }

    // Getters públicos
    public FighterMovement Movement => movement;
    public HealthSystem HealthSystem => healthSystem;
    public Animator Animator => animator;
    public float AttackDuration => attackDuration;
    public float DefaultHitStunDuration => defaultHitStunDuration;
    public float DefaultHitstopDuration => defaultHitstopDuration;

    private void Awake()
    {
        movement = GetComponent<FighterMovement>();
        healthSystem = GetComponent<HealthSystem>();

        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        RefreshHitboxCache();

        NeutralAnimHash = Animator.StringToHash(neutralAnimName);
        AttackAnimHash = Animator.StringToHash(attackAnimName);
        HitStunAnimHash = Animator.StringToHash(hitStunAnimName);
        KnockoutAnimHash = Animator.StringToHash(knockoutAnimName);
        StandingUpAnimHash = Animator.StringToHash(standingUpAnimName);
        Turn180AnimHash = Animator.StringToHash(turn180AnimName);

        NeutralState = new NeutralState();
        AttackState = new AttackState();
        HitStunState = new HitStunState();
        KnockoutState = new KnockoutState();

        if (movement != null && movement.IsPlayerControlled)
        {
            InitializeAttackInput();
        }
    }

    private void OnEnable()
    {
        if (movement != null && movement.IsPlayerControlled)
        {
            if (attackActionReference != null && attackActionReference.action != null)
            {
                attackActionReference.action.actionMap?.Enable();
                attackActionReference.action.Enable();
            }
            runtimeAttackAction?.Enable();
        }
    }

    private void OnDisable()
    {
        runtimeAttackAction?.Disable();
        DisableAllHitboxes();

        if (hitstopCoroutine != null)
        {
            StopCoroutine(hitstopCoroutine);
            hitstopCoroutine = null;
        }

        if (standingUpCoroutine != null)
        {
            StopCoroutine(standingUpCoroutine);
            standingUpCoroutine = null;
        }
    }

    private void OnDestroy()
    {
        if (attackActionReference == null && runtimeAttackAction != null)
        {
            runtimeAttackAction.Dispose();
        }
    }

    private void Start()
    {
        ChangeState(NeutralState);
    }

    private void Update()
    {
        CurrentState?.Update(this);
    }

    public void RefreshHitboxCache()
    {
        hitboxMap.Clear();
        if (hitboxes == null || hitboxes.Length == 0)
        {
            hitboxes = GetComponentsInChildren<Hitbox>(true);
        }

        foreach (var hb in hitboxes)
        {
            if (hb != null)
            {
                hb.Owner = this;
                hitboxMap[hb.LimbType] = hb;
            }
        }
    }

    public void ChangeState(IFighterState newState)
    {
        if (newState == null || CurrentState == newState) return;

        CurrentState?.Exit(this);
        CurrentState = newState;
        currentStateDebug = newState.GetType().Name;
        CurrentState.Enter(this);
    }

    public void CrossFadeAnimation(int animHash, float transitionDuration = 0.05f)
    {
        if (animator != null && animator.isActiveAndEnabled)
        {
            animator.Play(animHash, 0, 0f);
        }
    }

    public void TriggerAttack()
    {
        if (healthSystem != null && healthSystem.IsDead) return;

        if (CurrentState is NeutralState)
        {
            ChangeState(AttackState);
        }
    }

    public void TriggerKnockout()
    {
        ChangeState(KnockoutState);
    }

    public void TriggerTurn180()
    {
        if (healthSystem != null && healthSystem.IsDead) return;

        CrossFadeAnimation(Turn180AnimHash, 0.1f);
    }

    // ========================================================================
    // STANDING UP (LEVANTO DO CHÃO APÓS NOCAUTE / REMATCH)
    // ========================================================================

    /// <summary>
    /// Aciona a animação de levantar do chão (Standing Up) e reabilita o lutador para o combate.
    /// </summary>
    public void TriggerStandingUp(Action onComplete = null)
    {
        if (standingUpCoroutine != null)
        {
            StopCoroutine(standingUpCoroutine);
        }

        standingUpCoroutine = StartCoroutine(StandingUpRoutine(onComplete));
    }

    private IEnumerator StandingUpRoutine(Action onComplete)
    {
        // 1. Trava movimentação e desativa hitboxes enquanto levanta
        if (movement != null) movement.CanMove = false;
        DisableAllHitboxes();

        // 2. Restaura vida e flags
        if (healthSystem != null) healthSystem.ResetHealth();

        // 3. Dispara animação "Standing Up"
        CrossFadeAnimation(StandingUpAnimHash, 0.1f);

        // 4. Aguarda a conclusão da animação (aproximadamente 6.0 segundos a 1.75x)
        yield return new WaitForSeconds(6.0f);

        // 5. Restaura movimentação e retorna para estado Neutro (Idle)
        if (movement != null) movement.CanMove = true;
        ChangeState(NeutralState);

        standingUpCoroutine = null;
        onComplete?.Invoke();
    }

    // ========================================================================
    // HITSTOP (FRAME FREEZE FEEDBACK)
    // ========================================================================

    public void ApplyHitstop(float duration)
    {
        if (duration <= 0f || animator == null) return;

        if (hitstopCoroutine != null)
        {
            StopCoroutine(hitstopCoroutine);
        }

        hitstopCoroutine = StartCoroutine(HitstopRoutine(duration));
    }

    private IEnumerator HitstopRoutine(float duration)
    {
        float previousSpeed = animator.speed;
        animator.speed = 0f;

        yield return new WaitForSecondsRealtime(duration);

        if (animator != null)
        {
            animator.speed = previousSpeed > 0f ? previousSpeed : 1f;
        }

        hitstopCoroutine = null;
    }

    // ========================================================================
    // MÉTODOS PÚBLICOS PARA ANIMATION EVENTS
    // ========================================================================

    public void EnableHitbox(string limbName)
    {
        if (Enum.TryParse(limbName, true, out HitboxLimb limb))
        {
            EnableHitbox(limb);
        }
        else
        {
            Debug.LogWarning($"[FighterController] Membro inválido no Animation Event: {limbName}", this);
        }
    }

    public void DisableHitbox(string limbName)
    {
        if (Enum.TryParse(limbName, true, out HitboxLimb limb))
        {
            DisableHitbox(limb);
        }
    }

    public void EnableHitbox(HitboxLimb limb)
    {
        Vector3 knockbackDir = transform.forward * defaultKnockbackForce;
        DamageData defaultData = new DamageData(
            defaultAttackDamage,
            defaultHitStunDuration,
            knockbackDir,
            this,
            defaultHitstopDuration
        );
        EnableHitbox(limb, defaultData);
    }

    public void EnableHitbox(HitboxLimb limb, DamageData data)
    {
        if (hitboxMap.TryGetValue(limb, out var hb))
        {
            hb.Activate(data);
        }
    }

    public void DisableHitbox(HitboxLimb limb)
    {
        if (hitboxMap.TryGetValue(limb, out var hb))
        {
            hb.Deactivate();
        }
    }

    public void DisableAllHitboxes()
    {
        if (hitboxes == null) return;
        foreach (var hb in hitboxes)
        {
            if (hb != null && hb.IsActive)
            {
                hb.Deactivate();
            }
        }
    }

    // ========================================================================
    // PROCESSAMENTO DE DANO RECEBIDO VIA HURTBOX
    // ========================================================================

    public void TakeDamage(DamageData data, Hitbox sourceHitbox)
    {
        ApplyDamage(data, sourceHitbox);
    }

    public void ApplyDamage(DamageData data, Hitbox sourceHitbox)
    {
        float hitstopTime = data.hitstopDuration > 0f ? data.hitstopDuration : defaultHitstopDuration;
        ApplyHitstop(hitstopTime);

        if (data.attacker != null)
        {
            data.attacker.ApplyHitstop(hitstopTime);
        }

        if (healthSystem != null)
        {
            healthSystem.TakeDamage(data);
        }

        if (movement != null && movement.CharacterController != null && data.knockback.sqrMagnitude > 0.01f)
        {
            Vector3 push = data.knockback * Time.deltaTime;
            movement.CharacterController.Move(push);
        }

        if (healthSystem == null || !healthSystem.IsDead)
        {
            TakeHit(data.hitStunDuration);
        }
    }

    public void TakeHit(float stunDuration = -1f)
    {
        DisableAllHitboxes();

        if (stunDuration > 0f)
        {
            HitStunState.SetStunDuration(stunDuration);
        }

        ChangeState(HitStunState);
    }

    public bool IsAttackTriggered()
    {
        if (healthSystem != null && healthSystem.IsDead) return false;
        if (movement != null && !movement.IsPlayerControlled) return false;

        if (Keyboard.current != null)
        {
            bool isSpace = Keyboard.current.spaceKey.isPressed;
            bool isJ = Keyboard.current.jKey.isPressed;
            bool isEnter = Keyboard.current.enterKey.isPressed;

            bool triggered = (isSpace && !wasSpaceHeld) || (isJ && !wasJHeld) || (isEnter && !wasEnterHeld);

            wasSpaceHeld = isSpace;
            wasJHeld = isJ;
            wasEnterHeld = isEnter;

            if (triggered)
            {
                lastDetectedInput = isSpace ? "Espaço" : (isJ ? "J" : "Enter");
                return true;
            }
        }

        if (Mouse.current != null)
        {
            bool isMouse = Mouse.current.leftButton.isPressed;
            bool triggered = isMouse && !wasMouseHeld;
            wasMouseHeld = isMouse;

            if (triggered)
            {
                lastDetectedInput = "Mouse Esquerdo";
                return true;
            }
        }

        if (runtimeAttackAction != null && (runtimeAttackAction.triggered || runtimeAttackAction.WasPressedThisFrame()))
        {
            lastDetectedInput = "InputAction";
            return true;
        }

        if (Gamepad.current != null)
        {
            if (Gamepad.current.buttonSouth.wasPressedThisFrame ||
                Gamepad.current.buttonWest.wasPressedThisFrame)
            {
                lastDetectedInput = "Gamepad Button";
                return true;
            }
        }

        return false;
    }

    private void InitializeAttackInput()
    {
        if (attackActionReference != null && attackActionReference.action != null)
        {
            runtimeAttackAction = attackActionReference.action;
            attackActionReference.action.actionMap?.Enable();
            runtimeAttackAction.Enable();
        }
        else
        {
            runtimeAttackAction = new InputAction(name: "Attack", type: InputActionType.Button);
            runtimeAttackAction.AddBinding("<Keyboard>/space");
            runtimeAttackAction.AddBinding("<Keyboard>/j");
            runtimeAttackAction.AddBinding("<Keyboard>/enter");
            runtimeAttackAction.AddBinding("<Mouse>/leftButton");
            runtimeAttackAction.AddBinding("<Gamepad>/buttonSouth");
            runtimeAttackAction.AddBinding("<Gamepad>/buttonWest");
            runtimeAttackAction.Enable();
        }
    }

    private void OnGUI()
    {
        if (!showOnScreenControls) return;

        GUI.color = Color.white;
        var boxStyle = GUI.skin.box;

        var opponentController = movement.Opponent != null ? movement.Opponent.GetComponent<FighterController>() : null;
        var opHealth = opponentController != null ? opponentController.HealthSystem : null;
        var ai = movement.Opponent != null ? movement.Opponent.GetComponent<FighterSparringAI>() : null;

        // 1. Painel de Status & Barra de Vida em Tempo Real
        GUILayout.BeginArea(new Rect(15, 15, 390, 200), boxStyle);
        GUILayout.Label("<b>PAINEL DE COMBATE & DIFICULDADE DA IA</b>");

        float p1Hp = healthSystem != null ? healthSystem.CurrentHealth : 100f;
        float p1Max = healthSystem != null ? healthSystem.MaxHealth : 100f;
        string p1Status = healthSystem != null && healthSystem.IsDead ? "<color=red>K.O. (No Chão)</color>" : $"{p1Hp:F0}/{p1Max:F0}";
        GUILayout.Label($"P1 (Você - Blusa P/B): <b>{p1Status}</b> | Estado: <b><color=yellow>{CurrentState?.GetType().Name}</color></b>");

        float p2Hp = opHealth != null ? opHealth.CurrentHealth : 100f;
        float p2Max = opHealth != null ? opHealth.MaxHealth : 100f;
        string p2Status = opHealth != null && opHealth.IsDead ? "<color=red>K.O. (No Chão)</color>" : $"{p2Hp:F0}/{p2Max:F0}";
        GUILayout.Label($"P2 (IA Oponente): <b>{p2Status}</b> | Estado: <b><color=yellow>{(opponentController != null ? opponentController.CurrentState?.GetType().Name : "N/A")}</color></b>");

        string diffText = ai != null ? ai.Difficulty.ToString() : "N/A";
        string diffColor = diffText == "Easy" ? "lime" : (diffText == "Medium" ? "yellow" : "red");
        GUILayout.Label($"Dificuldade da IA: <b><color={diffColor}>{diffText}</color></b> | Hitstop: <b>{defaultHitstopDuration * 1000f:F0}ms</b>");
        GUILayout.Label($"Inputs P1: <b>Forward={movement.ForwardInput:F1}, Right={movement.RightInput:F1}</b>");
        GUILayout.EndArea();

        // 2. Controles Virtuais Interativos
        GUILayout.BeginArea(new Rect(15, 225, 390, 180), boxStyle);
        GUILayout.Label("<b>CONTROLES & SELETOR DE DIFICULDADE:</b>");

        // Movimento P1 (Frente, Trás, Órbita)
        GUILayout.BeginHorizontal();
        if (GUILayout.RepeatButton("<< A (Órbita)", GUILayout.Height(30)))
        {
            movement.ExternalInput = new Vector2(-1f, 0f);
        }
        else if (GUILayout.RepeatButton("D (Órbita) >>", GUILayout.Height(30)))
        {
            movement.ExternalInput = new Vector2(1f, 0f);
        }
        else if (GUILayout.RepeatButton("▲ W (Avançar)", GUILayout.Height(30)))
        {
            movement.ExternalInput = new Vector2(0f, 1f);
        }
        else if (GUILayout.RepeatButton("▼ S (Recuar)", GUILayout.Height(30)))
        {
            movement.ExternalInput = new Vector2(0f, -1f);
        }
        else
        {
            movement.ExternalInput = Vector2.zero;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        // Ações de Ataque
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("🥊 P1 SOCO (Espaço)", GUILayout.Height(32)))
        {
            TriggerAttack();
        }

        if (opponentController != null && GUILayout.Button("💥 P2 SOCO (IA)", GUILayout.Height(32)))
        {
            opponentController.TriggerAttack();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        // Seletor de Dificuldade da IA e Reset com Standing Up
        GUILayout.BeginHorizontal();
        if (ai != null)
        {
            string btnDiff = $"🎯 Dificuldade: [{ai.Difficulty}]";
            if (GUILayout.Button(btnDiff, GUILayout.Height(30)))
            {
                ai.CycleDifficulty();
            }

            string aiToggle = ai.AutoSparring ? "IA: [ON]" : "IA: [OFF]";
            if (GUILayout.Button(aiToggle, GUILayout.Width(75), GUILayout.Height(30)))
            {
                ai.AutoSparring = !ai.AutoSparring;
            }
        }

        // Botão Rematch que aciona Standing Up!
        if (GUILayout.Button("🔄 REMATCH (Levantar)", GUILayout.Height(30)))
        {
            ResetMatch();
        }
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    /// <summary>
    /// Reinicia a partida acionando a animação Standing Up para quem estiver no chão.
    /// </summary>
    public void ResetMatch()
    {
        bool p1WasDead = healthSystem != null && healthSystem.IsDead;
        var op = movement != null && movement.Opponent != null ? movement.Opponent.GetComponent<FighterController>() : null;
        bool p2WasDead = op != null && op.HealthSystem != null && op.HealthSystem.IsDead;

        // Se alguém caiu no chão, aquele que caiu levanta com Standing Up
        // Se nenhum caiu (reset manual), ambos executam o levante para recomeçar o round
        if (p1WasDead || (!p1WasDead && !p2WasDead))
        {
            TriggerStandingUp();
        }
        else
        {
            if (healthSystem != null) healthSystem.ResetHealth();
            ChangeState(NeutralState);
        }

        if (op != null)
        {
            if (p2WasDead || (!p1WasDead && !p2WasDead))
            {
                op.TriggerStandingUp();
            }
            else
            {
                if (op.HealthSystem != null) op.HealthSystem.ResetHealth();
                op.ChangeState(op.NeutralState);
            }
        }
    }
}
