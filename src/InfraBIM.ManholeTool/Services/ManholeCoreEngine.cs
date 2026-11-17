using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using InfraBIM.ManholeTool.Models;

namespace InfraBIM.ManholeTool.Services
{
    /// <summary>
    /// Lõi thực thi Revit API cho công cụ Rải Hố Ga
    /// Đặt Family, tính chiều cao thân, xoay góc PV, gán biến hình học theo STT, vật liệu và metadata
    /// </summary>
    public static class ManholeCoreEngine
    {
        public static (int TraiCount, int PhaiCount, int ErrorCount, List<string> Logs) BuildAllManholes(
            Document doc,
            IList<ManholeRowData> listTrai,
            IList<ManholeRowData> listPhai,
            bool runTrai,
            bool runPhai,
            FamilySymbol symbol,
            Level? level,
            bool placeByCdDinh,
            bool useSurveyPoint,
            string heightParamName,
            double heightOffsetM,
            Dictionary<string, Dictionary<string, string>> sttOverrides,
            IList<ManholeMaterialItem> materialMappings,
            ManholeBimMetadata bimMetadata,
            IList<CustomNonGeoParamItem>? customNonGeoParams,
            Action<int, int, string>? progressCallback = null)
        {
            var logs = new List<string>();
            int traiCount = 0;
            int phaiCount = 0;
            int errorCount = 0;

            if (doc == null || symbol == null)
            {
                logs.Add("LỖI: Document hoặc FamilySymbol chưa được xác định.");
                return (0, 0, 1, logs);
            }

            var targets = new List<ManholeRowData>();
            if (runTrai && listTrai != null)
            {
                targets.AddRange(listTrai.Where(r => r.IsSelected));
            }
            if (runPhai && listPhai != null)
            {
                targets.AddRange(listPhai.Where(r => r.IsSelected));
            }

            if (targets.Count == 0)
            {
                logs.Add("Không có hố ga nào được chọn để rải.");
                return (0, 0, 0, logs);
            }

            // Cache Material Ids để tối ưu tốc độ gán vật liệu
            var materialIdCache = new Dictionary<string, ElementId>(StringComparer.OrdinalIgnoreCase);
            if (materialMappings != null)
            {
                foreach (var mat in materialMappings)
                {
                    if (!string.IsNullOrWhiteSpace(mat.SelectedMaterialName) && !materialIdCache.ContainsKey(mat.SelectedMaterialName))
                    {
                        var matElem = new FilteredElementCollector(doc)
                            .OfClass(typeof(Material))
                            .Cast<Material>()
                            .FirstOrDefault(m => string.Equals(m.Name, mat.SelectedMaterialName, StringComparison.OrdinalIgnoreCase));
                        if (matElem != null)
                        {
                            materialIdCache[mat.SelectedMaterialName] = matElem.Id;
                        }
                    }
                }
            }

            using var tg = new TransactionGroup(doc, "ĐV_TOOL_HTKT - Rải Hố Ga Tự Động");
            tg.Start();

            // 1. Kích hoạt FamilySymbol nếu chưa kích hoạt
            if (!symbol.IsActive)
            {
                using var tAct = new Transaction(doc, "Kích hoạt Family Symbol Hố Ga");
                tAct.Start();
                symbol.Activate();
                doc.Regenerate();
                tAct.Commit();
            }

            // 2. Mở Transaction chính để rải toàn bộ hố ga
            using (var t = new Transaction(doc, "Rải các Hố Ga"))
            {
                var failOpts = t.GetFailureHandlingOptions();
                failOpts.SetFailuresPreprocessor(new SuppressAllWarningsFailuresPreprocessor());
                failOpts.SetClearAfterRollback(true);
                t.SetFailureHandlingOptions(failOpts);

                t.Start();

                int total = targets.Count;
                int current = 0;

                foreach (var row in targets)
                {
                    current++;
                    string sideKey = row.Side.Contains("TRAI") ? "TRAI" : "PHAI";
                    progressCallback?.Invoke(current, total, $"Đang rải hố ga {row.TenHoGa} ({current}/{total})...");

                    try
                    {
                        // 2.1 Tính tọa độ đặt X, Y, Z
                        double zM = placeByCdDinh ? row.CdDinh : row.CdDay;
                        XYZ pt = CoordinateService.ConvertVN2000ToRevitInternal(doc, row.ToaDoX, row.ToaDoY, zM, useSurveyPoint);

                        // 2.2 Đặt FamilyInstance
                        FamilyInstance inst;
                        if (level != null)
                        {
                            inst = doc.Create.NewFamilyInstance(pt, symbol, level, StructuralType.NonStructural);
                            Parameter pOffset = inst.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
                            if (pOffset != null && !pOffset.IsReadOnly)
                            {
                                pOffset.Set(pt.Z - level.Elevation);
                            }
                        }
                        else
                        {
                            inst = doc.Create.NewFamilyInstance(pt, symbol, StructuralType.NonStructural);
                        }

                        if (inst == null)
                        {
                            logs.Add($"LỖI [{row.Side} - STT {row.STT}]: Không tạo được FamilyInstance.");
                            errorCount++;
                            continue;
                        }

                        // 2.3 Xoay hố ga theo Góc Phương Vị (GOC PV)
                        if (Math.Abs(row.GocPv) > 1e-4)
                        {
                            try
                            {
                                double gocPvRad = row.GocPv * Math.PI / 180.0;
                                Line rotAxis = Line.CreateBound(pt, pt + XYZ.BasisZ);
                                ElementTransformUtils.RotateElement(doc, inst.Id, rotAxis, gocPvRad);
                            }
                            catch (Exception exRot)
                            {
                                logs.Add($"Cảnh báo xoay hố ga {row.TenHoGa}: {exRot.Message}");
                            }
                        }

                        // 2.4 Gán Chiều cao thân tự động = (CD DINH - CD DAY) + Offset
                        if (!string.IsNullOrWhiteSpace(heightParamName))
                        {
                            double hM = (row.CdDinh - row.CdDay) + heightOffsetM;
                            double hFeet = UnitUtils.ConvertToInternalUnits(hM, UnitTypeId.Meters);
                            SetParameterValue(inst, heightParamName, hFeet);
                        }

                        // 2.5 Gán các tham số hình học tùy biến theo STT
                        ApplySttCustomParameters(inst, row.STT, sideKey, sttOverrides);

                        // 2.6 Gán Vật liệu
                        ApplyMaterials(inst, row.Side, materialMappings, materialIdCache);

                        // 2.7 Gán Thông tin Phi hình học & Metadata BIM
                        ApplyBimMetadata(doc, inst, row, bimMetadata, customNonGeoParams, materialMappings, materialIdCache);

                        if (row.Side.Contains("TRAI"))
                        {
                            traiCount++;
                        }
                        else
                        {
                            phaiCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        logs.Add($"LỖI [{row.Side} - {row.TenHoGa}]: {ex.Message}");
                    }
                }

                t.Commit();
            }

            tg.Assimilate();

            logs.Add($"HOÀN THÀNH: Đã tạo {traiCount} hố ga trái, {phaiCount} hố ga phải. Số lỗi: {errorCount}.");
            return (traiCount, phaiCount, errorCount, logs);
        }

        private static void ApplySttCustomParameters(
            FamilyInstance inst, 
            int stt, 
            string sideKey, 
            Dictionary<string, Dictionary<string, string>> sttOverrides)
        {
            if (sttOverrides == null || sttOverrides.Count == 0) return;

            // Tìm cấu hình ghi đè cho STT này (Ưu tiên theo nhánh: TRAI_1, sau đó ALL_1)
            Dictionary<string, string>? overrideDict = null;
            string keySide = $"{sideKey}_{stt}";
            string keyAll = $"ALL_{stt}";

            if (sttOverrides.TryGetValue(keySide, out var specificDict))
            {
                overrideDict = specificDict;
            }
            else if (sttOverrides.TryGetValue(keyAll, out var allDict))
            {
                overrideDict = allDict;
            }

            if (overrideDict == null) return;

            foreach (var kvp in overrideDict)
            {
                string paramName = kvp.Key;
                string strVal = kvp.Value;
                SetParameterFromString(inst, paramName, strVal);
            }
        }

        private static void ApplyMaterials(
            FamilyInstance inst, 
            string side, 
            IList<ManholeMaterialItem>? materialMappings, 
            Dictionary<string, ElementId> cache)
        {
            if (materialMappings == null || materialMappings.Count == 0) return;

            foreach (var item in materialMappings)
            {
                if (string.IsNullOrWhiteSpace(item.SelectedMaterialName)) continue;

                // Kiểm tra phạm vi áp dụng (ALL, HO GA TRAI, HO GA PHAI)
                if (item.TargetSide != "ALL" && !string.Equals(item.TargetSide, side, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (cache.TryGetValue(item.SelectedMaterialName, out ElementId? matId) && matId != null)
                {
                    // Gán vào instance
                    SetParameterValue(inst, item.ParamName, matId);
                    // Nếu là type parameter thì thử gán vào symbol nếu cần
                    if (item.IsType && inst.Symbol != null)
                    {
                        SetParameterValue(inst.Symbol, item.ParamName, matId);
                    }
                }
            }
        }

        private static void ApplyBimMetadata(
            Document doc,
            FamilyInstance inst,
            ManholeRowData row,
            ManholeBimMetadata meta,
            IList<CustomNonGeoParamItem>? customParams,
            IList<ManholeMaterialItem>? materialMappings,
            Dictionary<string, ElementId> materialCache)
        {
            string sideLabel = row.Side.Contains("TRAI") ? meta.LeftPrefix : meta.RightPrefix;

            // 1. Gán Tên hố ga từ Excel
            if (!string.IsNullOrWhiteSpace(meta.TenHoGaParamName))
            {
                SetParameterValue(inst, meta.TenHoGaParamName, row.TenHoGa);
            }

            // 2. Gán vị trí / bên tuyến
            if (!string.IsNullOrWhiteSpace(meta.ViTriParamName))
            {
                SetParameterValue(inst, meta.ViTriParamName, sideLabel);
            }

            // 3. Gán các thông tin dự án tiêu chuẩn
            SetParameterValue(inst, "Tên công trình", meta.TenCongTrinh);
            SetParameterValue(inst, "Hạng mục", meta.HangMuc);
            SetParameterValue(inst, "Gói thầu", meta.GoiThau);
            SetParameterValue(inst, "Đơn vị tư vấn", meta.DonViTuVan);
            SetParameterValue(inst, "Nhà thầu thi công", meta.NhaThauThiCong);
            SetParameterValue(inst, "Lý trình", meta.LyTrinhTuyen);
            SetParameterValue(inst, "Ghi chú", meta.GhiChu);
            SetParameterValue(inst, "Mã cấu kiện", meta.MaCauKien);

            // 4. Gán các tham số văn bản tùy biến khác
            if (customParams != null)
            {
                foreach (var cp in customParams)
                {
                    if (!string.IsNullOrWhiteSpace(cp.ParamName) && !string.IsNullOrWhiteSpace(cp.ParamValue))
                    {
                        SetParameterValue(inst, cp.ParamName, cp.ParamValue);
                    }
                }
            }

            // 5. ĐỒNG BỘ XUỐNG CÁC FAMILY BỘ PHẬN CON (SubComponents)
            if (meta.SyncSubComponents)
            {
                try
                {
                    var subIds = inst.GetSubComponentIds();
                    if (subIds != null && subIds.Count > 0)
                    {
                        foreach (var subId in subIds)
                        {
                            var subElem = doc.GetElement(subId);
                            if (subElem != null)
                            {
                                // Gán tên hố ga và nhánh
                                if (!string.IsNullOrWhiteSpace(meta.TenHoGaParamName))
                                    SetParameterValue(subElem, meta.TenHoGaParamName, row.TenHoGa);
                                if (!string.IsNullOrWhiteSpace(meta.ViTriParamName))
                                    SetParameterValue(subElem, meta.ViTriParamName, sideLabel);

                                // Gán thông tin dự án
                                SetParameterValue(subElem, "Tên công trình", meta.TenCongTrinh);
                                SetParameterValue(subElem, "Hạng mục", meta.HangMuc);
                                SetParameterValue(subElem, "Gói thầu", meta.GoiThau);
                                SetParameterValue(subElem, "Ghi chú", meta.GhiChu);
                                SetParameterValue(subElem, "Mã cấu kiện", meta.MaCauKien);

                                // Gán vật liệu nếu cấu kiện con có chung biến vật liệu
                                if (materialMappings != null)
                                {
                                    foreach (var matItem in materialMappings)
                                    {
                                        if (string.IsNullOrWhiteSpace(matItem.SelectedMaterialName)) continue;
                                        if (materialCache.TryGetValue(matItem.SelectedMaterialName, out ElementId? subMatId) && subMatId != null)
                                        {
                                            SetParameterValue(subElem, matItem.ParamName, subMatId);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }
        }

        #region Helper Set Parameters An Toàn

        public static bool SetParameterValue(Element elem, string paramName, object val)
        {
            if (elem == null || string.IsNullOrWhiteSpace(paramName) || val == null) return false;

            Parameter p = elem.LookupParameter(paramName);
            if (p == null || p.IsReadOnly) return false;

            try
            {
                switch (p.StorageType)
                {
                    case StorageType.String:
                        p.Set(val.ToString() ?? "");
                        return true;
                    case StorageType.Double:
                        if (val is double dVal) { p.Set(dVal); return true; }
                        if (double.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedD))
                        {
                            p.Set(parsedD);
                            return true;
                        }
                        break;
                    case StorageType.Integer:
                        if (val is int iVal) { p.Set(iVal); return true; }
                        if (val is bool bVal) { p.Set(bVal ? 1 : 0); return true; }
                        if (int.TryParse(val.ToString(), out int parsedI))
                        {
                            p.Set(parsedI);
                            return true;
                        }
                        break;
                    case StorageType.ElementId:
                        if (val is ElementId idVal) { p.Set(idVal); return true; }
                        break;
                }
            }
            catch { }

            return false;
        }

        public static bool SetParameterFromString(Element elem, string paramName, string strVal)
        {
            if (elem == null || string.IsNullOrWhiteSpace(paramName)) return false;
            Parameter p = elem.LookupParameter(paramName);
            if (p == null || p.IsReadOnly) return false;

            try
            {
                switch (p.StorageType)
                {
                    case StorageType.String:
                        p.Set(strVal);
                        return true;

                    case StorageType.Double:
                        // Nếu là tham số chiều dài, chuyển từ Mét sang Feet
                        if (double.TryParse(strVal.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double valDouble))
                        {
                            try
                            {
                                var dt = p.Definition.GetDataType();
                                if (dt == SpecTypeId.Length)
                                {
                                    valDouble = UnitUtils.ConvertToInternalUnits(valDouble, UnitTypeId.Meters);
                                }
                                else if (dt == SpecTypeId.Angle)
                                {
                                    valDouble = valDouble * Math.PI / 180.0;
                                }
                            }
                            catch { }

                            p.Set(valDouble);
                            return true;
                        }
                        break;

                    case StorageType.Integer:
                        if (bool.TryParse(strVal, out bool bVal))
                        {
                            p.Set(bVal ? 1 : 0);
                            return true;
                        }
                        if (int.TryParse(strVal, out int iVal))
                        {
                            p.Set(iVal);
                            return true;
                        }
                        break;
                }
            }
            catch { }

            return false;
        }

        #endregion
    }

    /// <summary>
    /// Bộ triệt tiêu toàn bộ cảnh báo không nghiêm trọng trong tiến trình rải hố ga
    /// </summary>
    public class SuppressAllWarningsFailuresPreprocessor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            var failureMessages = failuresAccessor.GetFailureMessages();
            foreach (var fmsg in failureMessages)
            {
                var severity = fmsg.GetSeverity();
                if (severity == FailureSeverity.Warning)
                {
                    failuresAccessor.DeleteWarning(fmsg);
                }
                else if (severity == FailureSeverity.Error)
                {
                    if (failuresAccessor.IsFailureResolutionPermitted(fmsg))
                    {
                        failuresAccessor.ResolveFailure(fmsg);
                    }
                }
            }
            return FailureProcessingResult.Continue;
        }
    }
}
