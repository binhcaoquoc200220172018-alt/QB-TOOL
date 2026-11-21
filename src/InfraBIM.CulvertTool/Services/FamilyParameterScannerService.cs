using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using InfraBIM.CulvertTool.Models;

namespace InfraBIM.CulvertTool.Services
{
    public static class FamilyParameterScannerService
    {
        /// <summary>
        /// Quét toàn bộ tham số của một FamilySymbol bao gồm:
        /// 1. Giữ nguyên 100% thứ tự khai báo trong Family Types của Revit (không đảo thứ tự).
        /// 2. Lọc bỏ tuyệt đối các tham số ĐÃ CÓ GÁN FORMULA (như Angle X, A1-1, A10, A2, CX_L2, Angle TC, CH_B TONG...).
        /// 3. Phân nhóm rõ ràng: Dimensions (Kích thước) và Other (Khác).
        /// 4. Trong nhóm Other: hiển thị kiểu dữ liệu Length và Yes/No (ẩn hiện cấu kiện).
        /// 5. Bỏ qua hoàn toàn A_GÓC XOAY và không tự ý thêm biến lạ.
        /// 6. Hỗ trợ đầy đủ cả Type Parameters và Instance Parameters (như CO VAI KE).
        /// </summary>
        private static readonly Dictionary<string, List<ParameterMappingItem>> _familyParamCache = new Dictionary<string, List<ParameterMappingItem>>(StringComparer.OrdinalIgnoreCase);

        public static void ClearCache()
        {
            _familyParamCache.Clear();
        }

        private static List<ParameterMappingItem> CloneMappingList(List<ParameterMappingItem> source, string categoryName, string profileName)
        {
            return source.Select(m => new ParameterMappingItem
            {
                STT = m.STT,
                IsSelected = m.IsSelected,
                CategoryName = categoryName,
                ProfileName = profileName,
                FamilyName = m.FamilyName,
                InternalName = m.InternalName,
                DisplayName = m.DisplayName,
                GroupName = m.GroupName,
                DataType = m.DataType,
                IsDimension = m.IsDimension,
                IsOther = m.IsOther,
                IsVisibility = m.IsVisibility,
                DefaultValue = m.DefaultValue,
                CustomValue = m.CustomValue,
                MappedField = m.MappedField,
                IsInstance = m.IsInstance
            }).ToList();
        }

