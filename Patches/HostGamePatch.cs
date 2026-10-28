using BepInEx.Configuration;

namespace HyperRoles;

/// <summary>
/// Innersloth公式の「Among Us Modded Client Identification(AU MCI)」への登録。
/// 参考: https://github.com/Innersloth-LLC/AmongUsModdingInformation
///
/// 全員がMODを入れる必要があるMOD(All-client Mod)向けの仕組みで、次の2つが、GUIDを登録するだけで、自動で働く。
///  1. ホスト:  部屋を作るとき、通常の HostGame ではなく、GUID付きの HostModdedGame として、部屋を作る
///             (バニラのマッチメイキングの対象から外れ、バニラのクライアントは、入れなくなる)
///  2. 検索:   「ゲームを探す」の検索に、同じGUIDの「mod」フィルターが、自動で付く
///             (同じMODの部屋だけが、一覧に出る)
///
/// Among Us 18.0以降には、これを行う CurrentModRegistration が、ゲームの中に組み込まれている。
/// ModRegistrationGuidString に、GUIDを入れるだけでよい(空にすると、登録なし=バニラの扱いに戻る)。
/// </summary>
public static class ModRegistration
{
    /// <summary>
    /// このMODを識別するGUID(ランダムなV4 UUID)。
    /// 自分のMODだけの値にして、一度決めたら、変えない。ホストと検索で、同じ値が使われる。
    /// 別の値にしたい場合は、https://www.uuidgenerator.net/ などで作って、置き換える。
    /// </summary>
    public const string ModGuid = "7bafeb03-571a-4c98-aa17-bee4719a8053";

    static readonly BepInEx.Logging.ManualLogSource log =
        BepInEx.Logging.Logger.CreateLogSource("ModRegistration");

    static ConfigEntry<bool> enabled;

    /// <summary>
    /// プラグインの Load() で、一度だけ呼ぶ。引数は BasePlugin の Config。
    /// 設定ファイルの「Matchmaking」に、登録のオン/オフの項目ができる(既定はオン)。
    /// </summary>
    public static void Register(ConfigFile config)
    {
        enabled = config.Bind(
            "Matchmaking", "Register Mod GUID", true,
            "オンにすると、このMODの部屋として登録する(同じMODの部屋だけが検索に出て、バニラのクライアントは入れない)。" +
            "バニラの部屋で試したいときだけ、オフにする。");

        Apply();
        enabled.SettingChanged += (_, _) => Apply(); // ゲームを起動したまま、設定を変えたときにも反映する
    }

    /// <summary>登録を、いまの設定に合わせる。部屋を作る前、または検索する前に、反映されていればよい。</summary>
    static void Apply()
    {
        CurrentModRegistration.ModRegistrationGuidString = enabled.Value ? ModGuid : "";
        LogStatus();
    }

    /// <summary>
    /// 登録の状態をログに出す。
    /// TryGetModRegistrationGuid は、ゲームが、GUIDの有無と形式の確認に使っているのと同じ関数。
    /// </summary>
    public static void LogStatus()
    {
        if (CurrentModRegistration.TryGetModRegistrationGuid(out var guid))
            log.LogInfo($"[ModRegistration] registered. guid={guid}");
        else
            log.LogInfo("[ModRegistration] not registered (normal matchmaking)");
    }
}