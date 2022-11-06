using System.Threading.Tasks.Dataflow;

namespace TestsGeneratorScript;

public class TestsGeneratorService
{
    private readonly TestsGenerator.TestsGenerator _testsGenerator = new();

    private TransformBlock<string, string> _readerBlock;
    private TransformManyBlock<string, TestsGenerator.TestsGenerator.ClassInfo> _generatorBlock;
    private ActionBlock<TestsGenerator.TestsGenerator.ClassInfo> _writerBlock;

    public string SavePath { get; set; }
    public int DegreeOfParallelismRead { get; }
    public int DegreeOfParallelismGenerate { get; }
    public int DegreeOfParallelismWrite { get; }

    public TestsGeneratorService(int degreeOfParallelismRead, int degreeOfParallelismGenerate,
        int degreeOfParallelismWrite, string savePath = "")
    {
        DegreeOfParallelismRead = degreeOfParallelismRead;
        DegreeOfParallelismGenerate = degreeOfParallelismGenerate;
        DegreeOfParallelismWrite = degreeOfParallelismWrite;

        SavePath = savePath;

        _readerBlock = new TransformBlock<string, string>(async fileName =>
        {
            using var reader = File.OpenText(fileName);
            return await reader.ReadToEndAsync();
        }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = degreeOfParallelismRead });
        _generatorBlock =
            new TransformManyBlock<string, TestsGenerator.TestsGenerator.ClassInfo>(source =>
                    _testsGenerator.Generate(source),
                new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = degreeOfParallelismGenerate });
        _writerBlock = new ActionBlock<TestsGenerator.TestsGenerator.ClassInfo>(async classInfo =>
        {
            await using var writer = new StreamWriter(SavePath + "\\" + classInfo.ClassName + ".cs");
            await writer.WriteAsync(classInfo.TestsFile);
        }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = degreeOfParallelismWrite });

        _readerBlock.LinkTo(_generatorBlock, new DataflowLinkOptions { PropagateCompletion = true });
        _generatorBlock.LinkTo(_writerBlock, new DataflowLinkOptions { PropagateCompletion = true });
    }

    public async Task Generate(List<string> fileNames)
    {
        foreach (var fileName in fileNames)
        {
            _readerBlock.Post(fileName);
        }
        
        _readerBlock.Complete();
        await _writerBlock.Completion;
    }
}