namespace Joy.Core;

/// <summary>Public contract for the unique, finite-use M2 equipment prototype.</summary>
public static class WardClipCatalog
{
    public const string Id = "ward-clip";
    public const string Kind = "WardClip";
    public const string Name = "誊写护符夹";
    public const int MaximumCharges = 2;
    public const string Description = "唯一装备，占 1 格，整局共 2 次且不能补充。已持有时，成功正常搜查前若护符为 0、墨水至少 1，则在规则结算前自动消耗 1 墨水和 1 装备次数，护符 +1；没有反噬也照扣。有护符不消耗、不叠加。徒手搜查、搜查牌与有限档案柜共用此效果；模仿、敲门、拾取不触发，取得本次搜查不会触发装备。可拒收或放回房间保留次数；不能出售、研究或安装。";
}
