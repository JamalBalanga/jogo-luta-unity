using System.Collections.Generic;
using UnityEngine;

public enum HitboxLimb
{
    RightHand,
    LeftHand,
    RightFoot,
    LeftFoot
}

/// <summary>
/// Emissor de dano (Hitbox). Anexado aos membros de ataque (mãos e pés).
/// Controla frames ativos, previne múltiplos hits no mesmo oponente por golpe
/// e utiliza triggers com amostragem contínua para zero tunnel-clipping.
/// </summary>
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class Hitbox : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Membro de ataque correspondente a esta hitbox.")]
    [SerializeField] private HitboxLimb limbType = HitboxLimb.RightHand;

    [Tooltip("Lutador dono desta hitbox.")]
    [SerializeField] private FighterController owner;

    private Collider col;
    private SphereCollider sphereCol;
    private BoxCollider boxCol;
    private DamageData currentDamageData;
    private bool isActive;

    // Buffer pré-alocado para detecção de triggers sem alocação de GC
    private static readonly Collider[] overlapResults = new Collider[16];

    // Conjunto de lutadores já atingidos nesta janela ativa (Zero GC por frame)
    private readonly HashSet<FighterController> hitFighters = new HashSet<FighterController>();

    public HitboxLimb LimbType => limbType;
    public FighterController Owner { get => owner; set => owner = value; }
    public bool IsActive => isActive;

    private void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;
        col.enabled = false;

        sphereCol = col as SphereCollider;
        boxCol = col as BoxCollider;

        if (owner == null)
        {
            owner = GetComponentInParent<FighterController>();
        }
    }

    /// <summary>
    /// Ativa a hitbox com os parâmetros de impacto específicos do golpe.
    /// </summary>
    public void Activate(DamageData data)
    {
        currentDamageData = data;
        if (currentDamageData.attacker == null)
        {
            currentDamageData.attacker = owner;
        }

        hitFighters.Clear();
        isActive = true;
        col.enabled = true;

        // Amostragem imediata no primeiro frame ativo
        CheckOverlaps();
    }

    /// <summary>
    /// Desativa a hitbox ao encerrar a janela ativa do golpe.
    /// </summary>
    public void Deactivate()
    {
        isActive = false;
        col.enabled = false;
        hitFighters.Clear();
    }

    private void Update()
    {
        if (isActive)
        {
            CheckOverlaps();
        }
    }

    /// <summary>
    /// Detecta sobreposição com Hurtboxes (suporta tanto triggers quanto non-triggers com QueryTriggerInteraction).
    /// </summary>
    private void CheckOverlaps()
    {
        if (!isActive) return;

        int count = 0;

        if (sphereCol != null)
        {
            Vector3 worldCenter = transform.TransformPoint(sphereCol.center);
            float radius = sphereCol.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
            count = Physics.OverlapSphereNonAlloc(worldCenter, radius, overlapResults, ~0, QueryTriggerInteraction.Collide);
        }
        else if (boxCol != null)
        {
            Vector3 worldCenter = transform.TransformPoint(boxCol.center);
            Vector3 halfExtents = Vector3.Scale(boxCol.size * 0.5f, transform.lossyScale);
            count = Physics.OverlapBoxNonAlloc(worldCenter, halfExtents, overlapResults, transform.rotation, ~0, QueryTriggerInteraction.Collide);
        }

        for (int i = 0; i < count; i++)
        {
            ProcessHit(overlapResults[i]);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isActive)
        {
            ProcessHit(other);
        }
    }

    private void ProcessHit(Collider other)
    {
        if (!isActive || other == null) return;

        // Procura Hurtbox no objeto atingido
        var hurtbox = other.GetComponent<Hurtbox>();
        if (hurtbox == null || hurtbox.Owner == null) return;

        // Ignora colisão com o próprio lutador
        if (hurtbox.Owner == owner) return;

        // Previne múltiplos hits no mesmo oponente na mesma janela ativa
        if (hitFighters.Contains(hurtbox.Owner)) return;

        hitFighters.Add(hurtbox.Owner);

        // Repassa os dados de dano à Hurtbox
        hurtbox.ReceiveHit(currentDamageData, this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!isActive) return;
        if (col == null) col = GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.55f);
        Gizmos.matrix = transform.localToWorldMatrix;

        if (col is SphereCollider sphere)
        {
            Gizmos.DrawSphere(sphere.center, sphere.radius);
        }
        else if (col is BoxCollider box)
        {
            Gizmos.DrawCube(box.center, box.size);
        }
    }
#endif
}
