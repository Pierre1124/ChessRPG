/// <summary>獨立管理場地合成與欄位規則，供遊戲、製作工具與測試共用。</summary>
public static class FieldCardRules
{
    /// <summary>依兩張卡的無序配對查詢合成結果。</summary>
    public static string GetFusionId(string first, string second)
    {
        if (IsFieldPair(first, second, "F01", "F01")) return "F02";
        if (IsFieldPair(first, second, "F03", "F03")) return "F04";
        if (IsFieldPair(first, second, "F05", "F05")) return "F06";
        if (IsFieldPair(first, second, "F07", "F07")) return "F08";
        if (IsFieldPair(first, second, "F09", "F09")) return "F10";
        if (IsFieldPair(first, second, "F01", "F03")) return "F11";
        if (IsFieldPair(first, second, "F05", "F07")) return "F12";
        return null;
    }

    /// <summary>
    /// 判斷兩張場地卡是否符合指定配對，不限制排列順序。
    /// </summary>
    private static bool IsFieldPair(
        string first,
        string second,
        string requiredA,
        string requiredB
    )
    {
        return first == requiredA && second == requiredB ||
            first == requiredB && second == requiredA;
    }

    /// <summary>判斷場地是否佔用兩個欄位。</summary>
    public static bool IsAdvancedField(string fieldId)
    {
        switch (fieldId)
        {
            case "F02":
            case "F04":
            case "F06":
            case "F08":
            case "F10":
            case "F11":
            case "F12":
                return true;
            default:
                return false;
        }
    }

}
