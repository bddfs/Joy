using System;
using System.Collections.Generic;
using System.Linq;

namespace Joy.Core.GridPrototype;

/// <summary>Independent, deterministic gameplay slice. All field mutations flow through cards.</summary>
public sealed class GridEngine
{
    public GridGame Game { get; }
    public GridEngine(GridGame game) => Game = game;
    public string CityForecast => Game.Site("source").Cleared ? "源头已处置，不再发生道路扩散。" : Game.Day == 1 ? "本次归队后，若源头尚未处置：D3 道路将封闭；可绕行或花 2 资金抢修。" : Game.Day == 3 ? "本次归队后，若源头尚未处置：F3 道路将封闭；可绕行或花 2 资金抢修。" : "本次出勤归队后没有新的道路封闭。";
    public static bool IsBasic(string cardId) => Catalog.BasicIds.Contains(cardId);
    public static GridGame NewGame(int seed = 413)
    {
        var g = new GridGame { Seed = seed, RandomState = (uint)Math.Max(1, Math.Abs((long)seed) % uint.MaxValue) };
        foreach (var def in Catalog.Heroes)
        {
            var options = Catalog.StartingOptions(def.Id).Select(c => c.Id).ToList();
            g.Heroes.Add(new HeroState { Id = def.Id, Name = def.Name, Role = def.Role,
                Deck = options.Concat(options.Take(2)).ToList() });
        }
        for (int y = 0; y < 5; y++) for (int x = 0; x < 7; x++)
        {
            bool road = y == 2 || (y is 1 or 3 && x >= 1 && x <= 5);
            g.City.Add(new CityTile { X = x, Y = y, Terrain = road ? "road" : "block", Name = road ? "城市道路" : "封闭街区" });
        }
        SetSite(g, new(0, 2), "hq", "公司总部", "选择两名外勤；留守角色与留库异常在归队时发挥作用。", true);
        SetSite(g, new(2, 1), "hotel", "暮灯旅馆", "委托 8。调查回声，再决定驱散或收容回声侍者。接管后收入 +1 / 日。");
        SetSite(g, new(4, 3), "hospital", "白榆医院", "委托 10。镜柜不追逐声音，照明可以让它停顿。接管后收入 +1 / 日。");
        SetSite(g, new(6, 2), "source", "红箱源头", "委托 14。完成旅馆与医院后可出勤。源头处置、两个营业网点即可交接。");
        g.Journal.Add("雾港分局开张。先选择角色专精与两名外勤，再从城市地块进入任务。");
        return g;
    }
    private static void SetSite(GridGame g, Cell at, string id, string name, string desc, bool cleared = false)
    {
        var t = g.City.Single(t => t.Cell == at); t.SiteId = id; t.Name = name; t.Description = desc; t.Terrain = "site"; t.Cleared = cleared;
    }
    private Result CityCheck() => Game.Phase == "city" ? Result.Ok("") : Result.Fail("当前不能更改公司安排。");
    public Result ToggleHero(string id)
    {
        if (!CityCheck().Success) return CityCheck();
        if (!Game.Heroes.Any(h => h.Id == id)) return Result.Fail("没有这名角色。");
        if (Game.SelectedHeroes.Contains(id)) Game.SelectedHeroes.Remove(id);
        else { if (Game.SelectedHeroes.Count >= 2) return Result.Fail("外勤限两人，请先取消一名角色。"); Game.SelectedHeroes.Add(id); }
        return Result.Ok("已调整外勤队伍，未出勤的角色留守公司。");
    }
    public Result SelectAsset(string id)
    {
        if (!CityCheck().Success) return CityCheck();
        if (id != "" && !Game.Assets.Any(a => a.Id == id)) return Result.Fail("公司还没有收容这件异常。");
        if (id != "" && Game.Assets.First(a => a.Id == id).Charges <= 0) return Result.Fail("这件异常需要维护，当前没有使用次数。");
        Game.SelectedAsset = id; return Result.Ok(id == "" ? "全部异常留库发挥经营效果。" : $"将携带{Catalog.Asset(id).Name}，本次无法获得它的留库效果。");
    }
    public Result LearnSkill(string heroId, string branch)
    {
        var h = Game.Heroes.FirstOrDefault(h => h.Id == heroId);
        if (h is null || branch is not ("a" or "b")) return Result.Fail("无效的角色或专精。");
        var node = Enumerable.Range(1, 2).Select(i => $"{heroId}-{branch}{i}").FirstOrDefault(id => !h.LearnedTalents.Contains(id));
        if (node is null) return Result.Fail("这条旧专精的两个节点已经点亮；可在天赋树继续发展。");
        var result = LearnTalent(heroId, node);
        if (result.Success) { h.Specialization = branch; h.SkillLevel = h.LearnedTalents.Contains($"{heroId}-{branch}2") ? 2 : 1; }
        return result;
    }
    public Result PreviewTalent(string heroId, string nodeId)
    {
        if (!CityCheck().Success) return Result.Fail("请在公司准备阶段点亮天赋。");
        var hero = Game.Heroes.FirstOrDefault(h => h.Id == heroId);
        var node = TalentCatalog.Nodes.FirstOrDefault(n => n.Id == nodeId && n.HeroId == heroId);
        if (hero is null || node is null) return Result.Fail("这个天赋不属于当前角色。");
        if (hero.LearnedTalents.Contains(nodeId)) return Result.Fail("这个节点已经点亮，无需重复支付。");
        var missing = node.Prerequisites.Where(id => !hero.LearnedTalents.Contains(id)).ToArray();
        if (missing.Length > 0) return Result.Fail("前置未满足：" + string.Join("、", missing.Select(id => TalentCatalog.Node(id).Name)));
        if (hero.SkillPoints < node.Cost) return Result.Fail($"需要 {node.Cost} 技能点，当前 {hero.SkillPoints}；完成新地点委托可获得技能点。");
        return Result.Ok($"花费 {node.Cost} 技能点，点亮「{node.Name}」。\n{node.Description}" + (node.UnlockCardId != "" ? "\n解锁后可在个人牌组中加入或替换，不会自动塞入牌组。" : ""));
    }
    public Result LearnTalent(string heroId, string nodeId)
    {
        var valid = PreviewTalent(heroId, nodeId); if (!valid.Success) return valid;
        var hero = Game.Hero(heroId); var node = TalentCatalog.Node(nodeId);
        hero.SkillPoints -= node.Cost; hero.LearnedTalents.Add(nodeId);
        return Result.Ok($"{hero.Name}点亮「{node.Name}」，剩余 {hero.SkillPoints} 技能点。" + (node.UnlockCardId == "" ? "对应卡牌效果已更新。" : $"已解锁「{Catalog.Card(node.UnlockCardId).Name}」，请加入或替换到个人牌组。"));
    }
    public IReadOnlyList<CardDef> GetAvailableDeckCards(string heroId)
    {
        var hero = Game.Heroes.FirstOrDefault(h => h.Id == heroId);
        return hero is null ? Array.Empty<CardDef>() : Catalog.DeckOptions(heroId).Where(c => TalentCatalog.IsCardUnlocked(hero, c.Id)).Select(c => GetCardView(heroId, c.Id)).ToArray();
    }
    public CardDef GetCardView(string heroId, string cardId)
    {
        var card = Catalog.Card(cardId);
        if (!Game.Heroes.Any(h => h.Id == heroId)) return card;
        var a = new ActorState { HeroId = heroId }; int range = Range(a, card); string desc = card.Description;
        if (cardId is "move" or "sprint" or "dash" or "escort") desc = $"沿合法通路移动最多 {range} 格。" + (cardId == "escort" ? "并获得 1 防护。" : "不能穿过墙、关闭的门、敌人或队友。");
        if (cardId == "shot") desc = $"对可见目标造成 {BaseDamage(heroId, cardId)} 基础伤害；标定目标额外 +1。" + (HasSkill(heroId, "c", 2) ? "命中已标定目标时至少保留其 1 生命。" : "");
        if (cardId == "arc") desc = $"对可见目标造成 {BaseDamage(heroId, cardId)} 基础伤害；标定可提高伤害。";
        if (cardId == "soothe") desc = $"造成 {BaseDamage(heroId, cardId)} 基础伤害，并标定存活目标至下一轮；伤害可能击败目标。";
        if (cardId is "capture" or "quick-capture") desc = $"已取得证据、目标生命 ≤{CaptureThreshold(heroId)}，且被标定、束缚或照明：消耗 1 工具收容。作用距离 {range} 格。";
        if (cardId == "mark") desc = "标定可见目标至下一轮，受到的伤害 +1，并满足收容控制条件。" + MarkExtras(heroId);
        if (cardId == "chain-mark") desc = "标定目标及其相邻可见敌人至下一轮，受到的伤害 +1。" + MarkExtras(heroId);
        if (cardId == "bind") desc = $"让可见目标停顿 {BindDuration(heroId)} 次敌方行动，并满足收容控制条件。";
        if (cardId == "light") desc = $"在 {range} 格内布置半径 1 的灯区，持续 {LightDuration(heroId)} 轮。" + LightExtras();
        if (cardId == "mobile-light") desc = $"沿通路移动最多 {range} 格，并在落点建立半径 1 的灯区，持续 {LightDuration(heroId)} 轮。" + LightExtras();
        if (cardId == "light-dome") desc = $"在 {range} 格内建立半径 1 的灯区，持续 {LightDuration(heroId)} 轮；其中队员各获得 3 防护。" + LightExtras();
        if (cardId == "repair") desc = "为可见队友恢复 3 生命。" + (HasSkill(heroId, "c", 2) ? "额外提供 2 防护，满生命时也生效。" : "");
        if (cardId == "probe") desc = "取得可见线索、打开箱柜或房门，并获得 2 防护。" + (HasSkill(heroId, "c", 2) ? "打开箱柜后有货物位置时自动拾取物资。" : "");
        return card with { Cost = Cost(a, card), Range = range, Description = desc };
        string LightExtras() => "畏光敌人在灯区中停顿，灯区内敌人可满足收容控制条件。" + (HasSkill(heroId, "b") ? "队员在灯区每次受敌人攻击伤害 −1。" : "");
    }
    public Result ChangeDeck(string heroId, string removeCard, string addCard)
    {
        if (!CityCheck().Success) return CityCheck();
        var h = Game.Heroes.FirstOrDefault(h => h.Id == heroId);
        if (h is null) return Result.Fail("没有这名角色。");
        if (removeCard == "" && addCard == "") return Result.Fail("请指定要调整的牌。");
        if (removeCard != "" && !h.Deck.Contains(removeCard)) return Result.Fail("待移除的牌不在牌组中。");
        if (addCard != "" && !GetAvailableDeckCards(heroId).Any(c => c.Id == addCard)) return Result.Fail("只能加入该角色已经解锁的专属牌；特色牌需要先点亮天赋节点。");
        var proposed = h.Deck.ToList(); if (removeCard != "") proposed.Remove(removeCard); if (addCard != "") proposed.Add(addCard);
        if (proposed.Count is < 6 or > 10) return Result.Fail("个人牌组需要 6–10 张。");
        if (proposed.GroupBy(x => x).Any(gr => gr.Count() > 2)) return Result.Fail("同名专属牌最多两张。");
        h.Deck = proposed; return Result.Ok($"{h.Name}牌组已调整：{h.Deck.Count} 张。");
    }
    public List<Cell> RouteTo(string siteId)
    {
        var target = Game.City.FirstOrDefault(t => t.SiteId == siteId);
        var start = Game.City.FirstOrDefault(t => t.SiteId == "hq");
        if (target is null || start is null) return new();
        return FindPath(start.Cell, target.Cell, c => Game.City.Any(t => t.Cell == c && t.Terrain != "block" && !t.Blocked));
    }
    public Result BuildSite(string siteId)
    {
        if (!CityCheck().Success) return CityCheck();
        var site = Game.City.FirstOrDefault(t => t.SiteId == siteId);
        if (site is null || siteId is not ("hotel" or "hospital")) return Result.Fail("只有旅馆和医院可接管为营业网点。");
        if (!site.Cleared) return Result.Fail("先完成该地点的回收或处置委托并撤离。");
        if (site.Operating) return Result.Fail("这个网点已经营业。");
        if (RouteTo(siteId).Count == 0) return Result.Fail("总部无法连通此地，请先修复道路。");
        if (Game.Money < 4) return Result.Fail("接管需要 4 资金。");
        Game.Money -= 4; site.Operating = true;
        Game.LastReport = $"{site.Name}开业：每日收入 +1；下一次出勤额外获得 1 点共享防护。";
        Record(Game.LastReport); CheckVictory(); return Result.Ok(Game.LastReport);
    }
    public Result RepairRoad(Cell cell)
    {
        if (!CityCheck().Success) return CityCheck();
        var t = Game.City.FirstOrDefault(t => t.Cell == cell);
        if (t is null || !t.Blocked) return Result.Fail("这格道路无需修复。");
        if (Game.Money < 2) return Result.Fail("抢修需要 2 资金。");
        Game.Money -= 2; t.Blocked = false; return Result.Ok($"{cell} 道路已修复。");
    }
    public Result Resupply()
    {
        if (!CityCheck().Success) return CityCheck();
        if (Game.Money < 3) return Result.Fail("补给与维护需要 3 资金。");
        if (Game.SealCharges >= 4 && Game.Heroes.All(h => h.Health == h.MaxHealth) && Game.Assets.All(a => a.Charges == a.MaxCharges)) return Result.Fail("工具、生命与异常次数均已充足。");
        Game.Money -= 3; Game.SealCharges = Math.Min(4, Game.SealCharges + 2);
        foreach (var h in Game.Heroes) h.Health = Math.Min(h.MaxHealth, h.Health + 4);
        foreach (var a in Game.Assets) a.Charges = Math.Min(a.MaxCharges, a.Charges + 1);
        return Result.Ok("工具 +2（最多 4），全员恢复 4 生命，每件异常恢复 1 次。未推进日期。");
    }
    public Result StartMission(string siteId)
    {
        if (!CityCheck().Success) return CityCheck();
        var site = Game.City.FirstOrDefault(t => t.SiteId == siteId);
        if (site is null || siteId is not ("hotel" or "hospital" or "source")) return Result.Fail("请选择一个异常地点。");
        if (site.Cleared) return Result.Fail("这个地点已经完成处置；可安排接管，或选择其他委托。");
        if (Game.SelectedHeroes.Count != 2 || Game.SelectedHeroes.Distinct().Count() != 2 || Game.SelectedHeroes.Any(id => !Game.Heroes.Any(h => h.Id == id))) return Result.Fail("请选择两名不同的外勤角色。");
        if (Game.SelectedHeroes.Any(id => Game.Hero(id).Health < 3)) return Result.Fail("外勤角色需要至少 3 生命，请先补给治疗。");
        if (Game.SelectedHeroes.Any(id => !ValidDeck(Game.Hero(id)))) return Result.Fail("角色牌组不合法，需要 6–10 张职业牌，同名最多两张。");
        if (Game.Money < 2) return Result.Fail("出勤需要预付 2 资金。");
        if (Game.Day > Game.MaxDays) return Result.Fail("已超过经营期限。");
        if (RouteTo(siteId).Count == 0) return Result.Fail("没有从总部连通的道路，请先抢修。");
        if (siteId == "source" && (!Game.Site("hotel").Cleared || !Game.Site("hospital").Cleared)) return Result.Fail("完成旅馆和医院的委托后，才能定位源头入口。");
        if (Game.SelectedAsset != "" && !Game.Assets.Any(a => a.Id == Game.SelectedAsset && a.Charges > 0)) return Result.Fail("所选随行异常不可用，请重新配置。");
        var f = CreateField(siteId);
        for (int i = 0; i < Game.SelectedHeroes.Count; i++)
        {
            var actor = new ActorState { HeroId = Game.SelectedHeroes[i], X = 0, Y = 3 + i, Draw = Game.Hero(Game.SelectedHeroes[i]).Deck.ToList() };
            Shuffle(actor.Draw); Draw(actor); f.Actors.Add(actor);
        }
        f.AssetId = Game.SelectedAsset;
        f.Capacity = 3 + (Game.Assets.Any(a => a.Id == "wardrobe" && a.Id != f.AssetId) ? 1 : 0);
        f.Shield = Game.City.Count(t => t.Operating && RouteTo(t.SiteId).Count > 0);
        Game.Money -= 2; Game.Field = f; Game.Phase = "field";
        Log("出勤预付 2。先在 B3 调查线索；点击卡牌，再点击地图目标。结束回合会推进敌人。入口是 A4。");
        return Result.Ok($"进入{f.Name}。两名角色分别有 2 行动；基础牌随时可用。");
    }
    private static bool ValidDeck(HeroState h) => h.Deck.Count is >= 6 and <= 10 && h.Deck.All(id => Catalog.DeckOptions(h.Id).Any(c => c.Id == id) && TalentCatalog.IsCardUnlocked(h, id)) && h.Deck.GroupBy(id => id).All(gr => gr.Count() <= 2);
    private FieldState CreateField(string siteId)
    {
        var f = new FieldState { SiteId = siteId, Name = Game.Site(siteId).Name, RoundLimit = siteId == "source" ? 16 : 14 };
        for (int y = 0; y < 7; y++) for (int x = 0; x < 9; x++)
            f.Tiles.Add(new FieldTile { X = x, Y = y, Kind = x == 8 || y is 0 or 6 || (x == 0 && y is not (3 or 4)) ? "wall" : "floor" });
        Put(f, new(0, 3), "exit", "入口"); Put(f, new(0, 4), "floor", "接应区");
        Put(f, new(1, 2), "clue", "现场记录"); Put(f, new(2, 4), "bell", "服务铃");
        Put(f, new(2, 1), "crate", "物资柜"); Put(f, new(7, 4), "crate", "远端保险柜");
        if (siteId == "hotel")
        {
            foreach (int y in new[] { 1, 2, 4, 5 }) Put(f, new(3, y), "wall", "隔墙");
            Put(f, new(3, 3), "door", "走廊门");
            f.Evidence = "记录：回声侍者听见铃声会朝声音移动；在响铃后，两次普通攻击足以压低其状态。收容需取得记录、生命 ≤2，并处于标定、束缚或照明中。交涉专精可放宽生命条件。";
            f.RuleHint = "服务铃旁有拖拽痕迹。B3 的记录可能解释原因。";
            AddEnemy(f, "target", "回声侍者", "listener", new(5, 3), 6, "bell", true);
            AddEnemy(f, "shadow", "走廊游影", "shadow", new(7, 5), 3);
        }
        else if (siteId == "hospital")
        {
            foreach (var c in new[] { new Cell(3, 1), new Cell(3, 2), new Cell(5, 4), new Cell(5, 5) }) Put(f, c, "wall", "病房隔墙");
            Put(f, new(4, 3), "door", "隔离门");
            f.Evidence = "交班记录：镜柜不会回应铃声，站在远处就会投射镜片。灯区能让它暂停攻击；控制它后才能安全收容。条件：取得记录、生命 ≤2，并标定、束缚或照明。";
            f.RuleHint = "镜面柜反射着不存在的病房；旧交班记录在 B3。";
            AddEnemy(f, "target", "游走镜柜", "watcher", new(6, 3), 6, "wardrobe", true);
            AddEnemy(f, "shadow", "病历游影", "shadow", new(6, 1), 3);
        }
        else
        {
            foreach (var c in new[] { new Cell(3, 2), new Cell(3, 3), new Cell(5, 1), new Cell(5, 2) }) Put(f, c, "wall", "异常结晶");
            Put(f, new(3, 4), "door", "源头门");
            f.Evidence = "回收日志：食痛者会追逐最近的活人，攻击更重；它不回应声音。利用灯区、防护和束缚创造空隙。条件：取得记录、生命 ≤2，并标定、束缚或照明。";
            f.RuleHint = "源头吞食伤痛。B3 留着前一支队伍的记录。";
            AddEnemy(f, "target", "食痛者", "hunger", new(6, 3), 8, "pain", true);
            AddEnemy(f, "shadow", "伴生游影", "shadow", new(7, 5), 3);
            AddEnemy(f, "watcher", "窥视眼", "watcher", new(7, 1), 3);
        }
        return f;
    }
    private static void Put(FieldState f, Cell c, string kind, string label) { var t = f.Tile(c)!; t.Kind = kind; t.Label = label; }
    private static void AddEnemy(FieldState f, string id, string name, string kind, Cell c, int hp, string asset = "", bool objective = false)
        => f.Enemies.Add(new EnemyState { Id = id, Name = name, Kind = kind, X = c.X, Y = c.Y, Health = hp, MaxHealth = hp, CapturableAsset = asset, IsObjective = objective });

