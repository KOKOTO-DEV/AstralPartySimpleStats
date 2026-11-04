foreach (var test in PlayerDetailsTests.Cases)
{
    await test.Run();
    Console.WriteLine("PASS " + test.Name);
}
Console.WriteLine(PlayerDetailsTests.Cases.Count + " stats regression checks passed.");