        public static List<ParameterMappingItem> ScanParametersForFamily(Document doc, FamilySymbol sym, string categoryName, string profileName = "")
        {
            var result = new List<ParameterMappingItem>();
            if (sym == null) return result;

            string famName = sym.FamilyName ?? "";
            string cacheKey = $"{famName}_{categoryName}_{profileName}";
            if (_familyParamCache.TryGetValue(cacheKey, out var cachedList) && cachedList != null && cachedList.Count > 0)
            {
                return CloneMappingList(cachedList, categoryName, profileName);
            }

            var processedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // =========================================================================
            // 1. ƯU TIÊN 1: ĐỌC TỪ FAMILY DEFINITION (GIỮ NGUYÊN 100% THỨ TỰ TRONG REVIT)
            // =========================================================================
            bool scannedFromFamily = false;
            Document? famDoc = null;
            bool famDocWasAlreadyOpen = false;

            try
            {
                if (doc != null && sym.Family != null)
                {
                    // 1.1 Kiểm tra nếu Family Document ĐÃ mở sẵn trong Revit
                    foreach (Document d in doc.Application.Documents)
                    {
                        if (d.IsFamilyDocument && string.Equals(d.Title, sym.Family.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            famDoc = d;
                            famDocWasAlreadyOpen = true;
                            break;
                        }
                    }

                    // 1.2 Nếu chưa mở sẵn và có thể chỉnh sửa, mở bằng doc.EditFamily
                    if (famDoc == null && sym.Family.IsEditable)
                    {
                        try
                        {
                            famDoc = doc.EditFamily(sym.Family);
                        }
                        catch
                        {
                            famDoc = null;
                        }
                    }

                    if (famDoc != null)
                    {
                        ExtractParametersFromFamilyDefinition(famDoc, sym, categoryName, result, processedNames);
                        scannedFromFamily = result.Count > 0;
                    }
                }
            }
            catch { }
            finally
            {
                // Nếu ta tự mở famDoc thì đóng lại (không lưu) để giải phóng tài nguyên
                if (famDoc != null && !famDocWasAlreadyOpen)
                {
                    try
                    {
                        famDoc.Close(false);
                    }
                    catch { }
                }
            }

            // =========================================================================
            // 2. FALLBACK TỪ DỰ ÁN NẾU CHƯA QUÉT ĐƯỢC TỪ FAMILY
            // =========================================================================
            if (!scannedFromFamily)
            {
                try
                {
                    // Đưa danh mục chuẩn theo đúng 100% thứ tự trước để đảm bảo vị trí biến chuẩn xác
                    EnsureStandardParametersExist(sym, categoryName, result, processedNames);

                    // 2.1 Đọc Type Parameters từ Symbol
                    ExtractParametersFromSymbol(sym, categoryName, result, processedNames);

                    // 2.2 Đọc Instance Parameters từ Instance mẫu có sẵn trong dự án
                    FamilyInstance? sampleInst = null;
                    try
                    {
                        sampleInst = new FilteredElementCollector(doc)
                            .OfClass(typeof(FamilyInstance))
                            .Cast<FamilyInstance>()
                            .FirstOrDefault(fi => fi.Symbol != null && (fi.Symbol.Id == sym.Id || (sym.Family != null && fi.Symbol.Family != null && fi.Symbol.Family.Id == sym.Family.Id)));
                    }
                    catch { }

                    if (sampleInst != null)
                    {
                        ExtractParametersFromInstance(sampleInst, sym, categoryName, result, processedNames);
                    }
                    else if (doc != null && doc.IsModifiable)
                    {
                        try
                        {
                            if (!sym.IsActive) sym.Activate();

                            FamilyInstance? tempInst = null;
                            if (AdaptiveComponentInstanceUtils.IsAdaptiveFamilySymbol(sym))
                            {
                                tempInst = AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(doc, sym);
                            }
                            else
                            {
                                tempInst = doc.Create.NewFamilyInstance(XYZ.Zero, sym, StructuralType.NonStructural);
                            }

                            if (tempInst != null)
                            {
                                ExtractParametersFromInstance(tempInst, sym, categoryName, result, processedNames);
                                doc.Delete(tempInst.Id);
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
            else
            {
                // Bổ sung các biến Instance còn thiếu nếu có
                EnsureStandardParametersExist(sym, categoryName, result, processedNames);
            }

            // Sắp xếp: Mục Dimensions lên đầu tiên, sau đó mới tới mục Other (Khác).
            // Áp dụng GetStandardParameterOrder để đảm bảo CT_D đứng số 1, các biến dimensions chuẩn theo đúng thứ tự family
            var orderedResult = result
                .Where(p => !IsSystemJunkParameterName(p.InternalName) && IsParameterApplicableToFamily(sym.FamilyName, p.InternalName))
                .OrderBy(p => p.IsDimension ? 0 : (p.IsOther ? 1 : 2))
                .ThenBy(p => GetStandardParameterOrder(sym.FamilyName, p.InternalName))
                .ToList();

            // Gán số thứ tự STT (1, 2, 3...) theo danh mục đã ưu tiên Dimensions trước, Other sau
            for (int i = 0; i < orderedResult.Count; i++)
            {
                orderedResult[i].STT = i + 1;
                if (!string.IsNullOrEmpty(profileName))
                {
                    orderedResult[i].ProfileName = profileName;
                }
            }

            if (orderedResult.Count > 0)
            {
                _familyParamCache[cacheKey] = CloneMappingList(orderedResult, categoryName, profileName);
            }

            return orderedResult;
        }

        private static void ExtractParametersFromFamilyDefinition(
            Document famDoc,
            FamilySymbol sym,
            string categoryName,
            List<ParameterMappingItem> list,
            HashSet<string> processed)
        {
            var famMgr = famDoc.FamilyManager;
            if (famMgr == null) return;

            // Lấy danh sách FamilyParameter theo ĐÚNG THỨ TỰ hiển thị trong Family Types dialog
            IEnumerable<FamilyParameter> parameters;
            try
            {
                parameters = famMgr.GetParameters();
            }
            catch
            {
                parameters = famMgr.Parameters.Cast<FamilyParameter>();
            }

            string famNameUpper = (sym.FamilyName ?? "").ToUpperInvariant();

            foreach (FamilyParameter fp in parameters)
            {
                if (fp?.Definition == null || string.IsNullOrWhiteSpace(fp.Definition.Name)) continue;
                string name = fp.Definition.Name;
                if (processed.Contains(name)) continue;

                // Lọc bỏ triệt để các tham số hệ thống và tham số dự án rác
                if (IsSystemJunkParameterName(name)) continue;

                // YÊU CẦU QUAN TRỌNG: Lọc bỏ TOÀN BỘ parameter ĐÃ CÓ GÁN FORMULA!
                if (!string.IsNullOrWhiteSpace(fp.Formula)) continue;
                try
                {
                    if (fp.IsDeterminedByFormula) continue;
                }
                catch { }

                // YÊU CẦU: Lọc bỏ các tham số có công thức hoặc không cần thiết (như A_GÓC XOAY...)
                if (IsFormulaOrIgnoredParameter(name)) continue;

                // YÊU CẦU: Kiểm tra tính áp dụng chuẩn cho Family (chống đá biến giữa các loại cấu kiện)
                if (!IsParameterApplicableToFamily(sym.FamilyName, name)) continue;

                // Nhận diện kiểu dữ liệu chuẩn Revit
                string dataTypeStr = fp.StorageType.ToString();
                bool isBoolYesNo = false;
                try
                {
                    var dt = fp.Definition.GetDataType();
                    if (dt == SpecTypeId.Length) dataTypeStr = "Length (Chiều dài)";
                    else if (dt == SpecTypeId.Angle) dataTypeStr = "Angle (Góc)";
                    else if (dt == SpecTypeId.Slope) dataTypeStr = "Slope (Độ dốc)";
                    else if (dt == SpecTypeId.Number) dataTypeStr = "Number (Số)";
                    else if (dt == SpecTypeId.Boolean.YesNo) { dataTypeStr = "Yes/No (Có/Không)"; isBoolYesNo = true; }
                }
                catch { }

                // Nhận diện nhóm tham số trong Family
                string groupName = "Chung";
                try
                {
                    var groupTypeId = fp.Definition.GetGroupTypeId();
                    if (groupTypeId != null && !string.IsNullOrEmpty(groupTypeId.TypeId))
                    {
                        groupName = LabelUtils.GetLabelForGroup(groupTypeId);
                    }
                }
                catch { }

                string gLower = groupName.ToLowerInvariant();
                bool isLength = dataTypeStr.Contains("Length") || fp.StorageType == StorageType.Double;
                bool isAngle = dataTypeStr.Contains("Angle");
                bool isNumber = dataTypeStr.Contains("Number");
                bool isYesNo = dataTypeStr.Contains("Yes/No") || isBoolYesNo;

                bool isDim = false;
                bool isOther = false;
                bool isVis = false;

                if (gLower.Contains("dimen") || gLower.Contains("kích thước") || gLower.Contains("geom"))
                {
                    groupName = "Dimensions (Kích thước)";
                    isDim = true;
                    isOther = false;
                }
                else if (gLower.Contains("other") || gLower.Contains("khác") || isYesNo)
                {
                    groupName = "Other (Khác)";
                    isDim = false;
                    isOther = true;
                    if (isYesNo) isVis = true;
                }
                else if (isLength || isAngle || isNumber || dataTypeStr.Contains("Slope"))
                {
                    groupName = "Dimensions (Kích thước)";
                    isDim = true;
                    isOther = false;
                }
                else
                {
                    continue;
                }

                // Đọc giá trị mặc định từ Family Type hiện hành
                string defValue = string.Empty;
                try
                {
                    if (famMgr.CurrentType != null)
                    {
                        if (fp.StorageType == StorageType.Double)
                        {
                            double? dVal = famMgr.CurrentType.AsDouble(fp);
                            if (dVal.HasValue)
                            {
                                if (dataTypeStr.Contains("Angle"))
                                {
                                    double deg = UnitUtils.ConvertFromInternalUnits(dVal.Value, UnitTypeId.Degrees);
                                    defValue = deg.ToString("0.0#");
                                }
                                else if (dataTypeStr.Contains("Slope") || dataTypeStr.Contains("Dốc") || dataTypeStr.Contains("Doc"))
                                {
                                    try
                                    {
                                        double pct = UnitUtils.ConvertFromInternalUnits(dVal.Value, UnitTypeId.Percentage);
                                        defValue = pct.ToString("0.####") + "%";
                                    }
                                    catch
                                    {
                                        defValue = (dVal.Value * 100.0).ToString("0.####") + "%";
                                    }
                                }
                                else
                                {
                                    double mm = UnitUtils.ConvertFromInternalUnits(dVal.Value, UnitTypeId.Millimeters);
                                    defValue = mm.ToString("F1");
                                }
                            }
                        }
                        else if (fp.StorageType == StorageType.Integer)
                        {
                            int? iVal = famMgr.CurrentType.AsInteger(fp);
                            if (iVal.HasValue)
                            {
                                if (isBoolYesNo)
                                    defValue = (iVal.Value == 1) ? "Có" : "Không";
                                else
                                    defValue = iVal.Value.ToString();
                            }
                        }
                        else if (fp.StorageType == StorageType.String)
                        {
                            defValue = famMgr.CurrentType.AsString(fp) ?? string.Empty;
                        }
                    }
                }
                catch { }

                if (isBoolYesNo && string.IsNullOrEmpty(defValue))
                {
                    defValue = "Không";
                }

                processed.Add(name);
                list.Add(new ParameterMappingItem
                {
                    IsSelected = true,
                    CategoryName = categoryName,
                    FamilyName = sym.FamilyName,
                    InternalName = name,
                    DisplayName = name,
                    GroupName = groupName,
                    IsDimension = isDim,
                    IsOther = isOther,
                    IsVisibility = isVis || isBoolYesNo,
                    DataType = dataTypeStr,
                    DefaultValue = defValue,
                    CustomValue = defValue,
                    MappedField = DeduceDefaultMappedField(name),
                    IsInstance = fp.IsInstance
                });
            }
        }

        private static void ExtractParametersFromSymbol(
            FamilySymbol sym,
            string categoryName,
            List<ParameterMappingItem> list,
            HashSet<string> processed)
        {
            foreach (Parameter p in sym.Parameters)
            {
                if (p.Definition == null || string.IsNullOrWhiteSpace(p.Definition.Name)) continue;
                string name = p.Definition.Name;
                if (processed.Contains(name)) continue;

                var item = CreateMappingItemFromParameter(p, sym, categoryName, isInstance: false);
                if (item == null) continue;

                processed.Add(name);
                list.Add(item);
            }
        }

        private static void ExtractParametersFromInstance(
            FamilyInstance inst,
            FamilySymbol sym,
            string categoryName,
            List<ParameterMappingItem> list,
            HashSet<string> processed)
        {
            foreach (Parameter p in inst.Parameters)
            {
                if (p.Definition == null || string.IsNullOrWhiteSpace(p.Definition.Name)) continue;
                string name = p.Definition.Name;
                if (processed.Contains(name)) continue;

                var item = CreateMappingItemFromParameter(p, sym, categoryName, isInstance: true);
                if (item == null) continue;

                processed.Add(name);
                list.Add(item);
            }
        }

        public static bool IsSystemJunkParameterName(string? paramName)
        {
            if (string.IsNullOrWhiteSpace(paramName)) return true;
            string nLower = paramName.Trim().ToLowerInvariant();

            // 1. Lọc bỏ các tham số hệ thống Revit / IFC / Quản lý dự án theo tên
            if (nLower.StartsWith("ifc") ||
                nLower.StartsWith("omniclass") ||
                nLower == "default elevation" ||
                nLower == "cost" ||
                nLower == "mark" ||
                nLower == "type mark" ||
                nLower == "comments" ||
                nLower == "type comments" ||
                nLower == "description" ||
                nLower == "keynote" ||
                nLower == "model" ||
                nLower == "manufacturer" ||
                nLower == "url" ||
                nLower == "assembly code" ||
                nLower == "assembly description" ||
                nLower == "type image" ||
                nLower == "image" ||
                nLower == "flip" ||
                nLower == "work plane" ||
                nLower == "design option" ||
                nLower == "phase created" ||
                nLower == "phase demolished" ||
                nLower == "host id" ||
                nLower == "host" ||
                nLower == "level" ||
                nLower == "family name" ||
                nLower == "type name" ||
                nLower == "family and type" ||
                nLower == "type id" ||
                nLower == "moves with nearby elements" ||
                nLower == "elevation from level" ||
                nLower.Contains("ván khuôn") ||
                nLower.Contains("van khuon") ||
                nLower.Contains("khối lượng") ||
                nLower.Contains("khoi luong") ||
                nLower == "diện tích" ||
                nLower == "thể tích" ||
                nLower == "volume" ||
                nLower == "area" ||
                nLower == "model material" ||
                nLower.Contains("material") ||
                nLower == "công trình" ||
                nLower == "hạng mục" ||
                nLower == "nhóm cấu kiện" ||
                nLower == "loại cấu kiện" ||
                nLower == "loại bê tông")
            {
                return true;
            }

            return false;
        }

        public static int GetStandardParameterOrder(string? famName, string paramName)
        {
            if (string.IsNullOrWhiteSpace(paramName)) return 999;
            string fUpper = (famName ?? "").ToUpperInvariant();
            string pUpper = paramName.Trim().ToUpperInvariant();

            if (fUpper.Contains("CT_THAN CONG") || (fUpper.Contains("THAN CONG") && (fUpper.Contains("CT_") || fUpper.Contains("TRON"))))
            {
                switch (pUpper)
                {
                    case "CT_D": return 1;
                    case "CT_T": return 2;
                    case "CT_JOINT_THICKNESS": return 3;
                    case "L": return 4;
                    case "CT_D_OUTER_1": return 5;
                    case "CT_D_INTER_0": return 6;
                    case "CT_X1": return 7;
                    case "CT_X2": return 8;
                    case "CT_X3": return 9;
                    case "CT_X4": return 10;
                    case "CT_A": return 11;
                    case "CT_R": return 12;
                    case "X": return 13;
                    case "A1": return 14;
                }
            }
            else if (fUpper.Contains("CT_BE TONG LOT") || (fUpper.Contains("BE TONG LOT") && (fUpper.Contains("CT_") || fUpper.Contains("TRON"))))
            {
                switch (pUpper)
                {
                    case "B_BTL": return 1;
                    case "H_BTL": return 2;
                    case "H1_BTL": return 3;
                    case "B VAC": return 4;
                    case "H VAC": return 5;
                    case "L1": return 6;
                    case "L2": return 7;
                    case "H": return 8;
                }
            }
            else if (fUpper.Contains("CT_DA DAM DEM") || (fUpper.Contains("DA DAM") && (fUpper.Contains("CT_") || fUpper.Contains("TRON"))))
            {
                switch (pUpper)
                {
                    case "B_DDD": return 1;
                    case "H_DDD": return 2;
                    case "L1": return 3;
                    case "L2": return 4;
                    case "H": return 5;
                }
            }
            else if (fUpper.Contains("MOI NOI") || fUpper.Contains("MOI_NOI"))
            {
                switch (pUpper)
                {
                    case "D": return 1;
                    case "T": return 2;
                    case "B": return 3;
                    case "H": return 4;
                    case "L": return 5;
                }
            }
            else if (fUpper.Contains("THAN CONG") || fUpper.Contains("DUC SAN") || fUpper.Contains("DOT CONG"))
            {
                switch (pUpper)
                {
                    case "CH_B": return 1;
                    case "CH_H": return 2;
                    case "CH_T": return 3;
                    case "BQD_DO DOC": return 4;
                    case "BQD_L": return 5;
                    case "BQD_H": return 6;
                    case "BQD_BTL_L": return 7;
                    case "BQD_BTL_H": return 8;
                    case "CO VAI KE": return 9;
                }
            }

            return 100;
        }

        private static bool IsSystemJunkParameter(Parameter p)
        {
            if (p == null || p.Definition == null) return true;

            // 1. Toàn bộ tham số hệ thống BuiltInParameter của Revit đều bị lọc bỏ 100%
            if (p.Definition is InternalDefinition idDef && idDef.BuiltInParameter != BuiltInParameter.INVALID)
            {
                return true;
            }

            return IsSystemJunkParameterName(p.Definition.Name);
        }

        private static ParameterMappingItem? CreateMappingItemFromParameter(
            Parameter p,
            FamilySymbol sym,
            string categoryName,
            bool isInstance)
        {
            if (p?.Definition == null || string.IsNullOrWhiteSpace(p.Definition.Name)) return null;
            string name = p.Definition.Name;
            if (IsSystemJunkParameter(p)) return null;

            // Nếu tham số là ReadOnly trong Revit dự án -> Đã có gán Formula hoặc là tham số hệ thống không thể sửa
            if (p.IsReadOnly) return null;

            // Lọc bỏ tham số có gán formula hoặc không áp dụng
            if (IsFormulaOrIgnoredParameter(name)) return null;

            // Kiểm tra tính áp dụng chuẩn cho Family (chống đá biến)
            if (!IsParameterApplicableToFamily(sym.FamilyName, name)) return null;

            string groupName = "Chung";
            bool isDim = false;
            bool isOther = false;
            bool isVis = false;

            string dataTypeStr = p.StorageType.ToString();
            bool isBoolYesNo = false;
            try
            {
                var dt = p.Definition.GetDataType();
                if (dt == SpecTypeId.Length) dataTypeStr = "Length (Chiều dài)";
                else if (dt == SpecTypeId.Angle) dataTypeStr = "Angle (Góc)";
                else if (dt == SpecTypeId.Slope) dataTypeStr = "Slope (Độ dốc)";
                else if (dt == SpecTypeId.Number) dataTypeStr = "Number (Số)";
                else if (dt == SpecTypeId.Boolean.YesNo) { dataTypeStr = "Yes/No (Có/Không)"; isBoolYesNo = true; }
            }
            catch { }

            try
            {
                var groupTypeId = p.Definition.GetGroupTypeId();
                if (groupTypeId != null && !string.IsNullOrEmpty(groupTypeId.TypeId))
                {
                    groupName = LabelUtils.GetLabelForGroup(groupTypeId);
                }
            }
            catch { }

            string gLower = groupName.ToLowerInvariant();
            bool isLength = dataTypeStr.Contains("Length") || p.StorageType == StorageType.Double;
            bool isAngle = dataTypeStr.Contains("Angle");
            bool isNumber = dataTypeStr.Contains("Number");
            bool isYesNo = dataTypeStr.Contains("Yes/No") || isBoolYesNo;

            if (gLower.Contains("dimen") || gLower.Contains("kích thước") || gLower.Contains("geom"))
            {
                groupName = "Dimensions (Kích thước)";
                isDim = true;
                isOther = false;
            }
            else if (gLower.Contains("other") || gLower.Contains("khác") || isYesNo)
            {
                groupName = "Other (Khác)";
                isDim = false;
                isOther = true;
                if (isYesNo) isVis = true;
            }
            else if (isLength || isAngle || isNumber || dataTypeStr.Contains("Slope"))
            {
                groupName = "Dimensions (Kích thước)";
                isDim = true;
                isOther = false;
            }
            else
            {
                return null;
            }

            string defValue = p.AsValueString() ?? string.Empty;
            if (string.IsNullOrEmpty(defValue))
            {
                if (p.StorageType == StorageType.Double)
                {
                    try
                    {
                        var dt = p.Definition.GetDataType();
                        if (dt == SpecTypeId.Length)
                        {
                            double mm = UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Millimeters);
                            defValue = mm.ToString("F1");
                        }
                        else if (dt == SpecTypeId.Angle)
                        {
                            double deg = UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Degrees);
                            defValue = deg.ToString("0.0#");
                        }
                        else if (dt == SpecTypeId.Slope)
                        {
                            try
                            {
                                double pct = UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Percentage);
                                defValue = pct.ToString("0.####") + "%";
                            }
                            catch
                            {
                                defValue = (p.AsDouble() * 100.0).ToString("0.####") + "%";
                            }
                        }
                        else
                        {
                            defValue = p.AsDouble().ToString("F2");
                        }
                    }
                    catch
                    {
                        defValue = p.AsDouble().ToString("F2");
                    }
                }
                else if (p.StorageType == StorageType.Integer)
                {
                    if (isBoolYesNo)
                    {
                        defValue = (p.AsInteger() == 1) ? "Có" : "Không";
                    }
                    else
                    {
                        defValue = p.AsInteger().ToString();
                    }
                }
                else if (p.StorageType == StorageType.String)
                {
                    defValue = p.AsString() ?? string.Empty;
                }
            }

            if (isBoolYesNo && string.IsNullOrEmpty(defValue))
            {
                defValue = "Không";
            }

            return new ParameterMappingItem
            {
                IsSelected = true,
                CategoryName = categoryName,
                FamilyName = sym.FamilyName,
                InternalName = name,
                DisplayName = name,
                GroupName = groupName,
                IsDimension = isDim,
                IsOther = isOther,
                IsVisibility = isVis || isBoolYesNo,
                DataType = dataTypeStr,
                DefaultValue = defValue,
                CustomValue = defValue,
                MappedField = DeduceDefaultMappedField(name),
                IsInstance = isInstance
            };
        }

        private static void EnsureStandardParametersExist(
            FamilySymbol sym,
            string categoryName,
            List<ParameterMappingItem> list,
            HashSet<string> processed)
        {
            string famUpper = (sym.FamilyName ?? "").ToUpperInvariant();

            // 1. SÂN GIA CỐ (TNN_CX_SAN GIA CO) - Phải kiểm tra trước vì tên có thể chứa "CX_"
            if (famUpper.Contains("SAN GIA CO") || famUpper.Contains("SGC"))
            {
                // Toàn bộ 9 tham số Dimensions thực tế trong Family Revit:
                AddFallbackParam(list, processed, sym, categoryName, "CH_SGC_B", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "7220.0");
                AddFallbackParam(list, processed, sym, categoryName, "CH_SGC_BTL_H", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "100.0");
                AddFallbackParam(list, processed, sym, categoryName, "CH_SGC_GX", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Angle (Góc)", defVal: "0.0");
                AddFallbackParam(list, processed, sym, categoryName, "CH_SGC_H1", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1000.0");
                AddFallbackParam(list, processed, sym, categoryName, "CH_SGC_H2", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1000.0");
                AddFallbackParam(list, processed, sym, categoryName, "CH_SGC_L1", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "8465.0");
                AddFallbackParam(list, processed, sym, categoryName, "CH_SGC_L2", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "400.0");
                AddFallbackParam(list, processed, sym, categoryName, "CH_SGC_T", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "300.0");
                AddFallbackParam(list, processed, sym, categoryName, "CH_SGC_Void_H", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "800.0");

                // Tham số ẩn hiện cấu kiện Yes/No thuộc Other:
                AddFallbackParam(list, processed, sym, categoryName, "CX_SGC_SH", "Other (Khác)", isDim: false, isInst: true, dataType: "Yes/No (Có/Không)", defVal: "Có", isVis: true);
                AddFallbackParam(list, processed, sym, categoryName, "CX_SGC_BTL_SH", "Other (Khác)", isDim: false, isInst: true, dataType: "Yes/No (Có/Không)", defVal: "Không", isVis: true);
                AddFallbackParam(list, processed, sym, categoryName, "CX_SGC_DD_SH", "Other (Khác)", isDim: false, isInst: true, dataType: "Yes/No (Có/Không)", defVal: "Không", isVis: true);
            }
            // 2. HỘP NỐI CỐNG DỌC (HOP NOI CONG DOC)
            else if (famUpper.Contains("HOP NOI"))
            {
                // Toàn bộ 10 tham số Dimensions thực tế trong Family Revit:
                AddFallbackParam(list, processed, sym, categoryName, "HOP NOI CONG_B", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "2100.0");
                AddFallbackParam(list, processed, sym, categoryName, "HOP NOI CONG_W", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "2700.0");
                AddFallbackParam(list, processed, sym, categoryName, "HOP NOI CONG_T", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "250.0");
                AddFallbackParam(list, processed, sym, categoryName, "HOP NOI CONG_H THAN", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "2100.0");
                AddFallbackParam(list, processed, sym, categoryName, "HOP NOI CONG_H DAY", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "250.0");
                AddFallbackParam(list, processed, sym, categoryName, "HOP NOI CONG_H NAP", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "150.0");
                AddFallbackParam(list, processed, sym, categoryName, "CONG HOP_B", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1860.0");
                AddFallbackParam(list, processed, sym, categoryName, "CONG HOP_H", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1860.0");
                AddFallbackParam(list, processed, sym, categoryName, "TAM DAN_B", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "540.0");
                AddFallbackParam(list, processed, sym, categoryName, "DA DAM DEM_H", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "150.0");
            }
            // 3. HỐ KỸ THUẬT (TNN_CKT_HO KY THUAT 1.5x1.5M)
            else if ((famUpper.Contains("HO KY THUAT") || (famUpper.Contains("CKT") && (famUpper.Contains("HO") || famUpper.Contains("GIENG")))) && !famUpper.Contains("THAN CONG") && !famUpper.Contains("LOT") && !famUpper.Contains("DA DAM"))
            {
                AddFallbackParam(list, processed, sym, categoryName, "THAN GIENG_B", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1400.0");
                AddFallbackParam(list, processed, sym, categoryName, "THAN GIENG_W", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "2400.0");
                AddFallbackParam(list, processed, sym, categoryName, "THAN GIENG_B THANH", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "200.0");
                AddFallbackParam(list, processed, sym, categoryName, "THAN GIENG_H DAY", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "200.0");
                AddFallbackParam(list, processed, sym, categoryName, "THAN GIENG_H THAN GIENG", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "2780.0");
                AddFallbackParam(list, processed, sym, categoryName, "BTL_H", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "100.0");
                AddFallbackParam(list, processed, sym, categoryName, "CO GIENG_L1", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "900.0");
                AddFallbackParam(list, processed, sym, categoryName, "CO GIENG_L2", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "700.0");
                AddFallbackParam(list, processed, sym, categoryName, "CO GIENG_T", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "100.0");
                AddFallbackParam(list, processed, sym, categoryName, "CONG HOP_B", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1860.0");
                AddFallbackParam(list, processed, sym, categoryName, "CONG HOP_H", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1860.0");
                AddFallbackParam(list, processed, sym, categoryName, "HGDH_ChieuCao", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "40.0");
                AddFallbackParam(list, processed, sym, categoryName, "HGDH_ChieuCao 2", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "60.0");
                AddFallbackParam(list, processed, sym, categoryName, "HGDH_ChieuDai", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "900.0");
                AddFallbackParam(list, processed, sym, categoryName, "HGDH_ChieuRong", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "900.0");
                AddFallbackParam(list, processed, sym, categoryName, "HGDH_ChieuDay-Vach", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "160.0");
                AddFallbackParam(list, processed, sym, categoryName, "HGDH_ChieuDay-Vach 2", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "120.0");
                AddFallbackParam(list, processed, sym, categoryName, "TAM DAN_B THANH", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "100.0");
                AddFallbackParam(list, processed, sym, categoryName, "TAM DAN_PHAN DAY LEN", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1100.0");
                AddFallbackParam(list, processed, sym, categoryName, "ẨN PHẢI", "Other (Khác)", isDim: false, isInst: true, dataType: "Yes/No (Có/Không)", defVal: "Có", isVis: true);
                AddFallbackParam(list, processed, sym, categoryName, "ẨN TRÁI", "Other (Khác)", isDim: false, isInst: true, dataType: "Yes/No (Có/Không)", defVal: "Có", isVis: true);
            }
            // 4. MỐI NỐI CỐNG (TNN_CT_CX_MOI NOI, MOI NOI CONG, MOI_NOI) - Phải trước Cửa xả
            else if (famUpper.Contains("MOI NOI") || famUpper.Contains("MOI_NOI"))
            {
                AddFallbackParam(list, processed, sym, categoryName, "D", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1500.0");
                AddFallbackParam(list, processed, sym, categoryName, "T", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "137.0");
                AddFallbackParam(list, processed, sym, categoryName, "B", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1500.0");
                AddFallbackParam(list, processed, sym, categoryName, "H", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1500.0");
                AddFallbackParam(list, processed, sym, categoryName, "L", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1000.0");
            }
            // 5. CỬA XẢ & SÂN CỐNG (TNN_CX_SAN CONG) - Tuyệt đối KHÔNG có CX_TC_B dinh và CX_DO_H1!
            else if (famUpper.Contains("CX_SAN CONG") || famUpper.Contains("CUA XA") || (famUpper.Contains("CX_") && !famUpper.Contains("MOI NOI") && !famUpper.Contains("MOI_NOI")))
            {
                AddFallbackParam(list, processed, sym, categoryName, "CH_GX", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Angle (Góc)", defVal: "90.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TC_GX1", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Angle (Góc)", defVal: "20.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TC_GX2", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Angle (Góc)", defVal: "20.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_B Cong", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1800.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_B san cong_du", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "160.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_L san cong", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "2160.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_L1", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "400.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_SC_B", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "400.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_SC_BTL_H", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "100.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_SC_H", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "250.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_SC_H2", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1000.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TC_B day 11", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "814.1");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TC_B day 12", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "814.1");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TC_B day 2", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "454.1");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TC_B dinh 11", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "404.1");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TC_B dinh 12", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "404.1");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TC_H 1", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1640.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TC_H 2", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "200.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TD_DD_B", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "3500.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TD_DD_W", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1000.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TD_H", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1640.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TD_T", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "0.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TD_T day", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "0.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TD_T dinh", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "400.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TD_W day", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "910.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_DD_H1", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1600.0");
                AddFallbackParam(list, processed, sym, categoryName, "TD_H", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "-400.0");

                AddFallbackParam(list, processed, sym, categoryName, "CX_GLC", "Other (Khác)", isDim: false, isInst: true, dataType: "Yes/No (Có/Không)", defVal: "Không", isVis: true);
                AddFallbackParam(list, processed, sym, categoryName, "CX_TUONG DAU", "Other (Khác)", isDim: false, isInst: false, dataType: "Yes/No (Có/Không)", defVal: "Có", isVis: true);
                AddFallbackParam(list, processed, sym, categoryName, "CX_TUONG CANH", "Other (Khác)", isDim: false, isInst: false, dataType: "Yes/No (Có/Không)", defVal: "Có", isVis: true);
                AddFallbackParam(list, processed, sym, categoryName, "CX_SAN CONG", "Other (Khác)", isDim: false, isInst: false, dataType: "Yes/No (Có/Không)", defVal: "Có", isVis: true);
                AddFallbackParam(list, processed, sym, categoryName, "CX_BE TONG LOT", "Other (Khác)", isDim: false, isInst: false, dataType: "Yes/No (Có/Không)", defVal: "Có", isVis: true);
                AddFallbackParam(list, processed, sym, categoryName, "CX_DA DAM DEM", "Other (Khác)", isDim: false, isInst: false, dataType: "Yes/No (Có/Không)", defVal: "Có", isVis: true);
            }
            else if (famUpper.Contains("CT_DA DAM DEM") || (famUpper.Contains("DA DAM") && (famUpper.Contains("CT_") || famUpper.Contains("TRON"))))
            {
                AddFallbackParam(list, processed, sym, categoryName, "B_DDD", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "2260.0");
                AddFallbackParam(list, processed, sym, categoryName, "H_DDD", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "150.0");
                AddFallbackParam(list, processed, sym, categoryName, "L1", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "0.0");
                AddFallbackParam(list, processed, sym, categoryName, "L2", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "0.0");
                AddFallbackParam(list, processed, sym, categoryName, "H", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "0.0");
            }
            else if (famUpper.Contains("DA DAM") || famUpper.Contains("DEM CONG"))
            {
                AddFallbackParam(list, processed, sym, categoryName, "B_DDD", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "2260.0");
                AddFallbackParam(list, processed, sym, categoryName, "H_DDD", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "150.0");
                AddFallbackParam(list, processed, sym, categoryName, "H", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "0.0");
                AddFallbackParam(list, processed, sym, categoryName, "L1", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "0.0");
                AddFallbackParam(list, processed, sym, categoryName, "L2", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "0.0");
            }
            else if (famUpper.Contains("CT_BE TONG LOT") || (famUpper.Contains("BE TONG LOT") && (famUpper.Contains("CT_") || famUpper.Contains("TRON"))))
            {
                AddFallbackParam(list, processed, sym, categoryName, "B_BTL", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "4680.0");
                AddFallbackParam(list, processed, sym, categoryName, "H_BTL", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "100.0");
                AddFallbackParam(list, processed, sym, categoryName, "H1_BTL", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "300.0");
                AddFallbackParam(list, processed, sym, categoryName, "B VAC", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "550.0");
                AddFallbackParam(list, processed, sym, categoryName, "H VAC", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "380.0");
                AddFallbackParam(list, processed, sym, categoryName, "L1", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "0.0");
                AddFallbackParam(list, processed, sym, categoryName, "L2", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "0.0");
                AddFallbackParam(list, processed, sym, categoryName, "H", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "0.0");
            }
            else if (famUpper.Contains("BE TONG LOT") || famUpper.Contains("BTL") || famUpper.Contains("BT LOT"))
            {
                AddFallbackParam(list, processed, sym, categoryName, "B_BTL", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "2100.0");
                AddFallbackParam(list, processed, sym, categoryName, "H_BTL", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "100.0");
            }
            else if (famUpper.Contains("2X3X2") || famUpper.Contains("DO TAI CHO") || famUpper.Contains("DTC") || famUpper.Contains("LIEN NGOAI") || famUpper.Contains("LIEN TRONG"))
            {
                // Cống đổ tại chỗ: B, H1, H2, LỚP MÓNG (Tuyệt đối không gán biến cống đúc sẵn)
                AddFallbackParam(list, processed, sym, categoryName, "B", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "400.0");
                AddFallbackParam(list, processed, sym, categoryName, "H1", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "700.0");
                AddFallbackParam(list, processed, sym, categoryName, "H2", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "700.0");
                AddFallbackParam(list, processed, sym, categoryName, "LỚP MÓNG", "Other (Khác)", isDim: false, isInst: true, dataType: "Yes/No (Có/Không)", defVal: "Không", isVis: true);
            }
            else if (famUpper.Contains("CT_THAN CONG") || (famUpper.Contains("THAN CONG") && (famUpper.Contains("CT_") || famUpper.Contains("TRON"))))
            {
                // Cống tròn đúc sẵn (14 thông số chuẩn của Family TNN_CT_THAN CONG theo đúng thứ tự)
                AddFallbackParam(list, processed, sym, categoryName, "CT_D", "Dimensions (Kích thước)", isDim: true, isInst: false, dataType: "Length (Chiều dài)", defVal: "1500.0");
                AddFallbackParam(list, processed, sym, categoryName, "CT_T", "Dimensions (Kích thước)", isDim: true, isInst: false, dataType: "Length (Chiều dài)", defVal: "137.0");
                AddFallbackParam(list, processed, sym, categoryName, "CT_Joint_Thickness", "Dimensions (Kích thước)", isDim: true, isInst: false, dataType: "Length (Chiều dài)", defVal: "20.0");
                AddFallbackParam(list, processed, sym, categoryName, "L", "Dimensions (Kích thước)", isDim: true, isInst: false, dataType: "Length (Chiều dài)", defVal: "2500.0");
                AddFallbackParam(list, processed, sym, categoryName, "CT_D_Outer_1", "Dimensions (Kích thước)", isDim: true, isInst: false, dataType: "Length (Chiều dài)", defVal: "1890.0");
                AddFallbackParam(list, processed, sym, categoryName, "CT_D_Inter_0", "Dimensions (Kích thước)", isDim: true, isInst: false, dataType: "Length (Chiều dài)", defVal: "1720.0");
                AddFallbackParam(list, processed, sym, categoryName, "CT_X1", "Dimensions (Kích thước)", isDim: true, isInst: false, dataType: "Length (Chiều dài)", defVal: "130.0");
                AddFallbackParam(list, processed, sym, categoryName, "CT_X2", "Dimensions (Kích thước)", isDim: true, isInst: false, dataType: "Length (Chiều dài)", defVal: "160.0");
                AddFallbackParam(list, processed, sym, categoryName, "CT_X3", "Dimensions (Kích thước)", isDim: true, isInst: false, dataType: "Length (Chiều dài)", defVal: "127.0");
                AddFallbackParam(list, processed, sym, categoryName, "CT_X4", "Dimensions (Kích thước)", isDim: true, isInst: false, dataType: "Length (Chiều dài)", defVal: "146.0");
                AddFallbackParam(list, processed, sym, categoryName, "CT_A", "Dimensions (Kích thước)", isDim: true, isInst: false, dataType: "Length (Chiều dài)", defVal: "100.0");
                AddFallbackParam(list, processed, sym, categoryName, "CT_R", "Dimensions (Kích thước)", isDim: true, isInst: false, dataType: "Length (Chiều dài)", defVal: "750.0");
                AddFallbackParam(list, processed, sym, categoryName, "x", "Dimensions (Kích thước)", isDim: true, isInst: false, dataType: "Length (Chiều dài)", defVal: "60.0");
                AddFallbackParam(list, processed, sym, categoryName, "A1", "Dimensions (Kích thước)", isDim: true, isInst: false, dataType: "Length (Chiều dài)", defVal: "1890.0");
            }
            else if ((famUpper.Contains("THAN CONG") || famUpper.Contains("CH_THAN CONG") || famUpper.Contains("CKT_THAN CONG") || famUpper.Contains("DUC SAN") || famUpper.Contains("DOT CONG")) && !famUpper.Contains("CT_") && !famUpper.Contains("TRON"))
            {
                // Cống đúc sẵn (Theo đúng Hình 1 trong mục Dimensions và Hình 2 trong mục Other Yes/No)
                AddFallbackParam(list, processed, sym, categoryName, "CH_B", "Dimensions (Kích thước)", isDim: true, isInst: false, dataType: "Length (Chiều dài)", defVal: "1500.0");
                AddFallbackParam(list, processed, sym, categoryName, "CH_H", "Dimensions (Kích thước)", isDim: true, isInst: false, dataType: "Length (Chiều dài)", defVal: "1500.0");
                AddFallbackParam(list, processed, sym, categoryName, "CH_T", "Dimensions (Kích thước)", isDim: true, isInst: false, dataType: "Length (Chiều dài)", defVal: "180.0");
                AddFallbackParam(list, processed, sym, categoryName, "BQD_DO DOC", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Slope (Độ dốc)", defVal: "10.0000%");
                AddFallbackParam(list, processed, sym, categoryName, "BQD_L", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1500.0");
                AddFallbackParam(list, processed, sym, categoryName, "BQD_H", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "200.0");
                AddFallbackParam(list, processed, sym, categoryName, "BQD_BTL_L", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1300.0");
                AddFallbackParam(list, processed, sym, categoryName, "BQD_BTL_H", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "100.0");
                AddFallbackParam(list, processed, sym, categoryName, "CO VAI KE", "Other (Khác)", isDim: false, isInst: true, dataType: "Yes/No (Có/Không)", defVal: "Có", isVis: true);
            }
        }

        public static bool IsFormulaOrIgnoredParameter(string paramName)
        {
            if (string.IsNullOrWhiteSpace(paramName)) return true;
            string n = paramName.Trim().ToUpperInvariant();
            if (n.Contains("A_GOC") || n.Contains("A_GÓC") || n.Contains("GOC XOAY") || n.Contains("GÓC XOAY")) return true;
            if (n == "CH_B TONG" || n == "CH_H TONG" || n == "H_VOID" || n == "VOID_-A" ||
                n == "VOID_THAN CONG_B" || n == "VOID_THAN CONG_H" ||
                n.StartsWith("DAU AM DUONG_B ") || n == "DAU AM DUONG_B" ||
                n == "ANGLE X" || n == "ANGLE TC" || n == "CX_L2" ||
                n == "A1-1" || n == "A10" || n == "A2")
            {
                return true;
            }
            return false;
        }

        public static bool IsParameterApplicableToFamily(string? famName, string paramName)
        {
            if (string.IsNullOrWhiteSpace(paramName)) return false;
            string fUpper = (famName ?? "").ToUpperInvariant();
            string pUpper = paramName.Trim().ToUpperInvariant();

            // Loại bỏ hoàn toàn 2 biến không có thật trong family cửa xả
            if (pUpper == "CX_TC_B DINH" || pUpper == "CX_DO_H1")
            {
                return false;
            }

            // 0. Mối nối cống (TNN_CT_CX_MOI NOI, MOI NOI CONG, MOI_NOI) - Phải trước Cửa xả
            if (fUpper.Contains("MOI NOI") || fUpper.Contains("MOI_NOI"))
            {
                if (pUpper.StartsWith("CX_") || pUpper.StartsWith("CH_") || pUpper.StartsWith("SGC_") ||
                    pUpper.StartsWith("HOP NOI") || pUpper.StartsWith("THAN GIENG") || pUpper.StartsWith("CO GIENG") ||
                    pUpper.StartsWith("HGDH_") || pUpper.StartsWith("TAM DAN_") || pUpper == "CO VAI KE" ||
                    pUpper.StartsWith("B_BTL") || pUpper.StartsWith("H_BTL") || pUpper.StartsWith("BTL_") ||
                    pUpper.StartsWith("B_DDD") || pUpper.StartsWith("H_DDD") || pUpper.StartsWith("CT_") ||
                    pUpper.StartsWith("BQD_") || pUpper == "TD_H")
                {
                    return false;
                }
                return true;
            }

            // 0.1 Cống tròn (TNN_CT_THAN CONG) - Phải trước cống hộp
            if (fUpper.Contains("CT_THAN CONG") || (fUpper.Contains("THAN CONG") && (fUpper.Contains("CT_") || fUpper.Contains("TRON"))))
            {
                if (pUpper.StartsWith("CH_") || pUpper.StartsWith("BQD_") || pUpper == "CO VAI KE" ||
                    pUpper.StartsWith("CX_") || pUpper.StartsWith("SGC_") || pUpper.StartsWith("HOP NOI") ||
                    pUpper.StartsWith("THAN GIENG") || pUpper.StartsWith("CO GIENG") ||
                    pUpper.StartsWith("BTL_") || pUpper.StartsWith("B_BTL") || pUpper.StartsWith("H_BTL") ||
                    pUpper.StartsWith("B_DDD") || pUpper.StartsWith("H_DDD"))
                {
                    return false;
                }
                return true;
            }

            // 0.2 Bê tông lót cống tròn
            if (fUpper.Contains("CT_BE TONG LOT") || (fUpper.Contains("BE TONG LOT") && (fUpper.Contains("CT_") || fUpper.Contains("TRON"))))
            {
                if (pUpper.StartsWith("BTL_") || pUpper.StartsWith("B_DDD") || pUpper.StartsWith("H_DDD") ||
                    pUpper.StartsWith("CX_") || pUpper.StartsWith("SGC_") || pUpper.StartsWith("CH_") ||
                    pUpper.StartsWith("BQD_") || pUpper.StartsWith("CT_") || pUpper == "CO VAI KE" ||
                    pUpper.StartsWith("HOP NOI") || pUpper.StartsWith("THAN GIENG") || pUpper.StartsWith("CO GIENG"))
                {
                    return false;
                }
                return true;
            }

            // 0.3 Đá dăm đệm cống tròn
            if (fUpper.Contains("CT_DA DAM DEM") || (fUpper.Contains("DA DAM") && (fUpper.Contains("CT_") || fUpper.Contains("TRON"))))
            {
                if (pUpper.StartsWith("B_BTL") || pUpper.StartsWith("H_BTL") || pUpper.StartsWith("BTL_") ||
                    pUpper.StartsWith("CX_") || pUpper.StartsWith("SGC_") || pUpper.StartsWith("CH_") ||
                    pUpper.StartsWith("BQD_") || pUpper.StartsWith("CT_") || pUpper == "CO VAI KE" ||
                    pUpper.StartsWith("HOP NOI") || pUpper.StartsWith("THAN GIENG") || pUpper.StartsWith("CO GIENG"))
                {
                    return false;
                }
                return true;
            }

            // 1. Cống đổ tại chỗ (như TNN_CH_THAN CONG_2x3x2)
            if (fUpper.Contains("2X3X2") || fUpper.Contains("DO TAI CHO") || fUpper.Contains("DTC") || fUpper.Contains("LIEN NGOAI") || fUpper.Contains("LIEN TRONG"))
            {
                if (pUpper.StartsWith("CH_") || pUpper == "CO VAI KE" || pUpper.StartsWith("VAI KE") ||
                    pUpper.StartsWith("VOID_") || pUpper.Contains("DAU AM DUONG") || pUpper.StartsWith("BQD_") ||
                    pUpper.StartsWith("CX_") || pUpper.StartsWith("SGC_") || pUpper.StartsWith("HOP NOI") ||
                    pUpper.StartsWith("THAN GIENG") || pUpper.StartsWith("CO GIENG"))
                {
                    return false;
                }
                return true;
            }

            // 2. Cống đúc sẵn (như TNN_CH_THAN CONG)
            if ((fUpper.Contains("THAN CONG") || fUpper.Contains("DUC SAN") || fUpper.Contains("DOT CONG")) && !fUpper.Contains("CT_") && !fUpper.Contains("TRON"))
            {
                if (pUpper == "B" || pUpper == "H1" || pUpper == "H2" || pUpper.Contains("MONG") || pUpper.Contains("MÓNG") ||
                    pUpper.StartsWith("VAI KE") || pUpper.StartsWith("VOID_") || pUpper.Contains("DAU AM DUONG") ||
                    pUpper.StartsWith("CX_") || pUpper.StartsWith("SGC_") || pUpper.StartsWith("HOP NOI") ||
                    pUpper.StartsWith("THAN GIENG") || pUpper.StartsWith("CO GIENG"))
                {
                    return false;
                }
                return true;
            }

            // 3. Sân gia cố (TNN_CX_SAN GIA CO, SGC) - Phải kiểm tra trước Cửa xả vì tên family có thể chứa "CX_"
            if (fUpper.Contains("SAN GIA CO") || fUpper.Contains("SGC"))
            {
                // Chỉ nhận các biến bắt đầu bằng CH_SGC_ hoặc CX_SGC_
                if (pUpper.StartsWith("CH_SGC_") || pUpper.StartsWith("CX_SGC_"))
                {
                    return true;
                }
                return false;
            }

            // 4. Cửa xả & Sân cống (TNN_CX_SAN CONG, CUA XA)
            if (fUpper.Contains("CX_SAN CONG") || fUpper.Contains("CUA XA") || (fUpper.Contains("CX_") && !fUpper.Contains("SGC") && !fUpper.Contains("MOI NOI") && !fUpper.Contains("MOI_NOI")))
            {
                // Không nhận biến của Sân gia cố, Hộp nối, Hố ga, Cống thân
                if (pUpper.StartsWith("CX_SGC_") || pUpper.StartsWith("CH_SGC_") ||
                    pUpper.StartsWith("HOP NOI") || pUpper.StartsWith("THAN GIENG") || pUpper.StartsWith("CO GIENG") ||
                    pUpper.StartsWith("CH_B") || pUpper.StartsWith("CH_H") || pUpper.StartsWith("CH_T") ||
                    pUpper == "CO VAI KE" || pUpper.StartsWith("VAI KE") || pUpper.StartsWith("VOID_") ||
                    pUpper.StartsWith("B_DDD") || pUpper.StartsWith("H_DDD") || pUpper.StartsWith("B_BTL") || pUpper.StartsWith("H_BTL"))
                {
                    return false;
                }
                return true;
            }

            // 5. Hộp nối cống dọc (HOP NOI CONG DOC)
            if (fUpper.Contains("HOP NOI"))
            {
                if (pUpper.StartsWith("HOP NOI CONG_") || pUpper.StartsWith("CONG HOP_") ||
                    pUpper == "TAM DAN_B" || pUpper == "DA DAM DEM_H")
                {
                    return true;
                }
                return false;
            }

            // 6. Hố ga thân giếng (HO GA_THAN GIENG)
            if (fUpper.Contains("HO GA_THAN GIENG") || (fUpper.Contains("THAN GIENG") && !fUpper.Contains("CKT")))
            {
                if (pUpper.StartsWith("THAN GIENG_") || pUpper.StartsWith("CO GIENG_") ||
                    pUpper == "BTL_H" || pUpper.StartsWith("CONG HOP_") ||
                    pUpper == "ẨN PHẢI" || pUpper == "ẨN TRÁI")
                {
                    return true;
                }
                return false;
            }

            // 7. Hố ga tấm đan (HO GA_TAM DAN)
            if (fUpper.Contains("HO GA_TAM DAN") || (fUpper.Contains("TAM DAN") && !fUpper.Contains("HOP NOI")))
            {
                if (pUpper.StartsWith("TAM DAN_") || pUpper.StartsWith("VOID_"))
                {
                    return true;
                }
                return false;
            }

            // 8. Hố kỹ thuật (TNN_CKT_HO KY THUAT 1.5x1.5M)
            if ((fUpper.Contains("HO KY THUAT") || (fUpper.Contains("CKT") && (fUpper.Contains("HO") || fUpper.Contains("GIENG")))) && !fUpper.Contains("THAN CONG") && !fUpper.Contains("LOT") && !fUpper.Contains("DA DAM"))
            {
                if (pUpper.StartsWith("THAN GIENG_") || pUpper.StartsWith("CO GIENG_") ||
                    pUpper == "BTL_H" || pUpper.StartsWith("CONG HOP_") ||
                    pUpper.StartsWith("HGDH_") || pUpper.StartsWith("TAM DAN_") ||
                    pUpper == "ẨN PHẢI" || pUpper == "ẨN TRÁI")
                {
                    return true;
                }
                return false;
            }

            // 9. Đá dăm đệm
            if (fUpper.Contains("DA DAM") || fUpper.Contains("DEM CONG"))
            {
                if (pUpper.StartsWith("B_BTL") || pUpper.StartsWith("H_BTL") || pUpper.StartsWith("CX_") || pUpper.StartsWith("SGC_"))
                {
                    return false;
                }
                return true;
            }

            // 10. Bê tông lót
            if (fUpper.Contains("BE TONG LOT") || fUpper.Contains("BTL") || fUpper.Contains("BT LOT"))
            {
                if (pUpper.StartsWith("B_DDD") || pUpper.StartsWith("H_DDD") || pUpper.StartsWith("CX_") || pUpper.StartsWith("SGC_"))
                {
                    return false;
                }
                return true;
            }

            return true;
        }

        private static void AddFallbackParam(
            List<ParameterMappingItem> list,
            HashSet<string> processed,
            FamilySymbol sym,
            string categoryName,
            string paramName,
            string groupName,
            bool isDim,
            bool isInst,
            string dataType,
            string defVal,
            bool isVis = false)
        {
            if (processed.Contains(paramName)) return;
            processed.Add(paramName);

            list.Add(new ParameterMappingItem
            {
                IsSelected = true,
                CategoryName = categoryName,
                FamilyName = sym.FamilyName,
                InternalName = paramName,
                DisplayName = paramName,
                GroupName = groupName,
                IsDimension = isDim,
                IsOther = !isDim,
                IsVisibility = isVis,
                DataType = dataType,
                DefaultValue = defVal,
                CustomValue = defVal,
                MappedField = DeduceDefaultMappedField(paramName),
                IsInstance = isInst
            });
        }

        private static string DeduceDefaultMappedField(string paramName)
        {
            // Các tham số Cửa xả & Sân gia cố mặc định là Tùy biến để ưu tiên nhận số liệu người dùng nhập
            if (paramName.StartsWith("CX_", StringComparison.OrdinalIgnoreCase) ||
                paramName.StartsWith("TD_", StringComparison.OrdinalIgnoreCase) ||
                paramName.StartsWith("SGC_", StringComparison.OrdinalIgnoreCase))
            {
                return "Tùy biến";
            }

            string lower = paramName.ToLowerInvariant();
            if (lower.Contains("khau_do") || lower.Contains("khaudo") || lower.Contains("b_cong") || lower.Contains("d_cong"))
                return "KhauDo";
            if (lower.Contains("chieu_dai") || lower.Contains("chieudai") || lower == "l" || lower.Contains("l_std"))
                return "ChieuDai";
            if (lower.Contains("ngam") || lower.Contains("l_ngam"))
                return "L_Ngam_San";
            if (lower.Contains("b_san") || lower.Contains("san_cong"))
                return "B_san";
            if (lower.Contains("do_doc") || lower.Contains("dop"))
                return "DoDoc";
            return "Tùy biến";
        }
    }
}
