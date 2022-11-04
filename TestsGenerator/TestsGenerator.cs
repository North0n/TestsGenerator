using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace TestsGenerator
{
    public class TestsGenerator
    {
        public class ClassInfo
        {
            public string ClassName { get; }
            public string TestsFile { get; }

            public ClassInfo(string className, string testsFile)
            {
                ClassName = className;
                TestsFile = testsFile;
            }
        }

        private class ClassData
        {
            public string ClassName { get; }
            public MemberDeclarationSyntax TestClassDeclarationSyntax { get; }

            public ClassData(string className, MemberDeclarationSyntax testClassDeclarationSyntax)
            {
                ClassName = className;
                TestClassDeclarationSyntax = testClassDeclarationSyntax;
            }
        }
        
        public List<ClassInfo> Generate(string source)
        {
            var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();

            var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>();
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            var classesDeclarations = classes
                .Select(classDeclaration => GenerateTestsClassWithParents(classDeclaration))
                .ToList();

            return classesDeclarations.Select(classData => new ClassInfo(classData.ClassName,
                CompilationUnit()
                    .WithUsings(new SyntaxList<UsingDirectiveSyntax>(usings)
                        .Add(UsingDirective(QualifiedName(IdentifierName("NUnit"), IdentifierName("Framework")))))
                    .AddMembers(classData.TestClassDeclarationSyntax)
                    .NormalizeWhitespace().ToFullString())).ToList();
        }

        private ClassData GenerateTestsClassWithParents(ClassDeclarationSyntax classDeclaration)
        {
            SyntaxNode? current = classDeclaration;
            var testClassDeclaration = GenerateTestsClass(classDeclaration);
            var isFirstNamespace = true;
            var currentTree = current;
            var stringBuilder = new StringBuilder(testClassDeclaration.Identifier.Text);
            while (current.Parent is NamespaceDeclarationSyntax parent)
            {
                NamespaceDeclarationSyntax ns;
                if (isFirstNamespace)
                {
                    ns = NamespaceDeclaration(IdentifierName(parent.Name + ".Tests"))
                        .WithMembers(new SyntaxList<MemberDeclarationSyntax>(testClassDeclaration));
                    isFirstNamespace = false;
                }
                else
                {
                    ns = NamespaceDeclaration(parent.Name)
                        .WithMembers(new SyntaxList<MemberDeclarationSyntax>((NamespaceDeclarationSyntax)currentTree));
                }

                currentTree = ns;
                stringBuilder.Insert(0, $"{ns.Name}.");
                current = current.Parent;
            }

            return new ClassData(stringBuilder.ToString(), (MemberDeclarationSyntax)currentTree);
        }

        private ClassDeclarationSyntax GenerateTestsClass(ClassDeclarationSyntax classDeclaration)
        {
            var methods = classDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
            methods.Sort((method1, method2) =>
                string.Compare(method1.Identifier.Text, method2.Identifier.Text, StringComparison.Ordinal));

            var testMethods = new MemberDeclarationSyntax[methods.Count];
            var methodIndex = 0;
            for (var i = 0; i < methods.Count; ++i)
            {
                if (i != 0 && methods[i].Identifier.Text == methods[i - 1].Identifier.Text)
                {
                    methodIndex++;
                }
                else if (i != methods.Count - 1 && methods[i].Identifier.Text == methods[i + 1].Identifier.Text)
                {
                    methodIndex = 0;
                }
                else
                {
                    methodIndex = -1;
                }

                var suffix = methodIndex != -1 ? $"{methodIndex}" : "";
                var method = MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)),
                        Identifier($"{methods[i].Identifier.Text}{suffix}Test"))
                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword))).WithBody(Block())
                    .WithAttributeLists(
                        SingletonList(AttributeList(SingletonSeparatedList(Attribute(IdentifierName("Test"))))))
                    .WithBody(Block(
                        SingletonList<StatementSyntax>(
                            ExpressionStatement(
                                InvocationExpression(
                                        MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            IdentifierName("Assert"),
                                            IdentifierName("Fail")))
                                    .WithArgumentList(
                                        ArgumentList(
                                            SingletonSeparatedList(
                                                Argument(
                                                    LiteralExpression(
                                                        SyntaxKind.StringLiteralExpression,
                                                        Literal("autogenerated"))))))))));

                testMethods[i] = method;
            }

            var classDecl = ClassDeclaration(classDeclaration.Identifier.Text + "Tests")
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .WithMembers(new SyntaxList<MemberDeclarationSyntax>(testMethods))
                .WithAttributeLists(
                    SingletonList(AttributeList(SingletonSeparatedList(Attribute(IdentifierName("TestFixture"))))));
            return classDecl;
        }
    }
}