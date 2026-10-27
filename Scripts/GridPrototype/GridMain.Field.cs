using Godot;
using Joy.Core.GridPrototype;
using System;
using System.Linq;

namespace Joy.GridPrototype;

public partial class GridMain
{
    private void Field()
    {
        var game = _game!; var field = game.Field!;
        if (!field.Actors.Any(a => a.HeroId == _hero)) _hero = field.Actors[0].HeroId;
        var actor = field.Actor(_hero); var hero = game.Hero(_hero);
        var bar = H(18); _root.AddChild(bar);
        bar.AddChild(L(field.Name, 26, Ink, false, true));
        bar.AddChild(L(field.InCombat ? "● 交战中" : "◇ 探索中", 22, field.InCombat ? Red : Teal));
        bar.AddChild(L($"第 {field.Round:00}/{field.RoundLimit:00} 轮", 23, Gold));
        bar.AddChild(L($"货物 {field.Cargo.Count}/{field.Capacity} · 零钱 {field.Loot}", 21, Muted));
        var row = H(16); row.SizeFlagsVertical = SizeFlags.ExpandFill; _root.AddChild(row);

        var left = Panel(); left.CustomMinimumSize = new Vector2(295, 0); row.AddChild(left);
        var details = V(12); left.AddChild(Scroll(details));
        details.AddChild(L("外勤小队", 23, Teal));
        foreach (var member in field.Actors)
        {
            var h = game.Hero(member.HeroId); var def = Catalog.Hero(member.HeroId);
            details.AddChild(B($"{h.Name} / {h.Role}\n行动 {member.Actions} · 体力 {h.Health}/{h.MaxHealth}", () =>
            {
                _hero = member.HeroId; _card = ""; _target = member.Cell; Render();
            }, _hero == h.Id));
            details.AddChild(L($"位置 {member.Cell}  ·  防护 {member.Block}", 17, Muted));
        }
        details.AddChild(L("本次任务", 22, Gold));
        details.AddChild(L(field.ObjectiveDone ? "✓ 目标已处理\n把两名队员带回入口附近，打出撤离牌。" : "查证异常规律，处理标有「目标」的异常。可击败，也可收容。", 19, Ink, true));
        details.AddChild(L("观察与证据", 22, Teal));
        details.AddChild(L(field.EvidenceKnown ? field.Evidence : field.RuleHint, 18, Ink, true));
        if (!field.EvidenceKnown) details.AddChild(L("用「搜查」牌调查线索格。", 17, Gold, true));
        if (field.AssetId.Length > 0)
        {
            var asset = game.Assets.FirstOrDefault(a => a.Id == field.AssetId);
            details.AddChild(L("随队异常 / " + Catalog.Asset(field.AssetId).Name, 20, Teal, true));
            details.AddChild(L($"剩余使用次数 {asset?.Charges ?? 0}", 18, Muted));
        }
        details.AddChild(L("待撤出货物", 20, Gold));
        details.AddChild(L(field.Cargo.Count == 0 ? "空载" : string.Join("\n", field.Cargo.Select(id => Catalog.Assets.FirstOrDefault(a => a.Id == id)?.Name ?? id)), 18, Muted, true));

        var boardPanel = Panel(); boardPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(boardPanel);
        var boardCol = V(6); boardPanel.AddChild(boardCol);
        boardCol.AddChild(L($"现场网格 / 当前控制 {hero.Name}", 20, Teal));
        _board = new GridBoard { Game = game, Engine = _engine, SelectedHero = _hero, SelectedCard = _card, Selected = _target,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(640, 360) };
        _board.Chosen += cell => { _target = cell; Render(); }; boardCol.AddChild(_board);
        boardCol.AddChild(L("入口 A4 · 两人距入口 1 格内可撤离。浅绿格为可用目标，点击只选中。", 17, Muted, true));

        var right = Panel(); right.CustomMinimumSize = new Vector2(365, 0); row.AddChild(right);
        var action = V(10); right.AddChild(Scroll(action));
        action.AddChild(L("行动预览", 23, Teal));
        if (_card.Length == 0)
        {
            action.AddChild(L("从下方选择一张牌", 25, Gold, true));
            action.AddChild(L("基础牌始终可用。角色手牌提供更远的范围、特殊操作与连招。", 19, Ink, true));
        }
        else
        {
            var def = _engine.GetCardView(_hero, _card); var result = _engine.Preview(_hero, _card, _target);
            action.AddChild(L(def.Name, 28, Gold));
            action.AddChild(L($"{hero.Name} · {def.Cost} 行动 · 目标 {_target}", 18, Muted, true));
            action.AddChild(L(def.Description, 20, Ink, true));
            action.AddChild(L(result.Message, 19, result.Success ? Teal : Red, true));
            action.AddChild(B("打出此牌 →", () => Apply(() => _engine.Play(_hero, _card, _target)), true, 0, !result.Success));
        }
        action.AddChild(L("敌方下一步", 21, Gold));
        foreach (var enemy in field.Enemies.Where(e => e.Alive))
        {
            action.AddChild(L(enemy.Name + (enemy.IsObjective ? " / 目标" : "") + $" · {enemy.Health}/{enemy.MaxHealth}", 18, Red, true));
            action.AddChild(L(_engine.EnemyIntent(enemy), 17, Muted, true));
        }
        if (!field.Enemies.Any(e => e.Alive)) action.AddChild(L("现场敌人已清除。", 18, Teal));
        action.AddChild(L("最近发生", 21, Teal));
        foreach (var log in field.Log.TakeLast(4)) action.AddChild(L(log, 17, Muted, true));

        CardTray();
    }
    private void SelectCard(string cardId)
    {
        _card = cardId; var def = _engine.GetCardView(_hero, cardId);
        if (_game?.Field is not null && def.Target is "self" or "end" or "exit") _target = _game.Field.Actor(_hero).Cell;
        Render();
    }
    private void CardTray()
    {
        var field = _game!.Field!; var actor = field.Actor(_hero);
        var tray = Panel(new Color("101c2b")); tray.CustomMinimumSize = new Vector2(0, 215); _root.AddChild(tray);
        var row = H(18); tray.AddChild(row);
        var basic = V(8); basic.CustomMinimumSize = new Vector2(790, 0); row.AddChild(basic);
        basic.AddChild(L("常驻行动 / 所有操作都由卡牌发动", 19, Teal));
        var grid = new GridContainer { Columns = 6 }; grid.AddThemeConstantOverride("h_separation", 7); grid.AddThemeConstantOverride("v_separation", 7); basic.AddChild(grid);
        foreach (var cardId in Catalog.BasicIds)
        {
            var def = _engine.GetCardView(_hero, cardId);
            var button = B(def.Name + $"\n{def.Cost} 行动", () => SelectCard(cardId), _card == cardId, 122);
            button.CustomMinimumSize = new Vector2(122, 67); button.AddThemeFontSizeOverride("font_size", 18); button.TooltipText = def.Description;
            grid.AddChild(button);
        }
        var handCol = V(8); handCol.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(handCol);
        handCol.AddChild(L($"{_game.Hero(_hero).Name}的手牌 / 牌库 {actor.Draw.Count} · 弃牌 {actor.Discard.Count}", 19, Gold));
        var hand = H(9); hand.SizeFlagsVertical = SizeFlags.ExpandFill; handCol.AddChild(hand);
        foreach (var id in actor.Hand) hand.AddChild(CardButton(id));
        if (field.AssetId.Length > 0) hand.AddChild(CardButton(Catalog.Asset(field.AssetId).FieldCard, true));
        if (actor.Hand.Count == 0) hand.AddChild(L("本轮职业牌已用完。仍可使用常驻牌；结束回合后补充手牌。", 19, Muted, true, true));
    }
    private Control CardButton(string id, bool asset = false)
    {
        var card = _engine.GetCardView(_hero, id);
        var button = new Button { CustomMinimumSize = new Vector2(160, 150), SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None, TooltipText = card.Description };
        button.AddThemeStyleboxOverride("normal", Box(_card == id ? new Color("2a4a47") : asset ? new Color("352d3f") : new Color("1b2b3d"), _card == id ? Gold : asset ? new Color("9d8bc9") : new Color("3b566a"), _card == id ? 2 : 1));
        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 12);
        button.AddChild(margin); var col = V(5); col.MouseFilter = MouseFilterEnum.Ignore; margin.AddChild(col);
        col.AddChild(L((asset ? "异常 / " : "职业 / ") + card.Cost + " 行动", 15, Muted));
        col.AddChild(L(card.Name, 22, _card == id ? Gold : Ink, true));
        var summary = L(card.Description, 17, Muted, true); summary.MaxLinesVisible = 3; col.AddChild(summary);
        foreach (var child in col.GetChildren().OfType<Control>()) child.MouseFilter = MouseFilterEnum.Ignore;
        button.Pressed += () => SelectCard(id); return button;
    }
}
