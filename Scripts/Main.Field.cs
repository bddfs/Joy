using Godot;
using Joy.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Joy;

public partial class Main
{
    private int _fieldFactsSortie = -1;
    private FieldState? _fieldFactsState;
    private List<string> _fieldFacts = new();
    private bool _fieldCardExpanded;
    private bool _fieldDossierOpen;

    private void RenderField()
    {
        var g = _game!; var f = g.Field!; var site = g.Node(f.NodeId); var engine = new FieldEngine(g);
        if (!site.Rooms.Any(r => r.Id == _viewedRoom && r.Placed)) _viewedRoom = f.RoomId;
        if (!ReferenceEquals(_fieldFactsState, f) || _fieldFactsSortie != f.SortieId)
        { _fieldFactsState = f; _fieldFactsSortie = f.SortieId; _fieldFacts.Clear(); }
        var room = site.Rooms.First(r => r.Id == f.RoomId);
        var target = site.Rooms.First(r => r.Id == _viewedRoom);
        var layout = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        layout.AddThemeConstantOverride("separation", 12); _root.AddChild(layout);
        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 12); layout.AddChild(row);

        var left = Pane("01 / 现场状态", 250 + 55 * (UiScale - 1)); row.AddChild(left.Panel);
        left.Body.AddChild(Text($"现场轮次  {f.Round:00} / {f.RoundLimit:00}", 18, Mint));
        left.Body.AddChild(Text($"行动  {f.Actions} / 3", 25, Ink));
        left.Body.AddChild(Text($"心志 {f.Will}/{f.MaxWill}\n护符{f.Wards}/3  异动{f.Disturbance}/8\n墨水{f.Ink} · 零钱{f.Coins}", 18));
        left.Body.AddChild(Text($"真实照明：{(f.Light ? "已开启 · 本轮有效" : "未开启")}", 17, f.Light ? Amber : Ink));
        left.Body.AddChild(Text($"第 {f.RoundLimit} 轮结束封门。\n异动达 8：立即损失 3 心志并回落到 4，护符不能抵挡。", 14, Muted));
        left.Body.AddChild(new HSeparator());
        left.Body.AddChild(Text("模仿 / 参考动作", 14, Mint));
        var reference = f.MimicReference is { } referenceKind ? CardCatalog.Get(referenceKind) : null;
        left.Body.AddChild(Text(reference?.Name ?? "尚未建立", 22));
        left.Body.AddChild(Text(reference is null ? "先成功使用一张可建立参考的普通行动牌。" : reference.CanMimic ? "参考有效 · 疾走保留此指令" : "此参考不可模仿 · 请先换用合资格行动牌", 14, reference is { CanMimic: false } ? Amber : Muted));
        left.Body.AddChild(Text("模仿 / 当前作用位置", 14, Mint));
        left.Body.AddChild(Text(room.Name, 18));
        left.Body.AddChild(Text(f.Exhausted.Contains(CardKind.Mimic) ? "模仿已耗尽 · 本次出勤不再抽回" : "成功使用模仿后，本次出勤耗尽。", 14, f.Exhausted.Contains(CardKind.Mimic) ? Amber : Muted));
        left.Body.AddChild(Text("徒手搜查不建立参考；基础封装不覆盖参考。", 13, Muted));
        left.Body.AddChild(new HSeparator());
        left.Body.AddChild(Text("委托进度", 14, Mint));
        left.Body.AddChild(Text($"{(site.RuleIds.All(id => g.Rule(id).Confirmed) ? "✓" : "○")} 确认地点规程\n{(g.Items.Any(i => i.Id == site.TargetItemId && i.Location == ItemLocation.Backpack && i.OwnerId == f.AssignmentId) ? "✓" : "○")} 携带合同实物\n○ 返回入口，归队交付", 16));
        RenderFieldFacts(left.Body);
        var entrance = site.Rooms.FirstOrDefault(r => r.IsEntrance);
        var exitPath = entrance is null ? new List<string>() : RoomPath(room.Id, entrance.Id);
        left.Footer.AddChild(Text(room.IsEntrance ? "出口锚点 / 队伍已在入口" : $"出口锚点 / 距入口 {Math.Max(0, exitPath.Count - 1)} 格", 14, Mint));
        var withdraw = FieldButton("从入口免费撤离", () => ConfirmDialog("撤离现场", TransportText(), () => PerformFieldAction(engine.Withdraw, true)));
        withdraw.Disabled = !room.IsEntrance; withdraw.SetMeta("critical_action", true); left.Footer.AddChild(withdraw);
        left.Footer.AddChild(Text(room.IsEntrance ? "撤离 0 行动 · 先核对运输清单" : "需先移动回入口才能撤离", 12, Muted));

