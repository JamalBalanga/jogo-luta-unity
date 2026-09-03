using UnityEngine;

/// <summary>
/// Estado Neutro da FSM do lutador.
/// Permite livre locomoção 3D (longitudinal e órbita circular), alimenta parâmetros
/// de movimentação (Speed, ForwardInput, RightInput) no Animator e escuta comandos de ataque.
/// </summary>
public class NeutralState : IFighterState
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int ForwardInputHash = Animator.StringToHash("ForwardInput");
    private static readonly int RightInputHash = Animator.StringToHash("RightInput");

    public void Enter(FighterController fighter)
    {
        // Garante que a movimentação 3D está habilitada
        if (fighter.Movement != null)
        {
            fighter.Movement.CanMove = true;
        }

        // Toca animação neutra (Idle / Guarda de combate)
        fighter.CrossFadeAnimation(fighter.NeutralAnimHash, 0.1f);
    }

    public void Update(FighterController fighter)
    {
        // Monitora entrada de ataque para transicionar
        if (fighter.IsAttackTriggered())
        {
            fighter.ChangeState(fighter.AttackState);
            return;
        }

        // Alimenta os parâmetros de locomoção no Animator sem causar rotação de raiz
        if (fighter.Animator != null && fighter.Movement != null)
        {
            fighter.Animator.SetFloat(SpeedHash, fighter.Movement.CurrentSpeedMagnitude);
            fighter.Animator.SetFloat(ForwardInputHash, fighter.Movement.ForwardInput);
            fighter.Animator.SetFloat(RightInputHash, fighter.Movement.RightInput);
        }
    }

    public void Exit(FighterController fighter)
    {
        // Limpeza de parâmetros ao sair do estado Neutro
    }
}
