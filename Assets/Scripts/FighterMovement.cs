using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador de movimentação 3D para jogos de luta estilo Tekken.
/// Mantém o peito do lutador SEMPRE travado no oponente (look-at relativo no plano horizontal XZ).
/// O recuo (Input Vertical < 0) se desloca ao longo de -transform.forward sem girar o corpo.
/// Expõe ForwardInput e RightInput para animações sem Root Motion.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class FighterMovement : MonoBehaviour
{
    [Header("Opponent Target")]
    [Tooltip("Transform do lutador oponente para travamento de combate e órbita.")]
    [SerializeField] private Transform opponent;

    [Header("Control Settings")]
    [Tooltip("Define se este lutador responde aos comandos físicos do teclado/gamepad do jogador (P1) ou apenas à IA (P2).")]
    [SerializeField] private bool isPlayerControlled = true;

    [Header("Movement Velocities")]
    [Tooltip("Velocidade ao avançar em direção ao oponente.")]
    [SerializeField, Min(0f)] private float forwardSpeed = 4.5f;

    [Tooltip("Velocidade ao recuar para longe do oponente (backdash/walk back).")]
    [SerializeField, Min(0f)] private float backwardSpeed = 3.8f;

    [Tooltip("Velocidade de translação orbital lateral (Sidestep).")]
    [SerializeField, Min(0f)] private float sidestepSpeed = 4.0f;

    [Tooltip("Distância mínima permitida em relação ao oponente.")]
    [SerializeField, Min(0.1f)] private float minDistanceToOpponent = 0.75f;

    [Header("Rotation")]
    [Tooltip("Travar rotação horizontal sempre voltada para o oponente.")]
    [SerializeField] private bool lockFacingOpponent = true;

    [Tooltip("Velocidade de rotação horizontal em graus por segundo.")]
    [SerializeField, Min(0f)] private float rotationSpeed = 1080f;

    [Header("Physics & Gravity")]
    [Tooltip("Aceleração da gravidade aplicada no eixo Y.")]
    [SerializeField] private float gravity = -20f;

    [Tooltip("Força constante para baixo ao estar no chão para manter estabilidade.")]
    [SerializeField] private float groundedGravity = -2f;

    [Header("Input System")]
    [Tooltip("Ação de movimento (Vector2).")]
    [SerializeField] private InputActionReference moveActionReference;

    // Componentes e referências em cache
    private CharacterController characterController;
    private InputAction runtimeMoveAction;
    private float verticalVelocity;

    // Propriedades públicas solicitadas
    public Transform Opponent
    {
        get => opponent;
        set => opponent = value;
    }

    public bool IsPlayerControlled
    {
        get => isPlayerControlled;
        set => isPlayerControlled = value;
    }

    public bool CanMove { get; set; } = true;
    public bool IsGrounded => characterController != null && characterController.isGrounded;
    public CharacterController CharacterController => characterController;

    public Vector2 CurrentInput { get; private set; }
    public float CurrentSpeedMagnitude => CurrentInput.magnitude;

    /// <summary>
    /// Entrada vertical normalizada (-1 recuando, +1 avançando) para o Animator.
    /// </summary>
    public float ForwardInput => CurrentInput.y;

    /// <summary>
    /// Entrada lateral normalizada (-1 esquerda, +1 direita) para o Animator.
    /// </summary>
    public float RightInput => CurrentInput.x;

    public Vector2 ExternalInput { get; set; }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (isPlayerControlled)
        {
            InitializeInput();
        }
    }

    private void OnEnable()
    {
        if (isPlayerControlled)
        {
            if (moveActionReference != null && moveActionReference.action != null)
            {
                moveActionReference.action.actionMap?.Enable();
                moveActionReference.action.Enable();
            }
            runtimeMoveAction?.Enable();
        }
    }

    private void OnDisable()
    {
        runtimeMoveAction?.Disable();
    }

    private void OnDestroy()
    {
        if (moveActionReference == null && runtimeMoveAction != null)
        {
            runtimeMoveAction.Dispose();
        }
    }

    private void Update()
    {
        // 1. Processar entradas do jogador ou da IA
        CurrentInput = ReadMovementInput();

        // 2. Travar SEMPRE o peito e olhar no oponente no plano horizontal (XZ)
        UpdateFacing();

        // 3. Calcular translação horizontal (longitudinal e órbita)
        Vector3 horizontalMovement = CalculateHorizontalMovement(CurrentInput);

        // 4. Processar gravidade básica
        Vector3 verticalMovement = CalculateVerticalMovement();

        // 5. Aplicar deslocamento final através do CharacterController
        Vector3 finalDisplacement = horizontalMovement + verticalMovement;
        characterController.Move(finalDisplacement);
    }

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
            runtimeMoveAction = new InputAction(name: "Movement", type: InputActionType.Value, expectedControlType: "Vector2");
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

            runtimeMoveAction.AddBinding("<Gamepad>/leftStick");
            runtimeMoveAction.AddBinding("<Gamepad>/dpad");
            runtimeMoveAction.Enable();
        }
    }

    private Vector2 ReadMovementInput()
    {
        if (!CanMove) return Vector2.zero;

        if (ExternalInput.sqrMagnitude > 0.001f)
        {
            return ExternalInput;
        }

        if (!isPlayerControlled)
        {
            return Vector2.zero;
        }

        Vector2 input = Vector2.zero;

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

        if (input.sqrMagnitude < 0.001f && runtimeMoveAction != null && runtimeMoveAction.enabled)
        {
            input = runtimeMoveAction.ReadValue<Vector2>();
        }

        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        return input;
    }

    /// <summary>
    /// Mantém o peito do lutador SEMPRE rigorosamente voltado para o oponente no plano horizontal (XZ),
    /// ignorando por completo qualquer diferença de altura no eixo Y.
    /// O lutador NUNCA inverte ou rotaciona o corpo 180 graus ao recuar.
    /// </summary>
    private void UpdateFacing()
    {
        if (!lockFacingOpponent || opponent == null) return;

        Vector3 toOpponent = opponent.position - transform.position;
        toOpponent.y = 0f; // Look-at estritamente no plano horizontal XZ (ignora o eixo Y)

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
    /// Calcula o deslocamento horizontal.
    /// Ao pressionar para recuar (Input Vertical < 0), o personagem se desloca para trás
    /// ao longo de -transform.forward mantendo a orientação do peito fixa no oponente.
    /// </summary>
    private Vector3 CalculateHorizontalMovement(Vector2 input)
    {
        if (input.sqrMagnitude < 0.0001f) return Vector3.zero;

        float dt = Time.deltaTime;

        if (opponent == null)
        {
            float fallbackSpeed = input.y >= 0f ? forwardSpeed : backwardSpeed;
            return (transform.forward * (input.y * fallbackSpeed) + transform.right * (input.x * sidestepSpeed)) * dt;
        }

        // --- 1. Movimento Longitudinal (Avanço via transform.forward / Recuo via -transform.forward) ---
        Vector3 longitudinalDisplacement = Vector3.zero;

        if (Mathf.Abs(input.y) > 0.001f)
        {
            if (input.y > 0f)
            {
                // Avançando em direção ao oponente ao longo de transform.forward
                float step = forwardSpeed * input.y * dt;
                Vector3 toOpponent = opponent.position - transform.position;
                toOpponent.y = 0f;
                float currentDist = toOpponent.magnitude;

                if (currentDist - step > minDistanceToOpponent)
                {
                    longitudinalDisplacement = transform.forward * step;
                }
                else if (currentDist > minDistanceToOpponent)
                {
                    longitudinalDisplacement = transform.forward * (currentDist - minDistanceToOpponent);
                }
            }
            else
            {
                // Recuando para longe do oponente via -transform.forward mantendo a guarda frontal
                float step = backwardSpeed * Mathf.Abs(input.y) * dt;
                longitudinalDisplacement = -transform.forward * step;
            }
        }

        // --- 2. Movimento Lateral Orbital (Sidestep ao redor do oponente com raio fixo) ---
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

        return longitudinalDisplacement + orbitalDisplacement;
    }

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
}