        var center = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        center.AddThemeConstantOverride("separation", 9); row.AddChild(center);
        var scene = new FieldScene { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var observedReaction = g.Rules.SelectMany(r => r.EvidenceRecords).Any(e => e.StartsWith("现场记录：" + room.Name + "，", StringComparison.Ordinal) && e.Contains("观察到反噬", StringComparison.Ordinal) && !e.Contains("未观察到反噬", StringComparison.Ordinal));
        scene.Configure(ArtAssets.Texture("room"), site.Name, room.Name, f.Light, observedReaction,
            f.Shadows.Any(s => s.RoomId == room.Id), room.IsEntrance, Math.Max(0, exitPath.Count - 1), FieldFeedbackSummary(), target.Name);
        center.AddChild(scene);
        var navigation = FieldSurface(); center.AddChild(navigation);
        var nav = new VBoxContainer(); nav.AddThemeConstantOverride("separation", 8); navigation.AddChild(nav);
        var roomScroll = new ScrollContainer { VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, HorizontalScrollMode = ScrollContainer.ScrollMode.Auto };
        nav.AddChild(roomScroll);
        var doors = new HBoxContainer(); doors.AddThemeConstantOverride("separation", 8); roomScroll.AddChild(doors);
        foreach (var visibleRoom in site.Rooms.Where(r => r.Placed))
        {
            var viewed = visibleRoom; bool actual = room.Id == viewed.Id;
            string label = (actual ? "● 队伍 · " : room.Neighbors.Contains(viewed.Id) ? "门 → " : "路径 · ") + viewed.Name;
            var door = FieldButton(label, () => SelectFieldRoom(viewed.Id), viewed.Id == _viewedRoom, 14);
            door.TooltipText = viewed.IsEntrance ? "入口：队伍到达后可免费撤离。" : "查看此房间；移动需另行预览并执行。";
            doors.AddChild(door);
        }
        var basicScroll = new ScrollContainer { VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, HorizontalScrollMode = ScrollContainer.ScrollMode.Auto };
        nav.AddChild(basicScroll);
        var basic = new HBoxContainer(); basic.AddThemeConstantOverride("separation", 8); basicScroll.AddChild(basic);
        basic.AddChild(ActionButton("移动至查看房 · 1", new FieldAction { Kind = FieldActionKind.Move, TargetId = target.Id }));
        basic.AddChild(ActionButton("徒手搜查 · 2", new FieldAction { Kind = FieldActionKind.Search }));
        foreach (var unplaced in site.Rooms.Where(r => !r.Placed && r.Neighbors.Contains(room.Id)))
            basic.AddChild(ActionButton("放置侧房 · 1 + 墨水 1", new FieldAction { Kind = FieldActionKind.PlaceRoom, TargetId = unplaced.Id }));

        RenderFieldPreview(row, engine, room);
        RenderFieldHand(layout, engine, room);
    }

    private static PanelContainer FieldSurface()
    {
        var panel = new PanelContainer();
        var style = Box(Panel, new Color("394554"), 1, 4);
        style.ContentMarginTop = style.ContentMarginBottom = 10;
        style.ContentMarginLeft = style.ContentMarginRight = 12;
        panel.AddThemeStyleboxOverride("panel", style); return panel;
    }

    private static Button FieldButton(string label, Action action, bool primary = false, int size = 16)
    {
        var button = Button(label, action, primary);
        button.AddThemeFontSizeOverride("font_size", (int)Math.Round(size * UiScale));
        button.CustomMinimumSize = new Vector2(0, 38 * UiScale);
        return button;
    }

    private static Label FieldMetadata(string value, int size = 14, Color? color = null)
    {
        var label = Text(value, size, color ?? Muted);
        label.AutowrapMode = TextServer.AutowrapMode.Off;
        return label;
    }

    private void SelectFieldRoom(string id)
    {
        _viewedRoom = id;
        if (_pendingAction?.Card == CardKind.Sprint) _pendingAction = CardAction(CardKind.Sprint, _pendingAction.HandSlotIndex);
        else if (_pendingAction?.Kind == FieldActionKind.Move) _pendingAction.TargetId = id;
        Render();
    }

