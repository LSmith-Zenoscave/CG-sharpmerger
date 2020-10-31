using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;


namespace CodeCopier
{
    class Program
    {
        class Options
        {
            [Option('p', "path", Required = true, HelpText = "Base directory path to collect together.")]
            public string BasePath { get; set; }
            
            [Option('o', "output", Required = true, HelpText = "Destination file path for collected source file.")]
            public string OutputPath { get; set; }

            [Option('w', "watch", Default = false, HelpText = "Enable path watcher.")]
            public bool Watch { get; set; }
        }

        private static Options options;

        static async Task Main(string[] args)
        {
            var lastUpdated = DateTime.MinValue;

            Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(options =>
            {
                Program.options = options;
            });
        
            if (options == null)
            {
                return; 
            }

            do
            {
                try
                {
                    await Task.Delay(500);
                    var files = GetFiles().Where(f => f != options.OutputPath).ToArray();
                    var lastEdited = FindLastEdited(files);
                    if (lastEdited == lastUpdated) continue;
                    var filelines = files.Select(f => File.ReadAllLines(f));

                    var contentsets = new Dictionary<string, List<string>>();
                    foreach (var file in filelines)
                    {
                        var ns = file.First(l => l.StartsWith("namespace")).Split()[1];
                        var lines = file.Where(l =>
                        {
                            return (
                                !l.StartsWith("namespace") &&
                                !l.StartsWith("{") &&
                                !l.StartsWith("}")
                            );
                        }).ToList();

                        if (!contentsets.ContainsKey(ns))
                        {
                            contentsets[ns] = new List<string>();
                        }
                        contentsets[ns].AddRange(lines);
                    }


                    var mergedFile = CreateMergedCsFile(contentsets, lastEdited);
                    File.WriteAllText(options.OutputPath, mergedFile);
                    lastUpdated = lastEdited;
                    Console.WriteLine("Updated: " + lastEdited.ToString("MM/dd/yyyy H:mm \n"));
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.Message);
                }
            }
            while (options.Watch);
        }

        private static string CreateMergedCsFile(Dictionary<string, List<string>> content, DateTime edited)
        {
            var usings = new List<string>();
            foreach (var contentset in content)
            {
                usings.AddRange(contentset.Value.Where(c => c.StartsWith("using", StringComparison.Ordinal)).Distinct());
                contentset.Value.RemoveAll(c => c.StartsWith("using", StringComparison.Ordinal));
            }

            var namespaces = content.Select(n =>
            {
                var lines = new List<string>() { $"namespace {n.Key}", "{" };
                lines.AddRange(n.Value);
                lines.Add("}");
                return String.Join("\n", lines);
            });

            return string.Join("\n", usings) + "\n\n\n// LastEdited: " + edited.ToString("dd/MM/yyyy H:mm \n\n\n") + string.Join("\n", namespaces);
        }

        private static string[] GetFiles()
        {
            return Directory.GetFiles(options.BasePath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.ToLower().Contains("/bin/") && !f.ToLower().Contains("/obj/") && Path.GetFileName(f) != "AssemblyInfo.cs")
                .ToArray();
        }

        private static DateTime FindLastEdited(string[] filePaths)
        {
            return filePaths.Max(f => File.GetLastWriteTime(f));
        }
    }
}
