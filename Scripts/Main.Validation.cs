using Godot;
using Joy.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Joy;

public partial class Main
{
    private bool _captureItemGallery;
    private bool _capturePreviewPressure;
    private bool _captureBorrowedLayout;
    private static string CaptureCase(string[] args) => args.FirstOrDefault(a => a.StartsWith("--case="))?.Split('=')[1]
        ?? (args.Contains("--capture-field-used") ? "field-used" : args.Contains("--capture-field") ? "field" : "city");

    private static string CaptureName(string[] args) => $"art-{CaptureCase(args)}-{(args.Contains("--variant=b") ? "b" : "a")}-{UiScale.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}-{(args.Contains("--grayscale") ? "gray" : "color")}{(args.Contains("--cosmic") ? "-cosmic" : "")}.png";

    // Explicit QA launch arguments create isolated, unsaved scenarios; normal play never enters this method.
    private void PrepareArtCapture(string[] args)
    {
        string scene = CaptureCase(args);
        _game = WorldFactory.Create(413, mirrorVariantB: args.Contains("--variant=b"));
        _welcome = scene == "title";
        _lastSavedAt = null; _lastSavedCheckpoint = "";
        if (_welcome) return;
        if (scene == "item-states") { _captureItemGallery = true; return; }
        var city = new CityEngine(_game);
        if (scene.StartsWith("field") || scene is "preview-long" or "preview-ward" or "transport" or "feedback")
        {
            Require(city.AddTask(JobKind.Manual, "hotel")); Require(city.LockDay());
            var engine = new FieldEngine(_game);
            Require(engine.Start(_game.LockedPlan.First(a => a.Kind == JobKind.Manual)));
            Require(engine.Execute(new FieldAction { Kind = FieldActionKind.Move, TargetId = "hotel-room1" }));
            var f = _game.Field!;
            f.HandSlots = new() { CardKind.Investigate, CardKind.Light, CardKind.Mimic, CardKind.Ward, CardKind.Search };
            f.Hand = f.HandSlots.OfType<CardKind>().ToList();
            _viewedRoom = f.RoomId;
            if (scene is "field-used" or "field-lit")
            {
                Require(engine.Execute(new FieldAction { Kind = FieldActionKind.PlayCard, Card = CardKind.Light, HandSlotIndex = 1 }));
                if (scene == "field-used") Require(engine.Execute(new FieldAction { Kind = FieldActionKind.PlayCard, Card = CardKind.Ward, HandSlotIndex = 3 }));
            }
            if (scene == "feedback")
            {
                f.Wards = 1;
                Require(engine.Execute(new FieldAction { Kind = FieldActionKind.PlayCard, Card = CardKind.Search, HandSlotIndex = 4 }));
                _fieldTab = "log";
            }
            if (scene == "field-items")
            {
                foreach (var id in new[] { "hotel-clock", "lamp" })
                {
                    var item = _game.Item(id); item.Discovered = true; item.Location = ItemLocation.Backpack; item.OwnerId = f.AssignmentId;
                }
                _game.Item("hotel-clock").Sealed = true;
                _fieldTab = "items";
            }
            if (scene == "preview-long")
            {
                _capturePreviewPressure = true;
            }
            _pendingAction = new FieldAction { Kind = FieldActionKind.PlayCard, Card = CardKind.Search, HandSlotIndex = 4 };
            _captureBorrowedLayout = scene == "field-extra";
            if (scene == "feedback") { _pendingAction = null; _fieldDossierOpen = true; }
            if (scene == "preview-ward") { f.Wards = 1; _game.Rule("hotel-mirror").Confirmed = true; }
            if (scene == "transport")
            {
                // Actual transport preview, with unknown route rules from the supported legacy configuration.
                _game = WorldFactory.CreateLegacy(413); city = new CityEngine(_game);
                _game.Edges.First(e => e.Id == "hq-hotel").Passable = false;
                _game.Edges.First(e => e.Id == "hotel-depot").Passable = false;
                if (args.Contains("--variant=b")) _game.Rule("line-seal").Trigger = RuleTrigger.Knock;
                Require(city.AddTask(JobKind.Manual, "hotel")); Require(city.LockDay());
                Require(new FieldEngine(_game).Start(_game.LockedPlan.First(a => a.Kind == JobKind.Manual)));
                var item = _game.Item("hotel-clock"); item.Discovered = true; item.Location = ItemLocation.Backpack; item.OwnerId = _game.Field!.AssignmentId;
                _pendingAction = null; _viewedRoom = _game.Field.RoomId; _fieldTab = "items";
                _notice = "运输预览：只列已知后果，未确认线路规则仍未知。";
            }
            return;
        }
        if (scene == "items")
        {
            _page = "tools";
            var lamp = _game.Item("lamp"); lamp.Location = ItemLocation.Warehouse; lamp.Discovered = true;
            var clock = _game.Item("hotel-clock"); clock.Location = ItemLocation.Warehouse; clock.Discovered = true; clock.Sealed = true;
            Require(city.AddTask(JobKind.Manual, "hotel"));
            return;
        }
        if (scene is "settlement" or "outcome")
        {
            if (scene == "outcome") _game.Day = 5;
            Require(city.AddTask(JobKind.Survey, "hospital")); Require(city.AddTask(JobKind.Build, "depot")); Require(city.LockDay()); Require(city.SettleDay());
            _cityDetailTab = "settlement";
            return;
        }
        if (scene == "city-states")
        {
            _game.Day = 3;
            foreach (var id in new[] { "hotel", "hospital", "depot", "relay", "junction" }) { _game.Node(id).Built = true; _game.Node(id).BuiltDay = 1; }
            _game.Node("junction").BuiltDay = 3;
            _game.Node("depot").Invaded = true;
            _game.Stations["hotel"] = "e1"; _game.Stations["relay"] = "e2";
            _game.Edges.First(e => e.Id == "hq-relay").Passable = false;
            _game.Edges.First(e => e.Id == "relay-depot").Passable = false;
            _game.Node("hotel").BlockadeEdgeId = "hotel-junction";
            _game.Forecast.Add(new SpreadIntent { SourceId = "source", TargetId = "hospital", EdgeId = "hospital-source", Description = "红箱源头 → 白榆医院 / 已公开传播预告" });
            _selected = "hotel";
            Require(city.AddTask(JobKind.Manual, "hotel"));
            return;
        }
        Require(city.AddTask(JobKind.Manual, "hotel")); Require(city.AddTask(JobKind.Build, "depot"));
    }

