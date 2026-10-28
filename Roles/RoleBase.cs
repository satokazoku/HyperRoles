using System.Collections.Generic;
using HyperRoles.Modules;
using UnityEngine;
using UnityEngine.ProBuilder;
using static HyperRoles.CustomRoleManager;

namespace HyperRoles.Roles;

public abstract class RoleBase
{
    /// <summary>
    /// 役職名
    /// </summary>
    public abstract string RoleName { get; }
    /// <summary>
    /// イントロで表示される文(「タスクを行う」など)
    /// </summary>
    public abstract string Intro { get; }
    /// <summary>
    /// 役職の色
    /// </summary>
    public abstract Color RoleColor { get; }
    /// <summary>
    /// 陣営
    /// </summary>
    public abstract Team Team { get; }
    /// <summary>
    /// カウント
    /// </summary>
    public virtual Count CountType => Count.Crewmate;
    /// <summary>
    /// 移植元
    /// </summary>
    public virtual From FromMod => From.None;
    /// <summary>
    /// キルボタンが使えるか
    /// </summary>
    public virtual bool CanKill => false;
    /// <summary>
    /// インポスターベントボタンが使えるか
    /// </summary>
    public virtual bool CanVent => false;
    /// <summary>
    /// キルクールダウン(CanKillがtrueの場合のみ使う)
    /// </summary>
    public virtual float KillCoolDown => Main.NormalOptions.KillCooldown;
    /// <summary>
    /// タスクを持つか。false の役職は、全体のタスク数(タスクバーの進捗)に数えない。
    /// </summary>
    public virtual bool HasTask => true;

    // ------------------------------------------------------------
    // 役職ごとの設定(/set 役職名 オプションID 数値 の、オプションID 2以降)
    // ------------------------------------------------------------

    static readonly Dictionary<int, RoleOptionDef> EmptyOptions = new();

    /// <summary>
    /// この役職の設定の定義。キーが、/set で指定するオプションID(2以降)。
    /// 設定を持つ役職だけが override する。毎フレーム読まれることがあるので、
    /// override 側は、static readonly の辞書を返す形にする(new() を返すと、毎回作り直される)。
    /// </summary>
    public virtual Dictionary<int, RoleOptionDef> Options => EmptyOptions;

    /// <summary>
    /// 設定値を読む。/set で変えられていればその値、なければ Options に書いた既定値。
    /// </summary>
    protected float GetOption(int optionId)
    {
        var defaultValue = Options.TryGetValue(optionId, out var def) ? def.Default : 0f;
        return RoleSettings.GetOption(CustomRoleManager.GetId(this), optionId, defaultValue);
    }

    public static string GetString(string str, Dictionary<string, string> replacementDic = null) => Translator.GetString(str, replacementDic);
    static readonly BepInEx.Logging.ManualLogSource log =
        BepInEx.Logging.Logger.CreateLogSource("RoleBase");

    /// <summary>
    /// 設定の値を取得する
    /// </summary>
    /// <param name="roleid">取得する役職</param>
    /// <param name="optionid">設定のID</param>
    /// <param name="d">オプション自体が取得できない時に返す値</param>
    /// <returns>設定の値</returns>
    public static float GetOptionValue(RoleId roleid, int optionid, float d = 1f)
    {
        var setting = RoleSettings.Get(roleid);
        var template = CreateRole(roleid);

        if (setting == null)
        {
            log.LogWarning($"[RoleBase] RoleSettings.Get({nameof(roleid)}) is null!");
            return d;
        }

        log.LogInfo($"[RoleBase] Setting Values Count: {setting.Values.Count}");
        foreach (var kvp in setting.Values)
        {
            log.LogInfo($"[RoleBase] Stored Option ID {kvp.Key} = {kvp.Value}");
        }

        if (setting.Values.TryGetValue(optionid, out var val))
        {
            log.LogInfo($"[RoleBase] Found custom cooldown: {val}");
            return val;
        }

        if (template != null && template.Options.TryGetValue(optionid, out var def))
        {
            log.LogInfo($"[RoleBase] Using default cooldown: {def.Default}");
            return def.Default;
        }

        return d;
    }

    /// <summary>
    /// 役職が、プレイヤーに割り当てられたときに呼ばれる(CustomRoleManager.SetRole から)。
    /// </summary>
    public virtual void Add()
    { }
    /// <summary>
    /// 役職が、プレイヤーから外れるとき(別の役職に変わるときなど)に呼ばれる。
    /// </summary>
    public virtual void OnDestroy()
    { }
    /// <summary>
    /// 毎フレーム呼ばれる関数
    /// 本人確認不要
    /// </summary>
    /// <param name="player"></param>
    public virtual void OnFixedUpdate(PlayerControl player)
    { }
    /// <summary>
    /// 役職名の横に表示される文字(シェリフ(Progress)のように表示)
    /// </summary>
    /// <param name="player"></param>
    /// <returns></returns>
    public virtual string ProgressText(PlayerControl player) => "";
    /// <summary>
    /// MurderPlayer処理
    /// 本人確認必須
    /// </summary>
    /// <param name="killer">キラー</param>
    /// <param name="target">ターゲット</param>
    /// <param name="player">処理が実行されるプレイヤー</param>
    public virtual void OnMurderPlayer(PlayerControl killer, PlayerControl target, PlayerControl player)
    { }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="killer"></param>
    /// <param name="target"></param>
    /// <returns>falseを返すとキルが無効化される</returns>
    public virtual bool OnCheckMurder(PlayerControl killer, PlayerControl target) => true;
}

public enum Team
{
    none = 0,
    Impostor,
    Crewmate,
    Neutral,
}
public enum Count
{
    none = 0,
    Impostor,
    Crewmate,
    Jackal
}
public enum From
{
    None,
    TownOfHost_Pko,
    SheriffMod,
    TheOtherRoles
}