    private void RenderFieldPreview(HBoxContainer row, FieldEngine engine, RoomState room)
    {
        var f = _game!.Field!;
        var panel = FieldSurface(); panel.CustomMinimumSize = new Vector2(410 + 100 * (UiScale - 1), 0); row.AddChild(panel);
        var right = new VBoxContainer(); right.AddThemeConstantOverride("separation", 8); panel.AddChild(right);
        var headings = new HBoxContainer(); right.AddChild(headings);
        var title = Text("03 / 行动工作台", 15, Mint); title.SizeFlagsHorizontal = SizeFlags.ExpandFill; headings.AddChild(title);
        headings.AddChild(FieldMetadata("先预览 → 再执行", 12));
        var tabs = new HBoxContainer(); tabs.AddThemeConstantOverride("separation", 6); right.AddChild(tabs);
        bool showDossier = _fieldDossierOpen || _fieldTab is "bag" or "items" or "log";
        foreach (var (id, name) in new[] { ("evidence", "证据"), ("bag", "背包"), ("log", "事实日志") })
        {
            var tab = FieldButton(name, () =>
            {
                if (showDossier && _fieldTab == id) { _fieldDossierOpen = false; _fieldTab = "evidence"; }
                else { _fieldTab = id; _fieldDossierOpen = true; }
                Render();
            }, showDossier && _fieldTab == id, 14);
            tab.SizeFlagsHorizontal = SizeFlags.ExpandFill; tabs.AddChild(tab);
        }
        var detailScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.8f, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        detailScroll.Visible = showDossier; right.AddChild(detailScroll);
        var details = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; details.AddThemeConstantOverride("separation", 10); detailScroll.AddChild(details);
        if (_fieldTab == "evidence")
        {
            foreach (var rule in engine.ApplicableRules()) AddRule(details, rule, true);
            if (f.RevealCharges > 0)
                details.AddChild(ActionButton($"稀有揭晓 · 剩余 {f.RevealCharges}", new FieldAction { Kind = FieldActionKind.Reveal, TargetId = engine.ApplicableRules().FirstOrDefault(r => !r.Confirmed)?.Id ?? "" }));
        }
        else if (_fieldTab is "bag" or "items") RenderFieldInventory(details, engine, room);
        else
        {
            details.AddChild(Text("事实流水 / 记录实际触发、抵挡与消耗；不替你判断未知原因。", 14, Mint));
            foreach (var log in _game.Log.AsEnumerable().Reverse()) details.AddChild(Text(log, 16, Muted));
        }
        if (showDossier)
            details.AddChild(FieldButton("收起资料 · 回到完整预览", () => { _fieldDossierOpen = false; _fieldTab = "evidence"; Render(); }, false, 14));
        right.AddChild(new HSeparator());
        ActionPreview? preview = _pendingEndRound ? engine.PreviewEndRound() : _pendingAction is { } action ? engine.Preview(action) : null;
        right.AddChild(Text(_pendingEndRound ? $"预览 / 第 {f.Round} 轮结束 · 0 行动" : _pendingAction is { } pending ? $"预览 / {FieldActionLabel(pending)} · {preview!.Cost} 行动" : "常驻行动预览 / 尚未选择", 16, Mint));
        var previewScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.3f, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        right.AddChild(previewScroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; content.AddThemeConstantOverride("separation", 9); previewScroll.AddChild(content);
        if (preview is null)
            content.AddChild(Text("选择一张卡牌或基础动作。\n此处持续显示目标、费用、基础效果、已知后果与尚不确定的部分。", 18, Muted));
        else RenderPreviewSections(content, preview);
        if (_capturePreviewPressure)
        {
            content.AddChild(Text("未来文案压力样稿 / 以下为加长副本", 15, Amber));
            content.AddChild(Text(string.Concat(Enumerable.Repeat("核对记录：同一房间的行为次数、灯光、工具消耗与实际反噬须分别核对；尚未观察到的条件不表示安全。\n", 12)), 18));
        }
        if (_pendingAction?.Card is { } selectedCard)
        {
            var definition = CardCatalog.Get(selectedCard);
            content.AddChild(FieldButton(_fieldCardExpanded ? "收起完整卡牌说明 −" : "展开完整卡牌说明 +", () => { _fieldCardExpanded = !_fieldCardExpanded; Render(); }, false, 14));
            if (_fieldCardExpanded) content.AddChild(Text(definition.Name + " / " + definition.Description + (definition.Exhausts ? "\n成功使用：本次出勤耗尽；未使用换轮：普通弃置。" : ""), 18));
        }
        // Target controls share the scroll region. Only the commit controls occupy the fixed footer.
        bool search = _pendingAction?.Kind == FieldActionKind.Search || _pendingAction?.Card is CardKind.Search or CardKind.Knock || (_pendingAction?.Card == CardKind.Mimic && f.MimicReference is CardKind.Search or CardKind.Knock);
        if (search && _pendingAction is { } searchAction)
        {
            var accept = new CheckButton { Text = "取得物品时收入背包", ButtonPressed = searchAction.AcceptItem };
            accept.AddThemeFontSizeOverride("font_size", (int)Math.Round(16 * UiScale));
            accept.Toggled += value => { searchAction.AcceptItem = value; Render(); }; content.AddChild(accept);
            content.AddChild(Text("取消领取仍会执行搜查并支付动作费用。", 14, Muted));
        }
        else if (_pendingAction is { Card: CardKind.Seal } sealingAction)
        {
            var targets = engine.Backpack().Where(i => i.RequiresSealing && !i.Sealed).ToList();
            var choose = new OptionButton { CustomMinimumSize = new Vector2(0, 40 * UiScale) };
            choose.AddThemeFontSizeOverride("font_size", (int)Math.Round(16 * UiScale));
            foreach (var item in targets) choose.AddItem("封装对象：" + item.Name);
            int selected = targets.FindIndex(i => i.Id == sealingAction.TargetId); if (selected >= 0) choose.Select(selected);
            choose.ItemSelected += index => { sealingAction.TargetId = targets[(int)index].Id; Render(); }; content.AddChild(choose);
        }
        else if (_pendingAction is { Kind: FieldActionKind.Reveal } revealingAction)
        {
            var targets = engine.ApplicableRules().Where(r => !r.Confirmed).ToList();
            var choose = new OptionButton { CustomMinimumSize = new Vector2(0, 40 * UiScale) };
            choose.AddThemeFontSizeOverride("font_size", (int)Math.Round(16 * UiScale));
            foreach (var rule in targets) choose.AddItem("揭晓记录：" + rule.Name);
            int selected = targets.FindIndex(r => r.Id == revealingAction.TargetId); if (selected >= 0) choose.Select(selected);
            choose.ItemSelected += index => { revealingAction.TargetId = targets[(int)index].Id; Render(); }; content.AddChild(choose);
        }
        var executeRow = new HBoxContainer(); right.AddChild(executeRow);
        var execute = FieldButton(_pendingEndRound ? "执行轮末结算 →" : "执行选定行动 →", ExecuteSelected, true);
        execute.Disabled = preview is null || !preview.Allowed; execute.SizeFlagsHorizontal = SizeFlags.ExpandFill; execute.SetMeta("critical_action", true); executeRow.AddChild(execute);
        var cancel = FieldButton("取消", () => { _pendingAction = null; _pendingEndRound = false; Render(); }, false, 14);
        cancel.Disabled = _pendingAction is null && !_pendingEndRound; executeRow.AddChild(cancel);
        var endRound = FieldButton("结束本轮 · 先查看结算预览", () => { _pendingAction = null; _pendingEndRound = true; Render(); }, false, 15);
        endRound.SetMeta("critical_action", true); right.AddChild(endRound);
    }

    private static void RenderPreviewSections(VBoxContainer body, ActionPreview preview)
    {
        if (!preview.Allowed) { body.AddChild(Text("当前不可执行 / 已知条件", 15, Amber)); body.AddChild(Text(preview.Text, 18)); return; }
        var headings = new[] { "实际动作 / 对象 / 费用", "基础效果", "已知会触发", "相关已知不触发", "尚不确定" };
        var text = new List<string>();
        void Flush() { if (text.Count > 0) { body.AddChild(Text(string.Join("\n", text), 18)); text.Clear(); } }
        foreach (var line in preview.Text.Split('\n'))
        {
            if (headings.Contains(line))
            {
                Flush(); body.AddChild(Text($"{Array.IndexOf(headings, line) + 1:00} / {line}", 14, line == "尚不确定" ? Amber : Mint));
            }
            else text.Add(line);
        }
        Flush();
    }

    private void RenderFieldHand(VBoxContainer layout, FieldEngine engine, RoomState room)
    {
        var f = _game!.Field!;
        var shelf = FieldSurface(); layout.AddChild(shelf);
        var content = new VBoxContainer(); content.AddThemeConstantOverride("separation", 8); shelf.AddChild(content);
        var header = new HBoxContainer(); content.AddChild(header);
        header.AddThemeConstantOverride("separation", 20);
        var title = Text("行动档案 / 五张手牌", 15, Mint); title.SizeFlagsHorizontal = SizeFlags.ExpandFill; header.AddChild(title);
        string mimic = f.Exhausted.Contains(CardKind.Mimic) ? "已耗尽" : f.MimicReference is { } reference ? CardCatalog.Get(reference).Name + (CardCatalog.Get(reference).CanMimic ? "" : "（不合资格）") : "尚无";
        header.AddChild(FieldMetadata($"模仿参考：{mimic}  /  当前对象：{room.Name}", 13));
        header.AddChild(FieldMetadata($"牌库 {f.DrawPile.Count}  /  弃牌 {f.Discard.Count}  /  耗尽 {f.Exhausted.Count}"));
        var borrowed = FieldMetadata("借用区  /  无", 14, Ink); borrowed.CustomMinimumSize = new Vector2(160, 0); borrowed.HorizontalAlignment = HorizontalAlignment.Right; header.AddChild(borrowed);
        var cards = new HBoxContainer(); cards.AddThemeConstantOverride("separation", 10); content.AddChild(cards);
        if (_captureBorrowedLayout)
        {
            var extra = new HBoxContainer(); extra.AddThemeConstantOverride("separation", 16); content.AddChild(extra);
            var illustration = ArtAssets.Image("card-investigate", 64, TextureRect.StretchModeEnum.KeepAspectCentered);
            illustration.CustomMinimumSize = new Vector2(64, 64); illustration.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; extra.AddChild(illustration);
            var description = Text("额外借用区 / 组合稿 · 独立于原五槽\n调查 · 1 行动 · 取得一份可靠线索\n仅验证布局；当前 M2 没有借用牌发放机制。", 15, Muted);
            description.SizeFlagsHorizontal = SizeFlags.ExpandFill; extra.AddChild(description);
        }
        var slots = f.HandSlots.Count == 5 ? f.HandSlots : f.Hand.Take(5).Select(c => (CardKind?)c).Concat(Enumerable.Repeat<CardKind?>(null, 5)).Take(5).ToList();
        for (int index = 0; index < 5; index++)
        {
            int slot = index; var kind = slots[index];
            var card = new Button { CustomMinimumSize = new Vector2(0, 215 + (UiScale - 1) * 90), SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseDefaultCursorShape = CursorShape.PointingHand };
            bool selected = !_pendingEndRound && _pendingAction is { Kind: FieldActionKind.PlayCard } && _pendingAction.HandSlotIndex == slot;
            var style = Box(kind is null ? new Color("111823") : Panel, selected ? Amber : new Color("465366"), selected ? 2 : 1, 4);
            card.AddThemeStyleboxOverride("normal", style); card.AddThemeStyleboxOverride("disabled", style);
            cards.AddChild(card);
            var label = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            label.AddThemeConstantOverride("separation", 5); card.AddChild(label); label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            label.OffsetLeft = 9; label.OffsetRight = -9; label.OffsetTop = 7; label.OffsetBottom = -7;
            if (kind is { } value)
            {
                var definition = CardCatalog.Get(value); var action = CardAction(value, slot); var preview = engine.Preview(action);
                var art = ArtAssets.Image(CardArtId(value), 92, TextureRect.StretchModeEnum.KeepAspectCentered); art.MouseFilter = MouseFilterEnum.Ignore; label.AddChild(art);
                var heading = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; label.AddChild(heading);
                var name = Text(definition.Name, 20, selected ? Amber : Ink); name.SizeFlagsHorizontal = SizeFlags.ExpandFill; heading.AddChild(name);
                var cost = Text($"{definition.ActionCost} 行动", 16, Mint); cost.AutowrapMode = TextServer.AutowrapMode.Off; heading.AddChild(cost);
                label.AddChild(Text(value == CardKind.Sprint ? "目标 / 查看房间" : value == CardKind.Seal ? "目标 / 所选背包实物" : "目标 / " + room.Name, 13, Muted));
                label.AddChild(Text(CardSummary(value), 15));
                label.AddChild(Text(definition.Exhausts ? "每次出勤耗尽 · 详情 ＋" : preview.Allowed ? "选择并展开 →" : "条件不足 · 选择查看原因", 12, definition.Exhausts ? Amber : Muted));
                card.TooltipText = preview.Allowed ? definition.Description : preview.Text;
                // An unavailable card is still inspectable; the fixed execution button remains disabled.
                card.Pressed += () => { _fieldCardExpanded = false; PreviewAction(action); };
            }
            else
            {
                label.Alignment = BoxContainer.AlignmentMode.Center;
                var number = Text($"0{slot + 1}", 28, new Color("667283")); number.HorizontalAlignment = HorizontalAlignment.Center; label.AddChild(number);
                var empty = Text("已使用 / 空槽\n轮末再补牌", 16, Muted); empty.HorizontalAlignment = HorizontalAlignment.Center; label.AddChild(empty);
                card.Disabled = true;
            }
        }
    }

    private static string CardArtId(CardKind kind) => kind switch { CardKind.Light or CardKind.Ward => "card-light", CardKind.Mimic or CardKind.Banish => "card-mimic", _ => "card-investigate" };

    private void RenderFieldInventory(VBoxContainer body, FieldEngine engine, RoomState room)
    {
        var g = _game!; var f = g.Field!; var bag = engine.Backpack().ToList();
        body.AddChild(Text($"队伍背包  {bag.Sum(i => i.Size)} / {f.Capacity} 格", 19, Mint));
        if (!string.IsNullOrEmpty(f.AssignedLightSupportId))
        {
            body.AddChild(Text(f.LightSupportUsed ? "驻点灯具 / 今日支援已使用" : "驻点灯具 / 已分配今日一次支援", 16, f.LightSupportUsed ? Muted : Amber));
            body.AddChild(ActionButton("点灯支援 · 0 行动", new FieldAction { Kind = FieldActionKind.SupportLight }));
        }
        foreach (var item in bag) RenderFieldItem(body, item, true);
        body.AddChild(Text("房内已发现 / 尚未入包", 15, Mint));
        var floor = g.Items.Where(i => i.Location == ItemLocation.Room && i.RoomId == room.Id && i.NodeId == f.NodeId && i.Discovered).ToList();
        if (floor.Count == 0) body.AddChild(Text("当前没有可拾取的已发现实物。", 15, Muted));
        foreach (var item in floor) RenderFieldItem(body, item, false);
    }

    private void RenderFieldItem(VBoxContainer body, ItemState item, bool carried)
    {
        var surface = FieldSurface(); body.AddChild(surface);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 7); surface.AddChild(column);
        var row = new HBoxContainer(); column.AddChild(row);
        var art = ArtAssets.Image(ItemPresentation.ArtId(item), 70, TextureRect.StretchModeEnum.KeepAspectCentered); art.CustomMinimumSize = new Vector2(72, 70); art.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; row.AddChild(art);
        var identity = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; row.AddChild(identity);
        identity.AddChild(Text(item.Name, 18));
        identity.AddChild(Text((carried ? "携带中" : "已发现 · 留在房内") + $" / {item.Size} 格", 14, Mint));
        identity.AddChild(Text(item.RequiresSealing ? item.Sealed ? "□ 已封装" : "◇ 未封装" : item.Kind is "SealKit" or "SourceCase" or WardClipCatalog.Kind ? $"工具余量 {item.Charges} / {item.MaxCharges}" : "实物已登记", 14, item.RequiresSealing && !item.Sealed ? Amber : Muted));
        column.AddChild(Text(item.Description, 15, Muted));
        if (item.Kind is "SealKit" or "SourceCase" or WardClipCatalog.Kind)
            column.AddChild(Text($"容量 {item.MaxCharges} / 剩余 {item.Charges}，本次消耗见事实流水\n归属 / {(carried ? "当前出勤队伍" : "当前房间")}" + (!string.IsNullOrEmpty(item.ReservedBy) ? " · 已预留给本任务" : ""), 14));
        if (carried)
        {
            if (!item.Sealed && item.RequiresSealing) column.AddChild(ActionButton("基础封装 · 2 行动", new FieldAction { Kind = FieldActionKind.Seal, TargetId = item.Id }));
            column.AddChild(ActionButton("放回当前房间", new FieldAction { Kind = FieldActionKind.Drop, TargetId = item.Id }));
        }
        else column.AddChild(ActionButton("拾取实物", new FieldAction { Kind = FieldActionKind.Pickup, TargetId = item.Id }));
    }

