﻿using System;
using System.IO;
using Newtonsoft.Json;

namespace OsuHelper.Services
{
    public class FileCacheService : ICacheService
    {
        private readonly string _cacheDirPath;

        public FileCacheService()
        {
            _cacheDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache\\");
        }

        private string GetCacheFilePath(string id)
        {
            return Path.Combine(_cacheDirPath, id + ".ch");
        }

        public void Store<T>(string key, T obj) where T : class
        {
            if (typeof(T) == typeof(string))
            {
                string id = "Text_" + key;
                Directory.CreateDirectory(_cacheDirPath);
                File.WriteAllText(GetCacheFilePath(id), obj as string);
            }
            if (typeof(T) == typeof(Stream))
            {
                string id = "Bin_" + key;
                Directory.CreateDirectory(_cacheDirPath);
                using (var output = File.Create(GetCacheFilePath(id)))
                    (obj as Stream)?.CopyTo(output);
            }
            else
            {
                string id = typeof(T).Name + "_" + key;
                string serialized = JsonConvert.SerializeObject(obj);
                Directory.CreateDirectory(_cacheDirPath);
                File.WriteAllText(GetCacheFilePath(id), serialized);
            }
        }

        public T RetrieveOrDefault<T>(string key, T defaultValue = default(T)) where T : class
        {
            if (typeof(T) == typeof(string))
            {
                string id = "Text_" + key;
                if (File.Exists(GetCacheFilePath(id)))
                    return File.ReadAllText(GetCacheFilePath(id)) as T ?? defaultValue;
                return defaultValue;
            }
            if (typeof(T) == typeof(Stream))
            {
                string id = "Bin_" + key;
                if (File.Exists(GetCacheFilePath(id)))
                    return File.OpenRead(GetCacheFilePath(id)) as T ?? defaultValue;
                return defaultValue;
            }
            else
            {
                string id = typeof(T).Name + "_" + key;
                if (File.Exists(GetCacheFilePath(id)))
                {
                    string serialized = File.ReadAllText(GetCacheFilePath(id));
                    return JsonConvert.DeserializeObject<T>(serialized);
                }
                return defaultValue;
            }
        }
    }
}