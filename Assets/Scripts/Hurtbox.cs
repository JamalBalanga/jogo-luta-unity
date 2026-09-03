using UnityEngine;

/// <summary>
/// Receptor de dano (Hurtbox). Anexado aos ossos do esqueleto do lutador.
/// Possui um Collider Trigger e encaminha os impactos recebidos ao FighterController dono
/// acionando o método TakeDamage.
/// </summary>
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class Hurtbox : MonoBehaviour
{
    [Header("Owner")]
    [Tooltip("Referência ao FighterController dono deste osso/hurtbox.")]
    [SerializeField] private FighterController owner;

    private Collider col;

    public FighterController Owner
    {
        get => owner;
        set => owner = value;
    }

    private void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;

        if (owner == null)
        {
            owner = GetComponentInParent<FighterController>();
        }
    }

    /// <summary>
    /// Chamado pela Hitbox atacante ao colidir com esta Hurtbox.
    /// Aciona o TakeDamage no FighterController dono com os dados do impacto.
    /// </summary>
    public void ReceiveHit(DamageData data, Hitbox sourceHitbox)
    {
        if (owner == null) return;

        // Previne dano acidental contra si mesmo
        if (data.attacker == owner) return;

        owner.TakeDamage(data, sourceHitbox);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (col == null) col = GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = new Color(0f, 1f, 0.3f, 0.3f);
        Gizmos.matrix = transform.localToWorldMatrix;

        if (col is BoxCollider box)
        {
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }
        else if (col is CapsuleCollider capsule)
        {
            Gizmos.DrawWireSphere(capsule.center, capsule.radius);
        }
    }
#endif
}