    private void RenderFieldFacts(VBoxContainer body)
    {
        var f = _game!.Field!;
        var facts = _fieldFacts.Count > 0 ? _fieldFacts : LatestFieldFacts();
        body.AddChild(new HSeparator()); body.AddChild(Text("最近行动 / 事实反馈", 14, Mint));
        var observed = facts.LastOrDefault(x => x.Contains("现场记录：", StringComparison.Ordinal));
        bool reacted = facts.Any(x => x.Contains("实际观察：", StringComparison.Ordinal) || x.Contains("已知守则产生后果：", StringComparison.Ordinal));
        bool blocked = facts.Any(x => x.Contains("消耗 1 护符", StringComparison.Ordinal));
        body.AddChild(Text(reacted ? "! 已观察到规则反应" : observed is not null && observed.Contains("未观察到反噬", StringComparison.Ordinal) ? "本次未观察到反噬" : "触发情况以实际流水为准", 16, reacted ? Warning : Ink));
        body.AddChild(Text(blocked ? $"◇ 护符实际抵挡 {facts.Count(x => x.Contains("消耗 1 护符", StringComparison.Ordinal))} 次" : "本次流水没有护符抵挡记录", 15, blocked ? Amber : Muted));
        body.AddChild(Text($"最终心志 {f.Will}/{f.MaxWill} · 当前护符 {f.Wards}", 16));
        foreach (var expense in facts.Where(x => x.Contains("装备来源：", StringComparison.Ordinal) || x.Contains("封装", StringComparison.Ordinal) && x.Contains("次", StringComparison.Ordinal)))
            body.AddChild(Text(StripFieldLogPrefix(expense), 14, Muted));
        if (observed is not null) body.AddChild(Text(StripFieldLogPrefix(observed), 14, Muted));
        body.AddChild(Text("“未观察到”不等于以后安全。完整前后数值见事实日志。", 13, Muted));
    }

