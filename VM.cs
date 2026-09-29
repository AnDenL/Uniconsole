using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DevConsole 
{
    using static TokenType;

    public enum TokenType
    {
        Identifier, Number, StrLiteral,
        Plus, Minus, Multiply, Divide,
        LParen, RParen, Comma, Set,
        Eq, NotEq, Less, More, EqLess, EqMore,
        True, False, If, Else, Null,
        Newline, EndOfFile, Unknown
    }

    public readonly struct Token
    {
        public readonly TokenType Type;
        public readonly string Value;
        public Token(TokenType type, string value) { Type = type; Value = value; }
    }

    public enum OpCode : byte
    {
        PushInt, PushFloat, PushString, PushBool,
        Add, Subtract, Multiply, Divide,
        CallCommand, Return
    }

    public enum ValueType : byte { Null, Int, Float, String, Bool }

    [StructLayout(LayoutKind.Explicit)]
    public struct Value
    {
        [FieldOffset(0)] public ValueType Type;
        [FieldOffset(4)] public int AsInt;
        [FieldOffset(4)] public float AsFloat;
        [FieldOffset(4)] public int StringIndex;
        [FieldOffset(4)] public bool AsBool;

        public static Value FromInt(int v) => new() { Type = ValueType.Int, AsInt = v };
        public static Value FromFloat(float v) => new() { Type = ValueType.Float, AsFloat = v };
        public static Value FromString(int idx) => new() { Type = ValueType.String, StringIndex = idx };
        public static Value FromBool(bool v) => new() { Type = ValueType.Bool, AsBool = v };

        public string ToString(List<string> pool)
        {
            return Type switch
            {
                ValueType.Int    => AsInt.ToString(),
                ValueType.Float  => AsFloat.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ValueType.Bool   => AsBool ? "true" : "false",
                ValueType.String => pool != null && StringIndex < pool.Count ? pool[StringIndex] : string.Empty,
                _                => "null"
            };
        }

        public readonly bool TryGetInt(List<string> pool, out int i)
        {
            if (Type == ValueType.Int)
            {
                i = AsInt;
                return true;
            }
            if (Type == ValueType.Float)
            {
                i = (int)AsFloat;
                return true;
            }
            if (Type == ValueType.String && pool != null)
            {
                if (int.TryParse(pool[StringIndex], out i)) 
                {
                    return true;
                }
                
                DebugConsole.LogError($"Can't parse '{pool[StringIndex]}' to integer.");
            }

            i = 0;
            return false;
        }

        public readonly bool TryGetFloat(List<string> pool, out float v)
        {
            if (Type == ValueType.Int)
            {
                v = AsInt;
                return true;
            }
            if (Type == ValueType.Float)
            {
                v = AsFloat;
                return true;
            }
            if (Type == ValueType.String && pool != null)
            {
                if (float.TryParse(pool[StringIndex], out v)) 
                {
                    return true;
                }
                
                DebugConsole.LogError($"Can't parse '{pool[StringIndex]}' to float.");
            }

            v = 0;
            return false;
        }
    }

    public struct CompiledScript
    {
        public byte[] Code;
        public List<string> Strings;
        public string Source;
    }

        public class VM
    {
        private readonly Value[] stack = new Value[64];
        private int stackPtr = 0;
        
        private readonly List<byte> code = new();
        private readonly List<string> strings = new();
        private readonly Dictionary<string, ushort> stringLookup = new();
        private List<Token> tokens;
        private int pos;

        public bool HasError { get; private set; } 

        public List<Token> Lex(string code)
        {
            var tokens = new List<Token>();
            int i = 0;
            int length = code.Length;
            while (i < length)
            {
                char ch = code[i];
                char next = (i + 1 < length) ? code[i + 1] : '\0';
                
                if (ch is '\n' or ';')
                {
                    i++;
                    tokens.Add(new Token(Newline, string.Empty));
                    continue;
                }
                if (char.IsWhiteSpace(ch))
                {
                    i++;
                    continue;
                }
                if (char.IsLetter(ch) || ch == '_')
                {
                    int start = i;
                    while (i < length && (char.IsLetterOrDigit(code[i]) || code[i] == '_'))
                        i++;
                    string ident = code[start..i];
                    TokenType t = ident switch
                    {
                        "true"  => True,
                        "false" => False,
                        "if"    => If,
                        "else"  => Else,
                        "null"  => Null,
                        _       => Identifier
                    };
                    tokens.Add(new Token(t, ident));
                    continue;
                }
                if (char.IsDigit(ch))
                {
                    int start = i;
                    bool hasDot = false;
                    while (i < length && (char.IsDigit(code[i]) || (!hasDot && code[i] == '.' && i + 1 < length && char.IsDigit(code[i + 1]))))
                    {
                        if (code[i] == '.') hasDot = true;
                        i++;
                    }
                    string num = code[start..i];
                    tokens.Add(new Token(Number, num));
                    continue;
                }
                if (ch == '"')
                {
                    i++;
                    int start = i;
                    while (i < length && code[i] != '"')
                        i++;
                    
                    string str = code[start..i];
                    if (i < length) 
                    {
                        i++; 
                    }
                    else 
                    {
                        DebugConsole.LogError("[Syntax] Unclosed string literal (missing '\"')."); 
                    }
                     
                    tokens.Add(new Token(StrLiteral, str));
                    continue;
                }
                
                TokenType type = ch switch
                {
                    '+' => Plus, '-' => Minus, '*' => Multiply, '/' => Divide,
                    '(' => LParen, ')' => RParen, ',' => Comma,
                    '=' when next == '=' => Eq, '=' => Set,
                    '<' when next == '=' => EqLess, '<' => Less,
                    '>' when next == '=' => EqMore, '>' => More,
                    '!' when next == '=' => NotEq,
                    _   => Unknown
                };
                
                if (type is Eq or EqLess or EqMore or NotEq) i++;
                tokens.Add(new Token(type, ch.ToString()));
                i++;
            }
            tokens.Add(new Token(EndOfFile, string.Empty));
            return tokens;
        }

        public void Compile(List<Token> inputTokens)
        {
            tokens = inputTokens;
            pos = 0;
            code.Clear();
            HasError = false; 

            while (!IsAtEnd())
            {
                if (Check(TokenType.Newline)) { Advance(); continue; }
                
                CompileStatement();
                
                if (HasError) break; 
            }
            code.Add((byte)OpCode.Return);
        }

        public CompiledScript? CompileScript(string source)
        {
            var tokens = Lex(source);
            Compile(tokens);
            
            if (HasError) return null;
            
            return new CompiledScript 
            { 
                Code = code.ToArray(), 
                Strings = new List<string>(strings), 
                Source = source 
            };
        }

        private void CompileStatement()
        {
            if (Check(TokenType.Identifier))
            {
                string cmdName = Advance().Value;
                byte argCount = 0;
                while (!IsAtEnd() && !Check(TokenType.Newline))
                {
                    CompileExpression();
                    argCount++;
                    if (HasError) return;
                }
                code.Add((byte)OpCode.CallCommand);
                WriteStringId(cmdName);
                code.Add(argCount);
            }
            else
            {
                Advance();
            }
        }

        private void CompileExpression()
        {
            CompileTerm();
            while (Match(TokenType.Plus, TokenType.Minus))
            {
                TokenType op = Previous().Type;
                CompileTerm();
                code.Add((byte)(op == TokenType.Plus ? OpCode.Add : OpCode.Subtract));
            }
        }

        private void CompileTerm()
        {
            CompileFactor();
            while (Match(TokenType.Multiply, TokenType.Divide))
            {
                TokenType op = Previous().Type;
                CompileFactor();
                code.Add((byte)(op == TokenType.Multiply ? OpCode.Multiply : OpCode.Divide));
            }
        }

        private void CompileFactor()
        {
            bool isNegative = false;
            
            if (Match(TokenType.Minus))
            {
                isNegative = true;
            }

            if (Match(TokenType.Number))
            {
                string val = Previous().Value;
                if (val.Contains('.')) 
                {
                    float f = float.Parse(val, System.Globalization.CultureInfo.InvariantCulture);
                    WriteFloat(isNegative ? -f : f); 
                }
                else 
                {
                    int i = int.Parse(val);
                    WriteInt(isNegative ? -i : i);
                }
            }
            else if (Match(TokenType.StrLiteral))
            {
                if (isNegative) { DebugConsole.LogError("Cannot negate a string."); HasError = true; }
                code.Add((byte)OpCode.PushString);
                WriteStringId(Previous().Value);
            }
            else if (Match(TokenType.Identifier)) 
            {
                if (isNegative) { DebugConsole.LogError("Cannot negate an identifier."); HasError = true; }
                code.Add((byte)OpCode.PushString);
                WriteStringId(Previous().Value);
            }
            else if (Match(TokenType.True)) 
            { 
                if (isNegative) { DebugConsole.LogError("Cannot negate a boolean."); HasError = true; }
                code.Add((byte)OpCode.PushBool); code.Add(1); 
            }
            else if (Match(TokenType.False)) 
            { 
                if (isNegative) { DebugConsole.LogError("Cannot negate a boolean."); HasError = true; }
                code.Add((byte)OpCode.PushBool); code.Add(0); 
            }
            else if (Match(TokenType.LParen))
            {
                if (isNegative) { DebugConsole.LogError("Unary minus before parenthesis is not supported."); HasError = true; }
                CompileExpression();
                Consume(TokenType.RParen, "Expected ')' after expression.");
            }
            else
            {
                DebugConsole.LogError($"[Syntax] Unexpected token: '{Peek().Value}'.");
                HasError = true;
                Advance();
            }
        }

        private bool IsAtEnd() => pos >= tokens.Count || tokens[pos].Type == TokenType.EndOfFile;
        private Token Peek() => pos < tokens.Count ? tokens[pos] : new Token(TokenType.EndOfFile, string.Empty);
        private Token Previous() => pos > 0 && pos - 1 < tokens.Count ? tokens[pos - 1] : new Token(TokenType.Unknown, string.Empty);
        private Token Advance() { if (!IsAtEnd()) pos++; return Previous(); }
        private bool Check(TokenType type) => !IsAtEnd() && Peek().Type == type;
        
        private bool Match(params TokenType[] types)
        {
            foreach (var type in types)
            {
                if (Check(type)) { Advance(); return true; }
            }
            return false;
        }
        
        private void Consume(TokenType type, string message)
        {
            if (Check(type)) Advance();
            else 
            { 
                DebugConsole.LogError($"[Compiler Error] {message}"); 
                HasError = true; 
            }
        }
        
        public void WriteInt(int value)
        {
            code.Add((byte)OpCode.PushInt);
            code.Add((byte)(value & 0xFF));
            code.Add((byte)((value >> 8) & 0xFF));
            code.Add((byte)((value >> 16) & 0xFF));
            code.Add((byte)((value >> 24) & 0xFF));
        }

        public unsafe void WriteFloat(float value)
        {
            code.Add((byte)OpCode.PushFloat);
            uint raw = *(uint*)&value;
            code.Add((byte)(raw & 0xFF));
            code.Add((byte)((raw >> 8) & 0xFF));
            code.Add((byte)((raw >> 16) & 0xFF));
            code.Add((byte)((raw >> 24) & 0xFF));
        }

        private void WriteStringId(string str)
        {
            if (!stringLookup.TryGetValue(str, out ushort index))
            {
                index = (ushort)strings.Count;
                strings.Add(str);
                stringLookup[str] = index;
            }
            code.Add((byte)(index & 0xFF));
            code.Add((byte)((index >> 8) & 0xFF));
        }

        public unsafe void Execute()
        {
            if (HasError || code.Count == 0) return; 
            Execute(code.ToArray(), new List<string>(strings));
        }

        public unsafe void Execute(byte[] bytecode, List<string> scriptStrings)
        {
            stackPtr = 0;
            int ip = 0;

            try 
            {
                while (ip < bytecode.Length)
                {
                    OpCode op = (OpCode)bytecode[ip++];
                    switch (op)
                    {
                        case OpCode.PushInt:
                            if (stackPtr >= stack.Length) throw new Exception("Stack overflow.");
                            int iVal = bytecode[ip] | (bytecode[ip + 1] << 8) | (bytecode[ip + 2] << 16) | (bytecode[ip + 3] << 24);
                            ip += 4;
                            stack[stackPtr++] = Value.FromInt(iVal);
                            break;

                        case OpCode.PushFloat:
                            if (stackPtr >= stack.Length) throw new Exception("Stack overflow.");
                            uint fRaw = (uint)(bytecode[ip] | (bytecode[ip + 1] << 8) | (bytecode[ip + 2] << 16) | (bytecode[ip + 3] << 24));
                            ip += 4;
                            stack[stackPtr++] = Value.FromFloat(*(float*)&fRaw);
                            break;

                        case OpCode.PushString:
                            if (stackPtr >= stack.Length) throw new Exception("Stack overflow.");
                            ushort sIdx = (ushort)(bytecode[ip] | (bytecode[ip + 1] << 8));
                            ip += 2;
                            stack[stackPtr++] = Value.FromString(sIdx);
                            break;
                            
                        case OpCode.PushBool:
                            if (stackPtr >= stack.Length) throw new Exception("Stack overflow.");
                            bool bVal = bytecode[ip++] != 0;
                            stack[stackPtr++] = Value.FromBool(bVal);
                            break;

                        case OpCode.Add:
                        case OpCode.Subtract:
                        case OpCode.Multiply:
                        case OpCode.Divide:
                            if (stackPtr < 2) throw new Exception("Syntax: missing operands for math operation.");
                            
                            Value bOp = stack[--stackPtr];
                            Value aOp = stack[--stackPtr];
                            
                            if (aOp.Type == ValueType.Float || bOp.Type == ValueType.Float)
                            {
                                float f1 = aOp.Type == ValueType.Float ? aOp.AsFloat : aOp.AsInt;
                                float f2 = bOp.Type == ValueType.Float ? bOp.AsFloat : bOp.AsInt;
                                
                                float res = op switch {
                                    OpCode.Add => f1 + f2, OpCode.Subtract => f1 - f2,
                                    OpCode.Multiply => f1 * f2, _ => f1 / f2
                                };
                                stack[stackPtr++] = Value.FromFloat(res);
                            }
                            else
                            {
                                int res = op switch {
                                    OpCode.Add => aOp.AsInt + bOp.AsInt, OpCode.Subtract => aOp.AsInt - bOp.AsInt,
                                    OpCode.Multiply => aOp.AsInt * bOp.AsInt, _ => aOp.AsInt / bOp.AsInt
                                };
                                stack[stackPtr++] = Value.FromInt(res);
                            }
                            break;

                        case OpCode.CallCommand:
                            ushort cmdIdx = (ushort)(bytecode[ip] | (bytecode[ip + 1] << 8));
                            ip += 2;
                            byte argCount = bytecode[ip++];
                            string cmdName = scriptStrings[cmdIdx];

                            if (stackPtr < argCount) 
                                throw new Exception($"Syntax error: invalid arguments for '{cmdName}'.");

                            Value[] args = new Value[argCount];
                            for (int i = argCount - 1; i >= 0; i--)
                            {
                                args[i] = stack[--stackPtr];
                            }
                            
                            CommandRegistry.TryExecute(cmdName, args, scriptStrings);
                            break;

                        case OpCode.Return:
                            return;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugConsole.LogError($"[VM Execution Error] {ex.Message}");
            }
        }
    }
}