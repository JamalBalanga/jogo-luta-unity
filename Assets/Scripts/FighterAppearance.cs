using UnityEngine;

/// <summary>Reaplica o material autoral caso o Model Prefab perca a referência no import.</summary>
[DisallowMultipleComponent]
public sealed class FighterAppearance : MonoBehaviour
{
    [SerializeField] private Material characterMaterial;

    private void Awake()
    {
        if (characterMaterial == null) return;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0) materials = new[] { characterMaterial };
            else for (int i = 0; i < materials.Length; i++) materials[i] = characterMaterial;
            renderer.sharedMaterials = materials;
            renderer.enabled = true;
        }
    }
}
