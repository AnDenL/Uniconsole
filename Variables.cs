using System;
using System.Collections.Generic;

namespace DevConsole
{
    public abstract class ConVar
    {
        public string Name { get; protected set; }
        public string Description { get; protected set; }
        public ValueType Type { get; protected set; }

        public abstract void SetVMValue(Value val, List<string> pool);
        public abstract string AsString();
    }

    public class ConVarInt : ConVar
    {
        private int _val;
        
        public Func<int> Getter;
        public Action<int> Setter;
        public Action<int> OnChange; 

        public int Value 
        { 
            get => Getter != null ? Getter() : _val; 
            set 
            { 
                _val = value; 
                Setter?.Invoke(value); 
                OnChange?.Invoke(value); 
            } 
        }

        public ConVarInt(string name, int defaultVal, string desc = "", Func<int> getter = null, Action<int> setter = null, Action<int> onChange = null)
        {
            Name = name; _val = defaultVal; Description = desc; Type = ValueType.Int;
            Getter = getter; Setter = setter; OnChange = onChange;
            
            CommandRegistry.RegisterConVar(this);
        }

        public override void SetVMValue(Value val, List<string> pool)
        {
            if (val.TryGetInt(pool, out int res)) Value = res;
            else throw new Exception($"Cannot convert '{val.ToString(pool)}' to integer.");
        }

        public override string AsString() => Value.ToString();
    }

    public class ConVarFloat : ConVar
    {
        private float _val;
        public Func<float> Getter;
        public Action<float> Setter;
        public Action<float> OnChange;

        public float Value 
        { 
            get => Getter != null ? Getter() : _val; 
            set 
            { 
                _val = value; 
                Setter?.Invoke(value); 
                OnChange?.Invoke(value); 
            } 
        }

        public ConVarFloat(string name, float defaultVal, string desc = "", Func<float> getter = null, Action<float> setter = null, Action<float> onChange = null)
        {
            Name = name; _val = defaultVal; Description = desc; Type = ValueType.Float;
            Getter = getter; Setter = setter; OnChange = onChange;
            CommandRegistry.RegisterConVar(this);
        }

        public override void SetVMValue(Value val, List<string> pool)
        {
            if (val.TryGetFloat(pool, out float res)) Value = res;
            else throw new Exception($"Cannot convert '{val.ToString(pool)}' to float.");
        }

        public override string AsString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public class ConVarBool : ConVar
    {
        private bool _val;
        public Func<bool> Getter;
        public Action<bool> Setter;
        public Action<bool> OnChange;

        public bool Value 
        { 
            get => Getter != null ? Getter() : _val; 
            set 
            { 
                _val = value; 
                Setter?.Invoke(value); 
                OnChange?.Invoke(value); 
            } 
        }

        public ConVarBool(string name, bool defaultVal, string desc = "", Func<bool> getter = null, Action<bool> setter = null, Action<bool> onChange = null)
        {
            Name = name; _val = defaultVal; Description = desc; Type = ValueType.Bool;
            Getter = getter; Setter = setter; OnChange = onChange;
            CommandRegistry.RegisterConVar(this);
        }

        public override void SetVMValue(Value val, List<string> pool)
        {
            if (val.Type == ValueType.Bool) Value = val.AsBool;
            else if (val.TryGetInt(pool, out int res)) Value = res != 0;
            else if (val.Type == ValueType.String && pool != null)
            {
                string s = pool[val.StringIndex].ToLower();
                if (s == "true") Value = true;
                else if (s == "false") Value = false;
                else throw new Exception("Expected 'true', 'false', 1 or 0.");
            }
            else throw new Exception("Cannot convert to boolean.");
        }

        public override string AsString() => Value ? "true" : "false";
    }
}