    public Result Preview(string heroId, string cardId, Cell target)
    {
        var check = Validate(heroId, cardId, target);
        if (!check.Success) return check;
        var a = Game.Field!.Actor(heroId); var card = Catalog.Card(cardId); int cost = Cost(a, card);
        if (cardId == "end") return Result.Ok((Game.Field.Round >= Game.Field.RoundLimit ? "本轮结束将触发封锁，丢失现场货物！请先整队撤离。\n" : "执行敌人当前意图，推进一轮；未使用行动不会保留。\n") + string.Join("\n", Game.Field.Enemies.Where(e => e.Alive).Select(EnemyIntent)));
        if (cardId == "retreat") return Result.Ok($"整队撤离。货物 {Game.Field.Cargo.Count}/{Game.Field.Capacity}；现场资金 {Game.Field.Loot}。{(HasObjectiveCargo() ? "可交付本次委托。" : "未携带委托成果，仍可提前撤退。")}");
        string effect = GetCardView(heroId, cardId).Description;
        if (cardId is "attack" or "shot" or "arc" or "flash" or "soothe" or "pact" or "piercing-shot" or "chain-arc")
            effect = string.Join("；", AttackTargets(a, cardId, target).Select(e => $"{e.Name}预计受到 {Damage(a, cardId, e)} 伤害，生命 {e.Health} → {Math.Max(0, e.Health - Damage(a, cardId, e))}")) + "。"
                + (cardId == "flash" ? "目标停顿一次敌方行动。" : cardId is "pact" or "soothe" ? "存活目标会被标定至下一轮。" : "")
                + (cardId == "pact" || cardId == "shot" && HasSkill(heroId, "c", 2) && TargetEnemy(target)!.MarkedUntil >= Game.Field.Round ? "此次攻击保留至少 1 生命。" : "");
        if (cardId is "move" or "sprint" or "dash" or "escort" or "trailblaze" or "mobile-light")
        {
            effect = $"沿通路移动 {FieldPath(a.Cell, target, heroId).Count - 1} 格；当前最大范围 {Range(a, card)} 格。" + (cardId == "escort" ? "额外获得 1 防护。" : "");
            if (cardId == "trailblaze") effect += NearbyClue(target) is { } clue ? $"自动调查 {clue.Cell} 的线索，不另耗行动。" : "落点相邻没有尚未调查的线索。";
            if (cardId == "mobile-light") effect += $"落点建立半径 1 的灯区，持续至第 {Game.Field.Round + LightDuration(heroId) - 1} 轮末。";
        }
        if (cardId is "capture" or "quick-capture") effect = $"取得 {Catalog.Asset(TargetEnemy(target)!.CapturableAsset).Name}，货物位置 {Game.Field.Cargo.Count + 1}/{Game.Field.Capacity}；消耗 1 封装工具（剩余 {Game.SealCharges - 1}）。本角色收容生命上限 {CaptureThreshold(heroId)}，范围 {Range(a, card)}。";
        if (cardId is "light" or "light-dome")
        {
            effect = $"在 {target} 建立半径 1 的灯区，持续至第 {Game.Field.Round + LightDuration(heroId) - 1} 轮末。当前布灯范围 {Range(a, card)} 格。";
            if (cardId == "light-dome") effect += $"其中 {Game.Field.Actors.Count(x => x.Cell.Distance(target) <= 1 && Visible(target, x.Cell))} 名队员各获得 3 防护。";
        }
        if (cardId == "bind") effect = $"让目标停顿 {BindDuration(heroId)} 次敌方行动。";
        if (cardId is "mark" or "chain-mark") effect = "标定" + (cardId == "chain-mark" ? string.Join("、", NearbyEnemies(target).Select(e => e.Name)) : TargetEnemy(target)!.Name) + "至下一轮，受到的伤害 +1。" + MarkExtras(heroId);
        if (cardId == "seal-circle") effect = $"{string.Join("、", NearbyEnemies(target).Select(e => e.Name))}将停顿 2 次敌方行动。";
        if (cardId == "repair" || cardId == "heal")
        {
            var patient = Game.Hero(Game.Field.Actors.First(x => x.Cell == target).HeroId);
            effect = $"{patient.Name}实际恢复 {Math.Min(cardId == "heal" ? 4 : 3, patient.MaxHealth - patient.Health)} 生命。" + (cardId == "repair" && HasSkill(heroId, "c", 2) ? "额外获得 2 防护。" : "");
        }
        if (cardId == "probe" && HasSkill(heroId, "c", 2) && Game.Field.Tile(target)?.Kind == "crate") effect += Game.Field.Cargo.Count < Game.Field.Capacity ? "本次将自动拾取物资，零钱 +3，占用 1 货物位置。" : "货物已满，本次只打开箱柜，物资留在原处。";
        if (cardId == "team-aid") effect = string.Join("；", Game.Field.Actors.Select(x => { var h = Game.Hero(x.HeroId); return $"{h.Name}恢复 {Math.Min(4, h.MaxHealth - h.Health)} 生命，获得 1 防护"; })) + "。";
        if (card.Owner == "asset") effect += $" 消耗异常 1 次，剩余 {Game.Assets.Single(x => x.Id == Game.Field.AssetId).Charges - 1} 次。";
        return Result.Ok($"{Game.Hero(heroId).Name} · {card.Name} → {target}\n消耗 {cost} 行动；余 {a.Actions - cost}。{effect}");
    }
    private Result Validate(string heroId, string cardId, Cell target)
    {
        var f = Game.Field;
        if (Game.Phase != "field" || f is null) return Result.Fail("当前没有出勤现场。");
        var a = f.Actors.FirstOrDefault(a => a.HeroId == heroId); var card = Catalog.Cards.FirstOrDefault(c => c.Id == cardId);
        if (a is null || card is null) return Result.Fail("无效角色或卡牌。");
        if (card.Owner != "basic" && card.Owner != "asset" && (card.Owner != heroId || !a.Hand.Contains(cardId))) return Result.Fail("这张职业牌不在该角色手中。");
        if (card.Owner is not ("basic" or "asset") && !TalentCatalog.IsCardUnlocked(Game.Hero(heroId), cardId)) return Result.Fail("这张特色牌尚未通过天赋解锁。");
        if (card.Owner == "asset")
        {
            if (!Catalog.Assets.Any(x => x.Id == f.AssetId && x.FieldCard == cardId)) return Result.Fail("本次没有携带对应异常。");
            if (!Game.Assets.Any(x => x.Id == f.AssetId && x.Charges > 0)) return Result.Fail("异常的使用次数已耗尽。");
        }
        if (a.Actions < Cost(a, card)) return Result.Fail($"需要 {Cost(a, card)} 行动，当前只有 {a.Actions}。");
        if (cardId is "end" or "wait" or "guard" or "asset-pain" or "team-aid") return Result.Ok("");
        if (cardId == "retreat") return f.Actors.All(x => x.Cell.Distance(f.Anchor) <= 1) ? Result.Ok("") : Result.Fail("两名角色都需要回到入口 A4 的 1 格范围内。");
        if (cardId == "drop")
            return f.Cargo.Count > 0 && f.Tile(a.Cell)!.ItemId == "" ? Result.Ok("") : Result.Fail("没有可放下的货物，或脚下已有掉落物。");
        if (cardId == "asset-wardrobe")
            return Landing(a) is not null ? Result.Ok("") : Result.Fail("入口附近没有空格。");
        var tile = f.Tile(target);
        if (tile is null) return Result.Fail("目标不在地图内。");
        int range = Range(a, card);
        if (cardId is "move" or "sprint" or "dash" or "escort" or "trailblaze" or "mobile-light")
        {
            if (a.Cell == target) return Result.Fail("请选择另一格。");
            if (!Walkable(target, a.HeroId)) return Result.Fail("目标被墙、门、敌人或队友阻挡。");
            var path = FieldPath(a.Cell, target, a.HeroId);
            return path.Count > 0 && path.Count - 1 <= range ? Result.Ok("") : Result.Fail($"只能沿通路移动最多 {range} 格。");
        }
        if (a.Cell.Distance(target) > range) return Result.Fail($"目标超出 {range} 格范围。");
        if (!Visible(a.Cell, target)) return Result.Fail("视线被墙或关闭的门挡住。");
        if (card.Target == "enemy")
        {
            var e = TargetEnemy(target); if (e is null) return Result.Fail("该格没有活动敌人。");
            if (cardId == "piercing-shot" && a.X != target.X && a.Y != target.Y) return Result.Fail("贯穿投射需要与目标处于同一横行或纵列。");
            if (cardId is "capture" or "quick-capture")
            {
                if (e.CapturableAsset == "") return Result.Fail("这个衍生敌影不能收容。");
                if (Game.Assets.Any(x => x.Id == e.CapturableAsset) || f.Cargo.Contains(e.CapturableAsset)) return Result.Fail("此异常已经收容，不能复制唯一资产。");
                if (!f.EvidenceKnown) return Result.Fail("先调查 B3 的记录，了解收容条件。");
                int threshold = CaptureThreshold(heroId);
                if (e.Health > threshold) return Result.Fail($"先把目标生命降至 {threshold} 或以下。");
                if (!Controlled(e)) return Result.Fail("目标必须处于标定、束缚或照明中。");
                if (Game.SealCharges <= 0) return Result.Fail("没有封装工具次数。");
                if (f.Cargo.Count >= f.Capacity) return Result.Fail("货物位置已满，可先放下不需要的货物。");
            }
            return Result.Ok("");
        }
        if (card.Target == "ally") return f.Actors.Any(x => x.Cell == target) ? Result.Ok("") : Result.Fail("请选择一名角色所在的格子。");
        if (cardId is "search" or "survey" or "probe")
            return !tile.Used && tile.Kind is "clue" or "crate" or "door" ? Result.Ok("") : Result.Fail("这里没有可调查的记录、未开启箱柜或房门。");
        if (cardId == "pickup")
        {
            if (tile.ItemId == "") return Result.Fail("这里没有已发现的实物；箱柜请先搜查。");
            return f.Cargo.Count < f.Capacity ? Result.Ok("") : Result.Fail("货物位置已满。");
        }
        if (cardId == "ring") return tile.Kind == "bell" ? Result.Ok("") : Result.Fail("请选择现场服务铃。");
        if (card.Target == "cell" && tile.Kind is "wall" or "door") return Result.Fail("请选择可用的地面格。");
        if (cardId == "seal-circle" && NearbyEnemies(target).Count == 0) return Result.Fail("封锁阵范围内没有可作用的敌人。");
        return Result.Ok("");
    }
    public Result Play(string heroId, string cardId, Cell target)
    {
        var valid = Validate(heroId, cardId, target); if (!valid.Success) return valid;
        if (cardId == "end") return EndRound();
        var f = Game.Field!; var a = f.Actor(heroId); var card = Catalog.Card(cardId); int cost = Cost(a, card);
        a.Actions -= cost;
        if (card.Owner != "basic" && card.Owner != "asset") { a.Hand.Remove(cardId); a.Discard.Add(cardId); }
        if (card.Owner == "asset") Game.Assets.Single(x => x.Id == f.AssetId).Charges--;
        string message;
        var enemy = TargetEnemy(target); var tile = f.Tile(target);
        switch (cardId)
        {
            case "move": case "sprint": case "dash": case "escort": case "trailblaze": case "mobile-light":
                a.X = target.X; a.Y = target.Y; if (cardId == "escort") a.Block++;
                message = $"{Game.Hero(heroId).Name}移动到 {target}。";
                if (cardId == "mobile-light") { PlaceLight(heroId, target); message += $"落点亮起灯区，持续至第 {f.LightUntil} 轮末。"; }
                if (cardId == "trailblaze")
                {
                    var clue = NearbyClue(target);
                    if (clue is not null) { clue.Used = true; f.EvidenceKnown = true; message += "探路获得线索：" + f.Evidence; }
                    else message += "落点附近没有尚未调查的线索。";
                }
                break;
            case "search": case "survey": case "probe":
                if (tile!.Kind == "clue") { f.EvidenceKnown = true; tile.Used = true; message = f.Evidence; }
                else if (tile.Kind == "door") { tile.Kind = "floor"; tile.Used = true; tile.Label = "已开门"; message = $"开启 {target} 房门。"; }
                else { tile.Kind = "loot"; tile.Used = true; tile.ItemId = "loot:" + target; tile.Label = "可拾取物资"; message = $"{target} 箱柜已打开，里面有价值 3 的物资；需要拾取才能带走。"; }
                if (cardId == "probe")
                {
                    a.Block += 2;
                    if (HasSkill(heroId, "c", 2) && tile!.ItemId.StartsWith("loot:") && f.Cargo.Count < f.Capacity)
                    { f.Cargo.Add(tile.ItemId); f.Loot += 3; tile.ItemId = ""; tile.Label = "空柜"; message = $"取证回收：打开 {target} 箱柜并拾取物资，零钱 +3；获得 2 防护。"; }
                }
                break;
            case "pickup":
                f.Cargo.Add(tile!.ItemId); if (tile.ItemId.StartsWith("loot:")) f.Loot += 3;
                message = $"拾取{CargoName(tile.ItemId)}，货物 {f.Cargo.Count}/{f.Capacity}。"; tile.ItemId = ""; tile.Label = tile.Kind == "loot" ? "空柜" : tile.Label; break;
            case "drop":
                var dropped = f.Cargo[^1]; f.Cargo.RemoveAt(f.Cargo.Count - 1); f.Tile(a.Cell)!.ItemId = dropped;
                if (dropped.StartsWith("loot:")) f.Loot -= 3;
                message = $"在 {a.Cell} 放下{CargoName(dropped)}。"; break;
            case "attack": case "shot": case "arc": case "flash": case "soothe": case "pact": case "piercing-shot": case "chain-arc":
                var reports = new List<string>();
                foreach (var hit in AttackTargets(a, cardId, target))
                {
                    int damage = Damage(a, cardId, hit); hit.Health = Math.Max(0, hit.Health - damage); hit.Alert = true;
                    if (cardId == "flash") hit.StunnedUntil = Math.Max(hit.StunnedUntil, f.Round);
                    if (cardId is "soothe" or "pact") hit.MarkedUntil = Math.Max(hit.MarkedUntil, f.Round + 1);
                    reports.Add($"{hit.Name}受到 {damage} 伤害，剩余 {hit.Health}");
                    if (hit.Health == 0) { Defeat(hit); reports.Add(hit.IsObjective ? "委托凭证留在该格，请拾取后撤离；失去此次收容机会" : "敌影消散"); }
                }
                a.DamageBonus = 0; message = card.Name + "：" + string.Join("；", reports) + "。";
                break;
            case "guard": a.Block += 2; message = $"{Game.Hero(heroId).Name}获得 2 防护。"; break;
            case "mark": case "chain-mark":
                var marked = cardId == "chain-mark" ? NearbyEnemies(target) : new List<EnemyState> { enemy! };
                foreach (var mark in marked)
                {
                    mark.MarkedUntil = f.Round + 1; mark.Alert = true;
                    if (HasSkill(heroId, "b")) mark.StunnedUntil = Math.Max(mark.StunnedUntil, f.Round);
                }
                if (HasSkill(heroId, "b", 2)) foreach (var ally in f.Actors) ally.DamageBonus = 1;
                message = $"已标定{string.Join("、", marked.Select(e => e.Name))}，伤害与收容都能利用标记。"; break;
            case "bind":
                enemy!.StunnedUntil = Math.Max(enemy.StunnedUntil, f.Round + BindDuration(heroId) - 1); enemy.Alert = true;
                message = $"{enemy.Name}被束缚，将停顿 {BindDuration(heroId)} 次。"; break;
            case "seal-circle":
                var bound = NearbyEnemies(target);
                foreach (var restrained in bound) { restrained.StunnedUntil = Math.Max(restrained.StunnedUntil, f.Round + 1); restrained.Alert = true; }
                message = $"封锁阵使 {bound.Count} 个敌人停顿 2 次：{string.Join("、", bound.Select(e => e.Name))}。"; break;
            case "capture": case "quick-capture":
                Game.SealCharges--; f.Cargo.Add(enemy!.CapturableAsset); enemy.Alive = false; enemy.Health = 0;
                if (enemy.IsObjective) f.ObjectiveDone = true;
                message = $"收容{enemy.Name} → {Catalog.Asset(enemy.CapturableAsset).Name}。消耗 1 工具；撤离后才能入库使用。"; break;
            case "light": case "light-dome":
                PlaceLight(heroId, target);
                message = $"在 {target} 布置灯区，持续至第 {f.LightUntil} 轮末。畏光敌人会停顿，灯区内目标可以收容。";
                if (cardId == "light-dome")
                {
                    var protectedActors = f.Actors.Where(x => x.Cell.Distance(target) <= 1 && Visible(target, x.Cell)).ToArray();
                    foreach (var ally in protectedActors) ally.Block += 3;
                    message += $"{protectedActors.Length} 名灯区队员各获得 3 防护。";
                }
                break;
            case "ring": case "lure": case "asset-bell":
                int moved = LureEnemies(target); message = $"{target} 发出声响；{moved} 个听觉型敌人回应并移向声源。";
                if (f.SiteId == "hotel" && moved > 0) { f.EvidenceKnown = true; message += " 实际观察已记录：声音会牵引回声侍者与游影。"; }
                break;
            case "barrier": f.Actors.First(x => x.Cell == target).Block += 4; message = "目标获得 4 防护。"; break;
            case "repair": case "heal":
                var patientActor = f.Actors.First(x => x.Cell == target); var patient = Game.Hero(patientActor.HeroId); int healed = Math.Min(cardId == "heal" ? 4 : 3, patient.MaxHealth - patient.Health); patient.Health += healed;
                message = $"{patient.Name}恢复 {healed} 生命。";
                if (cardId == "repair" && HasSkill(heroId, "c", 2)) { patientActor.Block += 2; message += "维护屏障提供 2 防护。"; }
                break;
            case "team-aid":
                var care = new List<string>();
                foreach (var ally in f.Actors)
                {
                    var h = Game.Hero(ally.HeroId); int recovery = Math.Min(4, h.MaxHealth - h.Health); h.Health += recovery; ally.Block++;
                    care.Add($"{h.Name}恢复 {recovery} 生命并获得 1 防护");
                }
                message = string.Join("；", care) + "。"; break;
            case "swap":
                var other = f.Actors.First(x => x.Cell == target); var old = a.Cell; a.X = other.X; a.Y = other.Y; other.X = old.X; other.Y = old.Y;
                message = "两名角色交换位置，行动额度保持。"; break;
            case "asset-wardrobe":
                var landing = Landing(a)!.Value; a.X = landing.X; a.Y = landing.Y; message = $"折叠门把{Game.Hero(heroId).Name}送回 {landing}。"; break;
            case "asset-pain": f.Shield += 4; message = "食痛者将替队伍承受接下来的 4 点伤害。"; break;
            case "wait": a.Actions = 0; message = $"{Game.Hero(heroId).Name}待命。"; break;
            case "retreat": return ReturnToCity(false);
            default: throw new InvalidOperationException("Validated card has no implementation: " + cardId);
        }
        WakeEnemies(); Log(message); return Result.Ok(message);
    }
    private int Cost(ActorState a, CardDef c) => c.Id == "capture" && a.HeroId == "warden" && HasSkill(a.HeroId, "b", 2) ? 1 : c.Cost;
    private int Range(ActorState a, CardDef c)
    {
        int n = c.Range;
        if (c.Id == "move" && a.HeroId == "mapper" && HasSkill(a.HeroId, "a")) n++;
        if (c.Id == "sprint" && HasSkill(a.HeroId, "a", 2)) n++;
        if (c.Id == "dash" && HasSkill(a.HeroId, "a", 2)) n++;
        if (c.Id == "light" && HasSkill(a.HeroId, "a")) n++;
        if (c.Id is "capture" or "quick-capture" && a.HeroId == "warden" && HasSkill(a.HeroId, "a", 2)) n++;
        return n;
    }
    private bool HasSkill(string heroId, string branch, int level = 1) => Game.Hero(heroId).LearnedTalents.Contains($"{heroId}-{branch}{level}");
    private int CaptureThreshold(string heroId) => heroId == "warden" && HasSkill(heroId, "a") ? 4 : 2;
    private int LightDuration(string heroId) => 2 + (HasSkill(heroId, "a") ? 1 : 0) + (HasSkill(heroId, "b", 2) ? 1 : 0);
    private int BindDuration(string heroId) => HasSkill(heroId, "b") ? 2 : 1;
    private string MarkExtras(string heroId) => (HasSkill(heroId, "b") ? "另使目标停顿一次敌方行动。" : "") + (HasSkill(heroId, "b", 2) ? "两名队员各自下一次攻击伤害 +1。" : "");
    private int BaseDamage(string heroId, string cardId) => (cardId switch { "arc" or "pact" or "piercing-shot" => 3, "flash" => 1, _ => 2 })
        + ((cardId is "shot" or "arc" or "soothe") && HasSkill(heroId, "c") ? 1 : 0);
    private int Damage(ActorState a, string cardId, EnemyState e)
    {
        int damage = BaseDamage(a.HeroId, cardId) + (e.MarkedUntil >= Game.Field!.Round ? 1 : 0) + a.DamageBonus;
        if (cardId == "pact" || (cardId == "shot" && HasSkill(a.HeroId, "c", 2) && e.MarkedUntil >= Game.Field.Round)) damage = Math.Min(damage, Math.Max(0, e.Health - 1));
        return damage;
    }
    private List<EnemyState> NearbyEnemies(Cell center) => Game.Field!.Enemies.Where(e => e.Alive && e.Cell.Distance(center) <= 1 && Visible(center, e.Cell)).ToList();
    private List<EnemyState> AttackTargets(ActorState actor, string cardId, Cell target)
    {
        var targets = cardId == "chain-arc" ? NearbyEnemies(target) : new List<EnemyState> { TargetEnemy(target)! };
        if (cardId == "piercing-shot")
        {
            var behind = new Cell(target.X + Math.Sign(target.X - actor.X), target.Y + Math.Sign(target.Y - actor.Y));
            var second = TargetEnemy(behind); if (second is not null && Visible(target, behind)) targets.Add(second);
        }
        return targets;
    }
    private FieldTile? NearbyClue(Cell center) => Game.Field!.Tiles.FirstOrDefault(t => t.Kind == "clue" && !t.Used && t.Cell.Distance(center) <= 1 && Visible(center, t.Cell));
    private void PlaceLight(string heroId, Cell target) { Game.Field!.LightCell = target; Game.Field.LightUntil = Game.Field.Round + LightDuration(heroId) - 1; }
    private bool Lit(Cell c) => Game.Field!.LightUntil >= Game.Field.Round && Game.Field.LightCell.Distance(c) <= 1;
    private bool Controlled(EnemyState e) => e.MarkedUntil >= Game.Field!.Round || e.StunnedUntil >= Game.Field.Round || Lit(e.Cell);
    private EnemyState? TargetEnemy(Cell c) => Game.Field?.Enemies.FirstOrDefault(e => e.Alive && e.Cell == c);
    private Cell? Landing(ActorState a) => new[] { Game.Field!.Anchor, new Cell(0, 4), new Cell(1, 3) }.Cast<Cell?>().FirstOrDefault(c => c.HasValue && Walkable(c.Value, a.HeroId));
    private static string CargoName(string id) => id.StartsWith("loot:") ? "物资箱" : id == "contract" ? "委托凭证" : Catalog.Assets.FirstOrDefault(a => a.Id == id)?.Name ?? id;
    private void Defeat(EnemyState e)
    {
        e.Alive = false;
        if (e.IsObjective)
        {
            Game.Field!.ObjectiveDone = true; var t = Game.Field.Tile(e.Cell)!;
            if (t.ItemId == "") t.ItemId = "contract";
            else
            {
                var free = Game.Field.Tiles.Where(x => x.Kind is not ("wall" or "door") && x.ItemId == "").OrderBy(x => x.Cell.Distance(e.Cell)).First();
                free.ItemId = "contract";
            }
        }
    }
    private int LureEnemies(Cell target)
    {
        int count = 0;
        foreach (var e in Game.Field!.Enemies.Where(e => e.Alive && e.Kind is "listener" or "shadow"))
        {
            e.Alert = true; e.Lure = target; count++;
            for (int i = 0; i < 2; i++) if (!MoveEnemyToward(e, target)) break;
        }
        return count;
    }
    private void WakeEnemies()
    {
        var f = Game.Field!;
        foreach (var e in f.Enemies.Where(e => e.Alive && !e.Alert))
            if (f.Actors.Any(a => e.Cell.Distance(a.Cell) <= 4 && Visible(e.Cell, a.Cell))) e.Alert = true;
    }
    private sealed record EnemyPlan(string EnemyId, string Action, Cell Destination, string HeroId = "", bool ClearLure = false);
    private List<EnemyPlan> EnemyPlans()
    {
        var f = Game.Field!;
        var positions = f.Enemies.Where(e => e.Alive).ToDictionary(e => e.Id, e => e.Cell);
        var plans = new List<EnemyPlan>();
        foreach (var e in f.Enemies.Where(e => e.Alive))
        {
            bool clear = e.Lure.X >= 0 && e.Cell == e.Lure;
            if (!e.Alert) { plans.Add(new(e.Id, "sleep", e.Cell)); continue; }
            if (e.StunnedUntil >= f.Round) { plans.Add(new(e.Id, "stun", e.Cell)); continue; }
            if (e.Kind == "watcher" && Lit(e.Cell)) { plans.Add(new(e.Id, "light", e.Cell)); continue; }
            bool lured = e.Lure.X >= 0 && !clear;
            var victim = NearestActor(e); int range = e.Kind == "watcher" ? 3 : 1;
            var soundOccupant = lured ? f.Actors.FirstOrDefault(a => a.Cell == e.Lure && a.Cell.Distance(e.Cell) <= 1) : null;
            if (soundOccupant is not null)
            { plans.Add(new(e.Id, "attack", e.Cell, soundOccupant.HeroId, true)); continue; }
            if (!lured && e.Cell.Distance(victim.Cell) <= range && Visible(e.Cell, victim.Cell))
            { plans.Add(new(e.Id, "attack", e.Cell, victim.HeroId, clear)); continue; }
            if (!lured && e.Kind == "watcher") { plans.Add(new(e.Id, "watch", e.Cell, ClearLure: clear)); continue; }
            var destination = lured ? e.Lure : victim.Cell;
            bool Available(Cell c) => f.Tile(c) is { Kind: not ("wall" or "door") } && !f.Actors.Any(a => a.Cell == c) && !positions.Any(p => p.Key != e.Id && p.Value == c);
            var path = FindPath(e.Cell, destination, c => c == destination || Available(c));
            if (path.Count > 1 && Available(path[1]))
            {
                positions[e.Id] = path[1]; plans.Add(new(e.Id, lured ? "lure" : "move", path[1], ClearLure: clear));
            }
            else plans.Add(new(e.Id, "blocked", e.Cell, ClearLure: clear || lured));
        }
        return plans;
    }
    public string EnemyIntent(EnemyState enemy)
    {
        if (Game.Field is null || !enemy.Alive) return "已处置";
        var p = EnemyPlans().FirstOrDefault(p => p.EnemyId == enemy.Id);
        if (p is null) return "已处置";
        return enemy.Name + "：" + (p.Action switch
        {
            "sleep" => "警戒中（视线内 4 格唤醒）",
            "stun" => "受控，停顿",
            "light" => "受到照明，停顿",
            "attack" => $"攻击{Game.Hero(p.HeroId).Name}，{(enemy.Kind == "hunger" ? 3 : 2)} 伤害（防护前）",
            "watch" => "守望（射程 3，墙阻挡）",
            "move" => $"向 {p.Destination} 移动 1 格",
            "lure" => $"被声音牵引，移向 {p.Destination}",
            _ => "道路被阻挡"
        });
    }
    public Result EndRound()
    {
        if (Game.Phase != "field" || Game.Field is null) return Result.Fail("当前没有出勤现场。");
        var f = Game.Field; WakeEnemies();
        foreach (var plan in EnemyPlans())
        {
            var e = f.Enemies.Single(e => e.Id == plan.EnemyId);
            if (plan.ClearLure) e.Lure = new(-1, -1);
            if (plan.Action is "move" or "lure")
            {
                e.X = plan.Destination.X; e.Y = plan.Destination.Y; continue;
            }
            if (plan.Action == "attack")
            {
                var target = f.Actor(plan.HeroId);
                int damage = e.Kind == "hunger" ? 3 : 2;
                if (Lit(target.Cell) && f.Actors.Any(a => a.HeroId == "engineer" && HasSkill(a.HeroId, "b"))) damage = Math.Max(0, damage - 1);
                int shield = Math.Min(f.Shield, damage); f.Shield -= shield; damage -= shield;
                int block = Math.Min(target.Block, damage); target.Block -= block; damage -= block;
                var hero = Game.Hero(target.HeroId); hero.Health = Math.Max(0, hero.Health - damage);
                Log($"{e.Name}攻击{hero.Name}：防护吸收 {shield + block}，实际损失 {damage} 生命。");
                if (hero.Health == 0) return ReturnToCity(true, $"{hero.Name}失去行动能力，队伍紧急撤出。");
            }
        }
        if (f.Round >= f.RoundLimit) return ReturnToCity(true, "现场封锁，队伍未能带回货物。");
        f.Round++;
        foreach (var a in f.Actors)
        {
            a.Actions = 2; a.Block = 0; a.Discard.AddRange(a.Hand); a.Hand.Clear(); Draw(a);
        }
        Log($"第 {f.Round}/{f.RoundLimit} 轮：每人 2 行动，抽取 3 张专属牌。");
        return Result.Ok($"进入第 {f.Round} 轮。");
    }
    private ActorState NearestActor(EnemyState e) => Game.Field!.Actors.OrderBy(a => a.Cell.Distance(e.Cell)).ThenBy(a => a.HeroId, StringComparer.Ordinal).First();
    private Cell? NextEnemyStep(EnemyState e, Cell target)
    {
        var path = FindPath(e.Cell, target, c => c == target || EnemyWalkable(c, e.Id));
        if (path.Count < 2 || !EnemyWalkable(path[1], e.Id)) return null;
        return path[1];
    }
    private bool MoveEnemyToward(EnemyState e, Cell target)
    {
        var next = NextEnemyStep(e, target); if (next is null) return false;
        e.X = next.Value.X; e.Y = next.Value.Y; return true;
    }
    private bool EnemyWalkable(Cell c, string enemyId) => Game.Field!.Tile(c) is { Kind: not ("wall" or "door") }
        && !Game.Field.Actors.Any(a => a.Cell == c) && !Game.Field.Enemies.Any(e => e.Alive && e.Id != enemyId && e.Cell == c);
    private bool Walkable(Cell c, string heroId) => Game.Field!.Tile(c) is { Kind: not ("wall" or "door") }
        && !Game.Field.Actors.Any(a => a.HeroId != heroId && a.Cell == c) && !Game.Field.Enemies.Any(e => e.Alive && e.Cell == c);
    private List<Cell> FieldPath(Cell from, Cell to, string heroId) => FindPath(from, to, c => Walkable(c, heroId));
    private static IEnumerable<Cell> Neighbors(Cell c)
    {
        yield return new(c.X + 1, c.Y); yield return new(c.X, c.Y + 1); yield return new(c.X - 1, c.Y); yield return new(c.X, c.Y - 1);
    }
    private static List<Cell> FindPath(Cell from, Cell to, Func<Cell, bool> legal)
    {
        if (!legal(to)) return new();
        var q = new Queue<Cell>(); var prev = new Dictionary<Cell, Cell>(); q.Enqueue(from); prev[from] = from;
        while (q.Count > 0)
        {
            var c = q.Dequeue(); if (c == to) break;
            foreach (var n in Neighbors(c)) if (!prev.ContainsKey(n) && legal(n)) { prev[n] = c; q.Enqueue(n); }
        }
        if (!prev.ContainsKey(to)) return new();
        var path = new List<Cell> { to }; while (path[^1] != from) path.Add(prev[path[^1]]); path.Reverse(); return path;
    }
    private bool Visible(Cell from, Cell to)
    {
        // Bresenham line, checking every intermediate cell. The destination may be a closed door to interact with.
        int x = from.X, y = from.Y, dx = Math.Abs(to.X - x), dy = Math.Abs(to.Y - y);
        int sx = x < to.X ? 1 : -1, sy = y < to.Y ? 1 : -1, err = dx - dy;
        while (x != to.X || y != to.Y)
        {
            int doubled = 2 * err;
            if (doubled > -dy) { err -= dy; x += sx; }
            if (doubled < dx) { err += dx; y += sy; }
            if (x == to.X && y == to.Y) break;
            var tile = Game.Field!.Tile(new(x, y));
            if (tile is null || tile.Kind is "wall" or "door") return false;
        }
        return true;
    }
    private bool HasObjectiveCargo()
    {
        var f = Game.Field!; var target = f.Enemies.First(e => e.IsObjective);
        return f.ObjectiveDone && (f.Cargo.Contains("contract") || f.Cargo.Contains(target.CapturableAsset));
    }
    private Result ReturnToCity(bool failed, string reason = "")
    {
        var f = Game.Field!; var site = Game.Site(f.SiteId); bool delivered = !failed && HasObjectiveCargo();
        int income = 0; var messages = new List<string>();
        if (reason != "") messages.Add(reason);
        if (delivered && !site.Cleared)
        {
            int reward = f.SiteId == "hotel" ? 8 : f.SiteId == "hospital" ? 10 : 14;
            income += reward; site.Cleared = true; messages.Add($"{site.Name}委托交付 +{reward}");
            foreach (var h in Game.Heroes) h.SkillPoints++;
            messages.Add("全员获得 1 技能点");
        }
        else if (!failed && !delivered) messages.Add("提前撤离，未交付委托");
        if (!failed)
        {
            income += f.Loot; if (f.Loot > 0) messages.Add($"物资出售 +{f.Loot}");
            foreach (string id in f.Cargo.Where(id => Catalog.Assets.Any(a => a.Id == id)))
                if (!Game.Assets.Any(a => a.Id == id)) { Game.Assets.Add(new AssetState { Id = id }); messages.Add($"{Catalog.Asset(id).Name}已收容入库"); }
        }
        else { foreach (var h in f.Actors.Select(a => Game.Hero(a.HeroId))) h.Health = Math.Max(3, h.Health); messages.Add("现场货物与零钱遗失，已用工具和次数不返还"); }
        int operating = Game.City.Count(t => t.Operating && RouteTo(t.SiteId).Count > 0); income += operating;
        if (operating > 0) messages.Add($"网点收入 +{operating}");
        foreach (var staff in Game.Heroes.Where(h => !f.Actors.Any(a => a.HeroId == h.Id)))
        {
            if (staff.Id == "mapper")
            {
                int bonus = 2 + (HasSkill(staff.Id, "a") ? 1 : 0) + (HasSkill(staff.Id, "a", 2) ? 1 : 0) + (HasSkill(staff.Id, "b") ? 2 : 0);
                income += bonus; messages.Add($"林舟经营协作 +{bonus}");
            }
            if (staff.Id == "engineer")
            {
                var blocked = Game.City.FirstOrDefault(t => t.Blocked);
                if (blocked is not null) { blocked.Blocked = false; messages.Add($"许灯修复 {blocked.Cell} 道路"); }
                if (HasSkill(staff.Id, "b")) foreach (var asset in Game.Assets.Where(a => a.Id != f.AssetId)) asset.Charges = Math.Min(asset.MaxCharges, asset.Charges + 1);
            }
            if (staff.Id == "warden")
            {
                int healing = HasSkill(staff.Id, "a") ? 4 : 3;
                foreach (var h in Game.Heroes) h.Health = Math.Min(h.MaxHealth, h.Health + healing);
                Game.SealCharges = Math.Min(4, Game.SealCharges + (HasSkill(staff.Id, "b") ? 2 : 1));
                messages.Add($"陈墨护理全员 +{healing}，补充收容工具");
            }
        }
        // Only assets already at the company on departure work during this sortie; newly captured ones begin next sortie.
        foreach (var asset in Game.Assets.Where(a => a.Id != f.AssetId && !f.Cargo.Contains(a.Id)))
        {
            if (asset.Id == "bell") { income += 2; messages.Add("回声铃留库接应 +2"); }
            if (asset.Id == "pain") { foreach (var h in Game.Heroes) h.Health = Math.Min(h.MaxHealth, h.Health + 2); messages.Add("食痛者护理全员 +2"); }
        }
        foreach (var h in Game.Heroes) h.Health = Math.Min(h.MaxHealth, h.Health + 1);
        bool insolvent = Game.Money + income < 2;
        int actualExpense = Math.Min(2, Game.Money + income);
        Game.Money = Math.Max(0, Game.Money + income - 2);
        messages.Add(insolvent ? $"基本经营费需要 2，实际只能支付 {actualExpense}，公司资不抵支。" : "基本经营支出 −2；全员休整 +1 生命");
        Game.Day++; Game.Field = null; Game.Phase = "city"; Game.SelectedAsset = "";
        if (!Game.Site("source").Cleared && Game.Day is 2 or 4)
        {
            var closed = Game.City.Single(t => t.Cell == new Cell(Game.Day == 2 ? 3 : 5, 2));
            closed.Blocked = true; messages.Add($"异常扩散封闭 {closed.Cell} 道路；可绕路或花 2 资金抢修");
        }
        Game.LastReport = string.Join("\n", messages); Record(Game.LastReport);
        if (insolvent) { Game.Phase = "defeat"; Game.Outcome = "公司资金不足，经营中止。"; }
        else CheckVictory();
        if (Game.Phase != "victory" && Game.Phase != "defeat" && Game.Day > Game.MaxDays)
        { Game.Phase = "defeat"; Game.Outcome = "七日期限已到，未完成城市交接；网点需要在最后一次出勤前建好。"; }
        return Result.Ok(Game.LastReport);
    }
    private void CheckVictory()
    {
        if (Game.Site("source").Cleared && Game.City.Count(t => t.Operating && RouteTo(t.SiteId).Count > 0) >= 2)
        {
            Game.Phase = "victory";
            Game.Outcome = $"雾港交接成功！第 {Math.Max(1, Game.Day - 1)} 日处置源头，两个网点营业，收容 {Game.Assets.Count} 件异常，资金 {Game.Money}。";
        }
    }
    private void Draw(ActorState a)
    {
        while (a.Hand.Count < 3)
        {
            if (a.Draw.Count == 0)
            {
                if (a.Discard.Count == 0) break;
                a.Draw.AddRange(a.Discard); a.Discard.Clear(); Shuffle(a.Draw);
            }
            a.Hand.Add(a.Draw[0]); a.Draw.RemoveAt(0);
        }
    }
    private void Shuffle(List<string> deck)
    {
        for (int i = deck.Count - 1; i > 0; i--)
        {
            Game.RandomState = unchecked(Game.RandomState * 1664525u + 1013904223u);
            int j = (int)(Game.RandomState % (uint)(i + 1)); (deck[i], deck[j]) = (deck[j], deck[i]);
        }
    }
    private void Log(string text)
    {
        Game.Field!.Log.Add(text); if (Game.Field.Log.Count > 80) Game.Field.Log.RemoveAt(0);
    }
    private void Record(string text) { Game.Journal.Add(text); if (Game.Journal.Count > 40) Game.Journal.RemoveAt(0); }
}
