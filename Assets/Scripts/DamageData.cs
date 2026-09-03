using System;
using UnityEngine;

/// <summary>
/// Estrutura de dados imutável com os parâmetros físicos e temporais de um impacto.
/// Passada da Hitbox para a Hurtbox do oponente no instante exato da colisão.
/// </summary>
[Serializable]
public struct DamageData
{
    [Tooltip("Quantidade de dano numérico a ser subtraído da saúde.")]
    public float damage;

    [Tooltip("Duração em segundos da janela de atordoamento (HitStun).")]
    public float hitStunDuration;

    [Tooltip("Duração em segundos do congelamento de frames de impacto (Hitstop).")]
    public float hitstopDuration;

    [Tooltip("Vetor de direção e intensidade de empurrão (Knockback).")]
    public Vector3 knockback;

    [Tooltip("Referência ao FighterController que desferiu o golpe.")]
    public FighterController attacker;

    public DamageData(float damage, float hitStunDuration, Vector3 knockback, FighterController attacker = null, float hitstopDuration = 0.08f)
    {
        this.damage = damage;
        this.hitStunDuration = hitStunDuration;
        this.hitstopDuration = hitstopDuration;
        this.knockback = knockback;
        this.attacker = attacker;
    }
}
