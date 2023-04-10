using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Spectre.Console;
using Spectre.Console.Json;
using TeuJson;

class Program 
{
    private static CompilerConfig compiler;
    private static bool anotherTry;

    [STAThread]
    public static void Main(string[] args) 
    {
        var panel = new Panel("Teuria Json Compiler") 
        {
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(panel);
        compiler = File.Exists("compiler.json") 
            ? JsonConvert.DeserializeFromFile<CompilerConfig>("compiler.json") 
            : new CompilerConfig();
        compiler.JsonPaths ??= new List<string>();

        while (true) 
        {
            State state;
            if (anotherTry) 
            {
                anotherTry = false;
                state = State.List;
            }
            else 
            {
                state = AnsiConsole.Prompt(
                    new SelectionPrompt<State>()
                        .Title("Select a state")
                        .PageSize(10)
                        .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
                        .AddChoices(State.Add, State.Remove, State.Compile, State.List, State.Quit)
                );
            }

            switch (state) 
            {
            case State.Add:
                var stateAdd = AnsiConsole.Prompt(
                    new SelectionPrompt<AddState>()
                        .Title("Select Add types")
                        .PageSize(10)
                        .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
                        .AddChoices(AddState.AddBySelection, AddState.AddByLocal, AddState.Back)
                );
                switch (stateAdd) 
                {
                case AddState.AddBySelection:
                    var path = RfdSharp.RfdSharp.OpenFileWithFilter("./", new string[1] { "json" });

                    if (string.IsNullOrEmpty(path)) 
                    {
                        AnsiConsole.Prompt(
                            new TextPrompt<string>("[red]File dialog closed[/]")
                            .AllowEmpty());
                        break;
                    }
                    var newPath = path.Replace("\\", "/");
                    if (compiler.JsonPaths.Contains(newPath)) 
                    {

                        AnsiConsole.Prompt(
                            new TextPrompt<string>("[yellow]Json[/] path already exists")
                            .AllowEmpty());
                        break;
                    }

                    compiler.JsonPaths.Add(newPath);
                    Save();
                    break;
                case AddState.AddByLocal:
                    var paths = Directory.GetFiles(".", "*.json", SearchOption.AllDirectories).AsSpan();
                    var acceptedPath = new List<string>();
                    for (int i = 0; i < paths.Length; i++) 
                    {
                        var x = paths[i];
                        if (compiler == null) 
                        {
                            acceptedPath.Add(x);
                            continue;
                        }

                        if (compiler.Ignore != null) 
                        {
                            Span<string> spanned = compiler.Ignore.AsSpan();
                            for (int j = 0; j < spanned.Length; j++) 
                            {
                                var ignore = spanned[j];
                                if (x.Contains(ignore)) 
                                {
                                    goto Continue;
                                }
                            }
                        }
                        if (!compiler.JsonPaths.Contains(NormalizeRelativePath(x)))
                            acceptedPath.Add(x);
                        Continue:
                        continue;
                    }

                    var addPaths = AnsiConsole.Prompt(
                    new MultiSelectionPrompt<string>()
                        .Title("Select which path to add\n[gray]Tips: You can ignore some folders in the[/] [yellow]compiler.json[/]")
                        .PageSize(20)
                        .NotRequired()
                        .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
                        .AddChoices(acceptedPath)
                    );
                    var collections = CollectionsMarshal.AsSpan(addPaths);
                    foreach (var addPath in collections) 
                    {
                        compiler.JsonPaths.Add(NormalizeRelativePath(addPath));
                    }
                    Save();
                    break;
                case AddState.Back:
                    break;
                }
                break;
            case State.Remove:
                if (compiler.JsonPaths.Count <= 0) 
                {
                    AnsiConsole.Prompt(
                        new TextPrompt<string>("There is no path to remove.")
                            .AllowEmpty());
                    break;
                }
                var removePaths = AnsiConsole.Prompt(
                    new MultiSelectionPrompt<string>()
                        .Title("Select which path to remove")
                        .PageSize(10)
                        .NotRequired()
                        .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
                        .AddChoices(compiler.JsonPaths)
                );
                foreach (var path in removePaths) 
                {
                    compiler.JsonPaths.Remove(path);
                }
                Save();
                break;
            case State.Compile:
                if (compiler.JsonPaths.Count <= 0) 
                {
                    AnsiConsole.Prompt(
                        new TextPrompt<string>("[yellow]There is nothing to compile.[/]")
                        .AllowEmpty());
                    break;
                }
                AnsiConsole.Progress()
                    .Start(ctx => 
                    {
                        var task = ctx.AddTask("[green]Compiling...[/]");
                        while (!ctx.IsFinished) 
                        {
                            foreach (var path in compiler.JsonPaths) 
                            {
                                var value = JsonConvert.DeserializeFromFile(path);
                                var binExt = path.Replace(".json", ".bin");
                                JsonBinaryWriter.WriteToFile(binExt, value);
                                task.Increment(1.0f);
                            }
                        }
                    });

                AnsiConsole.Prompt(
                    new TextPrompt<string>("[green]Compiled successfully![/]")
                    .AllowEmpty());
                break;
            case State.List:
                if (compiler.JsonPaths.Count <= 0) 
                {
                    AnsiConsole.Prompt(
                        new TextPrompt<string>("There is no path.")
                            .AllowEmpty());
                    break;
                }
                var copy = new List<string>(compiler.JsonPaths);
                copy.Add("Back");
                var selectedPath = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select which path to view")
                        .PageSize(10)
                        .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
                        .AddChoices(copy)
                );
                if (selectedPath == "Back") 
                    break;
                
                var json = JsonTextReader.FromFile(selectedPath);
                var jsonText = new JsonText(json.ToString(JsonTextWriterOptions.Default));
                var table = new Table().Centered();
                var jsonPanel = new Panel(jsonText)
                    .Header("Json")
                    .Collapse()
                    .RoundedBorder()
                    .BorderColor(Color.Yellow);

                AnsiConsole.Write(jsonPanel);

                var todo = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .PageSize(10)
                        .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
                        .AddChoices(new string[2] { "Go Back", "Exit" })
                );
                if (todo == "Go Back") 
                {
                    anotherTry = true;
                }
                
                break;
            case State.Quit:
                AnsiConsole.Write("Exited");
                goto End;
            }
            Thread.Sleep(10);
        }
        End:
        return;
    }

    private static void Save() 
    {
        JsonConvert.SerializeToFile(compiler, "compiler.json");
    }

    private static string NormalizeRelativePath(string path) 
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace("\\", "/");
    }
}

partial class CompilerConfig : IDeserialize, ISerialize 
{
    public List<string> JsonPaths { get; set; }
    public string[] Ignore { get; set; }
}