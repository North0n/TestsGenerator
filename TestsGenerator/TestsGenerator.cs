using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace TestsGenerator
{
    public class TestsGenerator
    {
        public List<string> Generate(string source)
        {
            var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
            
            var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>();
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            var classesDeclarations = classes.Select(classDeclaration => GenerateTestsClass(classDeclaration))
                .ToList();
            
            return classesDeclarations.Select(decl =>
                CompilationUnit().WithUsings(new SyntaxList<UsingDirectiveSyntax>(usings)).AddMembers(decl)
                    .NormalizeWhitespace().ToFullString()).ToList();
        }

        private MemberDeclarationSyntax GenerateTestsClass(ClassDeclarationSyntax classDeclaration)
        {
            SyntaxNode? current = classDeclaration;
            var testClassDeclaration = ClassDeclaration(classDeclaration.Identifier.Text + "Tests")
                .AddModifiers(Token(SyntaxKind.PublicKeyword));
            var isFirstNamespace = true;
            var currentTree = current;
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
                current = current.Parent;
            }

            return (MemberDeclarationSyntax)currentTree;
        }
    }
}