    private static void Require(ActionResult result)
    {
        if (!result.Success) throw new InvalidOperationException("Invalid visual QA setup: " + result.Message);
    }

    private void SaveUiWitness(string imagePath)
    {
        var labels = new List<object>(); var buttons = new List<object>();
        void Walk(Node node)
        {
            if (node is Label label && label.IsVisibleInTree()) labels.Add(new { text = label.Text, fontSize = label.GetThemeFontSize("font_size"), characters = label.Text.Length });
            if (node is Button button && button.IsVisibleInTree())
            {
                var rect = button.GetGlobalRect();
                buttons.Add(new { text = button.Text, enabled = !button.Disabled, x = rect.Position.X, y = rect.Position.Y, width = rect.Size.X, height = rect.Size.Y,
                    critical = button.HasMeta("critical_action"), insideCanvas = new Rect2(Vector2.Zero, Size).Encloses(rect) });
            }
            foreach (var child in node.GetChildren()) Walk(child);
        }
        Walk(this);
        System.IO.File.WriteAllText(System.IO.Path.ChangeExtension(imagePath, ".json"), JsonSerializer.Serialize(new
        {
            canvas = new { width = Size.X, height = Size.Y }, scale = UiScale, staticMode = ReducedMotion, grayscale = _grayscale,
            layoutMinimum = new { width = _root.GetCombinedMinimumSize().X, height = _root.GetCombinedMinimumSize().Y }, labels, buttons
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
