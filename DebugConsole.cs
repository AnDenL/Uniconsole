using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DevConsole
{
    public class DebugConsole : MonoBehaviour
    {
        public static DebugConsole Instance;
        private static VM vm;
        
        private static readonly Dictionary<KeyCode, CompiledScript> keyBinds = new();
        
        private static readonly List<string> commandHistory = new() { " " };
        private readonly StringBuilder hintsBuilder = new();
        private int historyIndex = -1;

        [SerializeField] private TextMeshProUGUI historyText;
        [SerializeField] private TextMeshProUGUI hintsText;
        [SerializeField] private TMP_InputField inputField;

        public static event Action<bool> OnConsoleVisibilityChanged;
        
        private EventSystem eventSystem;
        private GameObject panel;
        private readonly List<string> currentSuggestions = new();
        private int suggestionIndex = 0;

        private readonly Queue<string> logLines = new();
        public static string LastStackTrace { get; private set; } = "";
        
        private const int MaxLogLines = 100;
        private const string hintHighlight = "#59ff5f";
        private const string errColor = "#ff5454", white = "#FFFFFF", warnColor = "#ffd754";

        #region Unity Lifecycle
        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            vm ??= new VM();
            
            CommandRegistry.RegisterPlugin(new CoreConsolePlugin());
        }

        private void Start()
        {
            eventSystem = EventSystem.current;
            panel = transform.GetChild(0).gameObject;
            panel.SetActive(false);
            
            inputField.onSubmit.AddListener(Enter);
            inputField.onValueChanged.AddListener(OnInputChanged);
            
            LoadConfig();
            
            string autoexecPath = Path.Combine(Application.streamingAssetsPath, "Scripts", "autoexec.txt");
            if (File.Exists(autoexecPath))
            {
                Log("<color=#ffd754>Running autoexec.txt...</color>");
                ExecuteScript(File.ReadAllText(autoexecPath));
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) ToggleConsole();
            
            if (!panel.activeInHierarchy)
            {
                foreach (var bind in keyBinds)
                {
                    if (Input.GetKeyDown(bind.Key))
                    {
                        vm.Execute(bind.Value.Code, bind.Value.Strings); 
                    }
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                ApplyTabAutofill();
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                NavigateHistory(1);
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                NavigateHistory(-1);
            }
        }

        private void OnEnable()
        {
            Application.logMessageReceived += HandleUnityLog;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= HandleUnityLog;
        }

        private void HandleUnityLog(string logString, string stackTrace, LogType type)
        {
            if (logString.StartsWith(">") || logString.StartsWith("[Command")) return;

            switch (type)
            {
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    LastStackTrace = stackTrace;
                    LogError($"[SYS ERROR] {logString}");
                    break;
                case LogType.Warning:
                    LogWarning($"[SYS WARN] {logString}");
                    break;
                case LogType.Log:
                    Log($"<color=#aaaaaa>[Unity]</color> {logString}");
                    break;
            }
        }
        #endregion

        #region Hints
        private void ApplyTabAutofill()
        {
            if (currentSuggestions.Count == 0) return;

            string currentText = inputField.text;
            int lastSpace = currentText.LastIndexOf(' ');
            string baseText = lastSpace == -1 ? "/" : currentText[..(lastSpace + 1)];
            
            suggestionIndex = Math.Clamp(suggestionIndex, 0, currentSuggestions.Count - 1);
            string chosen = currentSuggestions[suggestionIndex];
            
            inputField.text = baseText + chosen + " ";
            inputField.caretPosition = inputField.text.Length;

            if (currentSuggestions.Count != 0) suggestionIndex = (suggestionIndex + 1) % currentSuggestions.Count;
        }

        private void RenderHints(string prefix)
        {
            hintsBuilder.Clear();
            hintsBuilder.Append(prefix);

            for (int i = 0; i < currentSuggestions.Count; i++)
            {
                if (i == suggestionIndex)
                    hintsBuilder.Append($"<color={hintHighlight}>[{currentSuggestions[i]}]</color>");
                else
                    hintsBuilder.Append(currentSuggestions[i]);

                if (i < currentSuggestions.Count - 1)
                    hintsBuilder.Append("  ");
            }
            
            hintsText.text = hintsBuilder.ToString();
        }
        #endregion

        public void ToggleConsole()
        {
            bool state = !panel.activeInHierarchy;
            panel.SetActive(state);
            MainMenu.instance.CanPause = !state; 

            OnConsoleVisibilityChanged?.Invoke(state);

            if (state)
            {
                inputField.ActivateInputField();
                inputField.Select();
            }
        }

        private void NavigateHistory(int direction)
        {
            if (commandHistory.Count == 0) return;

            historyIndex = Math.Clamp(historyIndex - direction, 0, commandHistory.Count - 1);
            inputField.text = commandHistory[historyIndex];
            inputField.caretPosition = inputField.text.Length;
        }

        private void ClearInput()
        {
            historyIndex = commandHistory.Count;
            inputField.text = string.Empty;
            hintsText.text = string.Empty;
            inputField.ActivateInputField();
        }

        #region Input
        private void Enter(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;

            commandHistory.Add(input);
            historyIndex = commandHistory.Count;

            Log($"> {input}");

            if (input.StartsWith("/"))
            {
                string command = input[1..];
                vm.Compile(vm.Lex(command));
                vm.Execute();
            }
            else
            {
                Log(input);
            }

            ClearInput();
        }

        private void OnInputChanged(string text)
        {
            currentSuggestions.Clear();
            suggestionIndex = 0;

            if (string.IsNullOrWhiteSpace(text) || text[0] != '/')
            {
                hintsText.text = string.Empty;
                return;
            }

            string raw = text[1..];
            int firstSpace = raw.IndexOf(' ');
            bool endsWithSpace = raw.EndsWith(" ");

            if (firstSpace == -1)
            {
                foreach (string cmd in CommandRegistry.GetAllCommandNames())
                {
                    if (cmd.StartsWith(raw, StringComparison.OrdinalIgnoreCase))
                        currentSuggestions.Add(cmd);
                }
                RenderHints("Commands: ");
                return;
            }

            string cmdName = raw[..firstSpace];

            if (CommandRegistry.TryGet(cmdName, out CommandInfo info))
            {
                int argIndex = 0;
                int lastSpaceIndex = firstSpace;

                for (int i = firstSpace; i < raw.Length; i++)
                {
                    if (raw[i] == ' ')
                    {
                        if (i > 0 && raw[i - 1] != ' ') argIndex++;
                        lastSpaceIndex = i;
                    }
                }

                if (endsWithSpace) argIndex++;
                else argIndex = Math.Max(0, argIndex - 1);

                string currentArg = endsWithSpace ? string.Empty : raw[(lastSpaceIndex + 1)..];

                if (info.Hints != null)
                {
                    var options = info.Hints(argIndex, currentArg);
                    if (options != null) currentSuggestions.AddRange(options);
                }

                hintsBuilder.Clear();
                hintsBuilder.AppendLine(info.Syntax);
                RenderHints(hintsBuilder.ToString());
            }
            else
            {
                hintsText.text = string.Empty;
            }
        }
        #endregion

        #region Static
        private static int execDepth = 0;
        private const int MaxExecDepth = 16;

        public static void ExecuteScript(string scriptText)
        {
            if (execDepth >= MaxExecDepth)
            {
                LogError($"[Stack Overflow] Maximum script execution depth ({MaxExecDepth}) reached! Stopping execution.");
                return;
            }

            execDepth++; 
            try
            {
                var tokens = vm.Lex(scriptText);
                vm.Compile(tokens);
                vm.Execute();
            }
            catch (Exception ex)
            {
                LogError($"Script Execution Error: {ex.Message}");
            }
            finally
            {
                execDepth--; 
            }
        }

        public static void ExecuteDelayed(float delaySeconds, string command)
        {
            if (Instance != null)
                Instance.StartCoroutine(Instance.DelayedExecutionRoutine(delaySeconds, command));
        }

        private System.Collections.IEnumerator DelayedExecutionRoutine(float delay, string command)
        {
            yield return new WaitForSeconds(delay);
            
            var tokens = vm.Lex(command);
            vm.Compile(tokens);
            vm.Execute();
        }

        private static void AddLogLine(string line)
        {
            if (Instance == null) return;
            
            Instance.logLines.Enqueue(line);
            if (Instance.logLines.Count > MaxLogLines)
            {
                Instance.logLines.Dequeue();
            }
            
            Instance.historyText.text = string.Join("\n", Instance.logLines);
        }

        public static void ClearHistoryText()
        {
            Instance.logLines.Clear();
            Instance.historyText.text = string.Empty;
        }

        public static void Log(string text) => AddLogLine(text);
        public static void LogWarning(string text) => AddLogLine($"<color={warnColor}>{text}</color>");
        public static void LogError(string text) => AddLogLine($"<color={errColor}>{text}</color>");

        public static void AddBind(KeyCode key, string command)
        {
            var compiledScript = vm.CompileScript(command);
            if (compiledScript.HasValue)
            {
                keyBinds[key] = compiledScript.Value;
            }
            else
            {
                LogError($"[Bind] Syntax error in command for key [{key}].");
            }
        }

        public static bool RemoveBind(KeyCode key) => keyBinds.Remove(key);
        public static void ClearAllBinds() => keyBinds.Clear();

        public static List<string> GetBoundKeyNames(string query)
        {
            var list = new List<string>();
            foreach (var key in keyBinds.Keys)
            {
                string name = key.ToString();
                if (string.IsNullOrEmpty(query) || name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    list.Add(name);
            }
            return list;
        }

        public static void SaveConfig(string fileName = "config.cfg")
        {
            string path = Path.Combine(Application.persistentDataPath, fileName);
            using StreamWriter writer = new(path, false);
            writer.WriteLine("// Auto-generated keybinds");
            foreach (var bind in keyBinds)
            {
                writer.WriteLine($"bind {bind.Key} {bind.Value.Source}");
            }
            Log($"Config saved: <color=#59ff5f>{path}</color>");
        }

        public static void LoadConfig(string fileName = "config.cfg")
        {
            string path = Path.Combine(Application.persistentDataPath, fileName);
            if (!File.Exists(path)) return;
            string[] lines = File.ReadAllLines(path);
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//")) continue;
                ExecuteScript(line);
            }
        }
        #endregion
    }
}