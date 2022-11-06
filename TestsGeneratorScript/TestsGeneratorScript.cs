namespace TestsGeneratorScript;

internal static class TestsGeneratorScript
{
    public static async Task Main(string[] args)
    {
        if (args.Length != 5)
        {
            Console.WriteLine("Not enough arguments. Usage: <input files separated with \"|\"> <output directory> " +
                              "<degree of parallelism READ> <degree of parallelism GENERATE> <degree of parallelism WRITE>");
            return;
        }
        
        var inputFiles = args[0].Split('|');
        var outputDirectory = args[1];

        if (!int.TryParse(args[2], out var degreeOfParallelismRead))
        {
            Console.WriteLine($"Invalid degree of parallelism READ. Expected integer, got {args[2]}");
            return;
        }
        if (!int.TryParse(args[3], out var degreeOfParallelismGenerate))
        {
            Console.WriteLine($"Invalid degree of parallelism GENERATE. Expected integer, got {args[3]}");
            return;
        }
        if (!int.TryParse(args[4], out var degreeOfParallelismWrite))
        {
            Console.WriteLine($"Invalid degree of parallelism WRITE. Expected integer, got {args[4]}");
            return;
        }

        var testsGeneratorService = new TestsGeneratorService(degreeOfParallelismRead, degreeOfParallelismGenerate,
            degreeOfParallelismWrite, outputDirectory);
        await testsGeneratorService.Generate(inputFiles.ToList());
    }
}
