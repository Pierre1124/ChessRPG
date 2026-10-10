using UnityEngine;

/// <summary>棋子主動技能的資格檢查與電網開關；對局狀態不寫入卡牌資產。</summary>
public static class PieceActiveSkill
{
    /// <summary>辨認目前裝備的電網技能。</summary>
    public static bool HasSkill(Piece piece) => piece != null && piece.CardRuntime != null &&
        piece.cardDefinition != null && piece.cardDefinition.id == "J04";

    /// <summary>共用於按鈕與主機的資格檢查，拒絕過期棋子及非己方回合。</summary>
    public static bool CanUse(LogicManager logic, Piece piece, bool playerWhite, out string reason)
    {
        reason = "";
        if (logic == null || logic.IsClassicChess) reason = "目前模式無法使用技能";
        else if (!HasSkill(piece)) reason = "此棋子沒有主動技能";
        else if (Time.timeScale == 0 || logic.IsOperationLocked || logic.isPromotionActive || logic.IsFieldFusionPlaying)
            reason = "請等待目前操作完成";
        else if (piece.IsWhite != playerWhite || logic.isWhiteTurn != playerWhite) reason = "只能在自己的回合使用";
        else if (!BoardCoordinate.FromVector2(piece.GetCoordinates()).IsValid ||
            logic.boardMap[(int)piece.GetCoordinates().x, (int)piece.GetCoordinates().y] != piece) reason = "棋子已不在棋盤上";
        else if (piece.CardRuntime.activeSkillDisabled) reason = "電網已關閉；下次我方回合重新判定";
        else
        {
            foreach (var zone in logic.GetActiveBoardFieldZones())
                if (zone.type == BoardFieldEffectType.ElectricNet && (zone.firstSource == piece || zone.secondSource == piece)) return true;
            reason = "沒有相連的電網";
        }
        return false;
    }

    /// <summary>通過權限驗證後關閉電網；不消耗移動，方便本回合移動城堡。</summary>
    public static bool TryUse(LogicManager logic, Piece piece, bool playerWhite)
    {
        if (!CanUse(logic, piece, playerWhite, out _)) return false;
        ApplyDisabled(logic, piece);
        return true;
    }

    /// <summary>主機與可信結果接收端共同套用關閉狀態，使相連城堡都可移動。</summary>
    public static void ApplyDisabled(LogicManager logic, Piece piece)
    {
        if (logic == null || logic.IsClassicChess || !HasSkill(piece)) return;
        foreach (var zone in logic.GetActiveBoardFieldZones())
        {
            if (zone.type != BoardFieldEffectType.ElectricNet || (zone.firstSource != piece && zone.secondSource != piece)) continue;
            if (zone.firstSource.CardRuntime != null) zone.firstSource.CardRuntime.activeSkillDisabled = true;
            if (zone.secondSource.CardRuntime != null) zone.secondSource.CardRuntime.activeSkillDisabled = true;
        }
        piece.CardRuntime.activeSkillDisabled = true;
        logic.RefreshBoardFieldEffects();
        logic.UpdateCheckMap();
    }

    /// <summary>己方回合開始時解除手動關閉標記，只有仍平行的城堡會重建電網。</summary>
    public static void BeginTurn(LogicManager logic, bool white)
    {
        if (logic == null || logic.IsClassicChess) return;
        foreach (Piece piece in logic.boardMap)
            if (HasSkill(piece) && piece.IsWhite == white) piece.CardRuntime.activeSkillDisabled = false;
        logic.RefreshBoardFieldEffects();
    }
}
