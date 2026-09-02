using System.Collections;
using UnityEngine;

/// <summary>
/// IA de treino / sparring para o segundo lutador.
/// Permite testar animações de movimentação, socos e reações de dano em tempo real.
/// </summary>
[RequireComponent(typeof(FighterController))]
public class FighterSparringAI : MonoBehaviour
{
    [Header("Sparring Settings")]
    [Tooltip("Ativa o comportamento autônomo de treino para o oponente.")]
    [SerializeField] private bool autoSparring = true;

    [Tooltip("Intervalo entre decisões do oponente (segundos).")]
    [SerializeField] private float actionInterval = 2.0f;

    [Tooltip("Probabilidade de desferir um soco (0 = só anda, 1 = só soca).")]
    [SerializeField, Range(0f, 1f)] private float attackChance = 0.5f;

    private FighterController controller;
    private FighterMovement movement;
    private float timer;

    public bool AutoSparring
    {
        get => autoSparring;
        set => autoSparring = value;
    }

    private void Awake()
    {
        controller = GetComponent<FighterController>();
        movement = GetComponent<FighterMovement>();
    }

    private void Update()
    {
        if (!autoSparring || controller == null || movement == null) return;

        // Só toma decisões quando estiver no estado Neutro
        if (controller.CurrentState is not NeutralState) return;

        timer += Time.deltaTime;
        if (timer >= actionInterval)
        {
            timer = 0f;
            PerformDecision();
        }
    }

    private void PerformDecision()
    {
        float roll = Random.value;

        if (roll < attackChance)
        {
            // Para o movimento e disfere um soco
            movement.ExternalInput = Vector2.zero;
            controller.TriggerAttack();
        }
        else
        {
            // Escolhe uma direção: 1 (Direita), -1 (Esquerda) ou 0.5 (Aproximar)
            float randDir = Random.value;
            Vector2 chosenDir;
            if (randDir < 0.45f)
                chosenDir = new Vector2(1f, 0f);
            else if (randDir < 0.90f)
                chosenDir = new Vector2(-1f, 0f);
            else
                chosenDir = new Vector2(0f, 0.4f);

            StartCoroutine(PerformStepRoutine(chosenDir, Random.Range(0.6f, 1.2f)));
        }
    }

    private IEnumerator PerformStepRoutine(Vector2 dir, float duration)
    {
        movement.ExternalInput = dir;
        yield return new WaitForSeconds(duration);
        movement.ExternalInput = Vector2.zero;
    }
}
