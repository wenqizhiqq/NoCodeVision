using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using ClosedXML.Excel;
using NoCodeVision.ViewModels;   // PointRow, AxisPos, MotionRow, VarItem

namespace NoCodeVision.Services
{
    /// <summary>
    /// 通用 Excel 批量编辑：把面板里的行导出为 .xlsx，让用户在 Excel/WPS 里改，
    /// 关闭后由调用方回读并替换面板的 Items。列由 T 的公共可写属性决定，表头优先用中文映射。
    /// 点位表（4 轴模型）使用专用扁平导出/导入以处理嵌套的 Axes 集合。
    /// </summary>
    public static class ExcelBatchEdit
    {
        // 属性名 -> 中文表头（按面板类型维护，顺序即列顺序）
        private static readonly Dictionary<Type, Dictionary<string, string>> Headers = new()
        {
            [typeof(MotionRow)] = new()
            {
                ["Name"] = "名称", ["Status"] = "状态", ["Value"] = "数值", ["Unit"] = "单位",
                ["Enabled"] = "使能", ["Address"] = "地址", ["Type"] = "类型", ["Action"] = "动作",
                ["Speed"] = "速度", ["Acceleration"] = "加速度", ["Deceleration"] = "减速度",
                ["HomeOffset"] = "回原偏移", ["SoftLimitPos"] = "正软限", ["SoftLimitNeg"] = "负软限",
                ["Note"] = "备注", ["Polarity"] = "极性", ["Delay"] = "延时",
                ["ExtendTime"] = "伸出时间", ["RetractTime"] = "缩回时间"
            },
            [typeof(NoCodeVision.ViewModels.VarItem)] = new()
            {
                ["Name"] = "名称", ["Type"] = "类型", ["Value"] = "值", ["Remark"] = "备注"
            }
        };

