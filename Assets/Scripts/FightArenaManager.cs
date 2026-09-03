using System.Collections.Generic;
using UnityEngine;

public enum ArenaShape
{
    Circular,
    Rectangular
}

/// <summary>
/// Gerenciador físico e lógico da arena de luta 3D estilo Tekken.
/// Controla as dimensões jogáveis, gera colisores de contenção estáticos e espessos (Zero Tunneling),
/// aplica clamp de contenção forçada para CharacterControllers e fornece telemetria para a câmera.
/// </summary>
[DisallowMultipleComponent]
public class FightArenaManager : MonoBehaviour
{
    [Header("Arena Geometry & Mode")]
    [Tooltip("Formato da arena jogável.")]
    [SerializeField] private ArenaShape shape = ArenaShape.Circular;

    [Tooltip("Raio da arena circular jogável (em metros).")]
    [SerializeField, Min(3f)] private float arenaRadius = 7.5f;

    [Tooltip("Dimensões X (largura) e Z (comprimento) da arena retangular (em metros).")]
    [SerializeField] private Vector2 arenaDimensions = new Vector2(15f, 15f);

    [Tooltip("Offset tridimensional do centro da arena.")]
    [SerializeField] private Vector3 centerOffset = Vector3.zero;

    [Header("Boundary Colliders Settings")]
    [Tooltip("Altura das paredes invisíveis de contenção.")]
    [SerializeField, Min(1f)] private float wallHeight = 4.5f;

    [Tooltip("Espessura dos colisores de parede (espessura generosa elimina tunneling de CharacterController).")]
    [SerializeField, Min(0.5f)] private float wallThickness = 2.0f;

    [Tooltip("Número de segmentos para aproximação poligonal da arena circular.")]
    [SerializeField, Range(12, 48)] private int circleSegments = 24;

    [Tooltip("Layer atribuída às paredes geradas (ex: ArenaWall).")]
    [SerializeField] private string wallLayerName = "ArenaWall";

    [Header("Anti-Tunneling Failsafe")]
    [Tooltip("Aplica contenção forçada no LateUpdate como garantia contra tunneling sob knockbacks extremos.")]
    [SerializeField] private bool autoEnforceBounds = true;

    [Tooltip("Lutadores monitorados para contenção automática.")]
    [SerializeField] private List<Transform> registeredFighterTransforms = new List<Transform>();

    private GameObject boundaryContainer;
    private const string BoundaryContainerName = "_GeneratedArenaBoundaries";

    public ArenaShape Shape => shape;
    public float ArenaRadius => arenaRadius;
    public Vector2 ArenaDimensions => arenaDimensions;

    private void Awake()
    {
        GenerateBoundaries();
        FindAndRegisterFighters();
    }

    private void Start()
    {
        FindAndRegisterFighters();
    }

    private void LateUpdate()
    {
        if (autoEnforceBounds)
        {
            for (int i = 0; i < registeredFighterTransforms.Count; i++)
            {
                EnforceBounds(registeredFighterTransforms[i]);
            }
        }
    }

    /// <summary>
    /// Retorna o centro geométrico global da arena jogável.
    /// </summary>
    public Vector3 GetArenaCenter()
    {
        return transform.position + centerOffset;
    }

    /// <summary>
    /// Retorna o raio efetivo da arena (se retangular, calcula o raio circunscrito).
    /// </summary>
    public float GetArenaRadius()
    {
        if (shape == ArenaShape.Circular)
        {
            return arenaRadius;
        }

        // Raio circunscrito da diagonal para suporte à câmera
        return arenaDimensions.magnitude * 0.5f;
    }

    /// <summary>
    /// Registra o Transform de um lutador para contenção automática contínua.
    /// </summary>
    public void RegisterFighter(Transform target)
    {
        if (target != null && !registeredFighterTransforms.Contains(target))
        {
            registeredFighterTransforms.Add(target);
        }
    }

    private void FindAndRegisterFighters()
    {
        if (registeredFighterTransforms.Count == 0)
        {
            var fighters = FindObjectsByType<FighterMovement>(FindObjectsSortMode.None);
            foreach (var f in fighters)
            {
                RegisterFighter(f.transform);
            }
        }
    }

    /// <summary>
    /// Método de Failsafe: Clampeia forçadamente o Transform alvo dentro dos limites da arena no plano X/Z,
    /// considerando o raio do CharacterController para impedir qualquer saída do ringue.
    /// </summary>
    public void EnforceBounds(Transform targetTransform)
    {
        if (targetTransform == null) return;

        Vector3 pos = targetTransform.position;
        Vector3 center = GetArenaCenter();

        float fighterRadius = 0.5f;
        var cc = targetTransform.GetComponent<CharacterController>();
        if (cc != null) fighterRadius = cc.radius;

        if (shape == ArenaShape.Circular)
        {
            Vector3 delta = pos - center;
            delta.y = 0f;
            float dist = delta.magnitude;
            float maxAllowed = arenaRadius - fighterRadius;

            if (dist > maxAllowed && dist > 0.001f)
            {
                Vector3 clampedPos = center + (delta.normalized * maxAllowed);
                clampedPos.y = pos.y;
                targetTransform.position = clampedPos;
            }
        }
        else
        {
            float halfX = (arenaDimensions.x * 0.5f) - fighterRadius;
            float halfZ = (arenaDimensions.y * 0.5f) - fighterRadius;

            float clampedX = Mathf.Clamp(pos.x, center.x - halfX, center.x + halfX);
            float clampedZ = Mathf.Clamp(pos.z, center.z - halfZ, center.z + halfZ);

            targetTransform.position = new Vector3(clampedX, pos.y, clampedZ);
        }
    }

