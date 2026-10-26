using Godot;
using Joy.Core.GridPrototype;
using System;
using System.Linq;

namespace Joy.GridPrototype;

public partial class GridMain
{
    private void City()
    {
        var game = _game!;
        var tabs = H(10); _root.AddChild(tabs);
        foreach (var (id, name) in new[] { ("dispatch", "01 / 城市调度"), ("skills", "02 / 角色成长"), ("assets", "03 / 异常收容") })
            tabs.AddChild(B(name, () => { _page = id; Render(); }, _page == id, 200));
        tabs.AddChild(L("谁留守，谁出勤，哪些能力带去现场。", 19, Muted, false, true));
        if (game.Phase is "victory" or "defeat" || game.Outcome.Length > 0)
        {
            var outcome = Panel(new Color("233b38")); var content = V(); outcome.AddChild(content);
            content.AddChild(L(game.Outcome.Length > 0 ? game.Outcome : game.Phase == "victory" ? "雾港交接完成" : "本次远征结束", 28, Gold, true));
            content.AddChild(L(game.LastReport, 19, Ink, true)); _root.AddChild(outcome);
        }
        if (_page == "skills") Skills(); else if (_page == "assets") Assets(); else Dispatch();
    }

    private void Dispatch()
    {
        var game = _game!;
        var row = H(16); row.SizeFlagsVertical = SizeFlags.ExpandFill; _root.AddChild(row);
        var staff = Panel(); staff.CustomMinimumSize = new Vector2(350, 0); row.AddChild(staff);
        var people = V(12); people.AddChild(L("本日岗位", 25, Teal));
        people.AddChild(L($"外勤 {game.SelectedHeroes.Count} / 2 · 其余留守\n要换人时，先取消一名外勤。", 18, Muted, true));
        foreach (var hero in game.Heroes)
        {
            var def = Catalog.Hero(hero.Id); var selected = game.SelectedHeroes.Contains(hero.Id);
            var box = Panel(selected ? new Color("203d43") : new Color("172230")); var c = V(7); box.AddChild(c);
            c.AddChild(L(hero.Name + " / " + hero.Role, 22, new Color(def.Color)));
            c.AddChild(L($"体力 {hero.Health}/{hero.MaxHealth}   技能点 {hero.SkillPoints}", 18, Muted));
            c.AddChild(L(selected ? "出勤：携带个人牌组进入现场。" : def.CompanyEffect, 17, Ink, true));
            c.AddChild(B(selected ? "✓ 已选外勤 · 点击留守" : "加入外勤", () => Apply(() => _engine.ToggleHero(hero.Id)), selected));
            people.AddChild(box);
        }
        people.AddChild(L("随队异常", 20, Gold));
        people.AddChild(L(game.SelectedAsset.Length > 0 ? Catalog.Asset(game.SelectedAsset).Name : "未携带 / 收容后可配置", 18, Ink, true));
        people.AddChild(B("管理技能与牌组 →", () => { _page = "skills"; Render(); }));
        staff.AddChild(Scroll(people));

        var center = Panel(); center.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(center);
        var map = V(9); center.AddChild(map);
        map.AddChild(L("雾港 / 街区经营图", 25, Ink));
        map.AddChild(L("道路决定通行 · 选格查看 · 金色为本次派遣路线", 18, Muted, true));
        _board = new GridBoard { Game = game, Engine = _engine, CityMode = true, Selected = _cityCell, Route = _engine.RouteTo(_site), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(540, 390) };
        _board.Chosen += cell =>
        {
            _cityCell = cell;
            var tile = game.City.FirstOrDefault(t => t.Cell == cell);
            if (tile is not null && tile.SiteId.Length > 0 && tile.SiteId != "hq") _site = tile.SiteId;
            Render();
        };
        map.AddChild(_board);
        map.AddChild(L("■ 建筑  ━ 道路  × 阻断  ● 营业", 17, Muted));
        map.AddChild(L(_engine.CityForecast, 18, Gold, true));

        var side = Panel(); side.CustomMinimumSize = new Vector2(365, 0); row.AddChild(side);
        var info = V(12); side.AddChild(Scroll(info));
        var tileSelected = game.City.FirstOrDefault(t => t.Cell == _cityCell);
        var selectedSite = game.City.FirstOrDefault(t => t.SiteId == _site);
        info.AddChild(L("行动计划", 25, Teal));
        info.AddChild(L("第 7 日结束前完成三个委托与两处营业；最后一次出勤前建好网点。", 17, Muted, true));
        if (selectedSite is not null)
        {
            var artId = selectedSite.SiteId switch { "hotel" => "node-hotel", "hospital" => "node-hospital", _ => "node-source" };
            info.AddChild(ArtAssets.Image(artId, 150, TextureRect.StretchModeEnum.KeepAspectCentered));
            info.AddChild(L(selectedSite.Name, 27, Ink));
            info.AddChild(L(selectedSite.Operating ? "● 已营业" : selectedSite.Cleared ? "✓ 已处理 · 可以建设" : "◇ 待回收", 20, Gold));
            info.AddChild(L(selectedSite.Description, 19, Ink, true));
            var route = _engine.RouteTo(_site);
            info.AddChild(L(route.Count > 0 ? "通行路线\n" + string.Join(" → ", route) : "通路未连通，请检查阻断路段。", 17, Muted, true));
            info.AddChild(B("派遣外勤 →", () =>
            {
                _card = "";
                Apply(() => _engine.StartMission(_site));
                if (_game?.Field is not null) { _hero = _game.Field.Actors[0].HeroId; _target = _game.Field.Actors[0].Cell; Render(); }
            }, true, 0, game.Phase is "victory" or "defeat"));
            info.AddChild(B("建立网点 · 4 资金", () => Apply(() => _engine.BuildSite(_site)), false, 0, !selectedSite.Cleared || selectedSite.Operating));
        }
        if (tileSelected?.Blocked == true)
        {
            info.AddChild(L($"{_cityCell} 道路阻断", 21, Red));
            info.AddChild(B("修复道路 · 2 资金", () => Apply(() => _engine.RepairRoad(_cityCell)), true));
        }
        info.AddChild(B("补给与休整 · 3 资金", () => Apply(() => _engine.Resupply())));
        info.AddChild(L("上次归队", 20, Teal)); info.AddChild(L(game.LastReport, 18, Muted, true));
    }

