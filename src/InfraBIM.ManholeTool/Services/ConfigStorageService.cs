using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using InfraBIM.ManholeTool.Models;

namespace InfraBIM.ManholeTool.Services
{
    /// <summary>
    /// Dịch vụ Lưu và Đọc cấu hình công cụ Rải Hố Ga dạng file .json
    /// </summary>
    public static class ConfigStorageService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true
        };

        public static void SaveConfig(string filePath, ManholeConfigProfile profile)
        {
            if (string.IsNullOrWhiteSpace(filePath) || profile == null) return;
            string json = JsonSerializer.Serialize(profile, JsonOptions);
            File.WriteAllText(filePath, json);
        }

        public static ManholeConfigProfile? LoadConfig(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<ManholeConfigProfile>(json, JsonOptions);
        }
    }
}
