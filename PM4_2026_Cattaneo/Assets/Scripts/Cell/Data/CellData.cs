// ============================================
// Cell Data
// ============================================
// PURPOSE: Defines a cell species — identity, movement, and lifecycle.
// USAGE: Create via Assets > Create > Data > CellData.
//        Assign to cell prefab components (CellMotor, CellLifecycle, etc.).
//        One asset per species — same code, different data.
// ============================================

using UnityEngine;

[CreateAssetMenu(fileName = "NewCellData", menuName = "Data/CellData")]
public class CellData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string typeName;
    [SerializeField] private Color typeColor = Color.white;

    [Header("Movement")]
    [SerializeField] private float speed = 3f;

    [Header("Lifecycle")]
    [SerializeField] private float startingEnergy = 100f;
    [SerializeField] private float energyDecayRate = 5f;

    public string TypeName => typeName;
    public Color TypeColor => typeColor;
    public float Speed => speed;
    public float StartingEnergy => startingEnergy;
    public float EnergyDecayRate => energyDecayRate;
}

// ============================================
// SETUP
// ============================================
// 1. Create asset: Right-click > Create > Data > CellData
// 2. Set typeName and typeColor for species identity
// 3. Set speed for movement (used by CellMotor)
// 4. Set startingEnergy and energyDecayRate (used by CellLifecycle)
// 5. Assign to all cell components that reference CellData
// ============================================