    /// <summary>
    /// Gera proceduralmente a barreira de colisores BoxCollider estáticos invisíveis.
    /// Pode ser executado em runtime ou via ContextMenu no Editor.
    /// </summary>
    [ContextMenu("Regenerate Boundaries")]
    public void GenerateBoundaries()
    {
        // 1. Destrói contêiner anterior
        var existing = transform.Find(BoundaryContainerName);
        if (existing != null)
        {
            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);
        }

        // 2. Cria contêiner hierárquico limpo
        boundaryContainer = new GameObject(BoundaryContainerName);
        boundaryContainer.transform.SetParent(transform);
        boundaryContainer.transform.localPosition = centerOffset;
        boundaryContainer.transform.localRotation = Quaternion.identity;

        int targetLayer = LayerMask.NameToLayer(wallLayerName);
        if (targetLayer < 0) targetLayer = 0; // Default fallback

        if (shape == ArenaShape.Circular)
        {
            float angleStep = 360f / circleSegments;
            float segmentWidth = 2f * arenaRadius * Mathf.Tan(Mathf.Deg2Rad * (angleStep * 0.5f)) * 1.05f;

            for (int i = 0; i < circleSegments; i++)
            {
                float angle = i * angleStep;
                Quaternion rot = Quaternion.Euler(0f, angle, 0f);
                Vector3 dir = rot * Vector3.forward;

                Vector3 wallPos = dir * (arenaRadius + (wallThickness * 0.5f));
                wallPos.y = wallHeight * 0.5f;

                var wall = new GameObject($"Boundary_Segment_{i:00}");
                wall.transform.SetParent(boundaryContainer.transform);
                wall.transform.localPosition = wallPos;
                wall.transform.localRotation = rot;
                wall.layer = targetLayer;

                var col = wall.AddComponent<BoxCollider>();
                col.size = new Vector3(segmentWidth, wallHeight, wallThickness);
                col.isTrigger = false;
            }
        }
        else
        {
            float halfX = arenaDimensions.x * 0.5f;
            float halfZ = arenaDimensions.y * 0.5f;

            CreateWall(boundaryContainer.transform, targetLayer, "Wall_North",
                new Vector3(0f, wallHeight * 0.5f, halfZ + (wallThickness * 0.5f)),
                new Vector3(arenaDimensions.x + (wallThickness * 2f), wallHeight, wallThickness));

            CreateWall(boundaryContainer.transform, targetLayer, "Wall_South",
                new Vector3(0f, wallHeight * 0.5f, -(halfZ + (wallThickness * 0.5f))),
                new Vector3(arenaDimensions.x + (wallThickness * 2f), wallHeight, wallThickness));

            CreateWall(boundaryContainer.transform, targetLayer, "Wall_East",
                new Vector3(halfX + (wallThickness * 0.5f), wallHeight * 0.5f, 0f),
                new Vector3(wallThickness, wallHeight, arenaDimensions.y + (wallThickness * 2f)));

            CreateWall(boundaryContainer.transform, targetLayer, "Wall_West",
                new Vector3(-(halfX + (wallThickness * 0.5f)), wallHeight * 0.5f, 0f),
                new Vector3(wallThickness, wallHeight, arenaDimensions.y + (wallThickness * 2f)));
        }
    }

    private void CreateWall(Transform parent, int layer, string name, Vector3 localPos, Vector3 size)
    {
        var wall = new GameObject(name);
        wall.transform.SetParent(parent);
        wall.transform.localPosition = localPos;
        wall.transform.localRotation = Quaternion.identity;
        wall.layer = layer;

        var col = wall.AddComponent<BoxCollider>();
        col.size = size;
        col.isTrigger = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Vector3 center = GetArenaCenter();

        // 1. Centro da Arena (Esfera Amarela)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, 0.4f);

        // 2. Área Jogável (Verde)
        Gizmos.color = Color.green;
        if (shape == ArenaShape.Circular)
        {
            DrawWireDisc(center, arenaRadius);

            // 3. Barreiras Ativas (Linhas / Anéis Vermelhos)
            Gizmos.color = Color.red;
            DrawWireDisc(center, arenaRadius + wallThickness);
        }
        else
        {
            Vector3 playableSize = new Vector3(arenaDimensions.x, 0.05f, arenaDimensions.y);
            Gizmos.DrawWireCube(center, playableSize);

            // Barreiras Ativas (Retângulo Vermelho)
            Gizmos.color = Color.red;
            Vector3 outerSize = new Vector3(arenaDimensions.x + (wallThickness * 2f), 0.05f, arenaDimensions.y + (wallThickness * 2f));
            Gizmos.DrawWireCube(center, outerSize);
        }
    }

    private void DrawWireDisc(Vector3 center, float radius)
    {
        int segs = 36;
        float step = 360f / segs;
        Vector3 prev = center + new Vector3(Mathf.Cos(0f) * radius, 0f, Mathf.Sin(0f) * radius);

        for (int i = 1; i <= segs; i++)
        {
            float rad = Mathf.Deg2Rad * (i * step);
            Vector3 next = center + new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}
