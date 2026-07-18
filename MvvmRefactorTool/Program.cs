using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

class Program
{
    static void Main()
    {
        string mappingsFile = @"c:\Antigravity\KamatekCRM\command_mappings.txt";
        string viewModelsDir = @"c:\Antigravity\KamatekCRM\ViewModels";
        
        var lines = File.ReadAllLines(mappingsFile);
        var fileToCommands = new Dictionary<string, List<(string property, string method)>>();

        foreach (var line in lines)
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length == 3)
            {
                if (!fileToCommands.ContainsKey(parts[0]))
                    fileToCommands[parts[0]] = new List<(string, string)>();
                
                // cleanup method name
                var method = parts[2];
                if (method.StartsWith("async _ => await ")) method = method.Substring("async _ => await ".Length);
                else if (method.StartsWith("_ => ")) method = method.Substring("_ => ".Length);
                else if (method.StartsWith("param => ")) method = method.Substring("param => ".Length);
                else if (method.StartsWith("async s => await ")) method = method.Substring("async s => await ".Length);
                else if (method.StartsWith("async p => await ")) method = method.Substring("async p => await ".Length);
                
                var arrowIdx = method.IndexOf("=>");
                if (arrowIdx > 0) method = method.Substring(arrowIdx + 2).Trim();
                
                var parenIdx = method.IndexOf("(");
                if (parenIdx > 0) method = method.Substring(0, parenIdx);
                
                method = method.Trim();
                
                // For aliased commands like ProcessCashPaymentCommand = PayFullCashCommand;
                if (method.EndsWith("Command")) continue;
                
                fileToCommands[parts[0]].Add((parts[1], method));
            }
        }

        foreach (var kvp in fileToCommands)
        {
            string filePath = Path.Combine(viewModelsDir, kvp.Key);
            if (!File.Exists(filePath)) continue;

            string content = File.ReadAllText(filePath);
            bool changed = false;

            foreach (var cmd in kvp.Value)
            {
                // Delete constructor instantiation e.g. MyCommand = new RelayCommand(...)
                string pattern = $@"\s*{cmd.property}\s*=\s*new\s*(Async)?RelayCommand[^\r\n]*;";
                if (Regex.IsMatch(content, pattern))
                {
                    content = Regex.Replace(content, pattern, "");
                    changed = true;
                }
                else if (Regex.IsMatch(content, $@"\s*{cmd.property}\s*=\s*[a-zA-Z0-9_]+Command;"))
                {
                    content = Regex.Replace(content, $@"\s*{cmd.property}\s*=\s*[a-zA-Z0-9_]+Command;", "");
                    changed = true;
                }

                // Add [RelayCommand] to method if not already there
                string methodPattern = $@"(private|public|protected|internal)( async)? (Task|void) {cmd.method}\s*\(";
                var match = Regex.Match(content, methodPattern);
                if (match.Success)
                {
                    int index = match.Index;
                    int lastNewline = content.LastIndexOf('\n', index);
                    if (lastNewline >= 0)
                    {
                        string lineBefore = content.Substring(lastNewline - 30 > 0 ? lastNewline - 30 : 0, 30);
                        if (!lineBefore.Contains("[RelayCommand"))
                        {
                            string expectedMethodName = cmd.property.Replace("Command", "");
                            string actualMethodName = cmd.method;
                            if (actualMethodName.EndsWith("Async") && !expectedMethodName.EndsWith("Async")) 
                                expectedMethodName += "Async";
                            
                            if (expectedMethodName != actualMethodName && !cmd.method.StartsWith("Execute"))
                            {
                                string newMethodDecl = match.Value.Replace(cmd.method, expectedMethodName);
                                content = content.Remove(index, match.Length).Insert(index, newMethodDecl);
                                changed = true;
                            }
                            else if (cmd.method.StartsWith("Execute"))
                            {
                                expectedMethodName = cmd.method.Substring("Execute".Length);
                                string newMethodDecl = match.Value.Replace(cmd.method, expectedMethodName);
                                content = content.Remove(index, match.Length).Insert(index, newMethodDecl);
                                changed = true;
                            }

                            match = Regex.Match(content, $@"(private|public|protected|internal)( async)? (Task|void) {expectedMethodName}\s*\(");
                            if (match.Success)
                            {
                                content = content.Insert(match.Index, "[RelayCommand]\r\n        ");
                                changed = true;
                            }
                        }
                    }
                }
            }

            if (changed)
            {
                File.WriteAllText(filePath, content);
                Console.WriteLine($"Refactored {kvp.Key}");
            }
        }
    }
}