    private void Skills()
    {
        var game = _game!;
        var row = H(18); row.SizeFlagsVertical = SizeFlags.ExpandFill; _root.AddChild(row);
        foreach (var hero in game.Heroes)
        {
            var def = Catalog.Hero(hero.Id); var panel = Panel(); panel.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(panel);
            var col = V(12); panel.AddChild(Scroll(col));
            col.AddChild(L(hero.Name + " / " + hero.Role, 28, new Color(def.Color)));
            col.AddChild(L($"可用技能点 {hero.SkillPoints}  ·  专精等级 {hero.SkillLevel}", 20, Gold));
            col.AddChild(L(def.CompanyEffect, 19, Muted, true));
            foreach (var branch in new[] { "a", "b" })
            {
                string name = branch == "a" ? def.BranchA : def.BranchB, desc = branch == "a" ? def.BranchAText : def.BranchBText;
                var box = Panel(hero.Specialization == branch ? new Color("23413e") : new Color("182536"));
                var bcol = V(6); box.AddChild(bcol);
                bcol.AddChild(L(name + (hero.Specialization == branch ? "  ✓" : ""), 23, Teal));
                bcol.AddChild(L(desc, 19, Ink, true));
                bcol.AddChild(B(hero.Specialization == branch ? hero.SkillLevel >= 2 ? "已达到本版最高等级" : "强化此专精 · 1 技能点" : "选择此专精 · 1 技能点", () => Apply(() => _engine.LearnSkill(hero.Id, branch)), hero.Specialization == branch, 0, hero.SkillPoints == 0 || hero.SkillLevel >= 2 || (hero.Specialization.Length > 0 && hero.Specialization != branch)));
                col.AddChild(box);
            }
            col.AddChild(L($"个人牌组 / {hero.Deck.Count} 张", 23, Gold));
            foreach (var group in hero.Deck.GroupBy(id => id))
            {
                var card = Catalog.Card(group.Key);
                col.AddChild(L($"{card.Name} × {group.Count()}  ·  {card.Cost} 行动\n{card.Description}", 18, Ink, true));
            }
            col.AddChild(L("替换一张牌", 22, Teal));
            var remove = new OptionButton { CustomMinimumSize = new Vector2(0, 44) };
            var existing = hero.Deck.Distinct().ToArray(); foreach (var id in existing) remove.AddItem(Catalog.Card(id).Name);
            var add = new OptionButton { CustomMinimumSize = new Vector2(0, 44) };
            var options = Catalog.DeckOptions(hero.Id).ToArray(); foreach (var card in options) add.AddItem(card.Name);
            col.AddChild(L("移出", 17, Muted)); col.AddChild(remove); col.AddChild(L("加入", 17, Muted)); col.AddChild(add);
            var replacement = L(options.Length > 0 ? options[0].Description : "", 18, Muted, true); col.AddChild(replacement);
            add.ItemSelected += index => replacement.Text = options[(int)index].Description;
            col.AddChild(B("确认替换", () =>
            {
                if (existing.Length > 0 && options.Length > 0) Apply(() => _engine.ChangeDeck(hero.Id, existing[remove.Selected], options[add.Selected].Id));
            }));
        }
    }

    private void Assets()
    {
        var game = _game!;
        var intro = L("每件异常只有一个实体。随队使用卡牌，留守发挥经营能力；本次最多携带一件。", 21, Muted, true); _root.AddChild(intro);
        var row = H(18); row.SizeFlagsVertical = SizeFlags.ExpandFill; _root.AddChild(row);
        foreach (var def in Catalog.Assets)
        {
            var asset = game.Assets.FirstOrDefault(a => a.Id == def.Id);
            var panel = Panel(asset is null ? new Color("121c29") : Surface); panel.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(panel);
            var col = V(18); panel.AddChild(Scroll(col));
            col.AddChild(new TextureRect
            {
                Texture = GD.Load<Texture2D>($"res://Assets/Grid/{def.Id}.svg"),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(0, 210), SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore
            });
            col.AddChild(L(def.Name, 30, asset is null ? Muted : Gold));
            col.AddChild(L(asset is null ? "尚未收容 / 在现场创造条件后使用收容牌" : $"已收容 · 余量 {asset.Charges}/{asset.MaxCharges}", 20, asset is null ? Muted : Teal, true));
            col.AddChild(L(def.Description, 21, Ink, true));
            col.AddChild(L("经营 / 留守", 22, Teal)); col.AddChild(L(def.CompanyEffect, 20, Ink, true));
            col.AddChild(L("探索与战斗 / 随队", 22, Teal));
            var card = Catalog.Card(def.FieldCard); col.AddChild(L(card.Name + "\n" + card.Description, 20, Ink, true));
            col.AddChild(B(game.SelectedAsset == def.Id ? "✓ 已随队 · 点击留守" : "配置为随队异常", () => Apply(() => _engine.SelectAsset(def.Id)), game.SelectedAsset == def.Id, 0, asset is null));
        }
    }
}
