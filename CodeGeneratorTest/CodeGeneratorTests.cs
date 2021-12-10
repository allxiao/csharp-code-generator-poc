using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace CodeGeneratorTest;

public class CodeGeneratorTests
{
    [Test]
    public async Task Test1()
    {
        // Create the 'input' compilation that the generator will act on
        Compilation inputCompilation = CreateCompilation(@"
namespace MyCode
{
    public class Program
    {
        public static void Main(string[] args)
        {
        }
    }
}");

        // directly create an instance of the generator
        // (Note: in the compiler this is loaded from an assembly, and created via reflection at runtime)
        MySourceGenerator generator = new MySourceGenerator();

        // Create the driver that will control the generation, passing in our generator
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        // Run the generation pass
        // (Note: the generator driver itself is immutable, and all calls return an updated version of the driver that you should use for subsequent calls)
        driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation,
            out var diagnostics);

        // We can now assert things about the resulting compilation:
        Debug.Assert(diagnostics.IsEmpty); // there were no diagnostics created by the generators
        Debug.Assert(outputCompilation.SyntaxTrees.Count() ==
                     2); // we have two syntax trees, the original 'user' provided one, and the one added by the generator
        var diags = outputCompilation.GetDiagnostics();
        Debug.Assert(outputCompilation.GetDiagnostics()
            .IsEmpty); // verify the compilation with the added source has no diagnostics

        // Or we can look at the results directly:
        GeneratorDriverRunResult runResult = driver.GetRunResult();

        // The runResult contains the combined results of all generators passed to the driver
        Debug.Assert(runResult.GeneratedTrees.Length == 1);
        Debug.Assert(runResult.Diagnostics.IsEmpty);

        // Or you can access the individual results on a by-generator basis
        GeneratorRunResult generatorResult = runResult.Results[0];
        Debug.Assert(generatorResult.Generator == generator);
        Debug.Assert(generatorResult.Diagnostics.IsEmpty);
        Debug.Assert(generatorResult.GeneratedSources.Length == 1);
        Debug.Assert(generatorResult.Exception is null);
    }

    private static Compilation CreateCompilation(string source)
    {
        return CSharpCompilation.Create("compilation",
            new[] { CSharpSyntaxTree.ParseText(source) },
            GetGlobalReferences(),
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));
    }

    private static MetadataReference[] GetGlobalReferences()
    {
        var returnList = new List<MetadataReference>();

        var missingTypes = new[]
        {
            typeof(object),
            typeof(Console),
        };
        foreach (var t in missingTypes)
        {
            var location = t.Assembly.Location;
            returnList.Add(MetadataReference.CreateFromFile(location));
        }

        //The location of the .NET assemblies
        var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        // Adding some necessary .NET assemblies
        // These assemblies couldn't be loaded correctly via the same construction as above,
        // in specific the System.Runtime.
        var missingAssembly = new[]
        {
            "mscorlib.dll",
            "System.dll",
            "System.Core.dll",
            "System.Runtime.dll",
        };
        foreach (var assemblyFile in missingAssembly)
        {
            returnList.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, assemblyFile)));
        }

        return returnList.ToArray();
    }
}
