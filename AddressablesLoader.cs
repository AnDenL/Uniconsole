using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DevConsole
{
    public static class AddressablesLoader
    {
        public const string BasePath = "Assets/Game/";

        public readonly struct TypeRule
        {
            public readonly string SubFolder;
            public readonly string Extension;

            public TypeRule(string subFolder, string extension)
            {
                SubFolder = subFolder;
                Extension = extension.StartsWith(".") ? extension : $".{extension}";
            }
        }

        private static readonly Dictionary<string, AsyncOperationHandle> CachedHandles = new();
        private static readonly Dictionary<Type, TypeRule> TypeRules = new();
        private static readonly Dictionary<Type, List<string>> TypeCache = new();
        private static bool isInitialized;

        static AddressablesLoader()
        {
            RegisterType<GameObject>("Prefabs/", ".prefab");
            RegisterType<AudioClip>("Audio/", ".ogg");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInit()
        {
            var initHandle = Addressables.InitializeAsync();
            initHandle.Completed += _ =>
            {
                isInitialized = true;
                RefreshKeys();
            };
            SceneManager.activeSceneChanged += (_, _) => ClearCache();
        }

        public static void RegisterType<T>(string subFolder, string extension = ".asset") where T : Object
        {
            RegisterType(typeof(T), subFolder, extension);
        }

        public static void RegisterType(Type type, string subFolder, string extension = ".asset")
        {
            TypeRules[type] = new TypeRule(subFolder, extension);

            if (!TypeCache.ContainsKey(type))
                TypeCache[type] = new List<string>();

            if (isInitialized)
            {
                RefreshKeysForType(type);
            }
        }

        public static string FormatPath<T>(string assetName)
        {
            if (assetName.StartsWith("Assets/")) return assetName;

            if (TypeRules.TryGetValue(typeof(T), out var rule))
            {
                return $"{BasePath}{rule.SubFolder}{assetName}{rule.Extension}";
            }

            return $"{BasePath}{assetName}.asset";
        }

        public static void RefreshKeys()
        {
            foreach (var type in TypeRules.Keys)
            {
                RefreshKeysForType(type);
            }
        }

        private static void RefreshKeysForType(Type type)
        {
            if (!TypeRules.TryGetValue(type, out var rule)) return;

            if (!TypeCache.TryGetValue(type, out var list))
            {
                list = new List<string>();
                TypeCache[type] = list;
            }
            list.Clear();

            string prefix = $"{BasePath}{rule.SubFolder}";

            foreach (IResourceLocator locator in Addressables.ResourceLocators)
            {
                foreach (object keyObj in locator.Keys)
                {
                    if (keyObj is string key && !IsInternalGuid(key))
                    {
                        if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            string shortName = Path.GetFileNameWithoutExtension(key);
                            if (!list.Contains(shortName))
                            {
                                list.Add(shortName);
                            }
                        }
                    }
                }
            }
        }

        public static bool Exists(string key)
        {
            foreach (var locator in Addressables.ResourceLocators)
            {
                if (locator.Locate(key, typeof(Object), out var locations) && locations.Count > 0)
                    return true;
            }
            return false;
        }

        public static void LoadAsync<T>(string shortName, Action<T> onLoaded) where T : Object
        {
            string fullPath = FormatPath<T>(shortName);
            if (!Exists(fullPath))
            {
                Debug.LogWarning($"[Addressables] Asset not found: '{shortName}' (Path: '{fullPath}')");
                onLoaded?.Invoke(null);
                return;
            }

            if (CachedHandles.TryGetValue(fullPath, out var cachedHandle) && cachedHandle.IsValid())
            {
                onLoaded?.Invoke(cachedHandle.Result as T);
                return;
            }

            var handle = Addressables.LoadAssetAsync<T>(fullPath);
            CachedHandles[fullPath] = handle;

            handle.Completed += op =>
            {
                if (op.Status == AsyncOperationStatus.Succeeded)
                {
                    onLoaded?.Invoke(op.Result);
                }
                else
                {
                    Debug.LogError($"[Addressables] Failed to load asset: {fullPath}");
                    CachedHandles.Remove(fullPath);
                    Addressables.Release(handle);
                    onLoaded?.Invoke(null);
                }
            };
        }

        public static void ClearCache()
        {
            foreach (var handle in CachedHandles.Values)
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
            CachedHandles.Clear();
        }

        public static List<string> GetSuggestionsForType<T>(string query) where T : Object
        {
            var results = new List<string>();
            if (!TypeCache.TryGetValue(typeof(T), out var cachedNames)) return results;

            foreach (string shortName in cachedNames)
            {
                if (string.IsNullOrEmpty(query) || shortName.Contains(query, StringComparison.OrdinalIgnoreCase))
                    results.Add(shortName);
            }
            return results;
        }

        private static bool IsInternalGuid(string key) => key.Length == 32 && Guid.TryParse(key, out _);
    }
}