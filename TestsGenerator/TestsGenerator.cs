﻿using System.Text;
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
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Where(node => node.Modifiers.Any(n => n.Kind() == SyntaxKind.PublicKeyword)).ToList();

            var classesDeclarations = classes.Select(GenerateTestsClassWithParents).ToList();

            return classesDeclarations.Select(classData => new ClassInfo(classData.ClassName,
                CompilationUnit()
                    .WithUsings(new SyntaxList<UsingDirectiveSyntax>(usings)
                        .Add(UsingDirective(QualifiedName(IdentifierName("NUnit"), IdentifierName("Framework"))))
                        .Add(UsingDirective(IdentifierName("Moq"))))
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
            var methods = classDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(node => node.Modifiers.Any(n => n.Kind() == SyntaxKind.PublicKeyword)).ToList();
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

            var setupDecl = GenerateSetUp(classDeclaration);
            var classDecl = ClassDeclaration(classDeclaration.Identifier.Text + "Tests")
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .WithMembers(new SyntaxList<MemberDeclarationSyntax>(setupDecl.Concat(testMethods)))
                .WithAttributeLists(
                    SingletonList(AttributeList(SingletonSeparatedList(Attribute(IdentifierName("TestFixture"))))));
            return classDecl;
        }

        private List<MemberDeclarationSyntax> GenerateSetUp(ClassDeclarationSyntax classDeclaration)
        {
            var constructors = classDeclaration.DescendantNodes().OfType<ConstructorDeclarationSyntax>()
                .OrderBy(c => c.ParameterList.Parameters.Count).ToList();

            // TODO make field names _bebraClass instead of _BebraClass
            var members = new List<MemberDeclarationSyntax>
            {
                // Declaration of private field <ClassName> _<className>;
                FieldDeclaration(VariableDeclaration(IdentifierName(classDeclaration.Identifier.Text))
                        .WithVariables(
                            SingletonSeparatedList(
                                VariableDeclarator(Identifier($"_{classDeclaration.Identifier.Text}")))))
                    .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword)))
            };
            
            var setupBodyBlock = new List<StatementSyntax>();
            var classConstructorArguments = new List<SyntaxNodeOrToken>();
            if (constructors.Count != 0)
            {
                foreach (var parameter in constructors[0].ParameterList.Parameters)
                {
                    if (parameter.Type!.ToString().StartsWith("I"))
                    {
                        members.Add(FieldDeclaration(VariableDeclaration(IdentifierName(parameter.Type!.ToString()))
                                .WithVariables(SingletonSeparatedList(
                                    VariableDeclarator(Identifier($"_{parameter.Identifier.Text}")))))
                            .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword))));

                        setupBodyBlock.Add(ExpressionStatement(AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName($"_{parameter.Identifier.Text}"),
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                ObjectCreationExpression(GenericName(Identifier("Mock"))
                                        .WithTypeArgumentList(TypeArgumentList(
                                            SingletonSeparatedList<TypeSyntax>(
                                                IdentifierName(parameter.Type.ToString())))))
                                    .WithArgumentList(ArgumentList(SingletonSeparatedList(
                                        Argument(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                            IdentifierName("MockBehavior"), IdentifierName("Strict")))))),
                                IdentifierName("Object")))));
                        classConstructorArguments.Add(Argument(IdentifierName($"_{parameter.Identifier.Text}")));
                    }
                    else
                    {
                        setupBodyBlock.Add(LocalDeclarationStatement(
                            VariableDeclaration(parameter.Type).WithVariables(
                                SingletonSeparatedList(VariableDeclarator(Identifier(parameter.Identifier.Text))
                                    .WithInitializer(EqualsValueClause(LiteralExpression(
                                        SyntaxKind.DefaultLiteralExpression, Token(SyntaxKind.DefaultKeyword))))))));
                        classConstructorArguments.Add(Argument(IdentifierName(parameter.Identifier.Text)));
                    }

                    classConstructorArguments.Add(Token(SyntaxKind.CommaToken));
                }
                
                // Last argument would be comma, so we remove it
                if (classConstructorArguments.Count != 0)
                {
                    classConstructorArguments.RemoveAt(classConstructorArguments.Count - 1);
                }
            }

            setupBodyBlock.Add(ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                IdentifierName($"_{classDeclaration.Identifier.Text}"),
                ObjectCreationExpression(IdentifierName(classDeclaration.Identifier))
                    .WithArgumentList(ArgumentList(SeparatedList<ArgumentSyntax>(classConstructorArguments))))));
            members.Add(MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier("Setup"))
                .WithAttributeLists(
                    SingletonList(AttributeList(SingletonSeparatedList(Attribute(IdentifierName("SetUp"))))))
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithBody(Block(setupBodyBlock)));

            return members;
        }
    }
}