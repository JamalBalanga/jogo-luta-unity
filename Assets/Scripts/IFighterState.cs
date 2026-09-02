/// <summary>
/// Interface contratual para estados da Máquina de Estados Finitos (FSM) do lutador.
/// Garante desacoplamento total, recebendo a referência do FighterController em todas as operações.
/// </summary>
public interface IFighterState
{
    /// <summary>
    /// Chamado uma vez quando o estado é ativado.
    /// Usado para inicializar flags, travar/liberar componentes e disparar animações.
    /// </summary>
    void Enter(FighterController fighter);

    /// <summary>
    /// Chamado a cada frame durante o ciclo Update() do FighterController.
    /// Usado para verificar transições de estado, processar inputs e temporizadores.
    /// </summary>
    void Update(FighterController fighter);

    /// <summary>
    /// Chamado uma vez imediatamente antes de transicionar para um novo estado.
    /// Usado para limpar flags temporárias e restaurar configurações.
    /// </summary>
    void Exit(FighterController fighter);
}
