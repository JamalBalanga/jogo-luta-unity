using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador principal do lutador.
/// Gerencia a Máquina de Estados Finitos (FSM), coordena a movimentação, animações,
/// sistema de combate com Hitboxes/Hurtboxes e processa comandos via Unity Input System.
/// </summary>
[RequireComponent(typeof(FighterMovement))]
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
    [Tooltip("Saúde máxima do lutador.")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;

    [Tooltip("Dano padrão desferido por golpes básicos.")]
    [SerializeField, Min(0f)] private float defaultAttackDamage = 15f;

    [Tooltip("Força padrão de knockback aplicada ao atingir o oponente.")]
    [SerializeField, Min(0f)] private float defaultKnockbackForce = 4.0f;

    [Tooltip("Duração total do golpe (ativação + recovery) antes de voltar ao estado Neutro.")]
    [SerializeField, Min(0.05f)] private float attackDuration = 0.85f;

    [Tooltip("Duração padrão do congelamento por dano (Hit Stun) quando não especificado pelo golpe.")]
    [SerializeField, Min(0.05f)] private float defaultHitStunDuration = 0.45f;

    [Header("Input Actions")]
    [Tooltip("Ação de ataque do novo Input System. Opcional: cria fallback automático se nulo.")]
    [SerializeField] private InputActionReference attackActionReference;

    [Header("Animation State / Trigger Names")]
    [SerializeField] private string neutralAnimName = "Idle";
    [SerializeField] private string attackAnimName = "Attack";
    [SerializeField] private string hitStunAnimName = "HitStun";

    [Header("Debug")]
    [SerializeField] private bool showOnScreenControls = true;
    [SerializeField] private float currentHealth = 100f;
    [SerializeField] private string currentStateDebug;

    // Componentes e ações em cache
    private FighterMovement movement;
    private InputAction runtimeAttackAction;
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

    // Instâncias cacheadas dos estados FSM (Zero GC em transições)
    public NeutralState NeutralState { get; private set; }
    public AttackState AttackState { get; private set; }
    public HitStunState HitStunState { get; private set; }

    // Estado ativo
    public IFighterState CurrentState { get; private set; }

    // Getters públicos
    public FighterMovement Movement => movement;
    public Animator Animator => animator;
    public float AttackDuration => attackDuration;
    public float DefaultHitStunDuration => defaultHitStunDuration;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    private void Awake()
    {
        movement = GetComponent<FighterMovement>();
        currentHealth = maxHealth;

        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        // Cache de Hitboxes nos membros
        RefreshHitboxCache();

        NeutralAnimHash = Animator.StringToHash(neutralAnimName);
        AttackAnimHash = Animator.StringToHash(attackAnimName);
        HitStunAnimHash = Animator.StringToHash(hitStunAnimName);

        NeutralState = new NeutralState();
        AttackState = new AttackState();
        HitStunState = new HitStunState();

        InitializeAttackInput();
    }

    private void OnEnable()
    {
        if (attackActionReference != null && attackActionReference.action != null)
        {
            attackActionReference.action.actionMap?.Enable();
            attackActionReference.action.Enable();
        }
        runtimeAttackAction?.Enable();
    }

    private void OnDisable()
    {
        runtimeAttackAction?.Disable();
        DisableAllHitboxes();
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

    /// <summary>
    /// Registra e indexa todas as hitboxes dos nós filhos por membro.
    /// </summary>
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

    /// <summary>
    /// Transiciona de forma segura entre estados da FSM.
    /// </summary>
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
        if (CurrentState is NeutralState)
        {
            ChangeState(AttackState);
        }
    }

    // ========================================================================
    // MÉTODOS PÚBLICOS PARA ANIMATION EVENTS
    // ========================================================================

    /// <summary>
    /// Chamado por Animation Event para ativar a hitbox de um membro específico.
    /// Aceita string compatível com o enum HitboxLimb (Ex: "RightHand", "LeftHand", "RightFoot").
    /// </summary>
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

    /// <summary>
    /// Chamado por Animation Event para desativar a hitbox de um membro específico.
    /// </summary>
    public void DisableHitbox(string limbName)
    {
        if (Enum.TryParse(limbName, true, out HitboxLimb limb))
        {
            DisableHitbox(limb);
        }
    }

    /// <summary>
    /// Ativa a hitbox de um membro com os valores padrão de dano deste lutador.
    /// </summary>
    public void EnableHitbox(HitboxLimb limb)
    {
        Vector3 knockbackDir = transform.forward * defaultKnockbackForce;
        DamageData defaultData = new DamageData(defaultAttackDamage, defaultHitStunDuration, knockbackDir, this);
        EnableHitbox(limb, defaultData);
    }

    /// <summary>
    /// Ativa a hitbox de um membro fornecendo DamageData customizado.
    /// </summary>
    public void EnableHitbox(HitboxLimb limb, DamageData data)
    {
        if (hitboxMap.TryGetValue(limb, out var hb))
        {
            hb.Activate(data);
        }
    }

    /// <summary>
    /// Desativa a hitbox do membro especificado.
    /// </summary>
    public void DisableHitbox(HitboxLimb limb)
    {
        if (hitboxMap.TryGetValue(limb, out var hb))
        {
            hb.Deactivate();
        }
    }

    /// <summary>
    /// Desativa todas as hitboxes do personagem (chamado ao finalizar o ataque ou interromper por dano).
    /// </summary>
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

    /// <summary>
    /// Ponto de entrada oficial chamado pela Hurtbox quando uma Hitbox atinge este lutador.
    /// </summary>
    public void ApplyDamage(DamageData data, Hitbox sourceHitbox)
    {
        currentHealth = Mathf.Max(0f, currentHealth - data.damage);

        // Aplica impulso de recuo (Knockback) através do CharacterController
        if (movement != null && movement.CharacterController != null && data.knockback.sqrMagnitude > 0.01f)
        {
            Vector3 push = data.knockback * Time.deltaTime;
            movement.CharacterController.Move(push);
        }

        // Força transição para HitStun
        TakeHit(data.hitStunDuration);
    }

    /// <summary>
    /// Transiciona para o estado de congelamento por dano (HitStun).
    /// </summary>
    public void TakeHit(float stunDuration = -1f)
    {
        // Interrompe qualquer hitbox ativa ao tomar dano
        DisableAllHitboxes();

        if (stunDuration > 0f)
        {
            HitStunState.SetStunDuration(stunDuration);
        }

        ChangeState(HitStunState);
    }

    /// <summary>
    /// Verifica se a ação de ataque foi acionada (suporta Input Action, Teclado, Mouse e Gamepad).
    /// </summary>
    public bool IsAttackTriggered()
    {
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

        // 1. Painel de Status em Tempo Real
        GUILayout.BeginArea(new Rect(15, 15, 360, 180), boxStyle);
        GUILayout.Label("<b>CONTROLE & DIAGNÓSTICO DOS LUTADORES</b>");
        
        var opponentController = movement.Opponent != null ? movement.Opponent.GetComponent<FighterController>() : null;
        GUILayout.Label($"P1 Saúde: <b><color=lime>{currentHealth:F0}/{maxHealth}</color></b> | Estado: <b><color=yellow>{CurrentState?.GetType().Name}</color></b>");
        GUILayout.Label($"P2 Saúde: <b><color=cyan>{(opponentController != null ? opponentController.CurrentHealth.ToString("F0") : "N/A")}/100</color></b> | Estado: <b><color=yellow>{(opponentController != null ? opponentController.CurrentState?.GetType().Name : "N/A")}</color></b>");
        GUILayout.Label($"Input P1: X={movement.CurrentInput.x:F2}, Y={movement.CurrentInput.y:F2}");
        GUILayout.Label($"Último Comando: <color=lime>{lastDetectedInput}</color>");
        GUILayout.EndArea();

        // 2. Controles Virtuais Interativos
        GUILayout.BeginArea(new Rect(15, 205, 360, 175), boxStyle);
        GUILayout.Label("<b>TESTE RÁPIDO COM O MOUSE:</b>");

        GUILayout.BeginHorizontal();
        if (GUILayout.RepeatButton("<< P1 Órbita (A)", GUILayout.Height(32)))
        {
            movement.ExternalInput = new Vector2(-1f, 0f);
        }
        else if (GUILayout.RepeatButton("P1 Órbita (D) >>", GUILayout.Height(32)))
        {
            movement.ExternalInput = new Vector2(1f, 0f);
        }
        else
        {
            movement.ExternalInput = Vector2.zero;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("🥊 P1 SOCO", GUILayout.Height(34)))
        {
            TriggerAttack();
        }

        if (opponentController != null && GUILayout.Button("💥 P2 SOCO", GUILayout.Height(34)))
        {
            opponentController.TriggerAttack();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        var ai = movement.Opponent != null ? movement.Opponent.GetComponent<FighterSparringAI>() : null;
        if (ai != null)
        {
            string aiText = ai.AutoSparring ? "🤖 IA de Treino P2: [LIGADA]" : "🤖 IA de Treino P2: [DESLIGADA]";
            if (GUILayout.Button(aiText, GUILayout.Height(30)))
            {
                ai.AutoSparring = !ai.AutoSparring;
            }
        }

        GUILayout.EndArea();
    }
}
