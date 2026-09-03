using UnityEngine;

/// <summary>
/// Componente para membros da plateia/espectadores no ringue.
/// Desincroniza a animação de dança (Silly Dancing) e ajusta levemente o tempo
/// para criar um efeito de multidão animada, orgânica e divertida.
/// </summary>
[DisallowMultipleComponent]
public class Spectator : MonoBehaviour
{
    [Tooltip("Velocidade mínima da dança.")]
    [SerializeField] private float minSpeed = 0.90f;

    [Tooltip("Velocidade máxima da dança.")]
    [SerializeField] private float maxSpeed = 1.10f;

    private void Start()
    {
        var anim = GetComponent<Animator>();
        if (anim != null)
        {
            // Desloca o início da dança em um ponto aleatório do clipe
            anim.Play(0, 0, Random.value);
            anim.speed = Random.Range(minSpeed, maxSpeed);
        }
    }
}
