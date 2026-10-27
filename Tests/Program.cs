using Joy.Tests;

int talentFixtureIndex = Array.IndexOf(args, "--talent-ui-fixture");
if (talentFixtureIndex >= 0)
{
    if (talentFixtureIndex + 1 >= args.Length) { Console.WriteLine("Missing talent UI checkpoint output path."); return 1; }
    var checkpoint = GridWitness.CreateTalentUiCheckpoint();
    var save = Joy.Core.GridPrototype.GridSave.Save(args[talentFixtureIndex + 1], checkpoint);
    Console.WriteLine($"TALENT_UI_FIXTURE | {save.Success} | day {checkpoint.Day} | warden points {checkpoint.Hero("warden").SkillPoints} | {save.Message}");
    return save.Success ? 0 : 1;
}

int passed = 0, failed = 0;
void Check(bool condition, string description)
{
    if (condition) passed++;
    else { failed++; Console.WriteLine("FAIL | " + description); }
}
foreach (var suite in new (string Name, Action<Action<bool, string>> Run)[]
{
    ("City", CityTests.Run), ("M2 City", M2CityTests.Run), ("Field", FieldTests.Run), ("M2 Field", M2FieldTests.Run), ("Disturbance", DisturbanceTests.Run), ("Ward clip", WardClipTests.Run), ("Save", SaveTests.Run), ("Integration", IntegrationTests.Run), ("M2 integration", M2IntegrationTests.Run), ("Art presentation", ArtPresentationTests.Run), ("Grid Prototype", GridPrototypeTests.Run), ("Talent Trees", TalentsTests.Run)
})
{
    if (args.Contains("--grid-only") && suite.Name is not ("Grid Prototype" or "Talent Trees")) continue;
    if (args.Contains("--talents-only") && suite.Name != "Talent Trees") continue;
    int before = passed;
    try { suite.Run(Check); Console.WriteLine($"{suite.Name} | {passed - before} checks passed"); }
    catch (Exception exception) { failed++; Console.WriteLine($"FAIL | {suite.Name} | {exception}"); }
}
Console.WriteLine($"JOY_TESTS | {passed} passed | {failed} failed");
int witnessIndex = Array.IndexOf(args, "--witness");
if (failed == 0 && witnessIndex >= 0 && witnessIndex + 1 < args.Length)
    File.WriteAllText(args[witnessIndex + 1], System.Text.Json.JsonSerializer.Serialize(M2IntegrationTests.Witnesses, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
return failed == 0 ? 0 : 1;
