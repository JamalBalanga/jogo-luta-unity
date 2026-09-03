using UnityEngine;

/// <summary>
/// Controlador de câmera profissional para jogos de luta 3D estilo Tekken.
/// Posiciona-se perpendicular à linha de combate entre os dois lutadores,
/// calcula o ponto médio com zoom dinâmico proporcional à distância dos oponentes,
/// e aplica interpolação suave com Vector3.SmoothDamp e Quaternion.Slerp.
/// </summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class FightCameraController : MonoBehaviour
{
    [Header("Targets")]
    [Tooltip("Transform do primeiro lutador (Player 1).")]
    [SerializeField] private Transform fighter1;

    [Tooltip("Transform do segundo lutador (Player 2).")]
    [SerializeField] private Transform fighter2;

    [Header("Distance & Zoom Settings")]
    [Tooltip("Distância base da câmera quando os lutadores estão na proximidade padrão.")]
    [SerializeField, Min(1f)] private float baseDistance = 5.2f;

    [Tooltip("Distância mínima permitida da câmera (limite do zoom in).")]
    [SerializeField, Min(1f)] private float minDistance = 3.8f;

    [Tooltip("Distância máxima permitida da câmera (limite do zoom out).")]
    [SerializeField, Min(1f)] private float maxDistance = 9.0f;

    [Tooltip("Fator de afastamento proporcional à separação entre os lutadores.")]
    [SerializeField, Range(0.1f, 2f)] private float zoomFactor = 0.65f;

    [Header("Height & Offsets")]
    [Tooltip("Altura da câmera em relação ao ponto médio dos lutadores.")]
    [SerializeField] private float height = 1.60f;

    [Tooltip("Offset vertical do ponto de mira (foco) em relação ao ponto médio (altura do tronco).")]
    [SerializeField] private float lookAtHeightOffset = 1.15f;

    [Tooltip("Inverter lado da câmera em 180 graus caso necessário.")]
    [SerializeField] private bool invertSide = false;

    [Header("Damping & Smoothing")]
    [Tooltip("Tempo de amortecimento da translação via Vector3.SmoothDamp (em segundos).")]
    [SerializeField, Range(0.01f, 0.5f)] private float positionSmoothTime = 0.12f;

    [Tooltip("Velocidade angular de interpolação da rotação (Quaternion.Slerp).")]
    [SerializeField, Min(1f)] private float rotationSpeed = 9.0f;

    private Vector3 currentVelocity;

    public Transform Fighter1
    {
        get => fighter1;
        set => fighter1 = value;
    }

    public Transform Fighter2
    {
        get => fighter2;
        set => fighter2 = value;
    }

    private void LateUpdate()
    {
        if (fighter1 == null || fighter2 == null) return;

        UpdateCameraTransform();
    }

    /// <summary>
    /// Calcula a posição perpendicular ao vetor de combate e rotaciona suavemente em direção ao ponto médio.
    /// </summary>
    private void UpdateCameraTransform()
    {
        Vector3 pos1 = fighter1.position;
        Vector3 pos2 = fighter2.position;

        // 1. Calcula o ponto médio (midpoint) entre os lutadores
        Vector3 midpoint = (pos1 + pos2) * 0.5f;

        // 2. Calcula a linha de combate no plano horizontal XZ
        Vector3 combatLine = pos2 - pos1;
        combatLine.y = 0f;
        float fighterDistance = combatLine.magnitude;

        if (fighterDistance < 0.001f)
        {
            combatLine = Vector3.right;
            fighterDistance = 1f;
        }

        Vector3 combatDirection = combatLine.normalized;

        // 3. Vetor normal lateral perpendicular à linha de combate (visão lateral clássica de jogo de luta)
        Vector3 sideNormal = Vector3.Cross(Vector3.up, combatDirection);
        if (invertSide)
        {
            sideNormal = -sideNormal;
        }

        // 4. Cálculo do zoom dinâmico baseado na distância entre os lutadores com clampeamento
        float desiredDistance = baseDistance + (fighterDistance * zoomFactor);
        float clampedDistance = Mathf.Clamp(desiredDistance, minDistance, maxDistance);

        // 5. Posição alvo da câmera
        Vector3 targetPosition = midpoint + (sideNormal * clampedDistance);
        targetPosition.y = midpoint.y + height;

        // 6. Ponto de mira (foco) posicionado no ponto médio com elevação para a altura do peito
        Vector3 focusPoint = midpoint;
        focusPoint.y += lookAtHeightOffset;

        // 7. Rotação alvo olhando diretamente para o foco dos lutadores
        Vector3 lookDirection = focusPoint - targetPosition;
        Quaternion targetRotation = Quaternion.LookRotation(lookDirection);

        // 8. Interpolação suave: Vector3.SmoothDamp para posição e Quaternion.Slerp para rotação
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, positionSmoothTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (fighter1 == null || fighter2 == null) return;

        Vector3 p1 = fighter1.position;
        Vector3 p2 = fighter2.position;
        Vector3 mid = (p1 + p2) * 0.5f;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawWireSphere(mid, 0.2f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, mid + Vector3.up * lookAtHeightOffset);
    }
#endif
}