        private static List<PropertyInfo> GetExportProps(Type t)
        {
            var all = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0).ToList();
            if (!Headers.TryGetValue(t, out var map))
                return all.OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
            var ordered = new List<PropertyInfo>();
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var k in map.Keys)
            {
                var p = all.FirstOrDefault(x => x.Name == k);
                if (p != null) { ordered.Add(p); used.Add(k); }
            }
            foreach (var p in all.Where(x => !used.Contains(x.Name))) ordered.Add(p);
            return ordered;
        }

        private static string GetHeader(Type t, string prop)
            => Headers.TryGetValue(t, out var map) && map.TryGetValue(prop, out var h) ? h : prop;

        private static string MakePath(string? fileNameHint)
        {
            var dir = Path.Combine(Path.GetTempPath(), "NoCodeVision");
            Directory.CreateDirectory(dir);
            var safe = string.IsNullOrWhiteSpace(fileNameHint) ? "导出" : fileNameHint!;
            foreach (var c in Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_');
            return Path.Combine(dir, $"{safe}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }

        public static string Export<T>(IEnumerable<T> items, string? fileNameHint = null) where T : new()
        {
            var path = MakePath(fileNameHint);
            var props = GetExportProps(typeof(T));
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Sheet1");
            for (int c = 0; c < props.Count; c++)
                ws.Cell(1, c + 1).Value = GetHeader(typeof(T), props[c].Name);
            int r = 2;
            foreach (var item in items)
            {
                if (item == null) { r++; continue; }
                for (int c = 0; c < props.Count; c++)
                {
                    var v = props[c].GetValue(item);
                    ws.Cell(r, c + 1).Value = v?.ToString() ?? "";
                }
                r++;
            }
            if (props.Count > 0) ws.Columns().AdjustToContents();
            wb.SaveAs(path);
            return path;
        }

        public static List<T> Import<T>(string path) where T : new()
        {
            var result = new List<T>();
            var props = GetExportProps(typeof(T));
            var propByHeader = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in props) { propByHeader[GetHeader(typeof(T), p.Name)] = p; propByHeader[p.Name] = p; }

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet(1);
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
            if (lastRow < 2 || lastCol < 1) return result;

            for (int r = 2; r <= lastRow; r++)
            {
                var item = new T();
                bool any = false;
                for (int c = 1; c <= lastCol; c++)
                {
                    var header = ws.Cell(1, c).GetString().Trim();
                    if (!propByHeader.TryGetValue(header, out var prop) || prop == null) continue;
                    any = true;
                    var cell = ws.Cell(r, c);
                    if (cell.IsEmpty()) continue;
                    var raw = cell.GetString();
                    if (string.IsNullOrEmpty(raw)) continue;
                    try
                    {
                        var converted = Convert.ChangeType(raw, prop.PropertyType, CultureInfo.InvariantCulture);
                        prop.SetValue(item, converted);
                    }
                    catch { /* 类型不兼容时跳过该单元格 */ }
                }
                if (any) result.Add(item);
            }
            return result;
        }

        // ===== 点位表（4 轴模型）专用扁平导出/导入 =====
        private static readonly string[] PointHeaders =
        {
            "点位名", "轴1位置", "轴1速度", "轴2位置", "轴2速度",
            "轴3位置", "轴3速度", "轴4位置", "轴4速度", "时序标记", "同步组", "说明", "备注"
        };

        public static string ExportPoints(IEnumerable<PointRow> items, string? hint = null)
        {
            var path = MakePath(string.IsNullOrWhiteSpace(hint) ? "点位表" : hint);
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("点位表");
            for (int c = 0; c < PointHeaders.Length; c++)
                ws.Cell(1, c + 1).Value = PointHeaders[c];
            int r = 2;
            foreach (var it in items)
            {
                if (it == null) { r++; continue; }
                var a = it.Axes;
                object?[] vals =
                {
                    it.Name,
                    a.Count > 0 ? a[0].Position : 0, a.Count > 0 ? a[0].Speed : 0,
                    a.Count > 1 ? a[1].Position : 0, a.Count > 1 ? a[1].Speed : 0,
                    a.Count > 2 ? a[2].Position : 0, a.Count > 2 ? a[2].Speed : 0,
                    a.Count > 3 ? a[3].Position : 0, a.Count > 3 ? a[3].Speed : 0,
                    it.TimingMark, it.SyncGroup, it.Desc, it.Note
                };
                for (int c = 0; c < vals.Length; c++)
                    ws.Cell(r, c + 1).Value = vals[c]?.ToString() ?? "";
                r++;
            }
            ws.Columns().AdjustToContents();
            wb.SaveAs(path);
            return path;
        }

        public static List<PointRow> ImportPoints(string path)
        {
            var result = new List<PointRow>();
            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet(1);
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
            if (lastRow < 2) return result;

            int Col(string h)
            {
                for (int c = 1; c <= lastCol; c++)
                    if (ws.Cell(1, c).GetString().Trim() == h) return c;
                return -1;
            }
            int[] ci = PointHeaders.Select(Col).ToArray();
            double D(string s) => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

            for (int r = 2; r <= lastRow; r++)
            {
                var row = new PointRow();
                string Cell(int idx)
                {
                    int c = ci[idx];
                    if (c < 0) return "";
                    var cell = ws.Cell(r, c);
                    return cell.IsEmpty() ? "" : cell.GetString();
                }
                row.Name = Cell(0);
                if (row.Axes.Count > 0) { row.Axes[0].Position = D(Cell(1)); row.Axes[0].Speed = D(Cell(2)); }
                if (row.Axes.Count > 1) { row.Axes[1].Position = D(Cell(3)); row.Axes[1].Speed = D(Cell(4)); }
                if (row.Axes.Count > 2) { row.Axes[2].Position = D(Cell(5)); row.Axes[2].Speed = D(Cell(6)); }
                if (row.Axes.Count > 3) { row.Axes[3].Position = D(Cell(7)); row.Axes[3].Speed = D(Cell(8)); }
                row.TimingMark = Cell(9);
                row.SyncGroup = Cell(10);
                row.Desc = Cell(11);
                row.Note = Cell(12);
                if (!string.IsNullOrWhiteSpace(row.Name)) result.Add(row);
            }
            return result;
        }

        /// <summary>用系统关联程序打开导出的 Excel 文件（Excel / WPS）。</summary>
        public static void OpenInExcel(string path)
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch { /* 无关联程序时静默忽略 */ }
        }
    }
}
