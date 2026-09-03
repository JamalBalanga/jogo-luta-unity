using UnityEngine;

/// <summary>
/// Estado de Ataque: Trava o deslocamento, dispara a animação de golpe no Animator,
/// ativa a Hitbox do membro correspondente e aguarda a janela de recovery para retornar ao Neutro.
/// </summary>
public class AttackState : IFighterState
{
    private float elapsedTime;
    private bool hitboxesEnabled;
    private bool windowOpened;

    public void Enter(FighterController fighter)
    {
        // Trava a movimentação para comprometer o lutador no golpe
        if (fighter.Movement != null)
        {
            fighter.Movement.CanMove = false;
        }
        fighter.SetAttackRootMotion(true);

        elapsedTime = 0f;
        hitboxesEnabled = false;
        windowOpened = false;

        // Dispara a animação de ataque
        fighter.CrossFadeAnimation(fighter.CurrentAttackAnimHash, 0.05f);
        fighter.SetAnimatorSpeed(fighter.CurrentAttackTiming.playbackSpeed);

        // Ativa as Hitboxes das mãos para cobrir a sequência do soco
    }

    public void Update(FighterController fighter)
    {
        elapsedTime += Time.deltaTime;
        FighterAttackTiming timing = fighter.CurrentAttackTiming;
        bool hasAnimationProgress = fighter.TryGetCurrentAttackProgress(out float progress);
        float attackProgress = hasAnimationProgress
            ? progress
            : elapsedTime / Mathf.Max(0.05f, fighter.CurrentAttackDuration);

        if (!windowOpened && attackProgress >= timing.activeStartNormalized)
        {
            fighter.EnableCurrentAttackHitboxes();
            hitboxesEnabled = true;
            windowOpened = true;
        }

        // Desativa as hitboxes após a janela ativa do golpe (aos 0.50s)
        if (hitboxesEnabled && attackProgress >= timing.activeEndNormalized)
        {
            fighter.DisableAllHitboxes();
            hitboxesEnabled = false;
        }

        // Retorna ao Neutro após o tempo total do ataque (ativação + recovery)
        if (attackProgress >= timing.recoveryEndNormalized)
        {
            fighter.ChangeState(fighter.NeutralState);
        }
    }

    public void Exit(FighterController fighter)
    {
        elapsedTime = 0f;
        hitboxesEnabled = false;
        windowOpened = false;
        // Garante que nenhuma hitbox permaneça ativa após sair do ataque
        fighter.DisableAllHitboxes();
        fighter.SetAttackRootMotion(false);
        fighter.SetAnimatorSpeed(1f);
    }
}
