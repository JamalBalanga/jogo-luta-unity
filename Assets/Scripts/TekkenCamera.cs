using UnityEngine;

/// <summary>
/// Câmera dinâmica estilo jogos de luta 3D (Tekken / Soulcalibur).
/// Mantém os dois lutadores enquadrados, calculando o ponto médio e
/// orbitando perpendicularmente à linha de combate com zoom dinâmico.
/// </summary>
public class TekkenCamera : MonoBehaviour
{
    [Header("Targets")]
    [Tooltip("Lutador 1 (Jogador)")]
    [SerializeField] private Transform fighter1;

    [Tooltip("Lutador 2 (Oponente)")]
    [SerializeField] private Transform fighter2;

    [Header("Framing Settings")]
    [Tooltip("Distância base da câmera em relação ao ponto médio.")]
    [SerializeField] private float baseDistance = 5.0f;

    [Tooltip("Altura da câmera em relação ao chão.")]
    [SerializeField] private float height = 1.6f;

    [Tooltip("Altura do ponto de foco (look-at) acima do chão.")]
    [SerializeField] private float lookAtHeight = 1.1f;

    [Tooltip("Fator de recuo de zoom conforme os lutadores se distanciam.")]
    [SerializeField] private float zoomFactor = 0.6f;

    [Header("Damping")]
    [Tooltip("Suavização do movimento da câmera (valores menores = mais suave).")]
    [SerializeField] private float followDamping = 8.0f;

    [Tooltip("Suavização da rotação da câmera.")]
    [SerializeField] private float rotationDamping = 10.0f;

    public Transform Fighter1 { get => fighter1; set => fighter1 = value; }
    public Transform Fighter2 { get => fighter2; set => fighter2 = value; }

    private void LateUpdate()
    {
        if (fighter1 == null || fighter2 == null) return;

        // 1. Calcula o ponto médio entre os lutadores
        Vector3 p1 = fighter1.position;
        Vector3 p2 = fighter2.position;
        Vector3 midpoint = (p1 + p2) * 0.5f;

        // 2. Calcula a linha de combate no plano horizontal (XZ)
        Vector3 combatLine = p2 - p1;
        combatLine.y = 0f;
        float fighterDistance = combatLine.magnitude;

        if (fighterDistance < 0.001f)
        {
            combatLine = Vector3.right;
            fighterDistance = 1f;
        }

        // Vetor perpendicular à linha de combate (normal horizontal que define a visão lateral)
        Vector3 normal = Vector3.Cross(Vector3.up, combatLine.normalized);

        // 3. Calcula a distância e a posição alvo da câmera
        float currentDistance = baseDistance + (fighterDistance * zoomFactor);
        Vector3 targetPosition = midpoint + (normal * currentDistance);
        targetPosition.y = midpoint.y + height;

        // Interpolação suave de posição
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followDamping);

        // 4. Orientação da câmera olhando para o ponto médio dos lutadores
        Vector3 lookTarget = midpoint + Vector3.up * lookAtHeight;
        Vector3 directionToTarget = lookTarget - transform.position;

        if (directionToTarget.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationDamping);
        }
    }
}
