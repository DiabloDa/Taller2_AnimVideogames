using UnityEngine;

public static class CharacterInputFactory
{

    public static ICharacterInput CreateInput(InputType type)
    {
        switch (type)
        {
            case InputType.Player:
                return new PlayerInput();

            case InputType.Enemy:
                return new EnemyInput();

            default:
                throw new System.ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }
    public enum InputType
    {
        Player,
        Enemy
    }
}