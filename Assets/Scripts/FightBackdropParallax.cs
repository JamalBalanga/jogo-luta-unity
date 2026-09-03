using UnityEngine;

/// <summary>
/// Controla um painel de fundo (backdrop) em uma arena de luta 3D estilo Tekken.
/// Posiciona o painel instantaneamente atrás da linha de visão da câmera (sem atraso ou lag elástico),
/// com dimensões panorâmicas amplas (160m x 80m) que cobrem todo o horizonte em qualquer ângulo de 360º.
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public class FightBackdropParallax : MonoBehaviour
{
    [Header("Target References")]
    [Tooltip("Câmera principal do jogo. Se nulo, busca Camera.main automaticamente.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Lutador 1 (Player 1).")]
    [SerializeField] private Transform fighter1;

    [Tooltip("Lutador 2 (Player 2).")]
    [SerializeField] private Transform fighter2;

    [Header("Positioning Settings")]
    [Tooltip("Distância horizontal do Quad atrás do ponto médio do combate (em metros).")]
    [SerializeField, Min(10f)] private float distanceFromMidpoint = 45f;

    [Tooltip("Offset de elevação vertical do Quad em relação ao chão da arena.")]
    [SerializeField] private float heightOffset = 14f;

    [Tooltip("Dimensões panorâmicas de largura (X) e altura (Y) do Quad.")]
    [SerializeField] private Vector2 quadDimensions = new Vector2(160f, 80f);

    [Header("Parallax & Framing")]
    [Tooltip("Intensidade do deslocamento lateral de paralaxe ao orbitar.")]
    [SerializeField, Range(0f, 1f)] private float parallaxOffsetIntensity = 0.25f;

    [Tooltip("Trava o eixo vertical (Pitch) para manter o painel sempre reto e perpendicular.")]
    [SerializeField] private bool lockVerticalTilt = true;

    [Header("Physics & Isolation")]
    [Tooltip("Remove colisores para garantir zero interferência física com os lutadores.")]
    [SerializeField] private bool removeColliders = true;

    [Tooltip("Layer para isolamento físico.")]
    [SerializeField] private string targetLayerName = "BackgroundProps";

    public Camera TargetCamera { get => targetCamera; set => targetCamera = value; }
    public Transform Fighter1 { get => fighter1; set => fighter1 = value; }
    public Transform Fighter2 { get => fighter2; set => fighter2 = value; }

    private void Reset()
    {
        RemoveAttachedColliders();
        ResolveReferences();
        ApplyQuadDimensions();
    }

    private void Awake()
    {
        if (removeColliders)
        {
            RemoveAttachedColliders();
        }

        int layerId = LayerMask.NameToLayer(targetLayerName);
        if (layerId >= 0)
        {
            gameObject.layer = layerId;
        }

        ApplyQuadDimensions();
    }

    private void Start()
    {
        ResolveReferences();
        ApplyQuadDimensions();
        UpdateBackdropTransform();
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            ResolveReferences();
            if (targetCamera == null) return;
        }

        UpdateBackdropTransform();
    }

    [ContextMenu("Remove Colliders")]
    public void RemoveAttachedColliders()
    {
        var colliders = GetComponentsInChildren<Collider>();
        foreach (var c in colliders)
        {
            if (Application.isPlaying)
                Destroy(c);
            else
                DestroyImmediate(c);
        }
    }

    public void ResolveReferences()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (fighter1 == null || fighter2 == null)
        {
            var p1 = GameObject.Find("Fighter_P1");
            var p2 = GameObject.Find("Fighter_P2");

            if (p1 != null) fighter1 = p1.transform;
            if (p2 != null) fighter2 = p2.transform;

            if (fighter1 == null || fighter2 == null)
            {
                var fighters = FindObjectsByType<FighterMovement>(FindObjectsSortMode.None);
                if (fighters.Length >= 2)
                {
                    fighter1 = fighters[0].transform;
                    fighter2 = fighters[1].transform;
                }
            }
        }
    }

    [ContextMenu("Apply Dimensions")]
    public void ApplyQuadDimensions()
    {
        transform.localScale = new Vector3(quadDimensions.x, quadDimensions.y, 1f);
    }

    /// <summary>
    /// Posiciona o painel perfeitamente na linha de visão da câmera sem lag ou atraso elástico.
    /// Com dimensões panorâmicas amplas (160m x 80m), o fundo preenche 100% da visão em qualquer ângulo.
    /// </summary>
    private void UpdateBackdropTransform()
    {
        Vector3 midpoint = GetCombatMidpoint();

        // 1. Linha de visada horizontal direta da câmera através do ponto médio da luta
        Vector3 camToMid = midpoint - targetCamera.transform.position;
        if (lockVerticalTilt) camToMid.y = 0f;
        if (camToMid.sqrMagnitude < 0.001f) camToMid = Vector3.forward;
        Vector3 viewDirection = camToMid.normalized;

        // 2. Vetor lateral perpendicular para efeito sutil de paralaxe
        Vector3 sideVector = Vector3.Cross(Vector3.up, viewDirection);

        // 3. Posição Alvo: Cravada na linha de visão (sem lerp posicional lento que causava atraso)
        Vector3 targetPosition = midpoint + (viewDirection * distanceFromMidpoint);
        targetPosition.y = midpoint.y + heightOffset;

        // Aplica deslocamento lateral sutil de paralaxe proporcional à movimentação
        if (parallaxOffsetIntensity > 0f && fighter1 != null && fighter2 != null)
        {
            float fighterDeltaX = (fighter1.position.x - fighter2.position.x);
            targetPosition += sideVector * (fighterDeltaX * parallaxOffsetIntensity);
        }

        transform.position = targetPosition;

        // 4. Orientação: Encarando diretamente a câmera para nunca ficar de perfil
        transform.rotation = Quaternion.LookRotation(viewDirection);
    }

    private Vector3 GetCombatMidpoint()
    {
        if (fighter1 != null && fighter2 != null)
        {
            return (fighter1.position + fighter2.position) * 0.5f;
        }

        return Vector3.zero;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (quadDimensions.x > 0f && quadDimensions.y > 0f)
        {
            ApplyQuadDimensions();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.5f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
#endif
}
