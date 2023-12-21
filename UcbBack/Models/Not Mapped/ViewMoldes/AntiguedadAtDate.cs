using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UcbBack.Models.Not_Mapped.ViewMoldes
{
    [NotMapped]
    public class AntiguedadAtDate
    {
        public DateTime StartDate { get; set; }
        public int Años { get; set; }
        public int Meses { get; set; }
        public int Dias { get; set; }
    }
}