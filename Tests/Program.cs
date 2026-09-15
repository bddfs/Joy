using Joy.Tests;

int passed = 0, failed = 0;
void Check(bool condition, string description)
{
    if (condition) passed++;
    else { failed++; Console.WriteLine("FAIL | " + description); }
}
foreach (var suite in new (string Name, Action<Action<bool, string>> Run)[]
{
    ("City", CityTests.Run), ("M2 City", M2CityTests.Run), ("Field", FieldTests.Run), ("M2 Field", M2FieldTests.Run), ("Save", SaveTests.Run), ("Integration", IntegrationTests.Run), ("M2 integration", M2IntegrationTests.Run)
})
{
    int before = passed;
    try { suite.Run(Check); Console.WriteLine($"{suite.Name} | {passed - before} checks passed"); }
    catch (Exception exception) { failed++; Console.WriteLine($"FAIL | {suite.Name} | {exception}"); }
}
Console.WriteLine($"JOY_TESTS | {passed} passed | {failed} failed");
int witnessIndex = Array.IndexOf(args, "--witness");
if (failed == 0 && witnessIndex >= 0 && witnessIndex + 1 < args.Length)
    File.WriteAllText(args[witnessIndex + 1], System.Text.Json.JsonSerializer.Serialize(M2IntegrationTests.Witnesses, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
return failed == 0 ? 0 : 1;