    private List<string> LatestFieldFacts()
    {
        var logs = _game!.Log;
        int start = logs.FindLastIndex(x => x.Contains("行动前：", StringComparison.Ordinal) || x.Contains("轮结束。", StringComparison.Ordinal));
        int entry = logs.FindLastIndex(x => x.Contains("每轮 3 行动、5 手牌", StringComparison.Ordinal));
        return start > entry ? logs.Skip(start).ToList() : new();
    }

    private string FieldFeedbackSummary()
    {
        var f = _game!.Field!; var facts = _fieldFacts.Count > 0 ? _fieldFacts : LatestFieldFacts();
        bool reaction = facts.Any(x => x.Contains("实际观察：", StringComparison.Ordinal) || x.Contains("已知守则产生后果：", StringComparison.Ordinal));
        bool noReaction = facts.Any(x => x.Contains("本次未观察到反噬", StringComparison.Ordinal));
        int blocked = facts.Count(x => x.Contains("消耗 1 护符", StringComparison.Ordinal));
        if (!reaction && !noReaction && blocked == 0) return "";
        return (reaction ? "! 规则反应已记录" : noReaction ? "本次未观察到反噬" : "敌影攻击已记录") + $" / 护符抵挡 {blocked} 次 / 心志 {f.Will}";
    }

