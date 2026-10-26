using Godot;
using Joy.Core.GridPrototype;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Joy.GridPrototype;

public partial class GridBoard : Control
{
    public GridGame Game { get; set; } = null!;
    public GridEngine Engine { get; set; } = null!;
    public bool CityMode { get; set; }
    public Cell Selected { get; set; }
    public string SelectedHero { get; set; } = "";
    public string SelectedCard { get; set; } = "";
    public List<Cell> Route { get; set; } = new();
    public event Action<Cell>? Chosen;
    private float _step;
    private Vector2 _origin;
    private Font _font = null!;
    private Cell _hover = new(-1, -1);

    public override void _Ready()
    {
        _font = GD.Load<FontFile>("res://Assets/Fonts/NotoSansCJKsc-Regular.otf");
        MouseDefaultCursorShape = CursorShape.PointingHand;
        Resized += QueueRedraw;
        MouseExited += () => { _hover = new(-1, -1); QueueRedraw(); };
    }
    public override void _GuiInput(InputEvent input)
    {
        if (_step <= 0) return;
        if (input is InputEventMouseMotion motion)
        {
            var at = FromPoint(motion.Position);
            if (at != _hover) { _hover = at; TooltipText = Describe(at); QueueRedraw(); }
        }
        if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } button)
        {
            var at = FromPoint(button.Position);
            if (at.X >= 0 && at.Y >= 0 && at.X < (CityMode ? 7 : 9) && at.Y < (CityMode ? 5 : 7)) Chosen?.Invoke(at);
            AcceptEvent();
        }
    }
    private Cell FromPoint(Vector2 point) => new((int)Math.Floor((point.X - _origin.X) / _step), (int)Math.Floor((point.Y - _origin.Y) / _step));
    public override void _Draw()
    {
        if (Game is null) return;
        _font ??= GD.Load<FontFile>("res://Assets/Fonts/NotoSansCJKsc-Regular.otf");
        int width = CityMode ? 7 : 9, height = CityMode ? 5 : 7;
        _step = Mathf.Min((Size.X - 38) / width, (Size.Y - 38) / height);
        _origin = new Vector2((Size.X - _step * width) / 2 + 10, (Size.Y - _step * height) / 2 + 8);
        if (_step < 8) return;
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("0a1421"));
        for (int x = 0; x < width; x++) Text(((char)('A' + x)).ToString(), _origin + new Vector2((x + .5f) * _step, -8), 15, GridMain.Muted, true);
        for (int y = 0; y < height; y++) Text((y + 1).ToString(), _origin + new Vector2(-14, (y + .5f) * _step + 5), 15, GridMain.Muted, true);
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            var cell = new Cell(x, y); var rect = Rect(cell).Grow(-3);
            if (CityMode) DrawCityCell(cell, rect); else DrawFieldCell(cell, rect);
            if (cell == _hover) DrawRect(rect, new Color(1, 1, 1, .05f));
            if (cell == Selected) DrawRect(rect.Grow(1), GridMain.Gold, false, 3);
        }
        if (!CityMode && Game.Field is not null)
        {
            foreach (var enemy in Game.Field.Enemies.Where(e => e.Alive)) DrawEnemy(enemy);
            foreach (var actor in Game.Field.Actors) DrawActor(actor);
        }
    }
    private Rect2 Rect(Cell cell) => new(_origin + new Vector2(cell.X * _step, cell.Y * _step), new Vector2(_step, _step));
    private void DrawCityCell(Cell cell, Rect2 rect)
    {
        var tile = Game.City.FirstOrDefault(t => t.Cell == cell);
        if (tile is null) return;
        var color = tile.Terrain == "road" ? new Color("1d3544") : new Color("101e2b");
        if (tile.SiteId.Length > 0) color = tile.Operating ? new Color("23534b") : tile.Cleared ? new Color("334651") : new Color("313542");
        if (tile.SiteId == "hq") color = new("254755");
        if (tile.Blocked) color = new("492d39");
        DrawStyleBox(GridMain.Box(color, new Color("2a414e")), rect);
        if (tile.Terrain == "block")
        {
            var small = new Vector2(_step * .16f, _step * .24f);
            for (int i = 0; i < 3; i++) DrawRect(new Rect2(rect.Position + new Vector2(_step * (.15f + i * .23f), _step * .35f), small), new Color("1b2c39"));
        }
        if (tile.Terrain == "road")
        {
            var center = rect.GetCenter();
            foreach (var delta in new[] { new Cell(1, 0), new Cell(-1, 0), new Cell(0, 1), new Cell(0, -1) })
            {
                var next = Game.City.FirstOrDefault(t => t.X == cell.X + delta.X && t.Y == cell.Y + delta.Y);
                if (next is not null && next.Terrain != "block") DrawLine(center, center + new Vector2(delta.X, delta.Y) * _step * .5f, new Color("365364"), 8, true);
            }
        }
        if (Route.Contains(cell) && !tile.Blocked)
        {
            DrawRect(rect.Grow(-2), new Color(.92f, .75f, .45f, .14f));
            DrawCircle(rect.Position + new Vector2(10, 10), 3, GridMain.Gold);
        }
        if (tile.SiteId.Length > 0)
        {
            var id = tile.SiteId switch { "hq" => "node-hq", "hotel" => "node-hotel", "hospital" => "node-hospital", _ => "node-source" };
            float icon = _step * .52f;
            DrawTextureRect(ArtAssets.Texture(id), new Rect2(rect.GetCenter().X - icon / 2, rect.Position.Y + 7, icon, icon), false);
            Text(tile.Name, new Vector2(rect.GetCenter().X, rect.End.Y - 12), Math.Max(13, (int)(_step * .17f)), GridMain.Ink, true);
            if (tile.Operating) DrawCircle(rect.Position + new Vector2(10, 10), 5, GridMain.Teal);
        }
        if (tile.Blocked)
        {
            DrawLine(rect.Position + new Vector2(15, 15), rect.End - new Vector2(15, 15), GridMain.Red, 4, true);
            DrawLine(new(rect.End.X - 15, rect.Position.Y + 15), new(rect.Position.X + 15, rect.End.Y - 15), GridMain.Red, 4, true);
        }
    }
    private void DrawFieldCell(Cell cell, Rect2 rect)
    {
        var field = Game.Field!; var tile = field.Tile(cell); if (tile is null) return;
        var fill = tile.Kind switch
        {
            "wall" => new Color("263240"), "exit" => new Color("1e4a48"), "clue" => new Color("293e55"),
            "chest" or "loot" => new Color("4a3e31"), "bell" => new Color("463e56"), "door" => new Color("4b3b31"), _ => new Color("172635")
        };
        if (tile.Kind != "wall" && field.LightUntil >= field.Round && cell.Distance(field.LightCell) <= 1) fill = fill.Lerp(new Color("9c8651"), .3f);
        DrawStyleBox(GridMain.Box(fill, new Color("304451")), rect);
        if (tile.Kind == "wall")
        {
            DrawLine(rect.Position + new Vector2(7, rect.Size.Y / 2), rect.Position + new Vector2(rect.Size.X - 7, rect.Size.Y / 2), new Color("354350"), 2);
            return;
        }
        if (SelectedCard.Length > 0 && SelectedHero.Length > 0)
        {
            var card = Catalog.Card(SelectedCard);
            if (card.Target is not ("self" or "end" or "exit") && Engine.Preview(SelectedHero, SelectedCard, cell).Success)
                DrawRect(rect.Grow(-2), new Color(.38f, .82f, .73f, .17f));
        }
        string icon = tile.Kind switch { "exit" => "出口", "clue" => tile.Used ? "已查" : "线索", "chest" => "箱柜", "loot" => tile.ItemId.Length > 0 ? "物资" : "空柜", "bell" => "铃", "door" => "门", _ => tile.ItemId.Length > 0 ? "遗落" : "" };
        if (icon.Length > 0) Text(icon, rect.GetCenter() + new Vector2(0, 6), (int)Math.Clamp(_step * .24f, 14, 22), tile.Used && tile.Kind == "clue" ? GridMain.Muted : GridMain.Gold, true);
        if (field.LightUntil >= field.Round && field.LightCell == cell)
        {
            DrawCircle(rect.GetCenter(), _step * .16f, new Color(.95f, .83f, .4f, .28f));
            Text("灯", rect.Position + new Vector2(_step - 15, 20), 14, GridMain.Gold, true);
        }
    }
    private void DrawActor(ActorState actor)
    {
        var hero = Game.Hero(actor.HeroId); var def = Catalog.Hero(actor.HeroId); var rect = Rect(actor.Cell); var c = rect.GetCenter();
        DrawCircle(c, _step * .31f, new Color("0c1824")); DrawArc(c, _step * .31f, 0, Mathf.Tau, 32, new Color(def.Color), actor.HeroId == SelectedHero ? 4 : 2, true);
        Text(hero.Name, c + new Vector2(0, 5), (int)Math.Clamp(_step * .23f, 14, 22), new Color(def.Color), true);
        float barWidth = _step * .65f;
        DrawRect(new(c.X - barWidth / 2, rect.End.Y - 9, barWidth, 4), new Color("35404d"));
        DrawRect(new(c.X - barWidth / 2, rect.End.Y - 9, barWidth * hero.Health / Math.Max(1, hero.MaxHealth), 4), new Color(def.Color));
        Text(actor.Actions.ToString(), new(rect.End.X - 10, rect.Position.Y + 16), 14, GridMain.Gold, true);
    }
    private void DrawEnemy(EnemyState enemy)
    {
        var rect = Rect(enemy.Cell); var c = rect.GetCenter(); var body = new Rect2(c - new Vector2(_step * .30f, _step * .28f), new Vector2(_step * .60f, _step * .56f));
        DrawStyleBox(GridMain.Box(new("442634"), enemy.IsObjective ? GridMain.Gold : GridMain.Red, 2), body);
        string token = enemy.Kind switch { "watcher" => "目", "hunger" => "食", _ => "影" };
        Text(token, c + new Vector2(0, 7), (int)Math.Clamp(_step * .30f, 17, 26), GridMain.Red, true);
        Text(enemy.Health.ToString(), new(rect.End.X - 12, rect.Position.Y + 17), 15, GridMain.Ink, true);
        if (enemy.MarkedUntil >= Game.Field!.Round) Text("◇", new(rect.Position.X + 12, rect.Position.Y + 17), 17, GridMain.Teal, true);
        if (enemy.StunnedUntil >= Game.Field.Round) Text("停", new(c.X, rect.End.Y - 6), 13, GridMain.Gold, true);
    }
    private string Describe(Cell cell)
    {
        if (CityMode)
        {
            var tile = Game.City.FirstOrDefault(t => t.Cell == cell); return tile is null ? "" : $"{cell} · {tile.Name}\n{tile.Description}";
        }
        var field = Game.Field; var t = field?.Tile(cell); if (t is null) return "";
        var enemy = field!.Enemies.FirstOrDefault(e => e.Alive && e.Cell == cell);
        if (enemy is not null) return enemy.Name + $" · 体力 {enemy.Health}/{enemy.MaxHealth}\n" + Engine.EnemyIntent(enemy);
        var actor = field.Actors.FirstOrDefault(a => a.Cell == cell);
        if (actor is not null) return Game.Hero(actor.HeroId).Name + " · " + cell;
        return cell + " · " + (t.Label.Length > 0 ? t.Label : t.Kind == "wall" ? "墙壁" : "通行地面");
    }
    private void Text(string text, Vector2 point, int size, Color color, bool center = false)
    {
        if (center) point.X -= _font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X / 2;
        DrawString(_font, point, text, HorizontalAlignment.Left, -1, size, color);
    }
}
