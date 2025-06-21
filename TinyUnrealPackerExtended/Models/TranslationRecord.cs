using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TinyUnrealPackerExtended.Models
{
    public class TranslationRecord
    {
        public string Namespace { get; set; }
        public string Key { get; set; }
        public string Translation { get; set; }
    }
}
