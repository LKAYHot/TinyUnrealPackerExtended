using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CsvHelper;
using TinyUnrealPackerExtended.Models;

namespace TinyUnrealPackerExtended.Services
{
    public class ExcelService
    {
        public void ExportToExcel(string inputCsv, string outputXlsx)
        {
            using var reader = new StreamReader(inputCsv);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<InputRecord>().ToList();

            using var workbook = new XLWorkbook();
            var sheet = workbook.AddWorksheet("Localization");
            sheet.Cell(1, 1).Value = "TableName";
            sheet.Cell(1, 2).Value = "Key";
            sheet.Cell(1, 3).Value = "Translation";

            var row = 2;
            foreach (var rec in records)
            {
                var tableName = string.IsNullOrWhiteSpace(rec.Namespace) ? "NewStringTable" : rec.Namespace;
                sheet.Cell(row, 1).Value = tableName;
                sheet.Cell(row, 2).Value = rec.Key;
                sheet.Cell(row, 3).Value = rec.Translation;
                row++;
            }
            workbook.SaveAs(outputXlsx);
        }

        public void ImportFromExcel(string inputXlsx, string outputCsv)
        {
            using var workbook = new XLWorkbook(inputXlsx);
            var sheet = workbook.Worksheet(1);
            using var writer = new StreamWriter(outputCsv);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csv.WriteField("Namespace"); csv.WriteField("Key"); csv.WriteField("Translation"); csv.NextRecord();

            var rows = sheet.RowsUsed().Skip(1);
            foreach (var r in rows)
            {
                var tableName = r.Cell(1).GetString();
                var key = r.Cell(2).GetString();
                var translation = r.Cell(3).GetString();
                var ns = tableName == "NewStringTable" ? string.Empty : tableName;
                csv.WriteField(ns); csv.WriteField(key); csv.WriteField(translation); csv.NextRecord();
            }
        }
    }
}
