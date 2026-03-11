using UnityEngine;

public class EnemyInput : ICharacterInput
{
    float ICharacterInput.GetSpeedInput()
    {
        // Simple AI logic for enemy speed input
        return Random.Range(0f, 1f); // Random speed between 0 and 1
    }
}