    private static string StripFieldLogPrefix(string text)
    {
        int mark = text.IndexOf(" · ", StringComparison.Ordinal); return mark >= 0 ? text[(mark + 3)..] : text;
    }

    private static string CardSummary(CardKind card) => card switch
    {
        CardKind.Investigate => "取得一份可靠线索", CardKind.Search => "搜查本房真实库存",
        CardKind.Knock => "取得门内一份库存", CardKind.Light => "本轮照明 · 异动 −1",
        CardKind.Rest => "心志 +2 · 房间限两次", CardKind.Ward => "护符 +1 · 上限 3",
        CardKind.Sprint => "最多两格 · 保留参考", CardKind.Mimic => "执行参考的基础效果",
        CardKind.Seal => "封装一件背包实物", CardKind.Banish => "驱离当前普通敌影", _ => ""
    };

    private static string FieldActionLabel(FieldAction action) => action.Card is { } card ? CardCatalog.Get(card).Name : action.Kind switch
    {
        FieldActionKind.Move => "移动", FieldActionKind.Search => "徒手搜查", FieldActionKind.PlaceRoom => "放置房间",
        FieldActionKind.Seal => "基础封装", FieldActionKind.Drop => "放下实物", FieldActionKind.Pickup => "拾取实物",
        FieldActionKind.Reveal => "稀有揭晓", FieldActionKind.SupportLight => "驻点点灯支援", _ => "现场动作"
    };

