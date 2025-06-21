using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using LocresLib;
using TinyUnrealPackerExtended.Models;

namespace TinyUnrealPackerExtended.Services
{
    public class LocresService
    {
        public void Export(string inputLocres, string outputCsv)
        {
            var locres = new LocresFile();
            using var inStream = File.OpenRead(inputLocres);
            locres.Load(inStream);

            using var writer = new StreamWriter(outputCsv, false, Encoding.UTF8);
            using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ",",
                ShouldQuote = _ => true,
                NewLine = Environment.NewLine,
            });
            csv.WriteField("Namespace"); csv.WriteField("Key"); csv.WriteField("Translation"); csv.NextRecord();

            foreach (var ns in locres)
            {
                foreach (var entry in ns)
                {
                    var textEscaped = entry.Value.Replace("\r\n", "\\r\\n").Replace("\n", "\\n");
                    csv.WriteField(ns.Name); csv.WriteField(entry.Key); csv.WriteField(textEscaped); csv.NextRecord();
                }
            }
        }

        public void Import(string inputCsv, string outputLocres)
        {
            var original = Path.ChangeExtension(inputCsv, ".locres");
            if (!File.Exists(original)) throw new FileNotFoundException("Original .locres not found", original);

            var locres = new LocresFile();
            using (var inStream = File.OpenRead(original)) locres.Load(inStream);

            using var reader = new StreamReader(inputCsv, Encoding.UTF8);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<TranslationRecord>().ToList();

            foreach (var rec in records)
            {
                var ns = locres.FirstOrDefault(x => x.Name == rec.Namespace);
                if (ns == null) continue;
                var entry = ns.FirstOrDefault(e => e.Key == rec.Key);
                if (entry == null) continue;
                var unescaped = rec.Translation.Replace("\\r\\n", Environment.NewLine).Replace("\\n", "\n");
                entry.Value = unescaped;
            }

            using var outStream = File.Create(outputLocres);
            locres.Save(outStream, LocresVersion.Optimized);
        }
    }
}
