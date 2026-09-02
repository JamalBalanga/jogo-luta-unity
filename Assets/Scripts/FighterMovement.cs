using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador de movimentação 3D para jogos de luta estilo Tekken.
/// Gerencia travamento de rotação horizontal em direção ao oponente,
/// movimentação longitudinal (frente/trás) e órbita circular (sidestep)
/// com conservação exata da distância radial usando CharacterController e o novo Input System.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class FighterMovement : MonoBehaviour
{
    [Header("Opponent Target")]
    [Tooltip("Transform do lutador oponente para travamento de câmera/mira e órbita.")]
    [SerializeField] private Transform opponent;

    [Header("Movement Velocities")]
    [Tooltip("Velocidade ao avançar em direção ao oponente.")]
    [SerializeField, Min(0f)] private float forwardSpeed = 4.5f;

    [Tooltip("Velocidade ao recuar para longe do oponente.")]
    [SerializeField, Min(0f)] private float backwardSpeed = 3.5f;

    [Tooltip("Velocidade de translação orbital lateral (Sidestep).")]
    [SerializeField, Min(0f)] private float sidestepSpeed = 4.0f;

    [Tooltip("Distância mínima permitida em relação ao oponente.")]
    [SerializeField, Min(0.1f)] private float minDistanceToOpponent = 0.75f;

    [Header("Rotation")]
    [Tooltip("Travar rotação horizontal sempre voltada para o oponente.")]
    [SerializeField] private bool lockFacingOpponent = true;

    [Tooltip("Velocidade de rotação horizontal em graus por segundo (valores altos = resposta instantânea estilo arcade).")]
    [SerializeField, Min(0f)] private float rotationSpeed = 1080f;

    [Header("Physics & Gravity")]
    [Tooltip("Aceleração da gravidade aplicada no eixo Y.")]
    [SerializeField] private float gravity = -20f;

    [Tooltip("Força constante para baixo ao estar no chão para manter estabilidade em rampas/degraus.")]
    [SerializeField] private float groundedGravity = -2f;

    [Header("Input System")]
    [Tooltip("Ação de movimento (Vector2). Pode ser referenciada de um .inputactions ou configurada em runtime.")]
    [SerializeField] private InputActionReference moveActionReference;

    // Componentes e referências em cache
    private CharacterController characterController;
    private InputAction runtimeMoveAction;
    private float verticalVelocity;

    // Propriedades públicas para acesso externo e flexibilidade
    public Transform Opponent
    {
        get => opponent;
        set => opponent = value;
    }

    public bool CanMove { get; set; } = true;

    public bool IsGrounded => characterController != null && characterController.isGrounded;

    public CharacterController CharacterController => characterController;

    public Vector2 CurrentInput { get; private set; }

    public float CurrentSpeedMagnitude => CurrentInput.magnitude;

    public Vector2 ExternalInput { get; set; }

    private void Awake()
    {
        // Cache obrigatório para evitar overhead de GetComponent no ciclo de Update
        characterController = GetComponent<CharacterController>();

        InitializeInput();
    }

    private void OnEnable()
    {
        if (moveActionReference != null && moveActionReference.action != null)
        {
            moveActionReference.action.actionMap?.Enable();
            moveActionReference.action.Enable();
        }
        runtimeMoveAction?.Enable();
    }

    private void OnDisable()
    {
        runtimeMoveAction?.Disable();
    }

    private void OnDestroy()
    {
        // Limpeza de recursos alocados dinamicamente se a ação foi criada localmente
        if (moveActionReference == null && runtimeMoveAction != null)
        {
            runtimeMoveAction.Dispose();
        }
    }

    private void Update()
    {
        // 1. Atualizar e travar a orientação para encarar o oponente
        UpdateFacing();

        // 2. Processar entradas e calcular a translação
        CurrentInput = ReadMovementInput();
        Vector3 horizontalMovement = CalculateHorizontalMovement(CurrentInput);

        // 3. Processar física vertical (gravidade básica)
        Vector3 verticalMovement = CalculateVerticalMovement();

        // 4. Aplicar deslocamento final através do CharacterController
        Vector3 finalDisplacement = horizontalMovement + verticalMovement;
        characterController.Move(finalDisplacement);
    }

    /// <summary>
    /// Configura a ação do novo Unity Input System garantindo que o ActionMap seja ativado.
    /// </summary>
    private void InitializeInput()
    {
        if (moveActionReference != null && moveActionReference.action != null)
        {
            runtimeMoveAction = moveActionReference.action;
            moveActionReference.action.actionMap?.Enable();
            runtimeMoveAction.Enable();
        }
        else
        {
            // Fallback elegante: cria dinamicamente uma ação 2D para funcionar out-of-the-box
            runtimeMoveAction = new InputAction(
                name: "Movement",
                type: InputActionType.Value,
                expectedControlType: "Vector2"
            );

            // Teclado (WASD / Setas)
            runtimeMoveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            runtimeMoveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            // Gamepad (Analógico esquerdo e D-Pad)
            runtimeMoveAction.AddBinding("<Gamepad>/leftStick");
            runtimeMoveAction.AddBinding("<Gamepad>/dpad");

            runtimeMoveAction.Enable();
        }
    }

    /// <summary>
    /// Lê o vetor de entrada normalizado com suporte a ExternalInput, polling direto e InputActions.
    /// </summary>
    private Vector2 ReadMovementInput()
    {
        if (!CanMove) return Vector2.zero;

        // 1. Entrada forçada via botões da UI / OnGUI na tela
        if (ExternalInput.sqrMagnitude > 0.001f)
        {
            return ExternalInput;
        }

        Vector2 input = Vector2.zero;

        // 2. Polling direto do teclado físico via Input System
        if (Keyboard.current != null)
        {
            float x = 0f;
            float y = 0f;

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) y += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;

            input = new Vector2(x, y);
        }

        // 3. Se o teclado físico não foi pressionado, lê da InputAction (Gamepad / Stick analógico)
        if (input.sqrMagnitude < 0.001f && runtimeMoveAction != null && runtimeMoveAction.enabled)
        {
            input = runtimeMoveAction.ReadValue<Vector2>();
        }

        // Clampeamento para garantir que diagonais analógicas não excedam magnitude 1
        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        return input;
    }

    /// <summary>
    /// Mantém a rotação travada horizontalmente no oponente (ignorando a diferença de altura no eixo Y).
    /// </summary>
    private void UpdateFacing()
    {
        if (!lockFacingOpponent || opponent == null) return;

        Vector3 toOpponent = opponent.position - transform.position;
        toOpponent.y = 0f; // Look-at relativo estritamente no plano horizontal (XZ)

        if (toOpponent.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(toOpponent);

            if (rotationSpeed <= 0f)
            {
                transform.rotation = targetRotation;
            }
            else
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }
        }
    }

    /// <summary>
    /// Calcula o deslocamento horizontal combinando movimento longitudinal (frente/trás)
    /// com órbita circular (sidestep) para evitar qualquer descalibração de distância.
    /// </summary>
    private Vector3 CalculateHorizontalMovement(Vector2 input)
    {
        if (input.sqrMagnitude < 0.0001f) return Vector3.zero;

        float dt = Time.deltaTime;

        // Se não houver oponente definido, utiliza translação linear clássica
        if (opponent == null)
        {
            float fallbackSpeed = input.y >= 0f ? forwardSpeed : backwardSpeed;
            Vector3 linearMove = (transform.forward * (input.y * fallbackSpeed) +
                                  transform.right * (input.x * sidestepSpeed)) * dt;
            return linearMove;
        }

        // --- 1. Movimento Longitudinal (Frente / Trás ao longo de transform.forward) ---
        float speedAlongForward = input.y >= 0f ? forwardSpeed : backwardSpeed;
        Vector3 forwardDisplacement = transform.forward * (input.y * speedAlongForward * dt);

        // Prevenção de penetração frontal se estiver abaixo da distância mínima
        if (input.y > 0f)
        {
            Vector3 toOpponent = opponent.position - transform.position;
            toOpponent.y = 0f;
            float currentDist = toOpponent.magnitude;

            if (currentDist <= minDistanceToOpponent)
            {
                forwardDisplacement = Vector3.zero;
            }
            else if (currentDist - forwardDisplacement.magnitude < minDistanceToOpponent)
            {
                // Limita o passo para parar exatamente na distância mínima
                forwardDisplacement = transform.forward * (currentDist - minDistanceToOpponent);
            }
        }

        // --- 2. Movimento Lateral Orbital (Sidestep ao redor do oponente) ---
        Vector3 orbitalDisplacement = Vector3.zero;

        if (Mathf.Abs(input.x) > 0.001f)
        {
            Vector3 fromOpponentToPlayer = transform.position - opponent.position;
            fromOpponentToPlayer.y = 0f;
            float radius = fromOpponentToPlayer.magnitude;

            if (radius > 0.001f)
            {
                float angularSpeedDegrees = (sidestepSpeed / radius) * Mathf.Rad2Deg;
                float angleDelta = -input.x * angularSpeedDegrees * dt;

                Quaternion orbitRotation = Quaternion.Euler(0f, angleDelta, 0f);
                Vector3 rotatedOffset = orbitRotation * fromOpponentToPlayer;

                orbitalDisplacement = rotatedOffset - fromOpponentToPlayer;
            }
            else
            {
                orbitalDisplacement = transform.right * (input.x * sidestepSpeed * dt);
            }
        }

        return forwardDisplacement + orbitalDisplacement;
    }

    /// <summary>
    /// Gerencia a gravidade básica com grounding estável para o CharacterController.
    /// </summary>
    private Vector3 CalculateVerticalMovement()
    {
        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedGravity;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        return Vector3.up * (verticalVelocity * Time.deltaTime);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (opponent == null) return;

        Vector3 flatPlayerPos = transform.position;
        Vector3 flatOpponentPos = new Vector3(opponent.position.x, transform.position.y, opponent.position.z);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(flatPlayerPos, flatOpponentPos);

        float radius = Vector3.Distance(flatPlayerPos, flatOpponentPos);
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.35f);
        Gizmos.DrawWireSphere(flatOpponentPos, radius);

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(flatOpponentPos, minDistanceToOpponent);
    }
#endif
}
