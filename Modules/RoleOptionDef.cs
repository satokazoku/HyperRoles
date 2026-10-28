using System;

namespace HyperRoles.Roles;

/// <summary>
/// 役職ごとの設定項目の定義。RoleBase.Options の値として使う。
/// </summary>
/// <param name="Name">設定の名前(設定画面に表示される)</param>
/// <param name="Default">既定値</param>
/// <param name="Min">最小値</param>
/// <param name="Max">最大値</param>
/// <param name="Step">+/− を1回押したときに増減する間隔。省略(0)すると、範囲に合わせて自動で決める</param>
public readonly record struct RoleOptionDef(Func<string> name, float Default, float Min, float Max, float Step = 0f)
{
    /// <summary>
    /// 実際に使う間隔
    /// </summary>
    public float ResolvedStep
    {
        get
        {
            if (Step > 0f) return Step;
            return 1f;
        }
    }
}