    private FieldAction CardAction(CardKind kind, int slot = -1)
    {
        var g = _game!; var f = g.Field!;
        var action = new FieldAction { Kind = FieldActionKind.PlayCard, Card = kind, HandSlotIndex = slot };
        if (kind == CardKind.Sprint)
        {
            action.TargetId = _viewedRoom; action.Path = RoomPath(f.RoomId, _viewedRoom).Skip(1).ToList();
        }
        if (kind == CardKind.Seal)
        {
            var targets = g.Items.Where(i => i.Location == ItemLocation.Backpack && i.OwnerId == f.AssignmentId && !i.Sealed && i.RequiresSealing).ToList();
            var engine = new FieldEngine(g);
            var target = targets.FirstOrDefault(i => engine.Preview(new FieldAction { Kind = FieldActionKind.PlayCard, Card = CardKind.Seal, HandSlotIndex = slot, TargetId = i.Id }).Allowed) ?? targets.FirstOrDefault();
            action.TargetId = target?.Id ?? "";
        }
        return action;
    }

    private List<string> RoomPath(string start, string end)
    {
        var rooms = _game!.Node(_game.Field!.NodeId).Rooms; var queue = new Queue<List<string>>(); queue.Enqueue(new() { start }); var visited = new HashSet<string> { start };
        while (queue.Count > 0)
        {
            var path = queue.Dequeue(); if (path[^1] == end) return path;
            foreach (var next in rooms.First(r => r.Id == path[^1]).Neighbors.Where(id => rooms.Any(r => r.Id == id && r.Placed)))
                if (visited.Add(next)) queue.Enqueue(path.Concat(new[] { next }).ToList());
        }
        return new();
    }

    private Button ActionButton(string label, FieldAction action)
    {
        var preview = new FieldEngine(_game!).Preview(action);
        var b = FieldButton(label, () => PreviewAction(action), false, 14);
        b.TooltipText = preview.Allowed ? "选择后在右侧查看后果并执行。" : preview.Text;
        return b;
    }

    private void PreviewAction(FieldAction action) { _pendingAction = action; _pendingEndRound = false; Render(); }

    private void ExecuteSelected()
    {
        var engine = new FieldEngine(_game!);
        if (_pendingEndRound)
        {
            var preview = engine.PreviewEndRound();
            if (!preview.Allowed) { _notice = preview.Text; Render(); return; }
            void ExecuteRound() { _pendingEndRound = false; PerformFieldAction(engine.EndRound, true); }
            if (preview.RequiresConfirmation) ConfirmDialog("已知立即失败风险", preview.Text, ExecuteRound); else ExecuteRound();
            return;
        }
        if (_pendingAction is not { } action) return;
        var p = engine.Preview(action);
        if (!p.Allowed) { _notice = p.Text; Render(); return; }
        void ExecuteAction() { _pendingAction = null; PerformFieldAction(() => engine.Execute(action)); }
        if (p.RequiresConfirmation || action.Kind == FieldActionKind.Reveal)
            ConfirmDialog(action.Kind == FieldActionKind.Reveal ? "消耗唯一揭晓资源" : "已知立即失败风险", p.Text, ExecuteAction);
        else ExecuteAction();
    }

