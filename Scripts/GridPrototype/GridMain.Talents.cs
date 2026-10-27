using Godot;
using Joy.Core.GridPrototype;
using System;
using System.Linq;

namespace Joy.GridPrototype;

public partial class GridMain
{
    private string _talentHero = "mapper", _talentNode = "mapper-a1", _talentTab = "tree", _talentFlash = "", _deckCard = "";

    private void Skills()
    {
        var game = _game!; var hero = game.Hero(_talentHero); var def = Catalog.Hero(hero.Id);
        var row = H(16); row.SizeFlagsVertical = SizeFlags.ExpandFill; _root.AddChild(row);
        var roster = Panel(); roster.CustomMinimumSize = new Vector2(260, 0); row.AddChild(roster);
        var people = V(14); roster.AddChild(Scroll(people));
        people.AddChild(L("角色天赋", 27, Teal));
        people.AddChild(L("沿连接线学习前置节点，逐步构筑自己的打法。", 18, Muted, true));
        foreach (var h in game.Heroes)
        {
            var hd = Catalog.Hero(h.Id);
            people.AddChild(B($"{h.Name} / {h.Role}\n{h.SkillPoints} 点可用 · 已亮 {h.LearnedTalents.Count}/9", () =>
            {
                _talentHero = h.Id; _talentNode = h.Id + "-a1"; _deckCard = ""; Render();
            }, _talentHero == h.Id));
        }
        people.AddChild(L("三种节点", 22, Gold));
        people.AddChild(L("+ 数值强化\n距离、伤害与持续时间\n\n◇ 机制变化\n让已有卡牌产生新效果\n\n▣ 特色新牌\n解锁后配置到个人牌组", 18, Muted, true));
        people.AddChild(L("每个节点 1 点。完成新委托，全员获得 1 点。可以跨分支投入。", 18, Ink, true));

        var main = V(10); main.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(main);
        var top = H(12); main.AddChild(top);
        top.AddChild(L($"{hero.Name} · {hero.Role}", 28, new Color(def.Color), false, true));
        top.AddChild(L($"可用天赋点  {hero.SkillPoints}", 23, Gold));
        top.AddChild(B("天赋树", () => { _talentTab = "tree"; Render(); }, _talentTab == "tree", 110));
        top.AddChild(B("个人牌组", () => { _talentTab = "deck"; Render(); }, _talentTab == "deck", 130));
        if (_talentTab == "deck") { TalentDeck(main, hero); return; }
        var body = H(14); body.SizeFlagsVertical = SizeFlags.ExpandFill; main.AddChild(body);
        var treePanel = Panel(); treePanel.SizeFlagsHorizontal = SizeFlags.ExpandFill; body.AddChild(treePanel);
        var treeCol = V(8); treePanel.AddChild(treeCol);
        treeCol.AddChild(L("● 已点亮    ○ 可学习    ◌ 待解锁     点击任意节点查看效果", 17, Muted, true));
        var tree = new TalentTreeView
        {
            RootTitle = hero.Role, HeroColor = new Color(def.Color), SelectedId = _talentNode, FlashId = _talentFlash,
            BranchNames = new[] { def.BranchA, def.BranchB, "精修" },
            CustomMinimumSize = new Vector2(650, 620), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
            Nodes = TalentCatalog.ForHero(hero.Id).Select(n => new TalentVisual(n.Id, n.Name, n.Kind, n.Column, n.Row, n.Cost,
                hero.LearnedTalents.Contains(n.Id), _engine.PreviewTalent(hero.Id, n.Id).Success, n.Prerequisites)).ToArray()
        };
        _talentFlash = "";
        tree.NodeSelected += id => { _talentNode = id; Render(); }; treeCol.AddChild(tree);
        var detailPanel = Panel(); detailPanel.CustomMinimumSize = new Vector2(350, 0); body.AddChild(detailPanel);
        var detail = V(13); detailPanel.AddChild(Scroll(detail));
        var node = TalentCatalog.ForHero(hero.Id).FirstOrDefault(n => n.Id == _talentNode) ?? TalentCatalog.ForHero(hero.Id).First();
        _talentNode = node.Id;
        var learned = hero.LearnedTalents.Contains(node.Id); var canLearn = _engine.PreviewTalent(hero.Id, node.Id);
        detail.AddChild(L(TalentTreeView.KindSymbol(node.Kind) + " " + TalentTreeView.KindName(node.Kind), 20, Teal));
        detail.AddChild(L(node.Name, 31, learned ? Gold : Ink, true));
        detail.AddChild(L(learned ? "● 已点亮" : canLearn.Success ? "○ 可以学习" : "◌ 尚未解锁", 21, learned ? Gold : canLearn.Success ? Teal : Muted));
        detail.AddChild(L(node.Description, 21, Ink, true));
        detail.AddChild(L("前置节点", 19, Teal));
        detail.AddChild(L(node.Prerequisites.Count == 0 ? "职业根基 · 已掌握" : string.Join("\n", node.Prerequisites.Select(id =>
            (hero.LearnedTalents.Contains(id) ? "✓ " : "○ ") + TalentCatalog.Node(id).Name)), 19, Muted, true));
        detail.AddChild(B(learned ? "已经点亮" : $"点亮节点 · {node.Cost} 天赋点", () => Apply(() =>
        {
            var result = _engine.LearnTalent(hero.Id, node.Id);
            if (result.Success) _talentFlash = node.Id;
            return result;
        }), !learned && canLearn.Success, 0, learned || !canLearn.Success));
        if (!learned && !canLearn.Success) detail.AddChild(L(canLearn.Message, 18, Muted, true));
        if (node.UnlockCardId.Length > 0)
        {
            var card = _engine.GetCardView(hero.Id, node.UnlockCardId);
            detail.AddChild(L(learned ? "已解锁特色牌" : "将解锁特色牌", 21, Gold));
            detail.AddChild(L(card.Name + $" · {card.Cost} 行动\n" + card.Description, 19, Ink, true));
            detail.AddChild(B(learned ? "配置这张牌 →" : "查看个人牌库 →", () => { _deckCard = card.Id; _talentTab = "deck"; Render(); }, learned));
            detail.AddChild(L("解锁后进入可配牌库；由你决定加入或替换，不会自动改变牌组。", 17, Muted, true));
        }
        else if (node.AffectedCards.Count > 0)
        {
            detail.AddChild(L("相关卡牌 · 当前效果", 20, Gold));
            foreach (var id in node.AffectedCards)
            {
                var card = _engine.GetCardView(hero.Id, id);
                detail.AddChild(L(card.Name + "\n" + card.Description, 18, Muted, true));
            }
        }
    }

