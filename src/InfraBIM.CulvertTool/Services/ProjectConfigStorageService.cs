using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using InfraBIM.CulvertTool.Models;

namespace InfraBIM.CulvertTool.Services
{
    /// <summary>
    /// Quản lý Lưu trữ và Khôi phục trạng thái dự án cống:
    /// 1. Extensible Storage trong chính file Revit (.rvt)
    /// 2. Local Auto-Save phiên gần nhất (LastSessionConfig.json)
    /// 3. Xuất / Nạp mẫu Preset dự án (.json)
    /// </summary>
    public static class ProjectConfigStorageService
    {
        // GUID định danh duy nhất cho Schema Extensible Storage của Tool Cống
        private static readonly Guid SchemaGuid = new Guid("770B1DEC-7651-4B8E-9D11-6C23F1E85A74");

        private static Schema? GetOrCreateSchema()
        {
            StartupLogger.Log("[ConfigStorage] GetOrCreateSchema: Looking up schema...");
            Schema schema = Schema.Lookup(SchemaGuid);
            if (schema != null)
            {
                StartupLogger.Log("[ConfigStorage] GetOrCreateSchema: Found existing schema.");
                return schema;
            }

            try
            {
                StartupLogger.Log("[ConfigStorage] GetOrCreateSchema: Building schema...");
                SchemaBuilder builder = new SchemaBuilder(SchemaGuid);
                builder.SetSchemaName("DV_TOOL_HTKT_ProjectState");
                builder.SetReadAccessLevel(AccessLevel.Public);
                builder.SetWriteAccessLevel(AccessLevel.Public);
                builder.AddSimpleField("JsonData", typeof(string));
                var created = builder.Finish();
                StartupLogger.Log("[ConfigStorage] GetOrCreateSchema: Schema created successfully.");
                return created;
            }
            catch (Exception ex)
            {
                StartupLogger.Log($"[ConfigStorage] GetOrCreateSchema: Exception during Finish: {ex.Message}");
                return Schema.Lookup(SchemaGuid);
            }
        }

        /// <summary>
        /// Lưu cấu hình trực tiếp vào trong file Revit (.rvt) qua Extensible Storage
        /// </summary>
        public static bool SaveToDocument(Document doc, ProjectStateConfig config)
        {
            if (doc == null || config == null || doc.IsReadOnly) return false;
            try
            {
                Schema? schema = GetOrCreateSchema();
                if (schema == null) return false;

                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });

                Action saveAction = () =>
                {
                    DataStorage? ds = new FilteredElementCollector(doc)
                        .OfClass(typeof(DataStorage))
                        .Cast<DataStorage>()
                        .FirstOrDefault(d => d.GetEntity(schema).IsValid());

                    if (ds == null)
                    {
                        ds = DataStorage.Create(doc);
                    }

                    Entity entity = new Entity(schema);
                    entity.Set("JsonData", json);
                    ds.SetEntity(entity);
                };

                if (doc.IsModifiable)
                {
                    using (SubTransaction st = new SubTransaction(doc))
                    {
                        st.Start();
                        saveAction();
                        st.Commit();
                    }
                }
                else
                {
                    using (Transaction tx = new Transaction(doc, "DV_TOOL: Lưu cấu hình dự án cống"))
                    {
                        tx.Start();
                        saveAction();
                        tx.Commit();
                    }
                }

                // Đồng thời tự động cập nhật LastSession trên máy
                SaveToLastSession(config);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Nạp cấu hình đã lưu từ chính file Revit (.rvt) đang mở
        /// </summary>
        public static ProjectStateConfig? LoadFromDocument(Document doc)
        {
            if (doc == null) return null;
            try
            {
                StartupLogger.Log("[ConfigStorage] LoadFromDocument START");
                Schema? schema = GetOrCreateSchema();
                if (schema == null)
                {
                    StartupLogger.Log("[ConfigStorage] LoadFromDocument: schema is null, aborting");
                    return null;
                }

                StartupLogger.Log("[ConfigStorage] LoadFromDocument: Querying DataStorage elements...");
                DataStorage? ds = new FilteredElementCollector(doc)
                    .OfClass(typeof(DataStorage))
                    .Cast<DataStorage>()
                    .FirstOrDefault(d =>
                    {
                        try { return d.GetEntity(schema).IsValid(); } catch { return false; }
                    });

                if (ds == null)
                {
                    StartupLogger.Log("[ConfigStorage] LoadFromDocument: No DataStorage found with schema.");
                    return null;
                }

                StartupLogger.Log("[ConfigStorage] LoadFromDocument: Found DataStorage, getting entity...");
                Entity entity = ds.GetEntity(schema);
                if (!entity.IsValid()) return null;

                string json = entity.Get<string>("JsonData");
                if (string.IsNullOrWhiteSpace(json)) return null;

                StartupLogger.Log("[ConfigStorage] LoadFromDocument: Deserializing JSON...");
                return JsonSerializer.Deserialize<ProjectStateConfig>(json);
            }
            catch (Exception ex)
            {
                StartupLogger.Log($"[ConfigStorage] LoadFromDocument Exception: {ex.Message}");
                return null;
            }
        }

        private static string GetLastSessionFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "Autodesk", "Revit", "Addins", "2025", "DV_TOOL_HTKT", "UserConfig");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "LastSessionConfig.json");
        }

        /// <summary>
        /// Tự động ghi nhớ phiên làm việc gần nhất trên máy tính
        /// </summary>
        public static bool SaveToLastSession(ProjectStateConfig config)
        {
            if (config == null) return false;
            try
            {
                string path = GetLastSessionFilePath();
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json, System.Text.Encoding.UTF8);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Khôi phục cấu hình từ phiên làm việc gần nhất trên máy tính
        /// </summary>
        public static ProjectStateConfig? LoadFromLastSession()
        {
            try
            {
                string path = GetLastSessionFilePath();
                if (!File.Exists(path)) return null;
                string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json)) return null;
                var res = JsonSerializer.Deserialize<ProjectStateConfig>(json);
                if (res != null && !string.IsNullOrEmpty(res.ExcelFilePath) && !File.Exists(res.ExcelFilePath))
                {
                    res.ExcelFilePath = string.Empty;
                }
                return res;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Xuất cấu hình ra file mẫu Preset (.json) để chia sẻ hoặc lưu trữ
        /// </summary>
        public static bool ExportPresetToFile(string filePath, ProjectStateConfig config)
        {
            if (string.IsNullOrEmpty(filePath) || config == null) return false;
            try
            {
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Nạp cấu hình từ file mẫu Preset (.json)
        /// </summary>
        public static ProjectStateConfig? ImportPresetFromFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;
            try
            {
                string json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonSerializer.Deserialize<ProjectStateConfig>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}
