using UnityEngine;

/// <summary>
/// Componente simples para membros da plateia/espectadores no ringue.
/// Desincroniza a animação de repouso e ajusta levemente a velocidade individual
/// para criar um efeito de multidão orgânica e realista.
/// </summary>
[DisallowMultipleComponent]
public class Spectator : MonoBehaviour
{
    [Tooltip("Velocidade mínima da animação.")]
    [SerializeField] private float minSpeed = 0.40f;

    [Tooltip("Velocidade máxima da animação.")]
    [SerializeField] private float maxSpeed = 0.55f;

    private void Start()
    {
        var anim = GetComponent<Animator>();
        if (anim != null)
        {
            // Randomiza o ponto de início (fase) para não parecerem clones sincronizados
            anim.Play(0, 0, Random.value);
            anim.speed = Random.Range(minSpeed, maxSpeed);
        }
    }
}
