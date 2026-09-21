using System.Runtime.Loader;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// 從專案根目錄編譯真實來源，使用 Unity 已匯入的組件作為參考。
var ns = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003");
MetadataReference runtime = null;
var assemblies = new Dictionary<string, byte[]>();
var dependencyPaths = new Dictionary<string, string>();
foreach (string name in new[] { "Assembly-CSharp", "Assembly-CSharp-Editor" })
{
    string projectPath = name + ".csproj";
    if (!File.Exists(projectPath))
        throw new FileNotFoundException("請先在 Unity 產生專案檔，並從專案根目錄執行。", projectPath);
    var doc = XDocument.Load(projectPath);
    bool isEditor = name.EndsWith("Editor");
    var files = doc.Descendants(ns + "Compile").Select(x => x.Attribute("Include").Value)
        .Where(File.Exists)
        .Concat(Directory.GetFiles(isEditor ? "Assets/Editor" : "Assets/Scripts", "*.cs", SearchOption.AllDirectories))
        .Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase);
    var defines = doc.Descendants(ns + "DefineConstants").First().Value.Split(';');
    var options = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: defines);
    var trees = files.Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), options, path));
    var paths = doc.Descendants(ns + "HintPath").Select(x => x.Value).Where(File.Exists)
        .Concat(Directory.GetFiles("Library/ScriptAssemblies", "*.dll"))
        .Where(path => Path.GetFileNameWithoutExtension(path) != name &&
            !(isEditor && Path.GetFileNameWithoutExtension(path) == "Assembly-CSharp"))
        .Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase);
    foreach (string path in paths) dependencyPaths[Path.GetFileNameWithoutExtension(path)] = path;
    var references = paths.Select(path => MetadataReference.CreateFromFile(path)).Cast<MetadataReference>().ToList();
    if (isEditor) references.Add(runtime);
    var compilation = CSharpCompilation.Create(name, trees, references,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    using var stream = new MemoryStream();
    var result = compilation.Emit(stream);
    var errors = result.Diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error).ToList();
    foreach (var error in errors) Console.WriteLine(error);
    Console.WriteLine(name + ": " + errors.Count + " errors");
    if (!result.Success) Environment.Exit(1);
    assemblies[name] = stream.ToArray();
    if (!isEditor) runtime = MetadataReference.CreateFromImage(assemblies[name]);
}

// 僅執行無場景測試；不以替身模擬 Unity 或 Photon 行為。
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    if (assemblies.TryGetValue(name.Name, out var bytes))
        return context.LoadFromStream(new MemoryStream(bytes));
    if (dependencyPaths.TryGetValue(name.Name, out string path)) return context.LoadFromAssemblyPath(path);
    return null;
};
var editor = AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(assemblies["Assembly-CSharp-Editor"]));
var results = new List<string>();
Action<bool, string> check = (condition, name) =>
{
    if (!condition) throw new Exception(name);
    results.Add("PASS: " + name);
};
editor.GetType("CoreRulesRegression").GetMethod("RunManagedChecks").Invoke(null, new object[] { check });
Directory.CreateDirectory("output");
File.WriteAllLines("output/managed-rules-regression.txt", results);
Console.WriteLine("Managed rules: " + results.Count + " passed (actual compiled game assemblies)");
