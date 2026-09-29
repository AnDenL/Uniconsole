using System;
using System.Collections.Generic;

namespace DevConsole 
{
    public delegate List<string> HintProvider(int argIndex, string currentArg);

    public struct CommandInfo
    {
        public string Name;
        public string Syntax;
        public CommandRegistry.CommandAction Action;
        public HintProvider Hints;
    }

    public interface IConsolePlugin
    {
        void RegisterCommands();
    }

    public static class CommandRegistry
    {
        public delegate void CommandAction(Value[] args, List<string> stringPool);
        private static readonly Dictionary<string, CommandInfo> commands = new(StringComparer.OrdinalIgnoreCase);
        
        private static readonly Dictionary<string, ConVar> conVars = new(StringComparer.OrdinalIgnoreCase);

        public static void RegisterConVar(ConVar cv)
        {
            conVars[cv.Name] = cv;

            Register(cv.Name, $"/{cv.Name} [value] - {cv.Description}", (args, pool) =>
            {
                if (args.Length == 0)
                {
                    DebugConsole.Log($"<color=#ffd754>{cv.Name}</color> = {cv.AsString()}");
                }
                else
                {
                    cv.SetVMValue(args[0], pool);
                }
            }, 
            hintProvider: (argIndex, currentArg) => 
            {
                if (argIndex == 0 && cv is ConVarBool) return new List<string> { "true", "false", "0", "1" };
                return null;
            });
        }
        
        public static bool TryGetConVar(string name, out ConVar cv) => conVars.TryGetValue(name, out cv);

        public static void Register(string name, string syntax, CommandAction action, HintProvider hintProvider = null)
        {
            commands[name] = new CommandInfo
            {
                Name = name,
                Syntax = syntax,
                Action = action,
                Hints = hintProvider
            };
        }

        public static void RegisterPlugin(IConsolePlugin plugin)
        {
            plugin.RegisterCommands();
        }

        public static bool TryGet(string name, out CommandInfo info) => commands.TryGetValue(name, out info);
        public static IEnumerable<string> GetAllCommandNames() => commands.Keys;

        public static bool TryExecute(string name, Value[] args, List<string> stringPool)
        {
            if (commands.TryGetValue(name, out var cmd))
            {
                try
                {
                    cmd.Action(args, stringPool);
                    return true;
                }
                catch (Exception ex)
                {
                    DebugConsole.LogError($"[Command '{name}'] Error: {ex.Message}");
                }
            }
            else
            {
                DebugConsole.LogWarning($"Unknown command: {name}. Type 'help' for a list of commands.");
            }
            return false;
        }
    }
}