    private void TalentDeck(VBoxContainer main, HeroState hero)
    {
        main.AddChild(L("天赋解锁的特色牌会出现在牌库中。个人牌组 6–10 张，同名最多 2 张。", 19, Muted, true));
        var body = H(14); body.SizeFlagsVertical = SizeFlags.ExpandFill; main.AddChild(body);
        var deckPanel = Panel(); deckPanel.CustomMinimumSize = new Vector2(320, 0); body.AddChild(deckPanel);
        var deck = V(11); deckPanel.AddChild(Scroll(deck));
        deck.AddChild(L($"已装入 / {hero.Deck.Count} 张", 24, Gold));
        foreach (var group in hero.Deck.GroupBy(id => id))
        {
            string id = group.Key; var card = _engine.GetCardView(hero.Id, id);
            deck.AddChild(B(card.Name + $" × {group.Count()}", () => { _deckCard = id; Render(); }, _deckCard == id));
        }
        deck.AddChild(L("点击牌名可查看天赋强化后的实际效果。", 17, Muted, true));
        deck.AddChild(B("返回天赋树 →", () => { _talentTab = "tree"; Render(); }));

        var libraryPanel = Panel(); libraryPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill; body.AddChild(libraryPanel);
        var library = V(12); libraryPanel.AddChild(Scroll(library));
        library.AddChild(L("职业牌库", 24, Teal));
        var available = _engine.GetAvailableDeckCards(hero.Id).Select(c => c.Id).ToHashSet();
        foreach (var raw in Catalog.Cards.Where(c => c.Owner == hero.Id))
        {
            var card = _engine.GetCardView(hero.Id, raw.Id); bool unlocked = available.Contains(card.Id);
            var unlock = TalentCatalog.ForHero(hero.Id).FirstOrDefault(n => n.UnlockCardId == card.Id);
            library.AddChild(B((unlocked ? "" : "锁定 · ") + card.Name + (unlock is null ? "" : "  / 特色"), () => { _deckCard = card.Id; Render(); }, _deckCard == card.Id));
        }
        var inspectPanel = Panel(); inspectPanel.CustomMinimumSize = new Vector2(390, 0); body.AddChild(inspectPanel);
        var inspect = V(14); inspectPanel.AddChild(Scroll(inspect));
        var selected = Catalog.Cards.FirstOrDefault(c => c.Id == _deckCard && c.Owner == hero.Id) ?? Catalog.Cards.First(c => c.Owner == hero.Id);
        _deckCard = selected.Id; var view = _engine.GetCardView(hero.Id, selected.Id);
        bool isAvailable = available.Contains(selected.Id); int copies = hero.Deck.Count(id => id == selected.Id);
        inspect.AddChild(L(view.Name, 30, isAvailable ? Gold : Muted, true));
        inspect.AddChild(L($"{view.Cost} 行动 · 范围 {view.Range} · 已装入 {copies}/2", 18, Teal, true));
        inspect.AddChild(L(view.Description, 21, Ink, true));
        var required = TalentCatalog.ForHero(hero.Id).FirstOrDefault(n => n.UnlockCardId == selected.Id);
        if (!isAvailable)
        {
            inspect.AddChild(L("尚未解锁\n需要点亮「" + (required?.Name ?? "对应天赋") + "」。", 20, Muted, true));
            if (required is not null) inspect.AddChild(B("在树上查看 →", () => { _talentNode = required.Id; _talentTab = "tree"; Render(); }, true));
        }
        inspect.AddChild(B("加入一张", () => Apply(() => _engine.ChangeDeck(hero.Id, "", selected.Id)), isAvailable, 0,
            !isAvailable || copies >= 2 || hero.Deck.Count >= 10 || _game!.Phase != "city"));
        inspect.AddChild(B("移出一张", () => Apply(() => _engine.ChangeDeck(hero.Id, selected.Id, "")), false, 0,
            copies == 0 || hero.Deck.Count <= 6 || _game!.Phase != "city"));
        var replaceIds = hero.Deck.Distinct().Where(id => id != selected.Id).ToArray();
        inspect.AddChild(L("或替换牌组中的一张", 19, Teal));
        var replacement = new OptionButton { CustomMinimumSize = new Vector2(0, 48) };
        foreach (string id in replaceIds) replacement.AddItem(_engine.GetCardView(hero.Id, id).Name);
        inspect.AddChild(replacement);
        inspect.AddChild(B("替换为这张牌", () =>
        {
            if (replaceIds.Length > 0) Apply(() => _engine.ChangeDeck(hero.Id, replaceIds[replacement.Selected], selected.Id));
        }, false, 0, !isAvailable || copies >= 2 || replaceIds.Length == 0 || _game!.Phase != "city"));
    }
}
