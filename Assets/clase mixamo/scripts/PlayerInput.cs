using UnityEngine;

public class PlayerInput : ICharacterInput
{
    public float GetSpeedInput()
    {
        return new Vector2(
             Input.GetAxis("Horizontal"),
             Input.GetAxis("Vertical")
             ).magnitude;
    }
    
    /*float ICharacterInput.GetSpeedInput()
    {
        // Simple AI logic for enemy speed input
        return Random.Range(0f, 1f); // Random speed between 0 and 1
    }*/
}
