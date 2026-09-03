using UnityEngine;

/// <summary>
/// Utilitário para configuração e alinhamento do piso base da arena.
/// Cria ou ajusta um BoxCollider de alta espessura voltado para baixo, garantindo que
/// raycasts de gravidade, verificações isGrounded e física do CharacterController
/// nunca sofram tunneling ou vazamento através do solo, mesmo com quedas bruscas de framerate.
/// </summary>
[DisallowMultipleComponent]
[ExecuteInEditMode]
public class ArenaGroundSetup : MonoBehaviour
{
    [Header("Floor Alignment")]
    [Tooltip("Nível exato da superfície onde os pés dos lutadores pisam (geralmente Y = 0).")]
    [SerializeField] private float surfaceY = 0f;

    [Tooltip("Espessura física do colisor para baixo. Colisores com mais de 1.5m impedem tunneling de gravidade.")]
    [SerializeField, Min(0.5f)] private float floorThickness = 2.0f;

    [Tooltip("Dimensões horizontais X e Z da placa de solo.")]
    [SerializeField] private Vector2 groundSize = new Vector2(24f, 24f);

    [Header("Layer Configuration")]
    [Tooltip("Atribuir automaticamente à layer física de solo.")]
    [SerializeField] private bool autoAssignLayer = true;

    [Tooltip("Nome da layer configurada para solo (ex: ArenaGround).")]
    [SerializeField] private string groundLayerName = "ArenaGround";

    [Header("Physic Material")]
    [Tooltip("Material de física opcional com atrito customizado.")]
    [SerializeField] private PhysicsMaterial groundPhysicMaterial;

    private BoxCollider boxCollider;

    private void Awake()
    {
        SetupGroundCollider();
    }

    /// <summary>
    /// Instancia ou calibra o BoxCollider da base de combate.
    /// Posiciona a face superior em surfaceY e estende o volume sólido para baixo.
    /// </summary>
    [ContextMenu("Setup / Align Ground Collider")]
    public void SetupGroundCollider()
    {
        boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = gameObject.AddComponent<BoxCollider>();
        }

        // Garante colisão sólida (não trigger)
        boxCollider.isTrigger = false;

        if (groundPhysicMaterial != null)
        {
            boxCollider.material = groundPhysicMaterial;
        }

        // Calcula centro e tamanho relativo ao Transform local
        // O topo fica em surfaceY, o centro fica em surfaceY - (floorThickness / 2)
        float localSurfaceY = surfaceY - transform.position.y;
        float centerY = localSurfaceY - (floorThickness * 0.5f);

        boxCollider.center = new Vector3(0f, centerY, 0f);
        boxCollider.size = new Vector3(groundSize.x, floorThickness, groundSize.y);

        // Atribui Layer se configurado
        if (autoAssignLayer)
        {
            int layerId = LayerMask.NameToLayer(groundLayerName);
            if (layerId >= 0)
            {
                gameObject.layer = layerId;
            }
        }
    }

    /// <summary>
    /// Sincroniza as dimensões deste piso automaticamente com o FightArenaManager mais próximo.
    /// </summary>
    [ContextMenu("Sync with FightArenaManager")]
    public void SyncWithArenaManager()
    {
        var manager = GetComponentInParent<FightArenaManager>() ?? FindFirstObjectByType<FightArenaManager>();
        if (manager != null)
        {
            if (manager.Shape == ArenaShape.Circular)
            {
                float diameter = (manager.ArenaRadius + 4f) * 2f;
                groundSize = new Vector2(diameter, diameter);
            }
            else
            {
                groundSize = manager.ArenaDimensions + new Vector2(8f, 8f);
            }

            SetupGroundCollider();
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.4f, 0.35f);
        Vector3 center = transform.position + new Vector3(0f, surfaceY - transform.position.y - (floorThickness * 0.5f), 0f);
        Gizmos.DrawCube(center, new Vector3(groundSize.x, floorThickness, groundSize.y));
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(center, new Vector3(groundSize.x, floorThickness, groundSize.y));
    }
#endif
}
