using UnityEngine;

/// <summary>
/// Estado de Ataque: Trava o deslocamento, dispara a animação de golpe no Animator,
/// ativa a Hitbox do membro correspondente e aguarda a janela de recovery para retornar ao Neutro.
/// </summary>
public class AttackState : IFighterState
{
    private float elapsedTime;

    public void Enter(FighterController fighter)
    {
        // Trava a movimentação para comprometer o lutador no golpe
        if (fighter.Movement != null)
        {
            fighter.Movement.CanMove = false;
        }

        elapsedTime = 0f;

        // Dispara a animação de ataque
        fighter.CrossFadeAnimation(fighter.AttackAnimHash, 0.05f);

        // Ativa as Hitboxes das mãos para cobrir a sequência do soco
        fighter.EnableHitbox(HitboxLimb.RightHand);
        fighter.EnableHitbox(HitboxLimb.LeftHand);
    }

    public void Update(FighterController fighter)
    {
        elapsedTime += Time.deltaTime;

        // Desativa as hitboxes após a janela ativa do golpe (aos 0.50s)
        if (elapsedTime >= 0.50f)
        {
            fighter.DisableAllHitboxes();
        }

        // Retorna ao Neutro após o tempo total do ataque (ativação + recovery)
        if (elapsedTime >= fighter.AttackDuration)
        {
            fighter.ChangeState(fighter.NeutralState);
        }
    }

    public void Exit(FighterController fighter)
    {
        elapsedTime = 0f;
        // Garante que nenhuma hitbox permaneça ativa após sair do ataque
        fighter.DisableAllHitboxes();
    }
}
