using HarmonyLib;

namespace HyperRoles;

// Town Of Hostより
// 2026/10/20に追加

public static class GameStates
{
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoStartGame))]
    class ChangeRoleSettings
    {
        public static void Postfix(AmongUsClient __instance)
        {
            AlreadyDied = false;
            task = true;
            InGame = true;
            AfterIntro = false;
            CalledMeeting = false;
            ExiledAnimate = false;
            canmusic = false;
            AnyShapeShifter = false;
            turntimer = 0;
        }
    }
    public static bool InGame = false;
    public static bool IsOutro = false;
    public static bool AlreadyDied = false;
    public static bool IsLobby => AmongUsClient.Instance.GameState == AmongUsClient.GameStates.Joined;
    public static bool IsEnded => AmongUsClient.Instance.GameState == AmongUsClient.GameStates.Ended;
    public static bool IsNotJoined => AmongUsClient.Instance.GameState == AmongUsClient.GameStates.NotJoined;
    public static bool IsOnlineGame => AmongUsClient.Instance.NetworkMode == NetworkModes.OnlineGame;
    public static bool IsLocalGame => AmongUsClient.Instance.NetworkMode == NetworkModes.LocalGame;
    public static bool IsFreePlay => AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay;
    public static bool IsInTask => task;
    public static bool CalledMeeting;
    public static bool ExiledAnimate;
    public static bool IsMeeting => InGame && MeetingHud.Instance;
    public static bool IsCountDown => GameStartManager.InstanceExists && GameStartManager.Instance.startState == GameStartManager.StartingStates.Countdown;
    public static bool AfterIntro;
    public static bool task;
    public static bool canmusic;
    public static float turntimer;
    public static bool AnyShapeShifter;
}
