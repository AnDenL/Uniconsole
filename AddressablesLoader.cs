using UnityEngine.AddressableAssets;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections.Generic;
using UnityEngine.AddressableAssets.ResourceLocators;
using System;
using Object = UnityEngine.Object;
using UnityEngine.SceneManagement;

namespace DevConsole 
{
    public static class AddressablesLoader
    {
        public const string BasePath = "Assets/Game/";
        
        private static readonly Dictionary<string, AsyncOperationHandle> CachedHandles = new();
        private static readonly Dictionary<Type, string> TypeFolders = new()
        {
            { typeof(GameObject), "Prefabs/" },
            { typeof(UpgradeData), "Upgrades/" },
            { typeof(AudioClip), "Audio/" }
        };

        private static readonly Dictionary<Type, List<string>> TypeCache = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInit()
        {
            var initHandle = Addressables.InitializeAsync();
            initHandle.Completed += op =>
            {
                RefreshKeys();
            };

            SceneManager.activeSceneChanged += (s1, s2) => ClearCache();
        }

        public static void ClearCache()
        {
            foreach (var handle in CachedHandles.Values)
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
            CachedHandles.Clear();
        }

        public static string FormatPath<T>(string assetName)
        {
            if (assetName.StartsWith("Assets/")) return assetName;

            string subFolder = TypeFolders.TryGetValue(typeof(T), out var folder) ? folder : "";
            
            string extension = typeof(T) == typeof(GameObject) ? ".prefab" : 
                            typeof(T) == typeof(AudioClip) ? ".ogg" : ".asset"; 

            return $"{BasePath}{subFolder}{assetName}{extension}";
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

        public static void RefreshKeys()
        {
            TypeCache.Clear();

            var prefixes = new Dictionary<Type, string>();
            foreach (var type in TypeFolders.Keys)
            {
                TypeCache[type] = new List<string>();
                prefixes[type] = $"{BasePath}{TypeFolders[type]}";
            }

            foreach (IResourceLocator locator in Addressables.ResourceLocators)
            {
                foreach (object keyObj in locator.Keys)
                {
                    if (keyObj is string key && !IsInternalGuid(key))
                    {
                        foreach (var kvp in prefixes)
                        {
                            if (key.StartsWith(kvp.Value, StringComparison.OrdinalIgnoreCase))
                            {
                                string shortName = System.IO.Path.GetFileNameWithoutExtension(key);
                                
                                if (!TypeCache[kvp.Key].Contains(shortName))
                                {
                                    TypeCache[kvp.Key].Add(shortName);
                                }
                                
                                break; 
                            }
                        }
                    }
                }
            }
        }

        private static bool IsInternalGuid(string key) => key.Length == 32 && Guid.TryParse(key, out _);

        public static List<string> GetSuggestionsForType<T>(string query) where T : Object
        {
            var results = new List<string>();
            
            if (!TypeCache.TryGetValue(typeof(T), out var cachedNames))
                return results;

            foreach (string shortName in cachedNames)
            {
                if (string.IsNullOrEmpty(query) || shortName.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(shortName);
                }
            }

            return results;
        }
    }
}