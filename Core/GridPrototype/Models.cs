using System;
using System.Collections.Generic;
using System.Linq;

namespace Joy.Core.GridPrototype;

public readonly record struct Cell(int X, int Y)
{
    public int Distance(Cell other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
    public override string ToString() => $"{(char)('A' + X)}{Y + 1}";
}
public sealed class GridGame
{
    public int Version { get; set; } = 2;
    public int Seed { get; set; } = 413;
    public uint RandomState { get; set; } = 413;
    public int Day { get; set; } = 1;
    public int MaxDays { get; set; } = 7;
    public int Money { get; set; } = 16;
    public int SealCharges { get; set; } = 3;
    public List<HeroState> Heroes { get; set; } = new();
    public List<AssetState> Assets { get; set; } = new();
    public List<CityTile> City { get; set; } = new();
    public List<string> SelectedHeroes { get; set; } = new() { "mapper", "warden" };
    public string SelectedAsset { get; set; } = "";
    public FieldState? Field { get; set; }
    public string Phase { get; set; } = "city";
    public string Outcome { get; set; } = "";
    public string LastReport { get; set; } = "选择两名外勤，其余角色留守公司。完成三个地点，并建立两个营业网点。";
    public List<string> Journal { get; set; } = new();
    public HeroState Hero(string id) => Heroes.Single(h => h.Id == id);
    public CityTile Site(string id) => City.Single(t => t.SiteId == id);
}
public sealed class HeroState
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public int Health { get; set; } = 10;
    public int MaxHealth { get; set; } = 10;
    public int SkillPoints { get; set; } = 2;
    public List<string> LearnedTalents { get; set; } = new();
    public string Specialization { get; set; } = "";
    public int SkillLevel { get; set; }
    public List<string> Deck { get; set; } = new();
}
public sealed class AssetState
{
    public string Id { get; set; } = "";
    public int Charges { get; set; } = 2;
    public int MaxCharges { get; set; } = 2;
}
public sealed class CityTile
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Terrain { get; set; } = "block";
    public string SiteId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Cleared { get; set; }
    public bool Operating { get; set; }
    public bool Blocked { get; set; }
    public Cell Cell => new(X, Y);
}
public sealed class FieldState
{
    public string SiteId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Width { get; set; } = 9;
    public int Height { get; set; } = 7;
    public int Round { get; set; } = 1;
    public int RoundLimit { get; set; } = 14;
    public List<FieldTile> Tiles { get; set; } = new();
    public List<ActorState> Actors { get; set; } = new();
    public List<EnemyState> Enemies { get; set; } = new();
    public List<string> Cargo { get; set; } = new();
    public int Loot { get; set; }
    public int Capacity { get; set; } = 3;
    public bool EvidenceKnown { get; set; }
    public string Evidence { get; set; } = "";
    public string RuleHint { get; set; } = "";
    public bool ObjectiveDone { get; set; }
    public string AssetId { get; set; } = "";
    public int Shield { get; set; }
    public int LightUntil { get; set; }
    public Cell LightCell { get; set; } = new(-1, -1);
    public Cell Anchor { get; set; } = new(0, 3);
    public List<string> Log { get; set; } = new();
    public FieldTile? Tile(Cell cell) => Tiles.FirstOrDefault(t => t.X == cell.X && t.Y == cell.Y);
    public ActorState Actor(string id) => Actors.Single(a => a.HeroId == id);
    public bool InCombat => Enemies.Any(e => e.Alive && e.Alert);
}
public sealed class FieldTile
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Kind { get; set; } = "floor";
    public string Label { get; set; } = "";
    public bool Used { get; set; }
    public string ItemId { get; set; } = "";
    public Cell Cell => new(X, Y);
}
public sealed class ActorState
{
    public string HeroId { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Actions { get; set; } = 2;
    public int Block { get; set; }
    public int DamageBonus { get; set; }
    public List<string> Draw { get; set; } = new();
    public List<string> Hand { get; set; } = new();
    public List<string> Discard { get; set; } = new();
    public Cell Cell => new(X, Y);
}
public sealed class EnemyState
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "shadow";
    public int X { get; set; }
    public int Y { get; set; }
    public int Health { get; set; } = 4;
    public int MaxHealth { get; set; } = 4;
    public bool Alive { get; set; } = true;
    public bool Alert { get; set; }
    public int MarkedUntil { get; set; }
    public int StunnedUntil { get; set; }
    public Cell Lure { get; set; } = new(-1, -1);
    public string CapturableAsset { get; set; } = "";
    public bool IsObjective { get; set; }
    public Cell Cell => new(X, Y);
}
public sealed record CardDef(string Id, string Name, string Owner, int Cost, int Range, string Target, string Description, string Art = "card-investigate");
public sealed record HeroDef(string Id, string Name, string Role, string Color, string CompanyEffect, string BranchA, string BranchAText, string BranchB, string BranchBText);
public sealed record AssetDef(string Id, string Name, string Description, string CompanyEffect, string FieldCard, string Art);
public sealed record Result(bool Success, string Message)
{
    public static Result Ok(string message) => new(true, message);
    public static Result Fail(string message) => new(false, message);
}
