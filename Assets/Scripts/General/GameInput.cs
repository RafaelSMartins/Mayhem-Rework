using UnityEngine;

// Um único InputSystem_Actions partilhado por todas as cenas,
// para que as mesmas teclas façam sempre a mesma coisa.
public static class GameInput
{
    private static InputSystem_Actions actions;

    public static InputSystem_Actions.PlayerActions Player
    {
        get
        {
            if (actions == null)
            {
                actions = new InputSystem_Actions();
                actions.Player.Enable();
                Application.quitting += Release;
            }
            return actions.Player;
        }
    }

    // Desliga e liberta as ações ao sair (evita o aviso de "leak" do Input System).
    private static void Release()
    {
        Application.quitting -= Release;
        if (actions == null) return;
        actions.Player.Disable();
        actions.Dispose();
        actions = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => Release();
}
