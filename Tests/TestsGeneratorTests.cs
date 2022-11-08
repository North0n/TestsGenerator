using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;

namespace TestsGenerator.Tests;

public class TestsGeneratorTests
{
    private readonly TestsGenerator _generator = new();

    [Test]
    public void ClassesCountTest()
    {
        var classTests = _generator.Generate(ProgramText1 + ProgramText2 + ProgramText3);
        Assert.That(classTests, Has.Count.EqualTo(3));

        foreach (var classTest in classTests)
        {
            var parsedClass = CSharpSyntaxTree.ParseText(classTest.TestsFile).GetCompilationUnitRoot();
            Assert.That(parsedClass.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList(), Has.Count.EqualTo(1));   
        }
    }

    [Test]
    public void GeneratedMethodsCountTest()
    {
        var classTests = _generator.Generate(ProgramText1);
        Assert.That(classTests, Has.Count.EqualTo(1));
        var parsedClass = CSharpSyntaxTree.ParseText(classTests[0].TestsFile).GetCompilationUnitRoot();
        var methods = parsedClass.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
        Assert.That(methods, Has.Count.EqualTo(2));
        
        classTests = _generator.Generate(ProgramText2);
        Assert.That(classTests, Has.Count.EqualTo(1));
        parsedClass = CSharpSyntaxTree.ParseText(classTests[0].TestsFile).GetCompilationUnitRoot();
        methods = parsedClass.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
        Assert.That(methods, Has.Count.EqualTo(3));
        
        classTests = _generator.Generate(ProgramText3);
        Assert.That(classTests, Has.Count.EqualTo(1));
        parsedClass = CSharpSyntaxTree.ParseText(classTests[0].TestsFile).GetCompilationUnitRoot();
        methods = parsedClass.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
        Assert.That(methods, Has.Count.EqualTo(3));
    }

