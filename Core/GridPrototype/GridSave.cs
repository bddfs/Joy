using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Joy.Core.GridPrototype;

/// <summary>Independent prototype save; never reads or writes the M2 profile.</summary>
public static class GridSave
{
    private sealed class Envelope
    {
        public int Format { get; set; } = 1;
        public string Payload { get; set; } = "";
        public string Hash { get; set; } = "";
    }
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };
    public static Result Save(string path, GridGame game)
    {
        string temporary = path + ".tmp";
        try
        {
            Validate(game);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            string payload = JsonSerializer.Serialize(game, Options);
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Envelope { Payload = payload, Hash = Hash(payload) }, Options));
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak", true);
            else File.Move(temporary, path);
            return Result.Ok("进度已保存");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException)
        {
            return Result.Fail("保存失败，上一份进度仍保留：" + e.Message);
        }
    }
    public static GridGame? Load(string path, out string message)
    {
        message = "还没有此原型的存档。";
        if (!File.Exists(path)) return null;
        try { var game = Read(path); message = "已恢复上次完整操作。"; return game; }
        catch (NotSupportedException e) { message = e.Message; return null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException)
        {
            message = "主存档无法读取：" + e.Message;
            if (File.Exists(path + ".bak"))
                try { var game = Read(path + ".bak"); message = "主存档损坏，已恢复上一份备份。"; return game; }
                catch (Exception backup) when (backup is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException or NotSupportedException)
                { message += "；备份也无法读取。"; }
            return null;
        }
    }
    private static GridGame Read(string path)
    {
        if (new FileInfo(path).Length > 4_000_000) throw new InvalidDataException("存档大小异常。");
        var wrapper = JsonSerializer.Deserialize<Envelope>(File.ReadAllText(path, Encoding.UTF8), Options)
            ?? throw new InvalidDataException("存档为空。");
        if (wrapper.Format != 1) throw new NotSupportedException("此存档来自其他版本，未覆盖原文件。");
        if (wrapper.Hash != Hash(wrapper.Payload)) throw new InvalidDataException("校验不一致。");
        var game = JsonSerializer.Deserialize<GridGame>(wrapper.Payload, Options) ?? throw new InvalidDataException("没有游戏状态。");
        if (game.Version != 1) throw new NotSupportedException("此进度版本尚不支持，未覆盖原文件。");
        Validate(game);
        return game;
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static void Validate(GridGame game)
    {
        if (game.Version != 1 || game.Day < 1 || game.Day > 100 || game.Money < 0 || game.Money > 100000 || game.SealCharges < 0)
            throw new InvalidDataException("经营状态无效。");
        if (game.Phase is not ("city" or "field" or "victory" or "defeat") || (game.Phase == "field") != (game.Field is not null))
            throw new InvalidDataException("当前阶段与现场记录不一致。");
        if (game.Heroes is null || game.City is null || game.Assets is null || game.SelectedHeroes is null || game.Journal is null ||
            game.Heroes.Count != 3 || game.Heroes.Select(h => h.Id).Distinct().Count() != 3 ||
            game.Heroes.Any(h => h.Id is not ("mapper" or "engineer" or "warden") || h.Health < 0 || h.Health > h.MaxHealth || h.MaxHealth <= 0 || h.SkillPoints < 0 || h.Deck is null))
            throw new InvalidDataException("角色记录无效。");
        if (game.City.Count != 35 || game.City.Any(t => t.X < 0 || t.X >= 7 || t.Y < 0 || t.Y >= 5) || game.City.Select(t => t.Cell).Distinct().Count() != 35)
            throw new InvalidDataException("城市地块无效。");
        if (game.Assets.Any(a => a is null) || game.Assets.Select(a => a.Id).Distinct().Count() != game.Assets.Count || game.Assets.Any(a => !Catalog.Assets.Any(d => d.Id == a.Id) || a.Charges < 0 || a.Charges > a.MaxCharges || a.MaxCharges <= 0))
            throw new InvalidDataException("收容物记录无效。");
        if (!string.IsNullOrEmpty(game.SelectedAsset) && !game.Assets.Any(a => a.Id == game.SelectedAsset))
            throw new InvalidDataException("随队收容物不存在。");
        foreach (var hero in game.Heroes)
        {
            if (hero.Specialization is not ("" or "a" or "b") || hero.SkillLevel < 0 || hero.SkillLevel > 2 ||
                (hero.Specialization == "") != (hero.SkillLevel == 0) || hero.Deck.Count < 6 || hero.Deck.Count > 10 ||
                hero.Deck.GroupBy(id => id).Any(g => g.Count() > 2) || hero.Deck.Any(id => !Catalog.Cards.Any(c => c.Id == id && c.Owner == hero.Id)))
                throw new InvalidDataException("专精或个人牌组无效。");
        }
        if (game.SelectedHeroes.Count > 2 || game.SelectedHeroes.Distinct().Count() != game.SelectedHeroes.Count || game.SelectedHeroes.Any(id => !game.Heroes.Any(h => h.Id == id)))
            throw new InvalidDataException("队伍配置无效。");
        if (game.Field is { } f)
        {
            if (f.Width != 9 || f.Height != 7 || f.Tiles is null || f.Actors is null || f.Enemies is null || f.Cargo is null || f.Log is null ||
                f.Tiles.Count != 63 || f.Tiles.Select(t => t.Cell).Distinct().Count() != 63 || f.Round < 1 || f.Round > 100 || f.Actors.Count != 2 || f.Actors.Select(a => a.HeroId).Distinct().Count() != 2)
                throw new InvalidDataException("现场记录无效。");
            foreach (var a in f.Actors)
                if (!game.Heroes.Any(h => h.Id == a.HeroId) || a.X < 0 || a.X >= f.Width || a.Y < 0 || a.Y >= f.Height || a.Actions < 0 || a.Actions > 20 ||
                    a.Hand is null || a.Draw is null || a.Discard is null ||
                    a.Hand.Concat(a.Draw).Concat(a.Discard).Any(id => !Catalog.Cards.Any(c => c.Id == id && c.Owner == a.HeroId)))
                    throw new InvalidDataException("现场队员状态无效。");
            if (f.Enemies.Select(e => e.Id).Distinct().Count() != f.Enemies.Count || f.Enemies.Any(e => e.X < 0 || e.X >= f.Width || e.Y < 0 || e.Y >= f.Height))
                throw new InvalidDataException("现场目标状态无效。");
        }
    }
}
