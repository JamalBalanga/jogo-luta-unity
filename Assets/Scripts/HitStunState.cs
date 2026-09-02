using UnityEngine;

/// <summary>
/// Estado de Hit Stun: Bloqueia totalmente ações e locomoção do lutador por uma janela de tempo fixa,
/// disparando a animação de reação ao impacto (hurt/hit reaction).
/// </summary>
public class HitStunState : IFighterState
{
    private float elapsedTime;
    private float currentStunDuration;

    /// <summary>
    /// Define uma duração dinâmica para o stun (útil para golpes leves vs pesados).
    /// </summary>
    public void SetStunDuration(float duration)
    {
        currentStunDuration = duration;
    }

    public void Enter(FighterController fighter)
    {
        // Trava qualquer movimentação do personagem
        if (fighter.Movement != null)
        {
            fighter.Movement.CanMove = false;
        }

        elapsedTime = 0f;

        // Se nenhuma duração específica foi configurada para o golpe recebido, usa o padrão do lutador
        if (currentStunDuration <= 0f)
        {
            currentStunDuration = fighter.DefaultHitStunDuration;
        }

        // Dispara a animação de reação a dano
        fighter.CrossFadeAnimation(fighter.HitStunAnimHash, 0.05f);
    }

    public void Update(FighterController fighter)
    {
        elapsedTime += Time.deltaTime;

        // Libera o personagem de volta ao Neutro após o fim do stun
        if (elapsedTime >= currentStunDuration)
        {
            fighter.ChangeState(fighter.NeutralState);
        }
    }

    public void Exit(FighterController fighter)
    {
        elapsedTime = 0f;
        currentStunDuration = 0f;
    }
}