    [Test]
    public void OverloadedMethodsTest()
    {
        var classTests = _generator.Generate(ProgramText2);
        Assert.That(classTests, Has.Count.EqualTo(1));
        var parsedClass = CSharpSyntaxTree.ParseText(classTests[0].TestsFile).GetCompilationUnitRoot();
        var methods = parsedClass.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
        
        // The first one is setup method
        Assert.That(methods, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(methods[1].Identifier.Text, Is.EqualTo("Calculate0Test"));
            Assert.That(methods[2].Identifier.Text, Is.EqualTo("Calculate1Test"));
        });
    }

    [Test]
    public void SetupMethodTest()
    {
        var classTests = _generator.Generate(ProgramText3);
        Assert.That(classTests, Has.Count.GreaterThanOrEqualTo(1));
        var parsedClass = CSharpSyntaxTree.ParseText(classTests[0].TestsFile).GetCompilationUnitRoot();
        var methods = parsedClass.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
        Assert.That(methods, Has.Count.GreaterThanOrEqualTo(1));
        
        // Check existence of Setup method and its attributes
        var setupMethod = methods[0];
        Assert.That(setupMethod.Identifier.Text, Is.EqualTo("Setup"));
        Assert.That(setupMethod.Modifiers, Has.Count.EqualTo(1));
        Assert.That(setupMethod.AttributeLists, Has.Count.EqualTo(1));
        Assert.That(setupMethod.AttributeLists[0].Attributes, Has.Count.EqualTo(1));
        Assert.That(setupMethod.AttributeLists[0].Attributes[0].Name.ToString(), Is.EqualTo("SetUp"));
        
        // Check existence of field declarations for dependent interfaces and class itself
        var fieldDeclarations = parsedClass.DescendantNodes().OfType<FieldDeclarationSyntax>().ToList();
        Assert.That(fieldDeclarations, Has.Count.EqualTo(3)); // 2 interfaces and 1 class
        
        // Check method body
        var methodBody = setupMethod.Body;
        Assert.That(methodBody, Is.Not.Null);
        var statements = methodBody!.Statements;
        Assert.That(statements, Has.Count.EqualTo(4));
        Assert.Multiple(() =>
        {
            Assert.That(statements[0].ToString(), Is.EqualTo("_printable = new Mock<IPrintable>(MockBehavior.Strict).Object;"));
            Assert.That(statements[1].ToString(), Is.EqualTo("_bebrable = new Mock<IBebrable>(MockBehavior.Strict).Object;"));
            Assert.That(statements[2].ToString(), Is.EqualTo("int x = default;"));
            Assert.That(statements[3].ToString(), Is.EqualTo("_bebraClass = new BebraClass(_printable, _bebrable, x);"));
        });
    }

    [Test]
    public void StaticTestMethodBodyTest()
    {
        var classTests = _generator.Generate(ProgramText3);
        Assert.That(classTests, Has.Count.GreaterThanOrEqualTo(1));
        var parsedClass = CSharpSyntaxTree.ParseText(classTests[0].TestsFile).GetCompilationUnitRoot();
        var methods = parsedClass.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
        Assert.That(methods, Has.Count.GreaterThanOrEqualTo(2));

        var methodBody = methods[1].Body;
        Assert.That(methodBody, Is.Not.Null);
        var statements = methodBody!.Statements;
        Assert.That(statements, Has.Count.EqualTo(6));
        Assert.Multiple(() =>
        {
            Assert.That(statements[0].ToString(), Is.EqualTo("int a = default;"));
            Assert.That(statements[1].ToString(), Is.EqualTo("string str = default;"));
            Assert.That(statements[2].ToString(), Is.EqualTo("var actual = BebraClass.MultiplyString(a, str);"));
            Assert.That(statements[3].ToString(), Is.EqualTo("string expected = default;"));
            Assert.That(statements[4].ToString(), Is.EqualTo("Assert.That(actual, Is.EqualTo(expected));"));
            Assert.That(statements[5].ToString(), Is.EqualTo("Assert.Fail(\"autogenerated\");"));
        });
    }
    
    [Test]
    public void NonStaticTestMethodBodyTest()
    {
        var classTests = _generator.Generate(ProgramText1);
        Assert.That(classTests, Has.Count.GreaterThanOrEqualTo(1));
        var parsedClass = CSharpSyntaxTree.ParseText(classTests[0].TestsFile).GetCompilationUnitRoot();
        var methods = parsedClass.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
        Assert.That(methods, Has.Count.GreaterThanOrEqualTo(2));

        var methodBody = methods[1].Body;
        Assert.That(methodBody, Is.Not.Null);
        var statements = methodBody!.Statements;
        Assert.That(statements, Has.Count.EqualTo(4));
        Assert.Multiple(() =>
        {
            Assert.That(statements[0].ToString(), Is.EqualTo("var actual = _program.GetRandom();"));
            Assert.That(statements[1].ToString(), Is.EqualTo("int expected = default;"));
            Assert.That(statements[2].ToString(), Is.EqualTo("Assert.That(actual, Is.EqualTo(expected));"));
            Assert.That(statements[3].ToString(), Is.EqualTo("Assert.Fail(\"autogenerated\");"));
        });
    }
    
    [Test]
    public void VoidTestMethodBodyTest()
    {
        var classTests = _generator.Generate(ProgramText3);
        Assert.That(classTests, Has.Count.GreaterThanOrEqualTo(1));
        var parsedClass = CSharpSyntaxTree.ParseText(classTests[0].TestsFile).GetCompilationUnitRoot();
        var methods = parsedClass.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
        Assert.That(methods, Has.Count.GreaterThanOrEqualTo(3));

        var methodBody = methods[2].Body;
        Assert.That(methodBody, Is.Not.Null);
        var statements = methodBody!.Statements;
        Assert.That(statements, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(statements[0].ToString(), Is.EqualTo("int x = default;"));
            Assert.That(statements[1].ToString(), Is.EqualTo("_bebraClass.OuterMethod(x);"));
            Assert.That(statements[2].ToString(), Is.EqualTo("Assert.Fail(\"autogenerated\");"));
        });
    }

    [Test]
    public void NamespaceNameTest()
    {
        var classTests = _generator.Generate(ProgramText3);
        Assert.That(classTests, Has.Count.GreaterThanOrEqualTo(1));
        var parsedClass = CSharpSyntaxTree.ParseText(classTests[0].TestsFile).GetCompilationUnitRoot();
        var namespaceDeclaration = parsedClass.DescendantNodes().OfType<NamespaceDeclarationSyntax>().ToList();
        Assert.That(namespaceDeclaration, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(namespaceDeclaration[0].Name.ToString(), Is.EqualTo("Bebra.Bebra1"));
            Assert.That(namespaceDeclaration[1].Name.ToString(), Is.EqualTo("Bebra2.Tests"));
        });
    }

    private const string ProgramText1 = @"
        namespace HelloWorld
        {
            public class Program
            {
                private readonly Random _random;
                
                public Program(int seed)
                {
                    _random = new Random(seed);
                }
                
                static void Main(string[] args)
                {
                    Console.WriteLine(""Hello, World!"");
                }
             
                public int GetRandom()
                {
                    return _random.Next(1, 100);
                }
            }
        }";

    private const string ProgramText2 = @"
        namespace SecondNs.Second
        {
            namespace InnerNs
            {
                public class Program
                {
                    static void Main(string[] args)
                    {
                        Console.WriteLine(""Hello, World!"");
                    }

                    public double Calculate(double x)
                    {
                        return Math.Sqrt(x) * 9 - 42;
                    }

                    public int Calculate(int x)
                    {
                        return 42 * x;
                    }
                }
            }
        }";
    
    private const string ProgramText3 = @"
        namespace Interfaces
        {
            public interface IPrintable
            {
                void Print();
            }

            public interface IBebrable
            {
                void DoBebra();
            }
        }

        namespace Bebra.Bebra1
        {
            namespace Bebra2 
            {
                using Interfaces;
                public class BebraClass 
                {
                    private IPrintable _printable;
                    private IBebrable _bebrable;
                    
                    public BebraClass(IPrintable printable, IBebrable bebrable, int x)
                    {
                        _printable = printable;
                        _bebrable  = bebrable;
                    }

                    public static string MultiplyString(int a, string str)
                    {
                        string result = "";
                        for (int i = 0; i < a; i++)
                        {
                            result += str;
                        }
                        return result;
                    }

                    public void OuterMethod(int x)
                    {
                        Console.WriteLine($""{x}{x}{x}tentacion"");
                    }

                    private int InnerBebraMethod()
                    {
                        return 42;
                    }
                }
            }
        }";
}