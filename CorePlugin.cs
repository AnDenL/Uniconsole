using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DevConsole 
{
    public class CoreConsolePlugin : IConsolePlugin
    {
        public static float TimeScale = 1;
        public static ConVarFloat cvTimeScale;

        public void RegisterCommands()
        {
            cvTimeScale = new ConVarFloat("time_scale", 1f, "Global game time scale",
                getter: () => TimeScale * (MainMenu.instance.isPaused ? 0 : 1),
                setter: (val) => {
                    if (val < 0) throw new Exception("Time scale cannot be negative.");
                    TimeScale = val;
                    if (!MainMenu.instance.isPaused) Time.timeScale = TimeScale;
                },
                onChange: (val) => DebugConsole.Log($"Time scale set to {val}")
            );

            CommandRegistry.Register("print", "/print <message>", (args, pool) =>
            {
                if (args.Length == 0) return;
                
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < args.Length; i++)
                {
                    sb.Append(args[i].ToString(pool));
                    if (i < args.Length - 1) sb.Append(" ");
                }
                DebugConsole.Log(sb.ToString());
            });

            CommandRegistry.Register("clear", "/clear", (args, pool) => DebugConsole.ClearHistoryText());

            CommandRegistry.Register("scene", "/scene <build_index>", (args, pool) =>
            {
                if (args.Length == 0) throw new Exception("Provide scene build index.");
                
                if (!args[0].TryGetInt(pool, out var index)) return;
                if (index >= 0 && index < SceneManager.sceneCountInBuildSettings)
                {
                    Time.timeScale = 1f; 
                    SceneManager.LoadScene(index);
                    DebugConsole.Log($"Loading scene {index}...");
                }
                else throw new Exception($"Scene {index} not found in Build Settings.");
            });

            CommandRegistry.Register("help", "/help [command_name]", (args, pool) =>
            {
                if (args.Length == 0)
                {
                    DebugConsole.Log("Available commands:\n" + string.Join(", ", CommandRegistry.GetAllCommandNames()));
                }
                else
                {
                    string cmd = pool[args[0].StringIndex];
                    if (CommandRegistry.TryGet(cmd, out var info))
                        DebugConsole.Log($"{info.Name} -> Syntax: {info.Syntax}");
                    else
                        DebugConsole.LogError($"Unknown command: {cmd}");
                }
            }, 
            hintProvider: (argIndex, currentArg) => 
            {
                if (argIndex == 0) return new List<string>(CommandRegistry.GetAllCommandNames());
                return null;
            });

            CommandRegistry.Register("roll", "/roll <min> <max>", (args, pool) =>
            {
                if (args.Length < 2) throw new Exception("Provide min and max integer values.");
                if (!args[0].TryGetInt(pool, out var min)) return;
                if (!args[1].TryGetInt(pool, out var max)) return;
                
                int result = UnityEngine.Random.Range(min, max + 1); 
                DebugConsole.Log($"Rolled ({min}-{max}): <color=#ffd754>{result}</color>");
            });

            CommandRegistry.Register("echo", "/echo <text>", (args, pool) =>
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < args.Length; i++)
                    sb.Append(args[i].ToString(pool)).Append(" ");
                    
                DebugConsole.Log($"<color=#aaaaaa>{sb}</color>");
            });

            CommandRegistry.Register("trace", "/trace", (args, pool) =>
            {
                if (string.IsNullOrEmpty(DebugConsole.LastStackTrace))
                {
                    DebugConsole.Log("Stack trace is empty.");
                    return;
                }
                GUIUtility.systemCopyBuffer = DebugConsole.LastStackTrace;
                DebugConsole.Log("<color=#59ff5f>Last Stack trace is copied to your clipboard</color>");
            });

            CommandRegistry.Register("sysinfo", "/sysinfo", (args, pool) =>
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("<color=#59ff5f>=== System Info ===</color>");
                sb.AppendLine($"OS: {SystemInfo.operatingSystem}");
                sb.AppendLine($"CPU: {SystemInfo.processorType} [{SystemInfo.processorCount} cores]");
                sb.AppendLine($"GPU: {SystemInfo.graphicsDeviceName} [{SystemInfo.graphicsMemorySize} MB]");
                sb.AppendLine($"RAM: {SystemInfo.systemMemorySize} MB");
                DebugConsole.Log(sb.ToString());
            });

            CommandRegistry.Register("fps", "/fps <limit>", (args, pool) =>
            {
                if (args.Length == 0)
                {
                    DebugConsole.Log($"Target FPS: {(Application.targetFrameRate == -1 ? "Unlimited" : Application.targetFrameRate)}");
                    return;
                }

                if (args[0].TryGetInt(pool, out int fps))
                {
                    Application.targetFrameRate = fps;
                    DebugConsole.Log($"Target FPS set to: <color=#ffd754>{(fps == -1 ? "Unlimited" : fps)}</color>");
                }
            }, 
            hintProvider: (argIndex, currentArg) => 
            {
                if (argIndex == 0) return new List<string> { "-1", "30", "60", "120", "144" };
                return null;
            });

            CommandRegistry.Register("exec", "/exec <file_name>", (args, pool) =>
            {
                if (args.Length == 0) throw new Exception("Provide script file name.");

                string fileName = pool[args[0].StringIndex];
                if (!fileName.EndsWith(".txt") && !fileName.EndsWith(".cfg")) 
                    fileName += ".txt";

                string folder = System.IO.Path.Combine(Application.streamingAssetsPath, "Scripts");
                string fullPath = System.IO.Path.Combine(folder, fileName);

                if (!System.IO.File.Exists(fullPath))
                {
                    throw new System.IO.FileNotFoundException($"Script file not found at: {fullPath}");
                }

                string scriptCode = System.IO.File.ReadAllText(fullPath);
                DebugConsole.Log($"Executing script: <color=#ffd754>{fileName}</color>");

                DebugConsole.ExecuteScript(scriptCode);
            },
            hintProvider: (argIndex, currentArg) =>
            {
                if (argIndex == 0)
                {
                    string folder = System.IO.Path.Combine(Application.streamingAssetsPath, "Scripts");
                    if (!System.IO.Directory.Exists(folder)) return null;

                    var files = new List<string>();
                    foreach (var file in System.IO.Directory.GetFiles(folder))
                    {
                        if (file.EndsWith(".txt") || file.EndsWith(".cfg"))
                        {
                            string name = System.IO.Path.GetFileNameWithoutExtension(file);
                            if (string.IsNullOrEmpty(currentArg) || name.Contains(currentArg, StringComparison.OrdinalIgnoreCase))
                                files.Add(name);
                        }
                    }
                    return files;
                }
                return null;
            });

            CommandRegistry.Register("invoke", "/invoke <seconds> <\"command\">", (args, pool) =>
            {
                if (args.Length < 2) throw new Exception("Provide delay time and command string.");
                
                if (!args[0].TryGetFloat(pool, out float delay)) return;

                var cmdBuilder = new System.Text.StringBuilder();
                for (int i = 1; i < args.Length; i++)
                {
                    cmdBuilder.Append(args[i].ToString(pool));
                    if (i < args.Length - 1) cmdBuilder.Append(" ");
                }
                
                string commandToRun = cmdBuilder.ToString();
                
                DebugConsole.ExecuteDelayed(delay, commandToRun);
            });

            CommandRegistry.Register("stopall", "/stopall", (args, pool) =>
            {
                DebugConsole.Instance.StopAllCoroutines();
                DebugConsole.Log("<color=#ff5454>All invoked scripts and coroutines stopped.</color>");
            });

            CommandRegistry.Register("writecfg", "/writecfg [filename]", (args, pool) =>
            {
                string fileName = args.Length > 0 ? pool[args[0].StringIndex] : "config.cfg";
                if (!fileName.EndsWith(".cfg")) fileName += ".cfg";
                
                DebugConsole.SaveConfig(fileName);
            });

            CommandRegistry.Register("bind", "/bind <keycode> <command>", (args, pool) =>
            {
                if (args.Length < 2) throw new Exception("Syntax: /bind <keycode> <command>");
                
                string keyStr = pool[args[0].StringIndex];
                if (Enum.TryParse(keyStr, true, out KeyCode keyCode))
                {
                    var cmdBuilder = new System.Text.StringBuilder();
                    for (int i = 1; i < args.Length; i++)
                    {
                        cmdBuilder.Append(args[i].ToString(pool));
                        if (i < args.Length - 1) cmdBuilder.Append(" ");
                    }
                    
                    DebugConsole.AddBind(keyCode, cmdBuilder.ToString());
                    DebugConsole.Log($"Bound [{keyCode}] to '{cmdBuilder}'");
                }
                else throw new Exception($"Invalid KeyCode: {keyStr}");
            },
            hintProvider: (argIndex, currentArg) => 
            {
                if (argIndex == 0) return new List<string> { "Mouse0", "F5", "Space", "R" };
                return null;
            });

            CommandRegistry.Register("unbind", "/unbind <keycode>", (args, pool) =>
            {
                if (args.Length == 0) throw new Exception("Specify a key to unbind.");

                string keyStr = pool[args[0].StringIndex];
                if (Enum.TryParse(keyStr, true, out KeyCode keyCode))
                {
                    if (DebugConsole.RemoveBind(keyCode))
                        DebugConsole.Log($"Unbound key: <color=#ffd754>[{keyCode}]</color>");
                    else
                        DebugConsole.LogWarning($"No bind exists for [{keyCode}]");
                }
                else throw new Exception($"Invalid KeyCode: {keyStr}");
            },
            hintProvider: (argIndex, currentArg) =>
            {
                if (argIndex == 0) return DebugConsole.GetBoundKeyNames(currentArg);
                return null;
            });

            CommandRegistry.Register("unbindall", "/unbindall", (args, pool) =>
            {
                DebugConsole.ClearAllBinds();
                DebugConsole.Log("<color=#ff5454>All keybinds cleared.</color>");
            });

            CommandRegistry.Register("quit", "/quit", (args, pool) =>
            {
                DebugConsole.Log("Exiting game...");
                Application.Quit();
            });
        }
    }
}