    private void PerformFieldAction(Func<ActionResult> operation, bool checkpoint = false)
    {
        var before = _game!.Log.ToList();
        var result = operation(); var after = _game.Log;
        // Log storage is bounded. Find the retained overlap instead of assuming Count always grows.
        int overlap = Math.Min(before.Count, after.Count);
        while (overlap > 0 && !before.Skip(before.Count - overlap).SequenceEqual(after.Take(overlap))) overlap--;
        _fieldFacts = after.Skip(overlap).ToList(); _fieldFactsState = _game.Field; _fieldFactsSortie = _game.Field?.SortieId ?? -1;
        FinishFieldAction(result, checkpoint);
    }

    private void FinishFieldAction(ActionResult result, bool checkpoint = false)
    {
        var g = _game!; _notice = result.Message;
        if (g.Field is { Finished: true })
        {
            var done = new CityEngine(g).CompleteManual(); _notice += " · " + done.Message;
            Save($"第 {g.Day} 日 · 出勤已归队，待统一日结"); _page = "dispatch";
        }
        else if (result.Success && checkpoint) Save($"{g.Node(g.Field!.NodeId).Name} · 第 {g.Field.Round} 轮完整检查点");
        Render();
    }

    private void AddRule(VBoxContainer body, RuleState rule, bool field)
    {
        body.AddChild(Text(rule.Confirmed ? "✓ 已确认规程" : "□ 事实档案 / 待核实", 14, rule.Confirmed ? Mint : Amber));
        body.AddChild(Text(rule.Confirmed ? rule.Answer : rule.Name, 18));
        if (!string.IsNullOrEmpty(rule.Notice)) body.AddChild(Text("原始告示 / 未核实\n" + rule.Notice, 16, Muted));
        if (rule.EvidenceFound && (field || rule.Archived)) body.AddChild(Text("事实 / 可靠历史记录\n" + rule.Clue, 17));
        else if (rule.Observed && (field || rule.Archived)) body.AddChild(Text("事实 / 已有实际观察，详见行动记录；观察本身不等于因果确认。", 16, Muted));
        else body.AddChild(Text(field ? "事实 / 尚无可靠线索，可用调查牌取得。" : "事实 / 尚无已归档的可靠线索。", 16, Muted));
        if (field || rule.Archived)
            foreach (var evidence in rule.EvidenceRecords.Where(e => e != rule.Clue)) body.AddChild(Text("事实 / " + evidence, 16, Muted));
        if (rule.Confirmed) return;
        bool eligible = field ? rule.EvidenceFound || rule.Observed : rule.Archived;
        if (!eligible) return;
        body.AddChild(Text("△ 候选假说 / 点击提交验证", 14, Amber));
        bool usedToday = rule.LastCityAttemptDay == _game!.Day;
        bool usedRound = field && _game.Field!.LastConfirmationRound == _game.Field.Round;
        if (usedToday || usedRound) body.AddChild(Text(usedToday ? "今天已提交过这条判断，现场不能重复提交。" : "本轮现场验证次数已使用；下轮可再次判断。", 14, Muted));
        for (int index = 0; index < rule.Candidates.Count; index++)
        {
            int candidate = index; bool wrong = rule.WrongCandidates.Contains(index);
            var button = FieldButton("", () =>
            {
                if (field) PerformFieldAction(() => new FieldEngine(_game!).Confirm(rule.Id, candidate), true);
                else CityAction(new CityEngine(_game!).ConfirmRule(rule.Id, candidate));
            });
            button.Disabled = wrong || usedToday || (field ? usedRound : _game.Phase != DayPhase.Preparation);
            // The text is a container child, so its true wrapped height participates in scrolling.
            var candidateLabel = Text((wrong ? "已排除 / " : $"假说 {index + 1} / ") + rule.Candidates[index], 17, button.Disabled ? Muted : Ink);
            var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
            margin.AddThemeConstantOverride("margin_left", 12); margin.AddThemeConstantOverride("margin_right", 12);
            margin.AddThemeConstantOverride("margin_top", 10); margin.AddThemeConstantOverride("margin_bottom", 10);
            margin.AddChild(candidateLabel);
            var wrapper = new PanelContainer(); wrapper.AddThemeStyleboxOverride("panel", new StyleBoxEmpty()); wrapper.AddChild(button); wrapper.AddChild(margin);
            body.AddChild(wrapper);
        }
    }

    private string TransportText()
    {
        var g = _game!; var f = g.Field!; var city = new CityEngine(g);
        var items = g.Items.Where(i => i.Location == ItemLocation.Backpack && i.OwnerId == f.AssignmentId).Select(i => i.Id);
        return "从入口撤离不消耗行动。实物与零钱归队后登记，合同在统一日结时付款。\n\n" + city.PreviewTransport(items, city.FindRoute(f.NodeId).AsEnumerable().Reverse()).Text;
    }

    private string EndRoundText() => new FieldEngine(_game!).PreviewEndRound().Text;
}
